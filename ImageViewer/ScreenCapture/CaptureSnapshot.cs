using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WinRTInteropTools;

namespace ImageViewer.ScreenCapture
{
    static class CaptureSnapshot
    {
        public static async Task<Direct3D11Texture2D> TakeAsync(GraphicsCaptureItem item, BitmapSize bitmapSize, Direct3D11Device device)
        {
            var size = new SizeInt32();
            size.Width = (int)bitmapSize.Width;
            size.Height = (int)bitmapSize.Height;

            var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, size);
            var session = framePool.CreateCaptureSession(item);

            var completionSource = new TaskCompletionSource<Direct3D11Texture2D>();
            framePool.FrameArrived += (s, a) =>
            {
                using (var lockSession = device.Multithread.Lock())
                using (var frame = s.TryGetNextFrame())
                {
                    var frameTexture = Direct3D11Texture2D.CreateFromDirect3DSurface(frame.Surface);
                    var description = frameTexture.Description2D;
                    description.Usage = Direct3DUsage.Staging;
                    description.BindFlags = 0;
                    description.CpuAccessFlags = Direct3D11CpuAccessFlag.AccessRead;
                    description.MiscFlags = 0;
                    var copyTexture = device.CreateTexture2D(description);

                    device.ImmediateContext.CopyResource(copyTexture, frameTexture);

                    s.Dispose();
                    framePool.Dispose();

                    completionSource.SetResult(copyTexture);
                }
            };

            session.StartCapture();
            var texture = await completionSource.Task;
            return texture;
        }
    }
}
