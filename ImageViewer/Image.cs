using ImageViewer.ScreenCapture;
using ImageViewer.System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
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
            byte[] bytes = null;
            if (_isPlaying)
            {
                var texture = await CaptureSnapshot.TakeAsync(Item, Size, _device);
                bytes = texture.GetBytes();
            }
            else
            {
                bytes = (byte[])_pauseData.Clone();
            }

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

    class VideoImage : IImage
    {
        public static async Task<VideoImage> CreateAsync(StorageFile file)
        {
            var source = MediaSource.CreateFromStorageFile(file);
            await source.OpenAsync();
            var item = new MediaPlaybackItem(source);
            var image = new VideoImage(file, item);
            return image;
        }

        private StorageFile _file;
        private MediaPlayer _player;
        private MediaPlayerSurface _surface;
        private bool _isPlaying = false;
        private Color[] _pauseData = null;
        private Timer _pauseDataUpdateTimer = null;
        private DispatcherQueue _dispatcherQueue = null;
        private DispatcherQueueTimer _playbackTimer = null;
        private Windows.UI.Xaml.Controls.Slider _boundSlider = null;

        private VideoImage(StorageFile file, MediaPlaybackItem item)
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _file = file;
            _player = new MediaPlayer();
            _player.IsLoopingEnabled = true;
            _player.Volume = 0;
            _player.Source = item;
            var compositor = GraphicsManager.Current.Compositor;
            _surface = _player.GetSurface(compositor);

            var size = new BitmapSize() { Width = 0, Height = 0 };
            foreach (var track in item.VideoTracks)
            {
                var properties = track.GetEncodingProperties();
                if (size.Width < properties.Width && size.Height < properties.Height)
                {
                    size.Width = properties.Width;
                    size.Height = properties.Height;
                }
            }

            _player.SetSurfaceSize(new Size(size.Width, size.Height));
            Size = size;
            DisplayName = file.Name;
            Play();
        }

        public string DisplayName { get; }

        public BitmapSize Size { get; }

        public void Play()
        {
            if (!_isPlaying)
            {
                _isPlaying = true;
                _pauseData = null;
                _player.Play();
            }
        }

        public void Pause()
        {
            if (_isPlaying)
            {
                _isPlaying = false;
                _player.Pause();
                UpdatePauseData();
            }
        }

        public void NextFrame()
        {
            _player.StepForwardOneFrame();
            QueueDelayedUpdateToPauseData();
        }

        public void PreviousFrame()
        {
            _player.StepBackwardOneFrame();
            QueueDelayedUpdateToPauseData();
        }

        public void BindToSlider(Windows.UI.Xaml.Controls.Slider slider)
        {
            UnbindSlider();
            _boundSlider = slider;
            slider.Minimum = 0;
            slider.Maximum = _player.PlaybackSession.NaturalDuration.TotalSeconds;
            slider.StepFrequency = 1;
            slider.ValueChanged += OnSliderValueChanged;
            if (_playbackTimer == null)
            {
                _playbackTimer = _dispatcherQueue.CreateTimer();
                _playbackTimer.Tick += (s, a) =>
                {
                    if (_boundSlider != null)
                    {
                        _boundSlider.Value = _player.PlaybackSession.Position.TotalSeconds;
                    }
                };
            }
            _playbackTimer.Interval = TimeSpan.FromSeconds(1);
            _playbackTimer.Start();
        }

        private void UnbindSlider()
        {
            if (_boundSlider != null)
            {
                _playbackTimer?.Stop();
                _boundSlider.ValueChanged -= OnSliderValueChanged;
                _boundSlider = null;
            }
        }

        private void OnSliderValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            _player.PlaybackSession.Position = TimeSpan.FromSeconds(e.NewValue);
            if (!_isPlaying)
            {
                QueueDelayedUpdateToPauseData(3000);
            }
        }

        private CanvasBitmap GetCurrentBitmap()
        {
            var device = GraphicsManager.Current.CanvasDevice;
            var renderTarget = new CanvasRenderTarget(device, Size.Width, Size.Height, 96.0f);
            _player.CopyFrameToVideoSurface(renderTarget);
            return renderTarget;
        }

        private void UpdatePauseData()
        {
            using (var bitmap = GetCurrentBitmap())
            {
                _pauseData = bitmap.GetPixelColors();
            }
        }

        private void QueueDelayedUpdateToPauseData(int delayInMilliseconds = 1000)
        {
            if (_pauseDataUpdateTimer == null)
            {
                _pauseDataUpdateTimer = new Timer(OnTimerTick, this, Timeout.Infinite, Timeout.Infinite);
            }
            _pauseDataUpdateTimer.Change(delayInMilliseconds, Timeout.Infinite);
        }

        private void OnTimerTick(object state)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                UpdatePauseData();
            });
        }

        public ICompositionSurface CreateSurface(CompositionGraphicsDevice graphics)
        {
            return _surface.CompositionSurface;
        }

        public void Dispose()
        {
            if (_player != null)
            {
                UnbindSlider();
                _player.Pause();
                _player.Dispose();
                _player = null;
            }
        }

        public Color? GetColorFromPixel(int x, int y)
        {
            if (!_isPlaying && _pauseData != null)
            {
                if (x >= 0 && x < Size.Width && y >= 0 && y < Size.Height)
                {
                    var i = (y * Size.Width) + x;
                    return _pauseData[i];
                }
            }
            return null;
        }

        public void RegenerateSurface()
        {
            // Do nothing
        }

        public async Task SaveSnapshotToStreamAsync(IRandomAccessStream stream)
        {
            using (var bitmap = GetCurrentBitmap())
            {
                await bitmap.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            }
        }
    }
}
