using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace ImageViewer.Controls
{
    internal class MeasureSpace : UserControl
    {
        private static readonly DependencyProperty MeasurePositionXProperty = DependencyProperty.Register(nameof(MeasurePositionX), typeof(int), typeof(MeasureSpace), new PropertyMetadata(0, OnMeasurePositionXPropertyChanged));
        private static readonly DependencyProperty MeasurePositionYProperty = DependencyProperty.Register(nameof(MeasurePositionY), typeof(int), typeof(MeasureSpace), new PropertyMetadata(0, OnMeasurePositionYPropertyChanged));
        private static readonly DependencyProperty MeasureWidthProperty = DependencyProperty.Register(nameof(MeasureWidth), typeof(int), typeof(MeasureSpace), new PropertyMetadata(0, OnMeasureWidthPropertyChanged));
        private static readonly DependencyProperty MeasureHeightProperty = DependencyProperty.Register(nameof(MeasureHeight), typeof(int), typeof(MeasureSpace), new PropertyMetadata(0, OnMeasureHeightPropertyChanged));
        private static readonly DependencyProperty MeasureColorProperty = DependencyProperty.Register(nameof(MeasureColor), typeof(Color), typeof(MeasureSpace), new PropertyMetadata(Colors.Red, OnMeasureColorPropertyChanged));

        private static void OnMeasurePositionXPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (MeasureSpace)d;
            instance.OnMeasurePositionXChanged();
        }
        private static void OnMeasurePositionYPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (MeasureSpace)d;
            instance.OnMeasurePositionYChanged();
        }
        private static void OnMeasureWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (MeasureSpace)d;
            instance.OnMeasureWidthChanged();
        }
        private static void OnMeasureHeightPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (MeasureSpace)d;
            instance.OnMeasureHeightChanged();
        }
        private static void OnMeasureColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (MeasureSpace)d;
            instance.OnMeasureColorChanged();
        }

        private void OnMeasurePositionXChanged()
        {
            _measureRoot.Offset = new Vector3(MeasurePositionX, MeasurePositionY, 0);
        }
        private void OnMeasurePositionYChanged()
        {
            _measureRoot.Offset = new Vector3(MeasurePositionX, MeasurePositionY, 0);
        }
        private void OnMeasureWidthChanged()
        {
            _visual.Size = new Vector2(MeasureWidth * InternalSpaceScale, MeasureHeight * InternalSpaceScale) + (2 * new Vector2(Thickness, Thickness));
            _measureRoot.Size = new Vector2(MeasureWidth, MeasureHeight);
        }
        private void OnMeasureHeightChanged()
        {
            _visual.Size = new Vector2(MeasureWidth * InternalSpaceScale, MeasureHeight * InternalSpaceScale) + (2 * new Vector2(Thickness, Thickness));
            _measureRoot.Size = new Vector2(MeasureWidth, MeasureHeight);
        }
        private void OnMeasureColorChanged()
        {
            _colorBrush.Color = MeasureColor;
        }

        public int MeasurePositionX
        {
            get { return (int)GetValue(MeasurePositionXProperty); }
            set { SetValue(MeasurePositionXProperty, value); }
        }

        public int MeasurePositionY
        {
            get { return (int)GetValue(MeasurePositionYProperty); }
            set { SetValue(MeasurePositionYProperty, value); }
        }

        public int MeasureWidth
        {
            get { return (int)GetValue(MeasureWidthProperty); }
            set { SetValue(MeasureWidthProperty, value); }
        }

        public int MeasureHeight
        {
            get { return (int)GetValue(MeasureHeightProperty); }
            set { SetValue(MeasureHeightProperty, value); }
        }

        public Color MeasureColor
        {
            get { return (Color)GetValue(MeasureColorProperty); }
            set { SetValue(MeasureColorProperty, value); }
        }

        private const int InternalSpaceScale = 10;
        private const int Thickness = (int)(0.25f * InternalSpaceScale);

        private ContainerVisual _root;
        private ContainerVisual _measureRoot;
        private SpriteVisual _visual;
        private CompositionColorBrush _colorBrush;

        public MeasureSpace()
        {
            var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _colorBrush = compositor.CreateColorBrush(MeasureColor);

            var nineGridBrush = compositor.CreateNineGridBrush();
            nineGridBrush.SetInsets(Thickness);
            nineGridBrush.IsCenterHollow = true;
            nineGridBrush.Source = _colorBrush;

            _visual = compositor.CreateSpriteVisual();
            _visual.Size = new Vector2(MeasureWidth * InternalSpaceScale, MeasureHeight * InternalSpaceScale) + (2.0f * new Vector2(Thickness, Thickness));
            _visual.Offset = new Vector3(-Thickness, -Thickness, 0);
            _visual.Brush = nineGridBrush;

            var scaleVisual = compositor.CreateContainerVisual();
            scaleVisual.RelativeSizeAdjustment = new Vector2(1, 1);
            scaleVisual.Scale = new Vector3(1.0f/InternalSpaceScale, 1.0f/InternalSpaceScale, 1);
            scaleVisual.Children.InsertAtTop(_visual);

            _measureRoot = compositor.CreateContainerVisual();
            _measureRoot.Size = new Vector2(MeasureWidth, MeasureHeight);
            _measureRoot.Offset = new Vector3(MeasurePositionX, MeasurePositionY, 0);
            _measureRoot.Children.InsertAtTop(scaleVisual);

            _root = compositor.CreateContainerVisual();
            _root.RelativeSizeAdjustment = new Vector2(1, 1);
            _root.Children.InsertAtTop(_measureRoot);
            ElementCompositionPreview.SetElementChildVisual(this, _root);
        }
    }
}
