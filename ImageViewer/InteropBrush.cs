using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace ImageViewer
{
    class InteropBrush : XamlCompositionBrushBase
    {
        public InteropBrush(CompositionBrush brush)
        {
            CompositionBrush = brush;
        }

        public void SetBrush(CompositionBrush brush)
        {
            CompositionBrush = brush;
        }
    }

    class BindableCompositionSurfaceBrush : XamlCompositionBrushBase
    {
        private static readonly DependencyProperty SurfaceProperty = DependencyProperty.Register(nameof(Surface), typeof(ICompositionSurface), typeof(BindableCompositionSurfaceBrush), new PropertyMetadata(null, OnSurfaceChanged));

        private static void OnSurfaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var oldValue = e.OldValue as ICompositionSurface;
            var newValue = e.NewValue as ICompositionSurface;

            if (oldValue != newValue)
            {
                var self = (BindableCompositionSurfaceBrush)d;
                self.SetSurface(newValue);
            }
        }

        CompositionSurfaceBrush EnsureBrush(Compositor compositor)
        {
            if (CompositionBrush == null)
            {
                var brush = compositor.CreateSurfaceBrush();
                brush.Stretch = CompositionStretch.Uniform;
                CompositionBrush = brush;
            }
            return (CompositionSurfaceBrush)CompositionBrush;
        }

        public ICompositionSurface Surface
        {
            get { return (ICompositionSurface)GetValue(SurfaceProperty); }
            set { SetValue(SurfaceProperty, value); }
        }

        private void SetSurface(ICompositionSurface surface)
        {
            var brush = EnsureBrush(Window.Current.Compositor);
            brush.Surface = surface;
        }
    }
}
