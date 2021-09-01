using ImageViewer.ScreenCapture;
using ImageViewer.System;
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
        string DisplayName { get; }
        BitmapSize Size { get; }
        // TODO: Format?
        Task SaveSnapshotToStreamAsync(IRandomAccessStream stream);
        ICompositionSurface CreateSurface(CompositionGraphicsDevice graphics);
        void RegenerateSurface();
        Color? GetColorFromPixel(int x, int y);
    }

    class CanvasBitmapImage : IImage
    {
        private CompositionDrawingSurface _surface;
        private Color[] _colors;
        private BitmapSize _size;

        public CanvasBitmap Bitmap { get; private set; }

        public string DisplayName { get; }
        public BitmapSize Size => _size;

        public CanvasBitmapImage(CanvasBitmap bitmap, string displayName)
        {
            Bitmap = bitmap;
            DisplayName = displayName;
            _size = Bitmap.SizeInPixels;
            _colors = bitmap.GetPixelColors();
        }

        private void UpdateSurface()
        {
            using (var drawingSession = CanvasComposition.CreateDrawingSession(_surface))
            {
                drawingSession.Clear(Colors.Transparent);
                drawingSession.DrawImage(Bitmap);
            }
        }

        public async Task SaveSnapshotToStreamAsync(IRandomAccessStream stream)
        {
            await Bitmap.SaveAsync(stream, CanvasBitmapFileFormat.Png);
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

        public void Dispose()
        {
            Bitmap.Dispose();
        }

        public void RegenerateSurface()
        {
            Bitmap = CanvasBitmap.CreateFromColors(GraphicsManager.Current.CanvasDevice, _colors, (int)_size.Width, (int)_size.Height);
            if (_surface != null)
            {
                UpdateSurface();
            }
        }

        public Color? GetColorFromPixel(int x, int y)
        {
            if (x >= 0 && x < _size.Width && y >= 0 && y < _size.Height)
            {
                var i = (y * _size.Width) + x;
                return _colors[i];
            }
            return null;
        }
    }

    class FileImage : CanvasBitmapImage
    {
        public static async Task<FileImage> CreateAsync(IImportedFile file)
        {
            var bitmap = await file.ImportFileAsync(GraphicsManager.Current.CanvasDevice);
            var image = new FileImage(bitmap, file);
            return image;
        }

        public IImportedFile File { get; }

        private FileImage(CanvasBitmap bitmap, IImportedFile file) : base(bitmap, file.File.Name)
        {
            File = file;
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
        private string _file1Name;
        private string _file2Name;

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
        public string DisplayName { get; }
        public BitmapSize Size => Diff.ColorDiffBitmap.SizeInPixels;

        public DiffImage(DiffResult diff, string file1Name, string file2Name)
        {
            Diff = diff;
            _file1Name = file1Name;
            _file2Name = file2Name;
            DisplayName = $"{_file1Name} vs {_file2Name}";
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

        public void RegenerateSurface()
        {
            Diff.ReplaceDeviceResources(GraphicsManager.Current.CanvasDevice);
            if (_surface != null)
            {
                UpdateSurface();
            }
        }

        public Color? GetColorFromPixel(int x, int y)
        {
            if (x >= 0 && x < Size.Width && y >= 0 && y < Size.Height)
            {
                var i = (y * Size.Width) + x;
                switch (_viewMode)
                {
                    case DiffViewMode.Color:
                        return Diff.ColorDiffPixels[i];
                    case DiffViewMode.Alpha:
                        return Diff.AlphaDiffPixels[i];
                }
            }
            return null;
        }
    }

    class CaptureImage : IImage
    {
        private Direct3D11Device _device;
        private SimpleCapture _capture;
        private bool _showCursor = true;
        private bool _isPlaying = true;
        private byte[] _pauseData = null;

        public GraphicsCaptureItem Item { get; }

        public string DisplayName { get; }
        public BitmapSize Size { get; }

        public CaptureImage(GraphicsCaptureItem item, Direct3D11Device device)
        {
            Item = item;
            DisplayName = item.DisplayName;
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
            if (!_isPlaying)
            {
                _isPlaying = true;
                _pauseData = null;
                _capture.Continue();
            }
        }

        public void Pause()
        {
            if (_isPlaying)
            {
                _isPlaying = false;
                _pauseData = _capture.Pause();
            }
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

        public void RegenerateSurface()
        {
            _device = GraphicsManager.Current.CaptureDevice;
            _capture.Recreate(_device);
        }

        public Color? GetColorFromPixel(int x, int y)
        {
            if (!_isPlaying)
            {
                if (x >= 0 && x < Size.Width && y >= 0 && y < Size.Height)
                {
                    var i = (y * Size.Width) + x;
                    i *= 4; // BGRA8
                    var b = _pauseData[i];
                    var g = _pauseData[i + 1];
                    var r = _pauseData[i + 2];
                    var a = _pauseData[i + 3];
                    return new Color { A = a, R = r, G = g, B = b };
                }
            }
            return null;
        }
    }
}
