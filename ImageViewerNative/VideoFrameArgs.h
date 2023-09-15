#pragma once
#include "VideoFrameArgs.g.h"

namespace winrt::ImageViewerNative::implementation
{
    struct VideoFrameArgs : VideoFrameArgsT<VideoFrameArgs>
    {
        VideoFrameArgs(winrt::com_ptr<ID3D11Texture2D> const& texture);

        void Reset(int64_t timestamp, uint64_t frameId);

        winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface Surface() { return m_surface; }
        winrt::Windows::Foundation::TimeSpan Timestamp() { return m_timestamp; }
        uint64_t FrameId() { return m_frameId; }

    private:
        winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface m_surface{ nullptr };
        winrt::Windows::Foundation::TimeSpan m_timestamp = {};
        uint64_t m_frameId = 0;
    };
}
