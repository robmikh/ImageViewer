using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using WinRTInteropTools;

namespace ImageViewer.System
{
    // TODO: Handle device lost
    class GraphicsManager
    {
        private static GraphicsManager _graphicsManager;
        public static GraphicsManager Current
        {
            get
            {
                if (_graphicsManager == null)
                {
                    _graphicsManager = new GraphicsManager(Window.Current.Compositor, new CanvasDevice());
                }
                return _graphicsManager;
            }
        }

        private Direct3D11Device _captureDevice = null;

        private GraphicsManager(Compositor compositor, CanvasDevice canvasDevice)
        {
            Compositor = compositor;
            CanvasDevice = canvasDevice;
            CompositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, canvasDevice);
        }

        public Compositor Compositor { get; }
        public CanvasDevice CanvasDevice { get; }
        public CompositionGraphicsDevice CompositionGraphicsDevice { get; }
        public Direct3D11Device CaptureDevice
        {
            get
            {
                if (_captureDevice == null)
                {
                    _captureDevice = new Direct3D11Device();
                }
                return _captureDevice;
            }
        }
    }
}
