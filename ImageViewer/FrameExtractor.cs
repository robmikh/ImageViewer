using ImageViewerNative;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using WinRTInteropTools;

namespace ImageViewer
{
    class VideoFrame
    {
        public CompositionDrawingSurface Thumbnail { get; private set; }
        public Direct3D11Texture2D Surface { get; }
        public TimeSpan Timestamp { get; }
        public ulong FrameId { get; }

        public static async Task<List<VideoFrame>> ExtractFramesAsync(IRandomAccessStream stream, Direct3D11Device device, CompositionGraphicsDevice compGraphics)
        {
            var frames = await Task.Run(() =>
            {
                var result = new List<VideoFrame>();
                var context = device.ImmediateContext;
                var multithread = device.Multithread;
                multithread.IsMultithreadProtected = true;

                VideoFrameExtractor.ExtractFromStream(stream, device, (s, args) =>
                {
                    var sourceSurface = args.Surface;
                    var sourceDescription = sourceSurface.Description;

                    Direct3D11Texture2D texture;
                    using (var session = multithread.Lock())
                    {
                        var description = new Direct3D11Texture2DDescription();
                        description.Base = new Direct3DSurfaceDescription();
                        description.Base.Format = DirectXPixelFormat.B8G8R8A8UIntNormalized;
                        description.Base.Width = sourceDescription.Width;
                        description.Base.Height = sourceDescription.Height;
                        description.Base.MultisampleDescription = new Direct3DMultisampleDescription();
                        description.Base.MultisampleDescription.Count = 1;
                        description.Base.MultisampleDescription.Quality = 0;
                        description.ArraySize = 1;
                        description.BindFlags = Direct3DBindings.ShaderResource;
                        description.CpuAccessFlags = 0;
                        description.MiscFlags = 0;
                        description.MipLevels = 1;
                        texture = device.CreateTexture2D(description);
                        context.CopyResource(texture, sourceSurface);
                    }

                    // TODO: For some reason creating surfaces off the UI thread prevents the surfaces from
                    //       working properly. We'll make the surfaces and issue the copies later.

                    result.Add(new VideoFrame(null, texture, args.Timestamp, args.FrameId));
                });

                return result;
            });

            var sourceDescription2 = frames.First().Surface.Description;
            foreach (var frame in frames)
            {
                var surface = compGraphics.CreateDrawingSurface2(
                    new SizeInt32() { Width = sourceDescription2.Width, Height = sourceDescription2.Height },
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    DirectXAlphaMode.Premultiplied);
                frame.Thumbnail = surface;

                CompositionGraphics.CopyDirect3DSurfaceIntoCompositionSurface(device, frame.Surface, frame.Thumbnail);
            }

            return frames;
        }

        private VideoFrame(CompositionDrawingSurface thumbnail, Direct3D11Texture2D surface, TimeSpan timestamp, ulong frameId)
        {
            Thumbnail = thumbnail;
            Surface = surface;
            Timestamp = timestamp;
            FrameId = frameId;
        }
    }
}
