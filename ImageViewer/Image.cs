﻿using ImageViewer.ScreenCapture;
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
        BitmapSize Size { get; }
        // TODO: Format?
        Task SaveSnapshotToStreamAsync(IRandomAccessStream stream);
        ICompositionSurface CreateSurface(CompositionGraphicsDevice graphics);
        void RegenerateSurface();
    }

    class CanvasBitmapImage : IImage
    {
        private CompositionDrawingSurface _surface;
        private byte[] _bytes;
        private BitmapSize _size;

        public CanvasBitmap Bitmap { get; private set; }

        public BitmapSize Size => _size;

        public CanvasBitmapImage(CanvasBitmap bitmap)
        {
            Bitmap = bitmap;
            _size = Bitmap.SizeInPixels;
            _bytes = bitmap.GetPixelBytes();
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
            Bitmap = CanvasBitmap.CreateFromBytes(GraphicsManager.Current.CanvasDevice, _bytes, (int)_size.Width, (int)_size.Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
            if (_surface != null)
            {
                UpdateSurface();
            }
        }
    }

    class FileImage : IImage
    {
        public static async Task<FileImage> CreateAsync(IImportedFile file)
        {
            var bitmap = await file.ImportFileAsync(GraphicsManager.Current.CanvasDevice);
            var image = new FileImage(bitmap, file);
            return image;
        }

        private CanvasBitmap _bitmap;
        private CompositionDrawingSurface _surface;

        public CanvasBitmap Bitmap => _bitmap;
        public IImportedFile File { get; }

        public BitmapSize Size => _bitmap.SizeInPixels;

        private FileImage(CanvasBitmap bitmap, IImportedFile file)
        {
            _bitmap = bitmap;
            File = file;
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

        public async void RegenerateSurface()
        {
            _bitmap = await File.ImportFileAsync(GraphicsManager.Current.CanvasDevice);
            if (_surface != null)
            {
                UpdateSurface();
            }
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

        public void RegenerateSurface()
        {
            Diff.ReplaceDeviceResources(GraphicsManager.Current.CanvasDevice);
            if (_surface != null)
            {
                UpdateSurface();
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

        public void RegenerateSurface()
        {
            _device = GraphicsManager.Current.CaptureDevice;
            _capture.Recreate(_device);
        }
    }
}
