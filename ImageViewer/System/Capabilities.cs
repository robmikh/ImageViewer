using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;

namespace ImageViewer.System
{
    static class Capabilities
    {
        private static bool? _captureBorder = null;
        public static bool IsCaptureBorderPropertyAvailable
        {
            get
            {
                if (!_captureBorder.HasValue)
                {
                    _captureBorder = ApiInformation.IsTypePresent(typeof(GraphicsCaptureAccess).FullName);
                }
                return _captureBorder.Value;
            }
        }
    }
}
