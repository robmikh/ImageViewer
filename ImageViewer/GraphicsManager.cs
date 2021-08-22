using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace ImageViewer
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

        private GraphicsManager(Compositor compositor, CanvasDevice canvasDevice)
        {
            Compositor = compositor;
            CanvasDevice = canvasDevice;
            CompositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, canvasDevice);
        }

        public Compositor Compositor { get; }
        public CanvasDevice CanvasDevice { get; }
        public CompositionGraphicsDevice CompositionGraphicsDevice { get; }
    }
}
