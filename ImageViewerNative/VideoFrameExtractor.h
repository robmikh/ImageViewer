#pragma once
#include "VideoFrameExtractor.g.h"

namespace winrt::ImageViewerNative::implementation
{
    struct VideoFrameExtractor
    {
        VideoFrameExtractor() = default;

        static void ExtractFromStream(winrt::Windows::Storage::Streams::IRandomAccessStream const& stream, winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice const& device, winrt::Windows::Foundation::EventHandler<winrt::ImageViewerNative::VideoFrameArgs> const& callback);
    };
}
namespace winrt::ImageViewerNative::factory_implementation
{
    struct VideoFrameExtractor : VideoFrameExtractorT<VideoFrameExtractor, implementation::VideoFrameExtractor>
    {
    };
}
