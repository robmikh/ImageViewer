using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Diagnostics;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace ImageViewer
{
    public sealed partial class InvertableImage : UserControl
    {
        private static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register(nameof(SourcePath), typeof(string), typeof(InvertableImage), new PropertyMetadata(null, OnSourcePathPropertyChanged));
        private static readonly DependencyProperty InvertProperty = DependencyProperty.Register(nameof(Invert), typeof(bool), typeof(InvertableImage), new PropertyMetadata(null, OnInvertPropertyChanged));

        private static CompositionEffectFactory _effectFactory;
        private static readonly string EffectImageProperty = "Image";

        private static CompositionEffectFactory GetCompositionEffectFactory(Compositor compositor)
        {
            if (_effectFactory == null)
            {
                var effect = new InvertEffect
                {
                    Source = new CompositionEffectSourceParameter(EffectImageProperty),
                };
                _effectFactory = compositor.CreateEffectFactory(effect);
            }
            return _effectFactory;
        }

        private static void OnSourcePathPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = (InvertableImage)d;
            var newPath = e.NewValue as string;
            image.LoadImage(newPath == null ? null : new Uri($"ms-appx:///{newPath}"));
        }

        private static void OnInvertPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = (InvertableImage)d;
            var newInvert = (bool)e.NewValue;
            image.ApplyInvert(newInvert);
        }

        private Compositor _compositor;
        private InteropBrush _controlBrush;
        private CompositionEffectBrush _effectBrush;
        private CompositionSurfaceBrush _surfaceBrush;

        public InvertableImage()
        {
            this.InitializeComponent();
            _compositor = ElementCompositionPreview.GetElementVisual(MainGrid).Compositor;
            _surfaceBrush = _compositor.CreateSurfaceBrush();
            var effectFactory = GetCompositionEffectFactory(_compositor);
            _effectBrush = effectFactory.CreateBrush();
            _effectBrush.SetSourceParameter(EffectImageProperty, _surfaceBrush);
            _controlBrush = new InteropBrush(_surfaceBrush);
            MainGrid.Background = _controlBrush;
        }

        public string SourcePath
        {
            get { return (string)GetValue(SourcePathProperty); }
            set { SetValue(SourcePathProperty, value); }
        }

        public bool Invert
        {
            get { return (bool)GetValue(InvertProperty); }
            set { SetValue(InvertProperty, value); }
        }

        private async void LoadImage(Uri uri)
        {
            if (uri != null)
            {
                var graphicsManager = GraphicsManager.Current;
                var device = graphicsManager.CanvasDevice;
                var graphics = graphicsManager.CompositionGraphicsDevice;
                var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                using (var stream = await file.OpenAsync(FileAccessMode.Read))
                using (var document = await CanvasSvgDocument.LoadAsync(device, stream))
                {
                    var rootElement = document.Root;
                    Debug.Assert(document.Root.Tag == "svg");

                    if (rootElement.IsAttributeSpecified("viewBox"))
                    {
                        var viewBox = rootElement.GetRectangleAttribute("viewBox");
                        var size = new Size(viewBox.Width, viewBox.Height);
                        var surface = graphics.CreateDrawingSurface(size, DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
                        using (var drawingSession = CanvasComposition.CreateDrawingSession(surface))
                        {
                            drawingSession.Clear(Colors.Transparent);
                            drawingSession.DrawSvg(document, size);
                        }
                        _surfaceBrush.Surface = surface;
                    }
                    else
                    {
                        Debug.WriteLine($"No viewbox found for {uri}");
                    }
                }
            }
            else
            {
                _surfaceBrush.Surface = null;
            }
        }

        private void ApplyInvert(bool invert)
        {
            if (invert)
            {
                _controlBrush.SetBrush(_effectBrush);
            }
            else
            {
                _controlBrush.SetBrush(_surfaceBrush);
            }
        }
    }
}
