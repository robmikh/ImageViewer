using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Xaml.Controls.Maps;
using WinRTInteropTools;

namespace ImageViewer
{
    /*
    class FrameExtractor
    {
        private MediaPlayer _player;
        private SizeInt32 _size;
        private CompositionGraphicsDevice _compGraphics;
        private Direct3D11Device _device;
        private Direct3D11Texture2D _scratchTexture;

        private bool _ended = false;
        private TaskCompletionSource<bool> _currentFrameSource = null;
        private TaskCompletionSource<bool> _endedSource;

        public FrameExtractor(MediaSource source, CompositionGraphicsDevice compGraphics, Direct3D11Device device)
        {
            var item = new MediaPlaybackItem(source);
            var bitmapSize = new BitmapSize() { Width = 0, Height = 0 };
            foreach (var track in item.VideoTracks)
            {
                var properties = track.GetEncodingProperties();
                if (bitmapSize.Width < properties.Width && bitmapSize.Height < properties.Height)
                {
                    bitmapSize.Width = properties.Width;
                    bitmapSize.Height = properties.Height;
                }
            }
            _size = new SizeInt32() { Width = (int)bitmapSize.Width, Height = (int)bitmapSize.Height };

            _compGraphics = compGraphics;
            _device = device;
            _endedSource = new TaskCompletionSource<bool>();

            var description = new Direct3D11Texture2DDescription();
            description.Base = new Direct3DSurfaceDescription();
            description.Base.Format = DirectXPixelFormat.B8G8R8A8UIntNormalized;
            description.Base.Width = _size.Width;
            description.Base.Height = _size.Height;
            description.Base.MultisampleDescription = new Direct3DMultisampleDescription();
            description.Base.MultisampleDescription.Count = 1;
            description.Base.MultisampleDescription.Quality = 0;
            description.ArraySize = 1;
            description.BindFlags = Direct3DBindings.ShaderResource;
            description.CpuAccessFlags = 0;
            description.MiscFlags = 0;
            description.MipLevels = 1;
            _scratchTexture = device.CreateTexture2D(description);

            _player = new MediaPlayer();
            _player.IsVideoFrameServerEnabled = true;
            _player.AutoPlay = false;
            _player.VideoFrameAvailable += OnVideoFrameAvailable;
            _player.MediaEnded += OnMediaEnded;
            _player.Media
            _player.Source = item;
            _player.Pause();
        }

        public async Task<VideoFrame> TryGetNextFrameAsync()
        {
            if (_ended)
            {
                return null;
            }

            //global::System.Diagnostics.Debug.WriteLine("Get Frame Start");

            global::System.Diagnostics.Debug.Assert(_currentFrameSource == null);
            _currentFrameSource = new TaskCompletionSource<bool>();
            _player.StepForwardOneFrame();

            VideoFrame result = null;
            var task = await Task.WhenAny(_currentFrameSource.Task, _endedSource.Task);
            if (task == _currentFrameSource.Task && task.Result)
            {
                _player.CopyFrameToVideoSurface(_scratchTexture);
                var surface = _compGraphics.CreateDrawingSurface2(_size, DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
                CompositionGraphics.CopyDirect3DSurfaceIntoCompositionSurface(_device, _scratchTexture, surface);
                var timestamp = _player.PlaybackSession.Position;

                result = new VideoFrame(surface, timestamp);
            }
            _currentFrameSource = null;

            //global::System.Diagnostics.Debug.WriteLine("Get Frame End");

            return result;
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            _ended = true;
            _endedSource.SetResult(false);
        }

        private void OnVideoFrameAvailable(MediaPlayer sender, object args)
        {
            //global::System.Diagnostics.Debug.WriteLine("Frame Available");
            if (_currentFrameSource != null && !_currentFrameSource.Task.IsCompleted)
            {
                _currentFrameSource.SetResult(true);
            }
        }

        public SizeInt32 Size => _size;
    }
    */

    class VideoFrame
    {
        public CompositionDrawingSurface Surface { get; }
        public TimeSpan Timestamp { get; }
        public ulong FrameId { get; }

        public static async Task<List<VideoFrame>> ExtractFramesAsync(MediaSource source, SizeInt32 size, CompositionGraphicsDevice compGraphics, Direct3D11Device device)
        {
            var item = new MediaPlaybackItem(source);

            var description = new Direct3D11Texture2DDescription();
            description.Base = new Direct3DSurfaceDescription();
            description.Base.Format = DirectXPixelFormat.B8G8R8A8UIntNormalized;
            description.Base.Width = size.Width;
            description.Base.Height = size.Height;
            description.Base.MultisampleDescription = new Direct3DMultisampleDescription();
            description.Base.MultisampleDescription.Count = 1;
            description.Base.MultisampleDescription.Quality = 0;
            description.ArraySize = 1;
            description.BindFlags = Direct3DBindings.ShaderResource;
            description.CpuAccessFlags = 0;
            description.MiscFlags = 0;
            description.MipLevels = 1;
            var scratchTexture = device.CreateTexture2D(description);

            var completion = new TaskCompletionSource<List<VideoFrame>>();
            var frames = new List<VideoFrame>();
            ulong frameId = 0;
            var player = new MediaPlayer();
            player.IsVideoFrameServerEnabled = true;
            player.VideoFrameAvailable += (s, a) =>
            {
                lock (frames)
                {
                    player.CopyFrameToVideoSurface(scratchTexture);
                    var surface = compGraphics.CreateDrawingSurface2(size, DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
                    CompositionGraphics.CopyDirect3DSurfaceIntoCompositionSurface(device, scratchTexture, surface);
                    var timestamp = player.PlaybackSession.Position;
                    frames.Add(new VideoFrame(surface, timestamp, ++frameId));
                    player.StepForwardOneFrame();
                }
            };
            player.MediaEnded += (s, a) =>
            {
                lock (frames)
                {
                    completion.SetResult(frames);
                }
            };
            player.MediaFailed += (s, a) =>
            {
                completion.SetCanceled();
            };
            player.MediaOpened += (s, a) =>
            {
                player.StepForwardOneFrame();
            };
            player.AutoPlay = false;
            player.Source = item;

            return await completion.Task;
        }

        private VideoFrame(CompositionDrawingSurface surface, TimeSpan timestamp, ulong frameId)
        {
            Surface = surface;
            Timestamp = timestamp;
            FrameId = frameId;
        }
    }
}
