using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.UI;
using Windows.UI.Composition;

namespace ImageViewer
{
    interface IImage : IDisposable
    {
        BitmapSize Size { get; }
        CanvasBitmap GetSnapshot();
        ICompositionSurface CreateSurface(CompositionGraphicsDevice graphics);
    }

    class CanvasBitmapImage : IImage
    {
        public CanvasBitmap Bitmap { get; }

        public BitmapSize Size => Bitmap.SizeInPixels;

        public CanvasBitmapImage(CanvasBitmap bitmap)
        {
            Bitmap = bitmap;
        }

        public CanvasBitmap GetSnapshot()
        {
            return Bitmap;
        }

        public ICompositionSurface CreateSurface(CompositionGraphicsDevice graphics)
        {
            var size = Size;
            var width = (int)size.Width;
            var height = (int)size.Height;
            var imageSurface = graphics.CreateDrawingSurface2(
                new SizeInt32() { Width = width, Height = height },
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                DirectXAlphaMode.Premultiplied);
            using (var drawingSession = CanvasComposition.CreateDrawingSession(imageSurface))
            {
                drawingSession.Clear(Colors.Transparent);
                drawingSession.DrawImage(Bitmap);
            }
            return imageSurface;
        }

        public void Dispose()
        {
            Bitmap.Dispose();
        }
    }

    // TODO: Don't use Win2D for Capture
    class CaptureImage : IImage
    {
        private CanvasDevice _device;
        private SimpleCapture _capture;
        private bool _showCursor = true;

        public GraphicsCaptureItem Item { get; }

        public BitmapSize Size { get; }

        public CaptureImage(GraphicsCaptureItem item, CanvasDevice device)
        {
            Item = item;
            var itemSize = item.Size;
            var size = new BitmapSize();
            size.Width = (uint)itemSize.Width;
            size.Height = (uint)itemSize.Height;
            Size = size;
            _device = device;

            _capture = new SimpleCapture(device, item);
            _capture.StartCapture();
        }

        public CanvasBitmap GetSnapshot()
        {
            // TODO: Make async?
            // Use a seperate device so we don't have to deal
            // with synchronization with D2D
            return CaptureSnapshot.Take(Item, Size, new CanvasDevice());
        }

        public ICompositionSurface CreateSurface(CompositionGraphicsDevice graphics)
        {
            var compositor = graphics.Compositor;
            return _capture.CreateSurface(compositor);
        }

        public void Play()
        {
            _capture.Continue();
        }

        public void Pause()
        {
            _capture.Pause();
        }

        public void Dispose()
        {
            _capture.Dispose();
        }

        public bool ShowCursor
        {
            get { return _showCursor; }
            set
            {
                _showCursor = value;
                _capture.SetCursorCaptureState(_showCursor);
            }
        }
    }

}
