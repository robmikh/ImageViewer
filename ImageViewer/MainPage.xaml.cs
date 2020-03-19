using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

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

        public async Task OpenFileAsync(StorageFile file)
        {
            CanvasBitmap fileBitmap = null;
            var extension = file.FileType;
            switch (extension)
            {
                case ".bin":
                    var buffer = await FileIO.ReadBufferAsync(file);
                    var width = 0;
                    var height = 0;
                    var format = DirectXPixelFormat.B8G8R8A8UIntNormalized;
                    var dialogResult = await BinaryDetailsInputDialog.ShowAsync();
                    if (dialogResult == ContentDialogResult.Primary &&
                        ParseBinaryDetailsSizeBoxes(out width, out height))
                    {
                        fileBitmap = CanvasBitmap.CreateFromBytes(_canvasDevice, buffer, width, height, format);
                    }
                    break;
                default:
                    // open it with win2d
                    using (var stream = await file.OpenReadAsync())
                    {
                        fileBitmap = await CanvasBitmap.LoadAsync(_canvasDevice, stream);
                    }
                    break;
            }

            if (fileBitmap != null)
            {
                var size = fileBitmap.SizeInPixels;
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
                    drawingSession.DrawImage(fileBitmap);
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
                _currentFile = file;
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

        private void BinaryDetailsInputDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            // Reset the state
            BinaryDetailsInputDialog.IsPrimaryButtonEnabled = false;
            BinaryDetailsWidthTextBox.Text = "0";
            BinaryDetailsHeightTextBox.Text = "0";
        }

        private void BinaryDetailsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var width = 0;
                var height = 0;
                if (ParseBinaryDetailsSizeBoxes(out width, out height))
                {
                    BinaryDetailsInputDialog.IsPrimaryButtonEnabled = true;
                }
                else
                {
                    BinaryDetailsInputDialog.IsPrimaryButtonEnabled = false;
                }
            }
        }

        private bool ParseBinaryDetailsSizeBoxes(out int width, out int height)
        {
            width = 0;
            height = 0;
            var widthText = BinaryDetailsWidthTextBox.Text;
            var heightText = BinaryDetailsHeightTextBox.Text;
            if (!int.TryParse(widthText, out width) || width == 0 ||
                !int.TryParse(heightText, out height) || height == 0)
            {
                return false;
            }
            return true;
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
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bin");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await OpenFileAsync(file);
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
            if (_currentFile != null)
            {
                ImageBorderBrush.Color = Colors.Black;
            }
        }

        private void BorderButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _borderEnabled = false;
            if (_currentFile != null)
            {
                ImageBorderBrush.Color = Colors.Transparent;
            }
        }
    }
}
