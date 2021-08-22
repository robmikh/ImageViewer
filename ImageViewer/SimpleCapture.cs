using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Numerics;
using System.Threading;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Composition;

namespace ImageViewer
{
    class SimpleCapture : IDisposable
    {
        public SimpleCapture(CanvasDevice device, GraphicsCaptureItem item)
        {
            _item = item;
            _device = device;
            _pauseEvent = new ManualResetEvent(true);

            // TODO: Dpi?
            _swapChain = new CanvasSwapChain(_device, item.Size.Width, item.Size.Height, 96);

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
            return CanvasComposition.CreateCompositionSurfaceForSwapChain(compositor, _swapChain);
        }

        public void SetCursorCaptureState(bool showCursor)
        {
            _session.IsCursorCaptureEnabled = showCursor;
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

                using (var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface))
                using (var drawingSession = _swapChain.CreateDrawingSession(Colors.Transparent))
                {
                    drawingSession.DrawImage(bitmap, Vector2.Zero, new Rect()
                    {
                        X = 0,
                        Y = 0,
                        Width = Math.Min(_lastSize.Width, contentSize.Width),
                        Height = Math.Min(_lastSize.Height, contentSize.Height),
                    });
                }

            } // retire the frame

            _swapChain.Present();
        }

        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;
        private SizeInt32 _lastSize;

        private CanvasDevice _device;
        private CanvasSwapChain _swapChain;

        private ManualResetEvent _pauseEvent;
    }
}
