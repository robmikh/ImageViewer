using ImageViewerNative;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
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
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using WinRTInteropTools;

namespace ImageViewer
{
    class VideoFrame
    {
        public CompositionDrawingSurface Surface { get; }
        public TimeSpan Timestamp { get; }
        public ulong FrameId { get; }

        public static async Task<List<VideoFrame>> ExtractFramesAsync(IRandomAccessStream stream, SizeInt32 size, CompositionGraphicsDevice compGraphics, Direct3D11Device device)
        {
            var frames = await Task.Run(() =>
            {
                var result = new List<VideoFrame>();

                VideoFrameExtractor.ExtractFromStream(stream, device, (s, args) =>
                {
                    var surface = compGraphics.CreateDrawingSurface2(size, DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
                    CompositionGraphics.CopyDirect3DSurfaceIntoCompositionSurface(device, args.Surface, surface);
                    result.Add(new VideoFrame(surface, args.Timestamp, args.FrameId));
                });

                return result;
            });

            return frames;
        }

        private VideoFrame(CompositionDrawingSurface surface, TimeSpan timestamp, ulong frameId)
        {
            Surface = surface;
            Timestamp = timestamp;
            FrameId = frameId;
        }
    }
}
