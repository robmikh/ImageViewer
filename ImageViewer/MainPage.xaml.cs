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
using Windows.UI.Popups;
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

        private StorageFile _currentFile;
        private Point _lastPosition;
        private bool _borderEnabled = true;
        private CanvasBitmap _currentBitmap;

        private CompositionSurfaceBrush _backgroundBrush;
        private CompositionSurfaceBrush _imageBrush;

        enum InputMode
        {
            None,
            Drag
        }
        private InputMode _inputMode = InputMode.Drag;

        public MainPage()
        {
            this.InitializeComponent();

            _canvasDevice = new CanvasDevice();
            _compositor = Window.Current.Compositor;
            _compositionGraphics = CanvasComposition.CreateCompositionGraphicsDevice(_compositor, _canvasDevice);

            // Generate the background bitmap
            _backgroundCavnasBrush = CreateBackgroundBrush(_canvasDevice);

            // Create brushes
            _backgroundBrush = _compositor.CreateSurfaceBrush();
            _backgroundBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
            _imageBrush = _compositor.CreateSurfaceBrush();
            _imageBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;

            ImageGrid.Background = new InteropBrush(_backgroundBrush);
            ImageRectangle.Fill = new InteropBrush(_imageBrush);
            ImageBorderBrush.Color = Colors.Transparent;
        }

        public async Task OpenFileAsync(IImportedFile file)
        {
            var fileBitmap = await file.ImportFileAsync(_canvasDevice);

            if (fileBitmap != null)
            {
                OpenBitmap(fileBitmap);
                _currentFile = file.File;
            }
        }

        public void OpenBitmap(CanvasBitmap bitmap)
        {
            var size = bitmap.SizeInPixels;
            var backgroundSurface = _compositionGraphics.CreateDrawingSurface2(
                new SizeInt32() { Width = (int)size.Width, Height = (int)size.Height },
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                DirectXAlphaMode.Premultiplied);
            using (var drawingSession = CanvasComposition.CreateDrawingSession(backgroundSurface))
            {
                drawingSession.FillRectangle(0, 0, size.Width, size.Height, _backgroundCavnasBrush);
            }

            var imageSurface = _compositionGraphics.CreateDrawingSurface2(
                new SizeInt32() { Width = (int)size.Width, Height = (int)size.Height },
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
            ImageScrollViewer.ChangeView(0, 0, 1, true);
            if (_borderEnabled)
            {
                ImageBorderBrush.Color = Colors.Black;
            }
            _currentBitmap = bitmap;
            _currentFile = null; // In the case of diffs we don't have a file
            OnBitmapOpened();
        }

        private void OnBitmapOpened()
        {
            ViewMenu.Visibility = Visibility.Visible;
            MainMenu.SelectedItem = ViewMenu;
            var size = _currentBitmap.SizeInPixels;
            ImageSizeTextBlock.Text = $"{size.Width} x {size.Height}px";
            ZoomSlider.IsEnabled = true;
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

        private void ScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                _lastPosition = e.GetCurrentPoint((ScrollViewer)sender).Position;
            }
        }
        
        private void ScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
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
                }
                var picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.SuggestedFileName = $"{currentName.Substring(0, currentName.LastIndexOf('.'))}.modified";
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
                ImageBorderBrush.Color = Colors.Black;
            }
        }

        private void BorderButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _borderEnabled = false;
            if (_currentBitmap != null)
            {
                ImageBorderBrush.Color = Colors.Transparent;
            }
        }

        private void DiffImagesButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DiffSetupPage));
        }

        private async void ContinueImageDiff(DiffSetupResult diffSetup)
        {
            var result = await ImageDiffer.GenerateDiff(_canvasDevice, diffSetup.SelectedFile1, diffSetup.SelectedFile2);
            OpenBitmap(result.Bitmap);
            if (result.ColorChannelsMatch && result.AlphaChannelsMatch)
            {
                var dialog = new MessageDialog("Both images are an exact match!");
                await dialog.ShowAsync();
            }
            else if (result.ColorChannelsMatch)
            {
                var dialog = new MessageDialog("The color channels of both images match, but their alpha channels do not!");
                await dialog.ShowAsync();
            }
            else if (result.AlphaChannelsMatch)
            {
                var dialog = new MessageDialog("The alpha channels of both images match, but their color channels do not!");
                await dialog.ShowAsync();
            }
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
    }
}
