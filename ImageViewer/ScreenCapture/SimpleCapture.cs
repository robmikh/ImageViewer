﻿using ImageViewer.System;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Composition;
using WinRTInteropTools;

namespace ImageViewer.ScreenCapture
{
    class SimpleCapture : IDisposable
    {
        public SimpleCapture(Direct3D11Device device, GraphicsCaptureItem item)
        {
            _item = item;
            _device = device;
            _deviceContext = device.ImmediateContext;
            _pauseEvent = new ManualResetEvent(true);

            _swapChain = new SwapChain(
                _device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                new SizeInt32() { Width = item.Size.Width, Height = item.Size.Height });

            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    item.Size);
            _session = _framePool.CreateCaptureSession(item);
            _lastSize = item.Size;

            _framePool.FrameArrived += OnFrameArrived;
        }

        public void Continue()
        {
            _pauseEvent.Set();
        }

        public void Pause()
        {
            _pauseEvent.Reset();
        }

        public void StartCapture()
        {
            _session.StartCapture();
        }

        public ICompositionSurface CreateSurface(Compositor compositor)
        {
            return _swapChain.CreateSurface(compositor);
        }

        public void SetCursorCaptureState(bool showCursor)
        {
            _session.IsCursorCaptureEnabled = showCursor;
        }

        public async Task SetIsBorderRequiredAsync(bool isRequired)
        {
            if (Capabilities.IsCaptureBorderPropertyAvailable)
            {
                var access = await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);
                _session.IsBorderRequired = isRequired;
            }
        }

        public void Dispose()
        {
            _session?.Dispose();
            _framePool?.Dispose();
            _swapChain?.Dispose();

            _swapChain = null;
            _framePool = null;
            _session = null;
            _item = null;
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            _pauseEvent.WaitOne();

            using (var frame = sender.TryGetNextFrame())
            {
                var contentSize = frame.ContentSize;

                using (var sourceTexture = Direct3D11Texture2D.CreateFromDirect3DSurface(frame.Surface))
                using (var backBuffer = _swapChain.GetBuffer(0))
                using (var renderTargetView = _device.CreateRenderTargetView(backBuffer))
                {
                    _deviceContext.ClearRenderTargetView(renderTargetView, ClearColor);
                    var sourceBox = new Direct3D11Box()
                    {
                        Left = 0,
                        Top = 0,
                        Front = 0,
                        Right = (uint)Math.Min(_lastSize.Width, contentSize.Width),
                        Bottom = (uint)Math.Min(_lastSize.Height, contentSize.Height),
                        Back = 1
                    };
                    _deviceContext.CopySubresourceRegion(backBuffer, 0, new PositionUInt32(), sourceTexture, 0, sourceBox);
                }

            } // retire the frame

            _swapChain.Present();
        }

        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;
        private SizeInt32 _lastSize;

        private Direct3D11Device _device;
        private Direct3D11DeviceContext _deviceContext;
        private SwapChain _swapChain;

        private ManualResetEvent _pauseEvent;

        private static readonly float[] ClearColor = new float[] { 0.0f, 0.0f, 0.0f, 0.0f };
    }
}
