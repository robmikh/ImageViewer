using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Threading;
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
            var newSize = false;

            //_pauseEvent.WaitOne();

            using (var frame = sender.TryGetNextFrame())
            {

                if (frame.ContentSize.Width != _lastSize.Width ||
                    frame.ContentSize.Height != _lastSize.Height)
                {
                    // The thing we have been capturing has changed size.
                    // We need to resize our swap chain first, then blit the pixels.
                    // After we do that, retire the frame and then recreate our frame pool.
                    newSize = true;
                    _lastSize = frame.ContentSize;
                    _swapChain.ResizeBuffers(_lastSize.Width, _lastSize.Height);
                }

                using (var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface))
                using (var drawingSession = _swapChain.CreateDrawingSession(Colors.Transparent))
                {
                    drawingSession.DrawImage(bitmap);
                }

            } // retire the frame

            _swapChain.Present();

            //if (newSize)
            //{
            //    _framePool.Recreate(
            //        _device,
            //        DirectXPixelFormat.B8G8R8A8UIntNormalized,
            //        2,
            //        _lastSize);
            //}
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
