#pragma once

class VideoDecoderDevice
{
public:
    static std::vector<std::shared_ptr<VideoDecoderDevice>> EnumerateAll(winrt::guid const& inputSubType);

    VideoDecoderDevice(winrt::com_ptr<IMFActivate> const& transformSource);

    std::wstring const& Name() { return m_name; }

    winrt::com_ptr<IMFTransform> CreateTransform();

private:
    winrt::com_ptr<IMFActivate> m_transformSource;
    std::wstring m_name;
};