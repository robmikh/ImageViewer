using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace ImageViewer
{
    public sealed partial class MainPage : Page
    {
        private Compositor _compositor;
        private CanvasDevice _canvasDevice;
        private CompositionGraphicsDevice _compositionGraphics;

        private ICanvasBrush _backgroundCavnasBrush;
        private ICanvasBrush _gridLinesCavnasBrush;

        private StorageFile _currentFile;
        private DiffResult _currentDiff;

        private Point _lastPosition;
        private bool _borderEnabled = true;
        private CanvasBitmap _currentBitmap;

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
        }
        private ViewMode _viewMode = ViewMode.Image;

        public MainPage()
        {
            this.InitializeComponent();

            _canvasDevice = new CanvasDevice();
            _compositor = Window.Current.Compositor;
            _compositionGraphics = CanvasComposition.CreateCompositionGraphicsDevice(_compositor, _canvasDevice);

            // Generate the background bitmap
            _backgroundCavnasBrush = CreateBackgroundBrush(_canvasDevice);
            _gridLinesCavnasBrush = CreateGridLinesBrush(_canvasDevice);

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
                OpenBitmap(fileBitmap, ViewMode.Image);
                _currentFile = file.File;
                _currentDiff = null;
            }
        }

        private void OpenBitmap(CanvasBitmap bitmap, ViewMode viewMode, bool resetScrollViewer = true)
        {
            var size = bitmap.SizeInPixels;
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

            var imageSurface = _compositionGraphics.CreateDrawingSurface2(
                new SizeInt32() { Width = width, Height = height },
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                DirectXAlphaMode.Premultiplied);
            using (var drawingSession = CanvasComposition.CreateDrawingSession(imageSurface))
            {
                drawingSession.Clear(Colors.Transparent);
                drawingSession.DrawImage(bitmap);
            }

            _backgroundBrush.Surface = backgroundSurface;
            _imageBrush.Surface = imageSurface;
            ImageGrid.Width = size.Width;
            ImageGrid.Height = size.Height;
            if (resetScrollViewer)
            {
                ImageScrollViewer.ChangeView(0, 0, 1, true);
            }
            if (_borderEnabled)
            {
                ImageBorder.BorderBrush = ImageBorderBrush;
            }
            _currentBitmap = bitmap;
            _viewMode = viewMode;
            OnBitmapOpened(viewMode);
        }

        private void OnBitmapOpened(ViewMode viewMode)
        {
            ImageBorder.Visibility = Visibility.Visible;
            ViewMenu.Visibility = Visibility.Visible;
            DiffMenu.Visibility = viewMode == ViewMode.Diff ? Visibility.Visible : Visibility.Collapsed;
            MainMenu.SelectedItem = viewMode == ViewMode.Diff ? DiffMenu : ViewMenu;
            var size = _currentBitmap.SizeInPixels;
            ImageSizeTextBlock.Text = $"{size.Width} x {size.Height}px";
            ZoomSlider.IsEnabled = true;
            SaveAsButton.IsEnabled = true;
        }

        private async Task SaveToFileAsync(StorageFile file)
        {
            if (_currentBitmap != null)
            {
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await _currentBitmap.SaveAsync(stream, CanvasBitmapFileFormat.Png);
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

        private ICanvasBrush CreateGridLinesBrush(ICanvasResourceCreator device)
        {
            var bitmap = new CanvasRenderTarget(device, 10, 10, 96); // TODO: Dpi?
            using (var drawingSession = bitmap.CreateDrawingSession())
            {
                drawingSession.Clear(Colors.Transparent);
                drawingSession.DrawRectangle(-0.5f, -0.5f, 10, 10, Colors.LightGray, 0.5f);
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
            if (_currentBitmap != null)
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
            if (_currentBitmap != null)
            {
                ImageBorder.BorderBrush = ImageBorderBrush;
            }
        }

        private void BorderButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _borderEnabled = false;
            if (_currentBitmap != null)
            {
                ImageBorder.BorderBrush = null;
            }
        }

        private void DiffImagesButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DiffSetupPage));
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
            OpenBitmap(bitmap, ViewMode.Diff);
            _currentFile = null;
            _currentDiff = diff;
            ColorChannelsDiffStatus.IsChecked = diff.ColorChannelsMatch;
            AlphaChannelsDiffStatus.IsChecked = diff.AlphaChannelsMatch;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var param = e.Parameter;
            if (param is DiffSetupResult diffSetup)
            {
                Frame.BackStack.Clear();
                ContinueImageDiff(diffSetup);
            }
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
            if (_currentBitmap != null)
            {
                var size = _currentBitmap.SizeInPixels;
                var width = (int)size.Width;
                var height = (int)size.Height;
                var gridMultiplier = 10;
                var surfaceWidth = width * gridMultiplier;
                var surfaceHeight = height * gridMultiplier;
                var gridLinesSurface = _compositionGraphics.CreateVirtualDrawingSurface(
                    new SizeInt32() { Width = surfaceWidth, Height = surfaceHeight },
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    DirectXAlphaMode.Premultiplied);
                // TODO: Proper support for large virtual surfaces.
                // For now we are going to split very large sizes into 4 on each side.
                // We're picking 4000 arbitrarily here.
                if (surfaceWidth > 4000 || surfaceHeight > 4000)
                {
                    var tileWidth = surfaceWidth / 4.0;
                    var tileHeight = surfaceHeight / 4.0;
                    for (var i = 0; i < 4; i++)
                    {
                        var tileLeft = i * tileWidth;
                        for (var j = 0; j < 4; j++)
                        {
                            var tileTop = j * tileHeight;
                            var updateRect = new Rect(tileLeft, tileTop, tileWidth, tileHeight);
                            using (var drawingSession = CanvasComposition.CreateDrawingSession(gridLinesSurface, updateRect))
                            {
                                drawingSession.Clear(Colors.Transparent);
                                drawingSession.FillRectangle(0, 0, (float)tileWidth, (float)tileHeight, _gridLinesCavnasBrush);
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
            GenerateGridLines();
            GridLinesRectangle.Visibility = Visibility.Visible;
        }

        private void GridLinesButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _gridLinesBrush.Surface = null;
            GridLinesRectangle.Visibility = Visibility.Collapsed;
        }

        private void ColorDiffButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentDiff != null)
            {
                OpenBitmap(_currentDiff.ColorDiffBitmap, ViewMode.Diff, false);
            }
        }

        private void AlphaDiffButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentDiff != null)
            {
                OpenBitmap(_currentDiff.AlphaDiffBitmap, ViewMode.Diff, false);
            }
        }
    }
}
