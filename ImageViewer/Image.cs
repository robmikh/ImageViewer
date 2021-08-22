using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.UI;
using Windows.UI.Composition;

namespace ImageViewer
{
    interface IImage
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
    }
}
