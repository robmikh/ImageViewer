#include "pch.h"
#include "VideoDecoder.h"
#include "VideoDecoderDevice.h"

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Graphics;
    using namespace Windows::Storage::Streams;
}

namespace util
{
    using namespace robmikh::common::uwp;
}

inline winrt::com_ptr<ID3D11Texture2D> CreateNV12Texture(
    winrt::com_ptr<ID3D11Device> const& d3dDevice,
    winrt::SizeInt32 const& resolution,
    bool staging)
{
    winrt::com_ptr<ID3D11Texture2D> texture;
    D3D11_TEXTURE2D_DESC desc = {};
    desc.Width = resolution.Width;
    desc.Height = resolution.Height;
    desc.ArraySize = 1;
    desc.MipLevels = 1;
    desc.Format = DXGI_FORMAT_NV12;
    desc.SampleDesc.Count = 1;
    desc.Usage = staging ? D3D11_USAGE_STAGING : D3D11_USAGE_DEFAULT;
    desc.CPUAccessFlags = staging ? D3D11_CPU_ACCESS_WRITE : 0;
    desc.BindFlags = staging ? 0 : D3D11_BIND_SHADER_RESOURCE;
    winrt::check_hresult(d3dDevice->CreateTexture2D(&desc, nullptr, texture.put()));
    return texture;
}

// https://learn.microsoft.com/en-us/windows/win32/medfound/media-type-debugging-code
float OffsetToFloat(const MFOffset& offset)
{
    return offset.value + (static_cast<float>(offset.fract) / 65536.0f);
}

VideoDecoder::VideoDecoder(
    std::shared_ptr<VideoDecoderDevice> const& decoderDevice,
    winrt::com_ptr<ID3D11Device> const& d3dDevice,
    winrt::com_ptr<IMFMediaType> const& inputMediaType)
{
    uint32_t width = 0;
    uint32_t height = 0;
    winrt::check_hresult(MFGetAttributeSize(inputMediaType.get(), MF_MT_FRAME_SIZE, &width, &height));
    uint32_t frameRateNumerator = 0;
    uint32_t frameRateDenominator = 0;
    winrt::check_hresult(MFGetAttributeRatio(inputMediaType.get(), MF_MT_FRAME_RATE, &frameRateNumerator, &frameRateDenominator));
    GUID videoSubtype = {};
    winrt::check_hresult(inputMediaType->GetGUID(MF_MT_SUBTYPE, &videoSubtype));

    m_inputResolution = { static_cast<int32_t>(width), static_cast<int32_t>(height) };
    m_outputResolution = m_inputResolution;
    m_frameRateNumerator = frameRateNumerator;
    m_frameRateDenominator = frameRateDenominator;

    m_transform = decoderDevice->CreateTransform();
    m_d3dDevice = d3dDevice;
    m_d3dDevice->GetImmediateContext(m_d3dContext.put());

    m_d3dMultithread = m_d3dDevice.as<ID3D11Multithread>();
    m_d3dMultithread->SetMultithreadProtected(true);

    // Create MF device manager
    winrt::check_hresult(MFCreateDXGIDeviceManager(&m_deviceManagerResetToken, m_mediaDeviceManager.put()));
    winrt::check_hresult(m_mediaDeviceManager->ResetDevice(m_d3dDevice.get(), m_deviceManagerResetToken));

    // Setup MFTransform
    winrt::com_ptr<IMFAttributes> attributes;
    winrt::check_hresult(m_transform->GetAttributes(attributes.put()));
    winrt::check_hresult(attributes->SetUINT32(MF_LOW_LATENCY, 1));
    // MPEG2 specific
    if (videoSubtype == MFVideoFormat_MPEG2)
    {
        winrt::check_hresult(attributes->SetUINT32(CODECAPI_AVDecVideoAcceleration_MPEG2, 1));
        winrt::check_hresult(attributes->SetUINT32(CODECAPI_AVLowLatencyMode, 1));
    }

    DWORD numInputStreams = 0;
    DWORD numOutputStreams = 0;
    winrt::check_hresult(m_transform->GetStreamCount(&numInputStreams, &numOutputStreams));
    std::vector<DWORD> inputStreamIds(numInputStreams, 0);
    std::vector<DWORD> outputSteamIds(numOutputStreams, 0);
    {
        auto hr = m_transform->GetStreamIDs(numInputStreams, inputStreamIds.data(), numOutputStreams, outputSteamIds.data());
        // https://docs.microsoft.com/en-us/windows/win32/api/mftransform/nf-mftransform-imftransform-getstreamids
        // This method can return E_NOTIMPL if both of the following conditions are true:
        //   * The transform has a fixed number of streams.
        //   * The streams are numbered consecutively from 0 to n � 1, where n is the
        //     number of input streams or output streams. In other words, the first 
        //     input stream is 0, the second is 1, and so on; and the first output 
        //     stream is 0, the second is 1, and so on. 
        if (hr == E_NOTIMPL)
        {
            for (auto i = 0; i < inputStreamIds.size(); i++)
            {
                inputStreamIds[i] = i;
            }
            for (auto i = 0; i < outputSteamIds.size(); i++)
            {
                outputSteamIds[i] = i;
            }
        }
        else
        {
            winrt::check_hresult(hr);
        }
    }
    m_inputStreamId = inputStreamIds[0];
    m_outputStreamId = outputSteamIds[0];

    winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_SET_D3D_MANAGER, reinterpret_cast<ULONG_PTR>(m_mediaDeviceManager.get())));

    winrt::check_hresult(m_transform->SetInputType(m_inputStreamId, inputMediaType.get(), 0));

    MFT_REGISTER_TYPE_INFO outputInfo = { MFMediaType_Video, MFVideoFormat_NV12 };
    winrt::com_ptr<IMFMediaType> outputType;
    std::optional<D3D11_BOX> outputBox = std::nullopt;
    {
        auto count = 0;
        for (count = 0;; count++)
        {
            outputType = nullptr;
            outputBox = std::nullopt;
            m_fullOutputResolution = m_outputResolution;
            auto hr = m_transform->GetOutputAvailableType(m_outputStreamId, count, outputType.put());
            if (hr == MF_E_NO_MORE_TYPES)
            {
                break;
            }
            //winrt::check_hresult(LogMediaType(outputType.get()));
            //OutputDebugStringW(L"\n");
            winrt::check_hresult(hr);
            winrt::check_hresult(outputType->SetGUID(MF_MT_MAJOR_TYPE, outputInfo.guidMajorType));
            winrt::check_hresult(outputType->SetGUID(MF_MT_SUBTYPE, outputInfo.guidSubtype));
            // The frame size may be larger than the reported video resolution
            uint32_t outputWidth = 0;
            uint32_t outputHeight = 0;
            winrt::check_hresult(MFGetAttributeSize(outputType.get(), MF_MT_FRAME_SIZE, &outputWidth, &outputHeight));
            if (!(outputWidth == static_cast<uint32_t>(m_outputResolution.Width) &&
                  outputHeight == static_cast<uint32_t>(m_outputResolution.Height)) &&
                outputWidth >= static_cast<uint32_t>(m_outputResolution.Width) &&
                outputHeight >= static_cast<uint32_t>(m_outputResolution.Height))
            {
                // Let's figure out the bounds of the valid pixels in the frame
                MFVideoArea videoArea = {};
                uint32_t blobSize = 0;
                winrt::check_hresult(outputType->GetBlob(MF_MT_MINIMUM_DISPLAY_APERTURE, reinterpret_cast<uint8_t*>(&videoArea), sizeof(videoArea), &blobSize));
                auto x = static_cast<uint32_t>(OffsetToFloat(videoArea.OffsetX));
                auto y = static_cast<uint32_t>(OffsetToFloat(videoArea.OffsetY));
                auto videoAreaWidth = static_cast<uint32_t>(videoArea.Area.cx);
                auto videoAreaHeight = static_cast<uint32_t>(videoArea.Area.cy);
                outputBox = std::optional(D3D11_BOX{ x, y, 0, x + videoAreaWidth, y + videoAreaHeight, 1 });
                m_fullOutputResolution = { static_cast<int>(outputWidth), static_cast<int>(outputHeight) };
            }
            // Otherwise test our luck setting the frame size
            else
            {
                winrt::check_hresult(MFSetAttributeSize(outputType.get(), MF_MT_FRAME_SIZE, m_outputResolution.Width, m_outputResolution.Height));
            }
            winrt::check_hresult(MFSetAttributeRatio(outputType.get(), MF_MT_FRAME_RATE, m_frameRateNumerator, m_frameRateDenominator));
            winrt::check_hresult(outputType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, 1));
            winrt::check_hresult(outputType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive));
            hr = m_transform->SetOutputType(m_outputStreamId, outputType.get(), MFT_SET_TYPE_TEST_ONLY);
            if (hr == MF_E_INVALIDMEDIATYPE)
            {
                continue;
            }
            winrt::check_hresult(hr);
            break;
        }
        if (outputType == nullptr)
        {
            throw std::runtime_error("No suitable input type found");
        }
        winrt::check_hresult(m_transform->SetOutputType(m_outputStreamId, outputType.get(), 0));
        m_outputBox = outputBox;
    }

    StartDecode();
}

VideoDecoder::~VideoDecoder()
{
    StopDecode();
}

HRESULT VideoDecoder::ProcessInputSample(VideoDecoderInputSample const& inputSample)
{
    auto mfSample = CreateMFSample(inputSample);
    return ProcessInputSample(mfSample);
}

HRESULT VideoDecoder::ProcessInputSample(winrt::com_ptr<IMFSample> const& mfSample)
{
    if (m_started.load())
    {
        return ProcessInput(mfSample);
    }
    throw std::runtime_error("Must start decoder!");
}

SampleProcessResult VideoDecoder::ProcessOutputSample(winrt::com_ptr<ID3D11Texture2D>& result, int64_t& timeStamp)
{
    return ProcessOutput(result, timeStamp);
}

void VideoDecoder::StartDecode()
{
    bool expected = false;
    if (m_started.compare_exchange_strong(expected, true))
    {
        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_COMMAND_FLUSH, 0));
        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0));
        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_NOTIFY_START_OF_STREAM, 0));
    }
}

void VideoDecoder::StopDecode()
{
    bool expected = true;
    if (m_started.compare_exchange_strong(expected, false))
    {
        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0));
        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_NOTIFY_END_STREAMING, 0));
        winrt::check_hresult(m_transform->ProcessMessage(MFT_MESSAGE_COMMAND_FLUSH, 0));
    }
}

void VideoDecoder::OnStreamChange()
{
    MFT_REGISTER_TYPE_INFO outputInfo = { MFMediaType_Video, MFVideoFormat_NV12 };
    winrt::com_ptr<IMFMediaType> outputType;
    std::optional<D3D11_BOX> outputBox = std::nullopt;
    {
        auto count = 0;
        for (count = 0;; count++)
        {
            outputType = nullptr;
            outputBox = std::nullopt;
            m_fullOutputResolution = m_outputResolution;
            HRESULT hr = m_transform->GetOutputAvailableType(m_outputStreamId, count, outputType.put());
            if (hr == MF_E_NO_MORE_TYPES)
            {
                break;
            }
            //winrt::check_hresult(LogMediaType(outputType.get()));
            //OutputDebugStringW(L"\n");
            winrt::check_hresult(hr);
            winrt::check_hresult(outputType->SetGUID(MF_MT_MAJOR_TYPE, outputInfo.guidMajorType));
            winrt::check_hresult(outputType->SetGUID(MF_MT_SUBTYPE, outputInfo.guidSubtype));
            // The frame size may be larger than the reported video resolution
            uint32_t outputWidth = 0;
            uint32_t outputHeight = 0;
            winrt::check_hresult(MFGetAttributeSize(outputType.get(), MF_MT_FRAME_SIZE, &outputWidth, &outputHeight));
            if (!(outputWidth == static_cast<uint32_t>(m_outputResolution.Width) &&
                outputHeight == static_cast<uint32_t>(m_outputResolution.Height)) &&
                outputWidth >= static_cast<uint32_t>(m_outputResolution.Width) &&
                outputHeight >= static_cast<uint32_t>(m_outputResolution.Height))
            {
                // Let's figure out the bounds of the valid pixels in the frame
                MFVideoArea videoArea = {};
                uint32_t blobSize = 0;
                winrt::check_hresult(outputType->GetBlob(MF_MT_MINIMUM_DISPLAY_APERTURE, reinterpret_cast<uint8_t*>(&videoArea), sizeof(videoArea), &blobSize));
                auto x = static_cast<uint32_t>(OffsetToFloat(videoArea.OffsetX));
                auto y = static_cast<uint32_t>(OffsetToFloat(videoArea.OffsetY));
                auto width = static_cast<uint32_t>(videoArea.Area.cx);
                auto height = static_cast<uint32_t>(videoArea.Area.cy);
                outputBox = std::optional(D3D11_BOX{ x, y, 0, x + width, y + height, 1 });
                m_fullOutputResolution = { static_cast<int>(outputWidth), static_cast<int>(outputHeight) };
            }
            // Otherwise test our luck setting the frame size
            else
            {
                winrt::check_hresult(MFSetAttributeSize(outputType.get(), MF_MT_FRAME_SIZE, m_outputResolution.Width, m_outputResolution.Height));
            }
            winrt::check_hresult(MFSetAttributeRatio(outputType.get(), MF_MT_FRAME_RATE, m_frameRateNumerator, m_frameRateDenominator));
            winrt::check_hresult(outputType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, 1));
            winrt::check_hresult(outputType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive));
            hr = m_transform->SetOutputType(m_outputStreamId, outputType.get(), MFT_SET_TYPE_TEST_ONLY);
            if (hr == MF_E_INVALIDMEDIATYPE)
            {
                continue;
            }
            winrt::check_hresult(hr);
            break;
        }
        if (outputType == nullptr)
        {
            throw std::runtime_error("No suitable input type found");
        }
        winrt::check_hresult(m_transform->SetOutputType(m_outputStreamId, outputType.get(), 0));
        m_outputBox = outputBox;
    }
    winrt::com_ptr<IMFAttributes> attributes;
    winrt::check_hresult(m_transform->GetAttributes(attributes.put()));
    winrt::check_hresult(attributes->SetUINT32(MF_LOW_LATENCY, 1));
}

void VideoDecoder::EnsureOutputTexture()
{
    if (m_outputTexture == nullptr)
    {
        m_outputTexture = CreateNV12Texture(m_d3dDevice, m_fullOutputResolution, false);
    }
}

void VideoDecoder::EnsureStagingTexture()
{
    if (m_stagingTexture == nullptr)
    {
        m_stagingTexture = CreateNV12Texture(m_d3dDevice, m_fullOutputResolution, true);
    }
}

winrt::com_ptr<IMFSample> VideoDecoder::CreateMFSample(VideoDecoderInputSample const& inputSample)
{
    auto buffer = inputSample.Buffer;
    auto bufferLength = buffer.Length();
    winrt::com_ptr<IMFMediaBuffer> inputBuffer;
    winrt::check_hresult(MFCreateMemoryBuffer(bufferLength, inputBuffer.put()));
    {
        byte* bytes = nullptr;
        auto byteAccess = buffer.as<::Windows::Storage::Streams::IBufferByteAccess>();
        winrt::check_hresult(byteAccess->Buffer(&bytes));

        auto guard = util::MediaBufferGuard(inputBuffer);
        auto info = guard.Info();

        memcpy_s(reinterpret_cast<void*>(info.Bits), info.MaxLength, reinterpret_cast<void*>(bytes), bufferLength);
    }
    winrt::check_hresult(inputBuffer->SetCurrentLength(bufferLength));
    winrt::com_ptr<IMFSample> mfSample;
    winrt::check_hresult(MFCreateSample(mfSample.put()));
    winrt::check_hresult(mfSample->AddBuffer(inputBuffer.get()));
    winrt::check_hresult(mfSample->SetSampleTime(inputSample.TimeStamp));
    return mfSample;
}

HRESULT VideoDecoder::ProcessInput(winrt::com_ptr<IMFSample> const& mfSample)
{
    RETURN_IF_FAILED(m_transform->ProcessInput(m_inputStreamId, mfSample.get(), 0));
    return S_OK;
}

SampleProcessResult VideoDecoder::ProcessOutput(winrt::com_ptr<ID3D11Texture2D>& result, int64_t& timeStamp)
{
    winrt::com_ptr<ID3D11Texture2D> resultTexture;

    DWORD status = 0;
    MFT_OUTPUT_DATA_BUFFER outputBuffer = {};
    outputBuffer.dwStreamID = m_outputStreamId;

    HRESULT hr = m_transform->ProcessOutput(0, 1, &outputBuffer, &status);
    if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT)
    {
        return SampleProcessResult::NeedsMoreInput;
    }
    else if (hr == MF_E_TRANSFORM_STREAM_CHANGE)
    {
        OnStreamChange();
        return SampleProcessResult::StreamChanged;
    }
    winrt::check_hresult(hr);

    winrt::com_ptr<IMFSample> sample;
    sample.attach(outputBuffer.pSample);
    winrt::com_ptr<IMFCollection> events;
    events.attach(outputBuffer.pEvents);

    winrt::com_ptr<IMFMediaBuffer> mfBuffer;
    winrt::check_hresult(sample->GetBufferByIndex(0, mfBuffer.put()));

    int64_t sampleTime = 0;
    winrt::check_hresult(sample->GetSampleTime(&sampleTime));
    timeStamp = sampleTime;

    if (auto dxgiBuffer = mfBuffer.try_as<IMFDXGIBuffer>())
    {
        util::D3D11DeviceLock lock(m_d3dMultithread.get());
        EnsureOutputTexture();

        winrt::com_ptr<ID3D11Texture2D> sampleTexture;
        winrt::check_hresult(dxgiBuffer->GetResource(winrt::guid_of<ID3D11Texture2D>(), sampleTexture.put_void()));
        uint32_t subresourceIndex = 0;
        winrt::check_hresult(dxgiBuffer->GetSubresourceIndex(&subresourceIndex));
        m_d3dContext->CopySubresourceRegion(m_outputTexture.get(), 0, 0, 0, 0, sampleTexture.get(), subresourceIndex, nullptr);
        resultTexture = m_outputTexture;
    }
    else
    {
        {
            util::D3D11DeviceLock lock(m_d3dMultithread.get());
            EnsureStagingTexture();

            auto guard = util::MediaBufferGuard(mfBuffer);
            auto info = guard.Info();

            D3D11_TEXTURE2D_DESC desc = {};
            m_stagingTexture->GetDesc(&desc);

            D3D11_MAPPED_SUBRESOURCE mapped = {};
            winrt::check_hresult(m_d3dContext->Map(m_stagingTexture.get(), 0, D3D11_MAP_WRITE, 0, &mapped));
            auto scope = wil::scope_exit([=]()
                {
                    m_d3dContext->Unmap(m_stagingTexture.get(), 0);
                });

            memcpy_s(mapped.pData, info.CurrentLength, reinterpret_cast<void*>(info.Bits), info.CurrentLength);
        }
        resultTexture = m_stagingTexture;
    }

    assert(resultTexture != nullptr);
    result.copy_from(resultTexture.get());
    return SampleProcessResult::Success;
}