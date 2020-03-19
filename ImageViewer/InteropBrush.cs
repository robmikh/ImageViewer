using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
