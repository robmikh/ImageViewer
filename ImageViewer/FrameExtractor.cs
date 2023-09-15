using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    class VideoFrame
    {
        public CompositionDrawingSurface Surface { get; }
        public TimeSpan Timestamp { get; }
        public ulong FrameId { get; }

        public static async Task<List<VideoFrame>> ExtractFramesAsync(MediaSource source, SizeInt32 size, CompositionGraphicsDevice compGraphics, Direct3D11Device device)
        {
            var item = new MediaPlaybackItem(source);
            var stopwatch = new Stopwatch();

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
                    stopwatch.Stop();
                    global::System.Diagnostics.Debug.WriteLine($"VIDEO FRAME EXTRACTION TIME: {stopwatch.Elapsed}");
                }
            };
            player.MediaFailed += (s, a) =>
            {
                completion.SetCanceled();
            };
            player.MediaOpened += (s, a) =>
            {
                stopwatch.Start();
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
