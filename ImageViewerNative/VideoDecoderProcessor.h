#pragma once
#include "Fence.h"

class VideoDecoderProcessor
{
public:
    VideoDecoderProcessor(
        winrt::com_ptr<ID3D11Device> const& d3dDevice,
        DXGI_FORMAT const inputFormat,
        winrt::Windows::Graphics::SizeInt32 const& inputSize,
        DXGI_FORMAT const outputFormat,
        winrt::Windows::Graphics::SizeInt32 const& outputSize);

    winrt::com_ptr<ID3D11Texture2D> const& OutputTexture() { return m_videoOutputTexture; }

    void ProcessTexture(winrt::com_ptr<ID3D11Texture2D> const& inputTexture, std::optional<D3D11_BOX> const& box);

private:
    winrt::com_ptr<ID3D11Device> m_d3dDevice;
    winrt::com_ptr<ID3D11DeviceContext> m_d3dContext;

    winrt::com_ptr<ID3D11VideoDevice> m_videoDevice;
    winrt::com_ptr<ID3D11VideoContext> m_videoContext;
    winrt::com_ptr<ID3D11VideoProcessor> m_videoProcessor;
    winrt::com_ptr<ID3D11Texture2D> m_videoOutputTexture;
    winrt::com_ptr<ID3D11VideoProcessorOutputView> m_videoOutput;
    winrt::com_ptr<ID3D11Texture2D> m_videoInputTexture;
    winrt::com_ptr<ID3D11VideoProcessorInputView> m_videoInput;

    std::shared_ptr<D3D11Fence> m_fence;
};