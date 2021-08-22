using Microsoft.Graphics.Canvas;
using System.Threading;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.UI;

namespace ImageViewer
{
    static class CaptureSnapshot
    {
        public static CanvasBitmap Take(GraphicsCaptureItem item, BitmapSize bitmapSize, CanvasDevice device)
        {
            var size = new SizeInt32();
            size.Width = (int)bitmapSize.Width;
            size.Height = (int)bitmapSize.Height;

            var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, size);
            var session = framePool.CreateCaptureSession(item);

            CanvasRenderTarget renderTarget = null;
            var captureEvent = new ManualResetEvent(false);
            framePool.FrameArrived += (s, a) =>
            {
                var frame = s.TryGetNextFrame();
                // TODO: Dpi?
                renderTarget = new CanvasRenderTarget(device, size.Width, size.Height, 96.0f); 
                using (var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(device, frame.Surface, 96.0f))
                using (var drawingSession = renderTarget.CreateDrawingSession())
                {
                    drawingSession.Clear(Colors.Transparent);
                    drawingSession.DrawImage(bitmap);
                }

                captureEvent.Set();

                session.Dispose();
                s.Dispose();
            };

            session.StartCapture();
            captureEvent.WaitOne();

            return renderTarget;
        }
    }
}
