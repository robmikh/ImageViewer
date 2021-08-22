using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace ImageViewer
{
    public sealed partial class MainPage : Page
    {
        private static readonly DependencyProperty GridLinesColorProperty = DependencyProperty.Register(nameof(GridLinesColor), typeof(Color), typeof(MainPage), new PropertyMetadata(Colors.LightGray, OnGridLinesColorChanged));

        private static void OnGridLinesColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var page = (MainPage)d;
            var color = (Color)e.NewValue;
            var brush = page.CreateGridLinesBrush(page._canvasDevice, color);
            page._gridLinesCavnasBrush = brush;
            if (page._gridLinesEnabled)
            {
                page.GenerateGridLines();
            }
        }

        private Compositor _compositor;
        private CanvasDevice _canvasDevice;
        private CompositionGraphicsDevice _compositionGraphics;

        private ICanvasBrush _backgroundCavnasBrush;
        private ICanvasBrush _gridLinesCavnasBrush;

        private StorageFile _currentFile;
        private DiffResult _currentDiff;

        private Point _lastPosition;
        private bool _borderEnabled = true;
        private bool _gridLinesEnabled = false;
        private IImage _currentImage;

        private CompositionSurfaceBrush _backgroundBrush;
        private CompositionSurfaceBrush _imageBrush;
        private CompositionSurfaceBrush _gridLinesBrush;

        enum InputMode
        {
            None,
            Drag
        }
        private InputMode _inputMode = InputMode.Drag;

        enum ViewMode
        {
            Image,
            Diff,
            Capture,
        }
        private ViewMode _viewMode = ViewMode.Image;

        public Color GridLinesColor
        {
            get { return (Color)GetValue(GridLinesColorProperty); }
            set { SetValue(GridLinesColorProperty, value); }
        }

        public MainPage()
        {
            this.InitializeComponent();

            _canvasDevice = new CanvasDevice();
            _compositor = Window.Current.Compositor;
            _compositionGraphics = CanvasComposition.CreateCompositionGraphicsDevice(_compositor, _canvasDevice);

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
        }

        public async Task OpenFileAsync(IImportedFile file)
        {
            var fileBitmap = await file.ImportFileAsync(_canvasDevice);

            if (fileBitmap != null)
            {
                OpenImage(new CanvasBitmapImage(fileBitmap), ViewMode.Image);
                _currentFile = file.File;
                _currentDiff = null;
            }
        }

        private void OpenImage(IImage image, ViewMode viewMode, bool resetScrollViewer = true)
        {
            var size = image.Size;
            ImageGrid.Width = size.Width;
            ImageGrid.Height = size.Height;

            _currentImage?.Dispose();
            _currentImage = image;

            GenerateBackground();
            GenerateImage();
            if (_gridLinesEnabled)
            {
                GenerateGridLines();
            }

            if (resetScrollViewer)
            {
                ImageScrollViewer.ChangeView(0, 0, 1, true);
            }
            if (_borderEnabled)
            {
                ImageBorder.BorderBrush = ImageBorderBrush;
            }
            _viewMode = viewMode;
            OnBitmapOpened(viewMode);
        }

        private void GenerateImage()
        {
            if (_currentImage != null)
            {
                var surface = _currentImage.CreateSurface(_compositionGraphics);
                _imageBrush.Surface = surface;
            }
        }

        private void GenerateBackground()
        {
            if (_currentImage != null)
            {
                var size = _currentImage.Size;
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

        private TabbedCommandBarItem GetMenuForViewMode(ViewMode viewMode)
        {
            switch (viewMode)
            {
                case ViewMode.Diff:
                    return DiffMenu;
                case ViewMode.Capture:
                    return CaptureMenu;
                default:
                    return ViewMenu;
            }
        }

        private void OnBitmapOpened(ViewMode viewMode)
        {
            ImageBorder.Visibility = Visibility.Visible;
            ViewMenu.Visibility = Visibility.Visible;
            DiffMenu.Visibility = viewMode == ViewMode.Diff ? Visibility.Visible : Visibility.Collapsed;
            CaptureMenu.Visibility = viewMode == ViewMode.Capture ? Visibility.Visible : Visibility.Collapsed;
            MainMenu.SelectedItem = GetMenuForViewMode(viewMode);
            var size = _currentImage.Size;
            ImageSizeTextBlock.Text = $"{size.Width} x {size.Height}px";
            ZoomSlider.IsEnabled = true;
            SaveAsButton.IsEnabled = true;
            if (viewMode == ViewMode.Capture)
            {
                ShowCursorButton.IsChecked = true;
                PlayPauseButton.IsChecked = true;
            }
        }

        private async Task SaveToFileAsync(StorageFile file)
        {
            if (_currentImage != null)
            {
                var bitmap = _currentImage.GetSnapshot();
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await bitmap.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
            }
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
            }
        }
        
        private void ScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ShouldIgnorePointerEvent(e))
            {
                return;
            }

            var pointer = e.Pointer;
            if (_inputMode == InputMode.Drag && 
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
        }

        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var file = await FileImporter.OpenFileAsync();
            if (file != null)
            {
                await OpenFileAsync(file);
            }
        }

        private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage != null)
            {
                var currentName = "image";
                if (_currentFile != null)
                {
                    currentName = _currentFile.Name;
                    currentName = $"{currentName.Substring(0, currentName.LastIndexOf('.'))}.modified";
                }
                var picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.SuggestedFileName = currentName;
                picker.DefaultFileExtension = ".png";
                picker.FileTypeChoices.Add("PNG Image", new List<string> { ".png" });

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    await SaveToFileAsync(file);
                    await Launcher.LaunchFileAsync(file);
                }
            }
        }

        private void DragButton_Checked(object sender, RoutedEventArgs e)
        {
            _inputMode = InputMode.Drag;
        }

        private void DragButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _inputMode = InputMode.None;
        }

        private void BorderButton_Checked(object sender, RoutedEventArgs e)
        {
            _borderEnabled = true;
            if (_currentImage != null)
            {
                ImageBorder.BorderBrush = ImageBorderBrush;
            }
        }

        private void BorderButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _borderEnabled = false;
            if (_currentImage != null)
            {
                ImageBorder.BorderBrush = null;
            }
        }

        private async void DiffImagesButton_Click(object sender, RoutedEventArgs e)
        {
            var diffSetup = await DiffSetupPage.ShowAsync(Frame);
            if (diffSetup != null)
            {
                ContinueImageDiff(diffSetup);
            }
        }

        private CanvasBitmap GetDiffBitmapForCurrentChannelView(DiffResult diff)
        {
            if (ColorDiffButton.IsChecked.HasValue && ColorDiffButton.IsChecked.Value)
            {
                return diff.ColorDiffBitmap;
            }
            if (AlphaDiffButton.IsChecked.HasValue && AlphaDiffButton.IsChecked.Value)
            {
                return diff.AlphaDiffBitmap;
            }
            // If we get here, set the current channel view to color
            ColorDiffButton.IsChecked = true;
            return diff.ColorDiffBitmap;
        }

        private async void ContinueImageDiff(DiffSetupResult diffSetup)
        {
            var diff = await ImageDiffer.GenerateDiff(_canvasDevice, diffSetup.SelectedFile1, diffSetup.SelectedFile2);
            var bitmap = GetDiffBitmapForCurrentChannelView(diff);
            OpenImage(new CanvasBitmapImage(bitmap), ViewMode.Diff);
            _currentFile = null;
            _currentDiff = diff;
            ColorChannelsDiffStatus.IsChecked = diff.ColorChannelsMatch;
            AlphaChannelsDiffStatus.IsChecked = diff.AlphaChannelsMatch;
        }

        private void ImageRectangle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint((UIElement)sender).Position;
            PositionTextBlock.Text = $"{(int)point.X}, {(int)point.Y}px";
        }

        private void ImageRectangle_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            PositionTextBlock.Text = "";
        }

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AboutDialog();
            await dialog.ShowAsync();
        }

        private void GenerateGridLines()
        {
            if (_currentImage != null)
            {
                var size = _currentImage.Size;
                var width = (int)size.Width;
                var height = (int)size.Height;
                var gridMultiplier = 10;
                var surfaceWidth = width * gridMultiplier;
                var surfaceHeight = height * gridMultiplier;
                var gridLinesSurface = _compositionGraphics.CreateVirtualDrawingSurface(
                    new SizeInt32() { Width = surfaceWidth, Height = surfaceHeight },
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    DirectXAlphaMode.Premultiplied);
                // We're picking 4000 arbitrarily here.
                var tileWidth = 4000;
                var tileHeight = 4000;
                if (surfaceWidth > tileWidth || surfaceHeight > tileHeight)
                {
                    var widthInTiles = surfaceWidth / tileWidth;
                    if (surfaceWidth % tileWidth > 0)
                    {
                        widthInTiles += 1;
                    }
                    var heightInTiles = surfaceHeight / tileHeight;
                    if (surfaceHeight % tileHeight > 0)
                    {
                        heightInTiles += 1;
                    }
                    for (var i = 0; i < widthInTiles; i++)
                    {
                        var tileLeft = i * tileWidth;
                        for (var j = 0; j < heightInTiles; j++)
                        {
                            var tileTop = j * tileHeight;
                            var currentTileWidth = tileWidth;
                            if (tileLeft + tileWidth > surfaceWidth)
                            {
                                currentTileWidth = surfaceWidth - tileLeft;
                            }
                            var currentTileHeight = tileHeight;
                            if (tileTop + tileHeight > surfaceHeight)
                            {
                                currentTileHeight = surfaceHeight - tileTop;
                            }
                            var updateRect = new Rect(tileLeft, tileTop, currentTileWidth, currentTileHeight);
                            using (var drawingSession = CanvasComposition.CreateDrawingSession(gridLinesSurface, updateRect))
                            {
                                drawingSession.Clear(Colors.Transparent);
                                drawingSession.FillRectangle(0, 0, (float)currentTileWidth, (float)currentTileHeight, _gridLinesCavnasBrush);
                            }
                        }
                    }
                }
                else
                {
                    using (var drawingSession = CanvasComposition.CreateDrawingSession(gridLinesSurface))
                    {
                        drawingSession.Clear(Colors.Transparent);
                        drawingSession.FillRectangle(0, 0, size.Width * gridMultiplier, size.Height * gridMultiplier, _gridLinesCavnasBrush);
                    }
                }

                _gridLinesBrush.Surface = gridLinesSurface;
            }
        }

        private void GridLinesButton_Checked(object sender, RoutedEventArgs e)
        {
            _gridLinesEnabled = true;
            GenerateGridLines();
            GridLinesRectangle.Visibility = Visibility.Visible;
        }

        private void GridLinesButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _gridLinesEnabled = false;
            _gridLinesBrush.Surface = null;
            GridLinesRectangle.Visibility = Visibility.Collapsed;
        }

        private void ColorDiffButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentDiff != null)
            {
                OpenImage(new CanvasBitmapImage(_currentDiff.ColorDiffBitmap), ViewMode.Diff, false);
            }
        }

        private void AlphaDiffButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentDiff != null)
            {
                OpenImage(new CanvasBitmapImage(_currentDiff.AlphaDiffBitmap), ViewMode.Diff, false);
            }
        }

        private async void ScreenCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new GraphicsCapturePicker();
            var item = await picker.PickSingleItemAsync();
            if (item != null)
            {
                // Use a seperate device so we don't have to deal
                // with synchronization with D2D
                OpenImage(new CaptureImage(item, new CanvasDevice()), ViewMode.Capture);
                _currentFile = null;
                _currentDiff = null;
            }
        }

        private void ShowCursorButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentImage is CaptureImage image)
            {
                image.ShowCursor = true;
            }
        }

        private void ShowCursorButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_currentImage is CaptureImage image)
            {
                image.ShowCursor = false;
            }
        }

        private void PlayPauseButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentImage is CaptureImage image)
            {
                image.Play();
            }
        }

        private void PlayPauseButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_currentImage is CaptureImage image)
            {
                image.Pause();
            }
        }
    }
}
