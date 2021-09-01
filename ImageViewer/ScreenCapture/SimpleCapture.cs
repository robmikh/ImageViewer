using ImageViewer.System;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
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
            _multithread = device.Multithread;
            _deviceContext = device.ImmediateContext;
            _pauseEvent = new ManualResetEvent(true);
            _lastSize = item.Size;
            _stateLock = new object();

            _swapChain = new SwapChain(
                _device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                new SizeInt32() { Width = _lastSize.Width, Height = _lastSize.Height });

            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _lastSize);
            _session = _framePool.CreateCaptureSession(item);

            _framePool.FrameArrived += OnFrameArrived;
        }

        public void Continue()
        {
            _pauseEvent.Set();
        }

        public byte[] Pause()
        {
            _pauseEvent.Reset();
            lock (_stateLock)
            using (var lockSession = _multithread.Lock())
            using (var frontBuffer = _swapChain.GetBuffer(1))
            using (var texture = Direct3D11Texture2D.CreateFromDirect3DSurface(frontBuffer))
            {
                return texture.GetBytes();
            }
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

        public void Recreate(Direct3D11Device device)
        {
            var isPlaying = _pauseEvent.WaitOne(0);
            Pause();
            lock (_stateLock)
            using (var lockSession = _multithread.Lock())
            {
                _device = device;
                _deviceContext = device.ImmediateContext;
                // TODO: Is there a way to do this non-destructively?
                // Workaround is to get a new surface after this function
                _swapChain = new SwapChain(
                    device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    new SizeInt32() { Width = _lastSize.Width, Height = _lastSize.Height });
                _framePool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _lastSize);
            }
            Continue();
            if (!isPlaying)
            {
                Task.WaitAll(Task.Delay(30));
                Pause();
            }
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            _pauseEvent.WaitOne();

            lock (_stateLock)
            {
                using (var frame = sender.TryGetNextFrame())
                using (var deviceLock = _device.Multithread.Lock())
                {
                    var contentSize = frame.ContentSize;

                    using (var lockSession = _device.Multithread.Lock())
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

                    _swapChain.Present();

                } // retire the frame
            }
        }

        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;
        private SizeInt32 _lastSize;

        private Direct3D11Device _device;
        private Direct3D11Multithread _multithread;
        private Direct3D11DeviceContext _deviceContext;
        private SwapChain _swapChain;

        private ManualResetEvent _pauseEvent;
        private object _stateLock;

        private static readonly float[] ClearColor = new float[] { 0.0f, 0.0f, 0.0f, 0.0f };
    }
}
