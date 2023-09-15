#include "pch.h"
#include "VideoFrameExtractor.h"
#include "VideoFrameExtractor.g.cpp"
#include "VideoFrameArgs.h"
#include "VideoDecoderDevice.h"
#include "VideoDecoder.h"
#include "VideoDecoderProcessor.h"

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Graphics;
}

namespace winrt::ImageViewerNative::implementation
{
    void VideoFrameExtractor::ExtractFromStream(
        winrt::Windows::Storage::Streams::IRandomAccessStream const& stream, 
        winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice const& device, 
        winrt::Windows::Foundation::EventHandler<winrt::ImageViewerNative::VideoFrameArgs> const& callback)
    {
        auto d3dDevice = GetDXGIInterfaceFromObject<ID3D11Device>(device);

        // Create our source and reader
        winrt::com_ptr<IMFSourceResolver> sourceResolver;
        winrt::check_hresult(MFCreateSourceResolver(sourceResolver.put()));

        // IRandomAccessStream -> IStream -> IMFByteStream
        auto streamUnknown = stream.as<::IUnknown>();
        winrt::com_ptr<IStream> istream;
        winrt::check_hresult(CreateStreamOverRandomAccessStream(streamUnknown.get(), winrt::guid_of<IStream>(), istream.put_void()));
        winrt::com_ptr<IMFByteStream> mfByteStream;
        winrt::check_hresult(MFCreateMFByteStreamOnStreamEx(istream.get(), mfByteStream.put()));

        // Create our source resolver
        winrt::com_ptr<IMFSourceReader> sourceReader;
        winrt::check_hresult(MFCreateSourceReaderFromByteStream(mfByteStream.get(), nullptr, sourceReader.put()));
        
        // Configure our reader
        winrt::check_hresult(sourceReader->SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, (DWORD)false));
        winrt::check_hresult(sourceReader->SetStreamSelection((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, (DWORD)true));

        winrt::com_ptr<IMFMediaType> inputType;
        winrt::check_hresult(sourceReader->GetCurrentMediaType((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, inputType.put()));
        GUID videoSubtype = {};
        winrt::check_hresult(inputType->GetGUID(MF_MT_SUBTYPE, &videoSubtype));
        uint32_t width = 0;
        uint32_t height = 0;
        winrt::check_hresult(MFGetAttributeSize(inputType.get(), MF_MT_FRAME_SIZE, &width, &height));
        
        winrt::SizeInt32 resolution{ static_cast<int32_t>(width), static_cast<int32_t>(height) };

        // Setup our video decoder pipeline
        auto videoProcessor = VideoDecoderProcessor(d3dDevice, DXGI_FORMAT_NV12, resolution, DXGI_FORMAT_B8G8R8A8_UNORM, resolution);
        auto decoderDevices = VideoDecoderDevice::EnumerateAll(videoSubtype);
        auto decoderDevice = decoderDevices[0];
        auto videoDecoder = VideoDecoder(decoderDevice, d3dDevice, inputType);

        // Get ready for output
        uint32_t frameId = 0;
        auto args = winrt::make_self<winrt::ImageViewerNative::implementation::VideoFrameArgs>(videoProcessor.OutputTexture());

        // Start decoding
        while (true)
        {
            DWORD streamIndex = 0;
            DWORD flags = 0;
            LONGLONG timeStamp = 0;
            winrt::com_ptr<IMFSample> videoSample;
            winrt::check_hresult(sourceReader->ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, &streamIndex, &flags, &timeStamp, videoSample.put()));

            if (flags & MF_SOURCE_READERF_ENDOFSTREAM)
            {
                break;
            }

            assert(videoSample.get() != nullptr);

            bool processedInput = true;
            auto inputResult = videoDecoder.ProcessInputSample(videoSample);
            if (inputResult == MF_E_NOTACCEPTING)
            {
                processedInput = false;
            }
            else
            {
                winrt::check_hresult(inputResult);
            }

            auto decodeResult = SampleProcessResult::NeedsMoreInput;
            do
            {
                int64_t sampleTime = 0;
                winrt::com_ptr<ID3D11Texture2D> frame;
                decodeResult = videoDecoder.ProcessOutputSample(frame, sampleTime);

                while (decodeResult == SampleProcessResult::StreamChanged)
                {
                    inputResult = videoDecoder.ProcessInputSample(videoSample);
                    if (inputResult == MF_E_NOTACCEPTING)
                    {
                        processedInput = false;
                    }
                    else
                    {
                        winrt::check_hresult(inputResult);
                    }
                    decodeResult = videoDecoder.ProcessOutputSample(frame, sampleTime);
                }

                if (decodeResult == SampleProcessResult::Success)
                {
                    videoProcessor.ProcessTexture(frame, videoDecoder.OutputBox());
                    auto& outputTexture = videoProcessor.OutputTexture();

                    // Callback
                    auto currentFrameId = frameId++;
                    args->Reset(timeStamp, currentFrameId);
                    callback(nullptr, args.as<winrt::ImageViewerNative::VideoFrameArgs>());
                }

            } while (decodeResult != SampleProcessResult::NeedsMoreInput);

            if (!processedInput)
            {
                // Failing this means we drop the sample
                winrt::check_hresult(videoDecoder.ProcessInputSample(videoSample));
            }
        }
    }
}
