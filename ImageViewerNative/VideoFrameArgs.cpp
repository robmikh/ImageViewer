#include "pch.h"
#include "VideoFrameArgs.h"
#include "VideoFrameArgs.g.cpp"

namespace winrt::ImageViewerNative::implementation
{
    VideoFrameArgs::VideoFrameArgs(winrt::com_ptr<ID3D11Texture2D> const& texture)
    {
        m_surface = CreateDirect3DSurface(texture.as<IDXGISurface>().get());
    }

    void VideoFrameArgs::Reset(int64_t timestamp, uint64_t frameId)
    {
        m_timestamp = winrt::Windows::Foundation::TimeSpan{ timestamp };
        m_frameId = frameId;
    }
}
