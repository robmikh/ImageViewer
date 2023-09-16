#include "pch.h"
#include "VideoDecoderDevice.h"

namespace util
{
    using namespace robmikh::common::uwp;
}

std::vector<std::shared_ptr<VideoDecoderDevice>> VideoDecoderDevice::EnumerateAll(winrt::guid const& inputSubType)
{
    std::vector<std::shared_ptr<VideoDecoderDevice>> encoders;

    MFT_REGISTER_TYPE_INFO inputType = { MFMediaType_Video, inputSubType };
    MFT_REGISTER_TYPE_INFO outputType = { MFMediaType_Video,  MFVideoFormat_NV12 };
    auto transformSources = util::EnumerateMFTs(
        MFT_CATEGORY_VIDEO_DECODER,
        MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_TRANSCODE_ONLY | MFT_ENUM_FLAG_SORTANDFILTER,
        &inputType,
        &outputType);

    for (auto transformSource : transformSources)
    {
        auto encoder = std::make_shared<VideoDecoderDevice>(transformSource);
        encoders.push_back(encoder);
    }

    return encoders;
}

VideoDecoderDevice::VideoDecoderDevice(winrt::com_ptr<IMFActivate> const& transformSource)
{
    std::wstring friendlyName;
    if (auto name = util::GetStringAttribute(transformSource, MFT_FRIENDLY_NAME_Attribute))
    {
        friendlyName = name.value();
    }
    else
    {
        friendlyName = L"Unknown";
    }

    m_transformSource = transformSource;
    m_name = friendlyName;
}

winrt::com_ptr<IMFTransform> VideoDecoderDevice::CreateTransform()
{
    winrt::com_ptr<IMFTransform> transform;
    winrt::check_hresult(m_transformSource->ActivateObject(winrt::guid_of<IMFTransform>(), transform.put_void()));
    return transform;
}