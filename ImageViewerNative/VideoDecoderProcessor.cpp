#include "pch.h"
#include "VideoDecoderProcessor.h"

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Foundation::Numerics;
    using namespace Windows::Graphics;
}

//float ComputeScaleFactor(winrt::float2 const outputSize, winrt::float2 const inputSize)
//{
//    auto outputRatio = outputSize.x / outputSize.y;
//    auto inputRatio = inputSize.x / inputSize.y;
//
//    auto scaleFactor = outputSize.x / inputSize.x;
//    if (outputRatio > inputRatio)
//    {
//        scaleFactor = outputSize.y / inputSize.y;
//    }
//
//    return scaleFactor;
//}

//winrt::RectInt32 ComputeDestRect(winrt::SizeInt32 const outputSize, winrt::SizeInt32 const inputSize)
//{
//    auto scale = ComputeScaleFactor({ (float)outputSize.Width, (float)outputSize.Height }, { (float)inputSize.Width, (float)inputSize.Height });
//    winrt::SizeInt32 newSize{ (int)(inputSize.Width * scale), (int)(inputSize.Height * scale) };
//    auto offsetX = 0;
//    auto offsetY = 0;
//    if (newSize.Width != outputSize.Width)
//    {
//        offsetX = (outputSize.Width - newSize.Width) / 2;
//    }
//    if (newSize.Height != outputSize.Height)
//    {
//        offsetY = (outputSize.Height - newSize.Height) / 2;
//    }
//    return winrt::RectInt32{
//        offsetX,
//        offsetY,
//        newSize.Width,
//        newSize.Height
//    };
//}

VideoDecoderProcessor::VideoDecoderProcessor(
    winrt::com_ptr<ID3D11Device> const& d3dDevice,
    DXGI_FORMAT const inputFormat,
    winrt::SizeInt32 const& inputSize,
    DXGI_FORMAT const outputFormat,
    winrt::SizeInt32 const& outputSize)
{
    m_d3dDevice = d3dDevice;
    m_d3dDevice->GetImmediateContext(m_d3dContext.put());

    // Setup video conversion
    m_videoDevice = m_d3dDevice.as<ID3D11VideoDevice>();
    m_videoContext = m_d3dContext.as<ID3D11VideoContext>();

    winrt::com_ptr<ID3D11VideoProcessorEnumerator> videoEnum;
    D3D11_VIDEO_PROCESSOR_CONTENT_DESC videoDesc = {};
    videoDesc.InputFrameFormat = D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE;
    videoDesc.InputFrameRate.Numerator = 60;
    videoDesc.InputFrameRate.Denominator = 1;
    videoDesc.InputWidth = inputSize.Width;
    videoDesc.InputHeight = inputSize.Height;
    videoDesc.OutputFrameRate.Numerator = 60;
    videoDesc.OutputFrameRate.Denominator = 1;
    videoDesc.OutputWidth = outputSize.Width;
    videoDesc.OutputHeight = outputSize.Height;
    videoDesc.Usage = D3D11_VIDEO_USAGE_OPTIMAL_QUALITY;
    winrt::check_hresult(m_videoDevice->CreateVideoProcessorEnumerator(&videoDesc, videoEnum.put()));

    winrt::check_hresult(m_videoDevice->CreateVideoProcessor(videoEnum.get(), 0, m_videoProcessor.put()));

    D3D11_VIDEO_PROCESSOR_COLOR_SPACE colorSpace = {};
    colorSpace.Usage = 1; // Video processing
    colorSpace.Nominal_Range = D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_0_255;
    m_videoContext->VideoProcessorSetOutputColorSpace(m_videoProcessor.get(), &colorSpace);
    colorSpace.Nominal_Range = D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_16_235;
    m_videoContext->VideoProcessorSetStreamColorSpace(m_videoProcessor.get(), 0, &colorSpace);

    // If the input and output resolutions don't match, setup the
    // video processor to preserve the aspect ratio when scaling.
    //if (inputSize.Width != outputSize.Width || inputSize.Height != outputSize.Height)
    //{
    //    auto destRect = ComputeDestRect(outputSize, inputSize);
    //    auto rect = RECT{
    //        destRect.X,
    //        destRect.Y,
    //        destRect.X + destRect.Width,
    //        destRect.Y + destRect.Height,
    //    };
    //    m_videoContext->VideoProcessorSetStreamDestRect(m_videoProcessor.get(), 0, true, &rect);
    //}

    D3D11_TEXTURE2D_DESC textureDesc = {};
    textureDesc.Width = outputSize.Width;
    textureDesc.Height = outputSize.Height;
    textureDesc.ArraySize = 1;
    textureDesc.MipLevels = 1;
    textureDesc.Format = outputFormat;
    textureDesc.SampleDesc.Count = 1;
    textureDesc.Usage = D3D11_USAGE_DEFAULT;
    textureDesc.BindFlags = D3D11_BIND_RENDER_TARGET; // | D3D11_BIND_VIDEO_ENCODER;
    winrt::check_hresult(m_d3dDevice->CreateTexture2D(&textureDesc, nullptr, m_videoOutputTexture.put()));

    D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC outputViewDesc = {};
    outputViewDesc.ViewDimension = D3D11_VPOV_DIMENSION_TEXTURE2D;
    outputViewDesc.Texture2D.MipSlice = 0;
    winrt::check_hresult(m_videoDevice->CreateVideoProcessorOutputView(m_videoOutputTexture.get(), videoEnum.get(), &outputViewDesc, m_videoOutput.put()));

    textureDesc.Width = inputSize.Width;
    textureDesc.Height = inputSize.Height;
    textureDesc.Format = inputFormat;
    textureDesc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
    winrt::check_hresult(m_d3dDevice->CreateTexture2D(&textureDesc, nullptr, m_videoInputTexture.put()));

    D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC inputViewDesc = {};
    inputViewDesc.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
    inputViewDesc.Texture2D.MipSlice = 0;
    winrt::check_hresult(m_videoDevice->CreateVideoProcessorInputView(m_videoInputTexture.get(), videoEnum.get(), &inputViewDesc, m_videoInput.put()));
}

void VideoDecoderProcessor::ProcessTexture(winrt::com_ptr<ID3D11Texture2D> const& inputTexture, std::optional<D3D11_BOX> const& box)
{
    // The caller is responsible for making sure they give us a
    // texture that matches the input size we were initialized with.

    // Copy the texture to the video input texture
    if (box.has_value())
    {
        auto d3dBox = box.value();
        m_d3dContext->CopySubresourceRegion(m_videoInputTexture.get(), 0, 0, 0, 0, inputTexture.get(), 0, &d3dBox);
    }
    else
    {
        m_d3dContext->CopyResource(m_videoInputTexture.get(), inputTexture.get());
    }

    // Convert to NV12
    D3D11_VIDEO_PROCESSOR_STREAM videoStream = {};
    videoStream.Enable = true;
    videoStream.OutputIndex = 0;
    videoStream.InputFrameOrField = 0;
    videoStream.pInputSurface = m_videoInput.get();
    winrt::check_hresult(m_videoContext->VideoProcessorBlt(m_videoProcessor.get(), m_videoOutput.get(), 0, 1, &videoStream));
}