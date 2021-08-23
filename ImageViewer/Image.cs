using ImageViewer.ScreenCapture;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using WinRTInteropTools;

namespace ImageViewer
{
    public interface IImage : IDisposable
    {
        BitmapSize Size { get; }
        // TODO: Format?
        Task SaveSnapshotToStreamAsync(IRandomAccessStream stream);
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

        public async Task SaveSnapshotToStreamAsync(IRandomAccessStream stream)
        {
            await Bitmap.SaveAsync(stream, CanvasBitmapFileFormat.Png);
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

    public enum DiffViewMode
    {
        Color,
        Alpha
    }

    class DiffImage : IImage
    {
        private DiffViewMode _viewMode = DiffViewMode.Color;
        private CompositionDrawingSurface _surface;

        public DiffResult Diff { get; }
        public DiffViewMode ViewMode
        {
            get { return _viewMode; }
            set
            {
                _viewMode = value;
                UpdateSurface();
            }
        }
        public BitmapSize Size => Diff.ColorDiffBitmap.SizeInPixels;

        public DiffImage(DiffResult diff)
        {
            Diff = diff;
        }

        private void UpdateSurface()
        {
            using (var drawingSession = CanvasComposition.CreateDrawingSession(_surface))
            {
                drawingSession.Clear(Colors.Transparent);
                drawingSession.DrawImage(GetBitmapForViewMode(_viewMode));
            }
        }

        public ICompositionSurface CreateSurface(CompositionGraphicsDevice graphics)
        {
            if (_surface == null)
            {
                var size = Size;
                var width = (int)size.Width;
                var height = (int)size.Height;
                _surface = graphics.CreateDrawingSurface2(
                    new SizeInt32() { Width = width, Height = height },
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    DirectXAlphaMode.Premultiplied);
                UpdateSurface();
            }
            return _surface;
        }

        public async Task SaveSnapshotToStreamAsync(IRandomAccessStream stream)
        {
            var bitmap = GetBitmapForViewMode(_viewMode);
            await bitmap.SaveAsync(stream, CanvasBitmapFileFormat.Png);
        }

        public void Dispose()
        {
            Diff.ColorDiffBitmap.Dispose();
            Diff.AlphaDiffBitmap.Dispose();
        }

        private CanvasBitmap GetBitmapForViewMode(DiffViewMode viewMode)
        {
            switch(viewMode)
            {
                case DiffViewMode.Color:
                    return Diff.ColorDiffBitmap;
                case DiffViewMode.Alpha:
                    return Diff.AlphaDiffBitmap;
                default:
                    throw new InvalidOperationException();
            }
        }
    }

    class CaptureImage : IImage
    {
        private Direct3D11Device _device;
        private SimpleCapture _capture;
        private bool _showCursor = true;

        public GraphicsCaptureItem Item { get; }

        public BitmapSize Size { get; }

        public CaptureImage(GraphicsCaptureItem item, Direct3D11Device device)
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

        public async Task SaveSnapshotToStreamAsync(IRandomAccessStream stream)
        {
            var texture = await CaptureSnapshot.TakeAsync(Item, Size, _device);
            var bytes = texture.GetBytes();

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                Size.Width,
                Size.Height,
                1.0,
                1.0,
                bytes);
            await encoder.FlushAsync();
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

        public async Task SetBorderAsync(bool showBorder)
        {
            await _capture.SetIsBorderRequiredAsync(showBorder);
        }
    }
}
