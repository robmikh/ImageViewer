using Windows.UI.Composition;
using Windows.UI.Xaml.Media;

namespace ImageViewer
{
    class InteropBrush : XamlCompositionBrushBase
    {
        public InteropBrush(CompositionBrush brush)
        {
            CompositionBrush = brush;
        }
    }
}
