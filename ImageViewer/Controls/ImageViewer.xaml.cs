using ImageViewer.System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace ImageViewer.Controls
{
    public struct PositionInt32
    {
        public int X;
        public int Y;
    }

    public enum InputMode
    {
        None,
        Drag,
        Measure
    }

    public sealed partial class ImageViewer : UserControl
    {
        private static readonly DependencyProperty MeasurePositionXProperty = DependencyProperty.Register(nameof(MeasurePositionX), typeof(int), typeof(ImageViewer), new PropertyMetadata(0));
        private static readonly DependencyProperty MeasurePositionYProperty = DependencyProperty.Register(nameof(MeasurePositionY), typeof(int), typeof(ImageViewer), new PropertyMetadata(0));
        private static readonly DependencyProperty MeasureWidthProperty = DependencyProperty.Register(nameof(MeasureWidth), typeof(int), typeof(ImageViewer), new PropertyMetadata(0));
        private static readonly DependencyProperty MeasureHeightProperty = DependencyProperty.Register(nameof(MeasureHeight), typeof(int), typeof(ImageViewer), new PropertyMetadata(0));
        private static readonly DependencyProperty InputModeProperty = DependencyProperty.Register(nameof(InputMode), typeof(InputMode), typeof(ImageViewer), new PropertyMetadata(InputMode.Drag, OnInputModePropertyChanged));
        private static readonly DependencyProperty AreGridLinesVisibleProperty = DependencyProperty.Register(nameof(AreGridLinesVisible), typeof(bool), typeof(ImageViewer), new PropertyMetadata(false, OnAreGridLinesVisiblePropertyChanged));
        private static readonly DependencyProperty IsBorderVisibleProperty = DependencyProperty.Register(nameof(IsBorderVisible), typeof(bool), typeof(ImageViewer), new PropertyMetadata(true, OnIsBorderVisiblePropertyChanged));
        private static readonly DependencyProperty CursorPositionProperty = DependencyProperty.Register(nameof(CursorPosition), typeof(PositionInt32?), typeof(ImageViewer), new PropertyMetadata(null));
        private static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(IImage), typeof(ImageViewer), new PropertyMetadata(null, OnImagePropertyChanged));
        private static readonly DependencyProperty GridLinesColorProperty = DependencyProperty.Register(nameof(GridLinesColor), typeof(Color), typeof(ImageViewer), new PropertyMetadata(Colors.LightGray, OnGridLinesColorChanged));
        private static readonly DependencyProperty BorderColorProperty = DependencyProperty.Register(nameof(BorderColor), typeof(Color), typeof(ImageViewer), new PropertyMetadata(Colors.Black));
        private static readonly DependencyProperty MeasureColorProperty = DependencyProperty.Register(nameof(MeasureColor), typeof(Color), typeof(ImageViewer), new PropertyMetadata(Colors.Red));
        private static readonly DependencyProperty CurrentColorProperty = DependencyProperty.Register(nameof(CurrentColor), typeof(Color?), typeof(ImageViewer), new PropertyMetadata(null));

        private static void OnInputModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var viewer = (ImageViewer)d;
            viewer.OnInputModeChanged();
        }

        private static void OnAreGridLinesVisiblePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var viewer = (ImageViewer)d;
            viewer.UpdateGridLines();
        }

        private static void OnIsBorderVisiblePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var viewer = (ImageViewer)d;
            viewer.UpdateBorder();
        }

        private static void OnImagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var viewer = (ImageViewer)d;
            var oldImage = e.OldValue as IImage;
            oldImage?.Dispose();
            var newImage = e.NewValue as IImage;
            viewer.OnImageChnaged();
        }

        private static void OnGridLinesColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var viewer = (ImageViewer)d;
            var color = (Color)e.NewValue;
            var brush = viewer.CreateGridLinesBrush(viewer._canvasDevice, color);
            viewer._gridLinesCavnasBrush = brush;
            if (viewer.AreGridLinesVisible)
            {
                viewer.GenerateGridLines();
            }
        }

        private Compositor _compositor;
        private CanvasDevice _canvasDevice;
        private CompositionGraphicsDevice _compositionGraphics;

        private ICanvasBrush _backgroundCavnasBrush;
        private ICanvasBrush _gridLinesCavnasBrush;

        private CompositionSurfaceBrush _backgroundBrush;
        private CompositionSurfaceBrush _imageBrush;
        private CompositionSurfaceBrush _gridLinesBrush;

        private Point _lastPosition;
        private Point _startMeasurePoint;

        public ImageViewer()
        {
            this.InitializeComponent();

            var graphicsManager = GraphicsManager.Current;
            _canvasDevice = graphicsManager.CanvasDevice;
            _compositor = graphicsManager.Compositor;
            _compositionGraphics = graphicsManager.CompositionGraphicsDevice;

            // Generate the background bitmap
            _backgroundCavnasBrush = CreateBackgroundBrush(_canvasDevice);
            _gridLinesCavnasBrush = CreateGridLinesBrush(_canvasDevice, GridLinesColor);

            // Create brushes
            _backgroundBrush = _compositor.CreateSurfaceBrush();
            _backgroundBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
            _imageBrush = _compositor.CreateSurfaceBrush();
            _imageBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
            _gridLinesBrush = _compositor.CreateSurfaceBrush();
            _gridLinesBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;

            ImageGrid.Background = new InteropBrush(_backgroundBrush);
            ImageRectangle.Fill = new InteropBrush(_imageBrush);
            GridLinesRectangle.Fill = new InteropBrush(_gridLinesBrush);
            ImageBorder.Visibility = Visibility.Collapsed;

            graphicsManager.CompositionGraphicsDevice.RenderingDeviceReplaced += OnRenderingDeviceReplaced;
            graphicsManager.CaptureDeviceReplaced += OnCaptureDeviceReplaced;
            Unloaded += OnUnloaded;

            var displayInfo = DisplayInformation.GetForCurrentView();
            var dpiScale = (float)displayInfo.RawPixelsPerViewPixel;
            ProcessDpiChanged(dpiScale);
            displayInfo.DpiChanged += OnDpiChanged;
        }

        private void OnDpiChanged(DisplayInformation sender, object args)
        {
            var dpiScale = (float)sender.RawPixelsPerViewPixel;
            ProcessDpiChanged(dpiScale);
        }

        private void ProcessDpiChanged(float dpiScale)
        {
            var scale = 1.0f / dpiScale;
            RootScaleTransform.ScaleX = scale;
            RootScaleTransform.ScaleY = scale;

            RefreshImageGridSize();
        }

        private void RefreshImageGridSize()
        {
            var image = Image;
            if (image != null)
            {
                var size = Image.Size;
                ImageGrid.Width = size.Width;
                ImageGrid.Height = size.Height;
            }
        }

        private void OnCaptureDeviceReplaced(object sender, WinRTInteropTools.Direct3D11Device e)
        {
            if (Image != null && Image is CaptureImage captureImage)
            {
                captureImage.RegenerateSurface();
                // TODO: Unfortunately the CaptureImage's regeneration
                // is destructive to the surface. Get a new one.
                GenerateImage();
            }
        }

        private void OnRenderingDeviceReplaced(CompositionGraphicsDevice sender, RenderingDeviceReplacedEventArgs args)
        {
            var graphicsManager = GraphicsManager.Current;
            _canvasDevice = graphicsManager.CanvasDevice;
            _backgroundCavnasBrush = CreateBackgroundBrush(_canvasDevice);
            _gridLinesCavnasBrush = CreateGridLinesBrush(_canvasDevice, GridLinesColor);
            GenerateBackground();
            UpdateGridLines();
            UpdateBorder();

            if (Image != null)
            {
                Image.RegenerateSurface();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OnUnloaded;
            var graphicsManager = GraphicsManager.Current;
            graphicsManager.CompositionGraphicsDevice.RenderingDeviceReplaced -= OnRenderingDeviceReplaced;
            graphicsManager.CaptureDeviceReplaced -= OnCaptureDeviceReplaced;
            var displayInfo = DisplayInformation.GetForCurrentView();
            displayInfo.DpiChanged -= OnDpiChanged;
        }

        public ScrollViewer ScrollViewer => ImageScrollViewer;

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

        public InputMode InputMode
        {
            get { return (InputMode)GetValue(InputModeProperty); }
            set { SetValue(InputModeProperty, value); }
        }

        public bool AreGridLinesVisible
        {
            get { return (bool)GetValue(AreGridLinesVisibleProperty); }
            set { SetValue(AreGridLinesVisibleProperty, value); }
        }

        public bool IsBorderVisible
        {
            get { return (bool)GetValue(IsBorderVisibleProperty); }
            set { SetValue(IsBorderVisibleProperty, value); }
        }

        public PositionInt32? CursorPosition
        {
            get { return (PositionInt32?)GetValue(CursorPositionProperty); }
            set { SetValue(CursorPositionProperty, value); }
        }

        public IImage Image
        {
            get { return (IImage)GetValue(ImageProperty); }
            set { SetValue(ImageProperty, value); }
        }

        public Color GridLinesColor
        {
            get { return (Color)GetValue(GridLinesColorProperty); }
            set { SetValue(GridLinesColorProperty, value); }
        }

        public Color BorderColor
        {
            get { return (Color)GetValue(BorderColorProperty); }
            set { SetValue(BorderColorProperty, value); }
        }

        public Color MeasureColor
        {
            get { return (Color)GetValue(MeasureColorProperty); }
            set { SetValue(MeasureColorProperty, value); }
        }

        public Color? CurrentColor
        {
            get { return (Color?)GetValue(CurrentColorProperty); }
            set { SetValue(CurrentColorProperty, value); }
        }

        private ICanvasBrush CreateBackgroundBrush(ICanvasResourceCreator device)
        {
            var bitmap = new CanvasRenderTarget(device, 16, 16, 96); // TODO: Dpi?
            using (var drawingSession = bitmap.CreateDrawingSession())
            {
                drawingSession.Clear(Colors.Gray);
                drawingSession.FillRectangle(0, 0, 8, 8, Colors.LightGray);
                drawingSession.FillRectangle(8, 8, 8, 8, Colors.LightGray);
            }

            var brush = new CanvasImageBrush(device, bitmap);
            brush.ExtendX = CanvasEdgeBehavior.Wrap;
            brush.ExtendY = CanvasEdgeBehavior.Wrap;

            return brush;
        }

        private ICanvasBrush CreateGridLinesBrush(ICanvasResourceCreator device, Color gridLinesColor)
        {
            var bitmap = new CanvasRenderTarget(device, 10, 10, 96); // TODO: Dpi?
            using (var drawingSession = bitmap.CreateDrawingSession())
            {
                drawingSession.Clear(Colors.Transparent);
                drawingSession.DrawRectangle(-0.5f, -0.5f, 10, 10, gridLinesColor, 0.5f);
            }

            var brush = new CanvasImageBrush(device, bitmap);
            brush.ExtendX = CanvasEdgeBehavior.Wrap;
            brush.ExtendY = CanvasEdgeBehavior.Wrap;

            return brush;
        }

        private void GenerateGridLines()
        {
            if (Image != null)
            {
                var size = Image.Size;
                var width = (int)size.Width;
                var height = (int)size.Height;
                var gridMultiplier = 10;
                var surfaceWidth = width * gridMultiplier;
                var surfaceHeight = height * gridMultiplier;

                var gridLinesSurface = GenerateSurfaceFromTiledBrush(_gridLinesCavnasBrush, surfaceWidth, surfaceHeight);
                _gridLinesBrush.Surface = gridLinesSurface;
            }
        }

        private void GenerateBackground()
        {
            if (Image != null)
            {
                var size = Image.Size;
                var width = (int)size.Width;
                var height = (int)size.Height;
                var backgroundSurface = _compositionGraphics.CreateDrawingSurface2(
                    new SizeInt32() { Width = width, Height = height },
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    DirectXAlphaMode.Premultiplied);
                using (var drawingSession = CanvasComposition.CreateDrawingSession(backgroundSurface))
                {
                    drawingSession.FillRectangle(0, 0, size.Width, size.Height, _backgroundCavnasBrush);
                }
                _backgroundBrush.Surface = backgroundSurface;
            }
        }

        private void GenerateImage()
        {
            if (Image != null)
            {
                var surface = Image.CreateSurface(_compositionGraphics);
                _imageBrush.Surface = surface;
            }
        }

        private ICompositionSurface GenerateSurfaceFromTiledBrush(ICanvasBrush brush, int surfaceWidth, int surfaceHeight)
        {
            ICompositionSurface resultSurface = null;
            // We're picking 4000 arbitrarily here.
            var tileWidth = 4000;
            var tileHeight = 4000;
            if (surfaceWidth > tileWidth || surfaceHeight > tileHeight)
            {
                var surface = _compositionGraphics.CreateVirtualDrawingSurface(
                    new SizeInt32() { Width = surfaceWidth, Height = surfaceHeight },
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    DirectXAlphaMode.Premultiplied);
                TiledBrushRenderer.Render(surface, _gridLinesCavnasBrush, tileWidth, tileHeight);
                resultSurface = surface;
            }
            else
            {
                var surface = _compositionGraphics.CreateDrawingSurface2(
                    new SizeInt32() { Width = surfaceWidth, Height = surfaceHeight },
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    DirectXAlphaMode.Premultiplied);
                using (var drawingSession = CanvasComposition.CreateDrawingSession(surface))
                {
                    drawingSession.Clear(Colors.Transparent);
                    drawingSession.FillRectangle(0, 0, surfaceWidth, surfaceHeight, _gridLinesCavnasBrush);
                }
                resultSurface = surface;
            }
            return resultSurface;
        }

        private void ImageRectangle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint((UIElement)sender).Position;
            CursorPosition = new PositionInt32() { X = (int)point.X, Y = (int)point.Y };
        }

        private void ImageRectangle_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            CursorPosition = null;
        }

        private bool ShouldIgnorePointerEvent(PointerRoutedEventArgs e)
        {
            // If the position is where the scroll bars should be, ignore
            // the pointer event. Otherwise the scroll bars are unusable
            // when the input mode is set to drag.
            var pointInParentSpace = e.GetCurrentPoint(ImageScrollViewerContainer).Position;
            var scrollBarSize = 16.0; // https://docs.microsoft.com/en-us/windows/apps/design/controls/scroll-controls
            var parentWidth = ImageScrollViewerContainer.ActualWidth;
            var parentHeight = ImageScrollViewerContainer.ActualHeight;
            return parentWidth - pointInParentSpace.X < scrollBarSize ||
                parentHeight - pointInParentSpace.Y < scrollBarSize;
        }

        private void ScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ShouldIgnorePointerEvent(e))
            {
                return;
            }

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                _lastPosition = e.GetCurrentPoint((ScrollViewer)sender).Position;

                if (InputMode == InputMode.Measure)
                {
                    _startMeasurePoint = e.GetCurrentPoint(MeasureCanvas).Position;
                    _startMeasurePoint.X = (int)_startMeasurePoint.X;
                    _startMeasurePoint.Y = (int)_startMeasurePoint.Y;
                    MeasureWidth = 0;
                    MeasureHeight = 0;
                    MeasurePositionX = (int)_startMeasurePoint.X;
                    MeasurePositionY = (int)_startMeasurePoint.Y;
                }
            }
        }

        private void ScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ShouldIgnorePointerEvent(e))
            {
                return;
            }

            var pointer = e.Pointer;
            if (InputMode == InputMode.Drag &&
                pointer.PointerDeviceType == PointerDeviceType.Mouse &&
                pointer.IsInContact)
            {
                var scrollViewer = (ScrollViewer)sender;
                var point = e.GetCurrentPoint(scrollViewer);
                var position = point.Position;

                var diffX = _lastPosition.X - position.X;
                var diffY = _lastPosition.Y - position.Y;

                double? horizontalOffset = null;
                double? verticalOffset = null;
                if (Math.Abs(diffX) > double.Epsilon)
                {
                    horizontalOffset = scrollViewer.HorizontalOffset + diffX;
                }
                if (Math.Abs(diffY) > double.Epsilon)
                {
                    verticalOffset = scrollViewer.VerticalOffset + diffY;
                }
                scrollViewer.ChangeView(horizontalOffset, verticalOffset, null, true);

                _lastPosition = position;
            }
            else if (InputMode == InputMode.Measure && pointer.IsInContact)
            {
                var point = e.GetCurrentPoint(MeasureCanvas);
                var position = point.Position;
                position.X = (int)position.X + 1;
                position.Y = (int)position.Y + 1;

                var diffX = (int)(position.X - _startMeasurePoint.X);
                var diffY = (int)(position.Y - _startMeasurePoint.Y);

                MeasureWidth = Math.Abs(diffX);
                MeasureHeight = Math.Abs(diffY);
                if (diffX < 0)
                {
                    MeasurePositionX = (int)position.X;
                }
                if (diffY < 0)
                {
                    MeasurePositionY = (int)position.Y;
                }
            }

            if (Image != null)
            {
                var point = e.GetCurrentPoint(ImageRectangle);
                var position = point.Position;

                var color = Image.GetColorFromPixel((int)position.X, (int)position.Y);
                CurrentColor = color;
            }
        }

        private void OnImageChnaged()
        {
            if (Image != null)
            {
                RefreshImageGridSize();

                GenerateBackground();
                GenerateImage();
                UpdateGridLines();
                UpdateBorder();

                ImageScrollViewer.ChangeView(0, 0, 1, true);
                ImageBorder.Visibility = Visibility.Visible;
            }
            else
            {
                ImageBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateBorder()
        {
            if (IsBorderVisible)
            {
                ImageBorder.BorderBrush = ImageBorderBrush;
            }
            else
            {
                ImageBorder.BorderBrush = null;
            }
        }

        private void UpdateGridLines()
        {
            if (AreGridLinesVisible)
            {
                GenerateGridLines();
                GridLinesRectangle.Visibility = Visibility.Visible;
            }
            else
            {
                _gridLinesBrush.Surface = null;
                GridLinesRectangle.Visibility = Visibility.Collapsed;
            }
        }

        private void OnInputModeChanged()
        {
            switch (InputMode)
            {
                case InputMode.Drag:
                    MeasureCanvas.Visibility = Visibility.Collapsed;
                    break;
                case InputMode.Measure:
                    MeasureCanvas.Visibility = Visibility.Visible;
                    break;
                default:
                    MeasureCanvas.Visibility = Visibility.Collapsed;
                    break;
            }
        }
    }
}
