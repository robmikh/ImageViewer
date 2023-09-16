#pragma once

class VideoDecoderDevice;

struct VideoDecoderInputSample
{
    LONGLONG TimeStamp;
    winrt::Windows::Storage::Streams::IBuffer Buffer{ nullptr };
};

enum SampleProcessResult
{
    Success,
    NeedsMoreInput,
    StreamChanged,
};

class VideoDecoder
{
public:
    VideoDecoder(
        std::shared_ptr<VideoDecoderDevice> const& decoderDevice,
        winrt::com_ptr<ID3D11Device> const& d3dDevice,
        winrt::com_ptr<IMFMediaType> const& inputMediaType);
    ~VideoDecoder();

    winrt::com_ptr<IMFMediaType> const& OutputType() { return m_outputType; }
    HRESULT ProcessInputSample(VideoDecoderInputSample const& inputSample);
    HRESULT ProcessInputSample(winrt::com_ptr<IMFSample> const& mfSample);
    SampleProcessResult ProcessOutputSample(winrt::com_ptr<ID3D11Texture2D>& result, int64_t& timeStamp);
    std::optional<D3D11_BOX> const& OutputBox() { return m_outputBox; }

private:
    void StartDecode();
    void StopDecode();
    void OnStreamChange();
    void EnsureOutputTexture();
    void EnsureStagingTexture();
    winrt::com_ptr<IMFSample> CreateMFSample(VideoDecoderInputSample const& inputSample);
    HRESULT ProcessInput(winrt::com_ptr<IMFSample> const& mfSample);
    SampleProcessResult ProcessOutput(winrt::com_ptr<ID3D11Texture2D>& result, int64_t& timeStamp);

private:
    winrt::com_ptr<ID3D11Device> m_d3dDevice;
    winrt::com_ptr<ID3D11DeviceContext> m_d3dContext;
    winrt::com_ptr<ID3D11Multithread> m_d3dMultithread;
    winrt::com_ptr<IMFDXGIDeviceManager> m_mediaDeviceManager;
    uint32_t m_deviceManagerResetToken = 0;

    winrt::com_ptr<IMFTransform> m_transform;
    DWORD m_inputStreamId = 0;
    DWORD m_outputStreamId = 0;
    winrt::com_ptr<IMFMediaType> m_outputType;

    std::atomic<bool> m_started = false;
    winrt::Windows::Graphics::SizeInt32 m_inputResolution = { 0, 0 };
    winrt::Windows::Graphics::SizeInt32 m_outputResolution = { 0, 0 };

    winrt::com_ptr<ID3D11Texture2D> m_stagingTexture;
    winrt::com_ptr<ID3D11Texture2D> m_outputTexture;

    uint32_t m_frameRateNumerator = 0;
    uint32_t m_frameRateDenominator = 0;
    
    winrt::Windows::Graphics::SizeInt32 m_fullOutputResolution = { 0, 0 };
    std::optional<D3D11_BOX> m_outputBox = std::nullopt;
};