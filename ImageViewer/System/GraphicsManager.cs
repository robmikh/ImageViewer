using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using Windows.Graphics.DirectX.Direct3D11;
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
        private DeviceLostWatcher _canvasWatcher;
        private DeviceLostWatcher _captureWatcher;

        private GraphicsManager(Compositor compositor, CanvasDevice canvasDevice)
        {
            Compositor = compositor;
            CanvasDevice = canvasDevice;
            CompositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, canvasDevice);
            _canvasWatcher = new DeviceLostWatcher();
            _captureWatcher = new DeviceLostWatcher();

            _canvasWatcher.DeviceLost += OnCanvasDeviceLost;
            _canvasWatcher.WatchDevice(CanvasDevice);
            _captureWatcher.DeviceLost += OnCaptureDeviceLost;
        }

        private void OnCaptureDeviceLost(object sender, IDirect3DDevice e)
        {
            _captureDevice?.Dispose();
            _captureDevice = null;
            var device = CaptureDevice;
            CaptureDeviceReplaced?.Invoke(this, device);
        }

        private void OnCanvasDeviceLost(object sender, IDirect3DDevice e)
        {
            var canvasDevice = (CanvasDevice)e;
            canvasDevice.Dispose();
            CanvasDevice = new CanvasDevice();
            _canvasWatcher.WatchDevice(CanvasDevice);
            CanvasComposition.SetCanvasDevice(CompositionGraphicsDevice, CanvasDevice);
        }

        public Compositor Compositor { get; }
        public CanvasDevice CanvasDevice { get; private set; }
        public CompositionGraphicsDevice CompositionGraphicsDevice { get; }
        public Direct3D11Device CaptureDevice
        {
            get
            {
                if (_captureDevice == null)
                {
                    _captureDevice = new Direct3D11Device();
                    _captureWatcher.WatchDevice(_captureDevice);
                }
                return _captureDevice;
            }
        }

        public event EventHandler<Direct3D11Device> CaptureDeviceReplaced;
    }
}
