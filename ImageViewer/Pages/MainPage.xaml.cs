﻿using ImageViewer.Controls;
using ImageViewer.Dialogs;
using ImageViewer.System;
using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Capture;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ImageViewer.Pages
{
    class Settings
    {
        public bool ShowImageBorder = false;
        public Color ImageBorderColor = Colors.Black;
        public bool ShowGridLines = false;
        public Color GridLinesColor = Colors.LightGray;
        public Color MeasureColor = Colors.Gray;
    }

    class BottomBarSegment
    {
        public FrameworkElement Control { get; set; }
    }

    class Range
    {
        public int Min = 0;
        public int Max = 0;
    }

    enum FileType
    {
        Unknown,
        Image,
        Video
    }


    public sealed partial class MainPage : Page
    {
        enum ViewMode
        {
            Image,
            Diff,
            Capture,
            Video,
        }
        private ViewMode _viewMode = ViewMode.Image;
        private BottomBarSegment[] _bottomBarSegments;
        private int _currentBottomBarSegmentLevel = 0;
        private Range[] _bottomBarLayoutRanges;

        public MainPage()
        {
            var themeHelper = ThemeHelper.EnsureThemeHelper(this);
            this.InitializeComponent();

            var settings = ApplicationSettings.GetCachedSettings<Settings>();
            MainImageViewer.IsBorderVisible = settings.ShowImageBorder;
            MainImageViewer.BorderColor = settings.ImageBorderColor;
            MainImageViewer.AreGridLinesVisible = settings.ShowGridLines;
            MainImageViewer.GridLinesColor = settings.GridLinesColor;
            MainImageViewer.MeasureColor = settings.MeasureColor;

            _bottomBarSegments = new BottomBarSegment[]
                {
                    new BottomBarSegment() { Control = PositionContainer },
                    new BottomBarSegment() { Control = SizeContainer },
                    new BottomBarSegment() { Control = MeasureContainer },
                    new BottomBarSegment() { Control = ColorContainer },
                };
            _currentBottomBarSegmentLevel = _bottomBarSegments.Length;
            ComputeSegmentLayoutRanges();

            var applicationView = ApplicationView.GetForCurrentView();
            if (applicationView.IsViewModeSupported(ApplicationViewMode.CompactOverlay))
            {
                CompactOverlayButton.Visibility = Visibility.Visible;
            }
            if (GraphicsCaptureSession.IsSupported())
            {
                ScreenCaptureButton.Visibility = Visibility.Visible;
            }
            if (Capabilities.IsCaptureBorderPropertyAvailable)
            {
                CaptureBorderButton.Visibility = Visibility.Visible;
            }
            Window.Current.CoreWindow.KeyUp += CoreWindow_KeyUp;
        }

        public async Task OpenFileAsync(IImportedFile file)
        {
            var image = await FileImage.CreateAsync(file);
            OpenImage(image, ViewMode.Image);
        }

        public async Task OpenDiffAsync(DiffSetupResult diffSetup)
        {
            var device = GraphicsManager.Current.CanvasDevice;
            var file1 = diffSetup.SelectedFile1;
            var file2 = diffSetup.SelectedFile2;
            var diff = await ImageDiffer.GenerateDiff(device, file1, file2);
            OpenImage(new DiffImage(diff, file1.File.Name, file2.File.Name), ViewMode.Diff);
            ColorChannelsDiffStatus.IsChecked = diff.ColorChannelsMatch;
            AlphaChannelsDiffStatus.IsChecked = diff.AlphaChannelsMatch;
        }

        public async Task OpenVideoAsync(StorageFile file)
        {
            var image = await VideoImage.CreateAsync(file);
            OpenImage(image, ViewMode.Video);
        }

        public async Task<bool> OpenStorageItemsAsync(IReadOnlyList<IStorageItem> items)
        {
            bool opened = false;

            if (items.Count > 0)
            {
                // If we open two files, try to diff them
                if (items.Count == 2)
                {
                    var item1 = items[0];
                    var item2 = items[1];
                    if (item1 is StorageFile file1 && item2 is StorageFile file2)
                    {
                        var importedFile1 = await FileImporter.ProcessStorageFileAsync(file1);
                        var importedFile2 = await FileImporter.ProcessStorageFileAsync(file2);
                        var diffSetup = new DiffSetupResult(importedFile1, importedFile2);
                        await OpenDiffAsync(diffSetup);
                        opened = true;
                    }
                }
                else
                {
                    var item = items.First();
                    if (item is StorageFile file)
                    {
                        var fileType = GetFileType(file);
                        switch (fileType)
                        {
                            case FileType.Image:
                                {
                                    var importedFile = await FileImporter.ProcessStorageFileAsync(file);
                                    await OpenFileAsync(importedFile);
                                    opened = true;
                                }
                                break;
                            case FileType.Video:
                                {
                                    await OpenVideoAsync(file);
                                    opened = true;
                                }
                                break;
                        }
                    }
                }
            }

            return opened;
        }

        public void CacheCurrentSettings()
        {
            var settings = new Settings();
            settings.ShowImageBorder = MainImageViewer.IsBorderVisible;
            settings.ImageBorderColor = MainImageViewer.BorderColor;
            settings.ShowGridLines = MainImageViewer.AreGridLinesVisible;
            settings.GridLinesColor = MainImageViewer.GridLinesColor;
            settings.MeasureColor = MainImageViewer.MeasureColor;
            ApplicationSettings.CacheSettings(settings);
        }

        private void OpenImage(IImage image, ViewMode viewMode)
        {
            MainImageViewer.Image = image;
            var titleBar = ApplicationView.GetForCurrentView().Title = image.DisplayName;
            _viewMode = viewMode;
            OnBitmapOpened(viewMode);
        }

        private TabbedCommandBarItem GetMenuForViewMode(ViewMode viewMode)
        {
            switch (viewMode)
            {
                case ViewMode.Diff:
                    return DiffMenu;
                case ViewMode.Capture:
                    return CaptureMenu;
                case ViewMode.Video:
                    return VideoMenu;
                default:
                    return ViewMenu;
            }
        }

        private void OnBitmapOpened(ViewMode viewMode)
        {
            ViewMenu.Visibility = Visibility.Visible;
            DiffMenu.Visibility = viewMode == ViewMode.Diff ? Visibility.Visible : Visibility.Collapsed;
            CaptureMenu.Visibility = viewMode == ViewMode.Capture ? Visibility.Visible : Visibility.Collapsed;
            VideoMenu.Visibility = viewMode == ViewMode.Video ? Visibility.Visible : Visibility.Collapsed;
            MainMenu.SelectedItem = GetMenuForViewMode(viewMode);
            var size = MainImageViewer.Image.Size;
            ImageSizeTextBlock.Text = $"{size.Width} x {size.Height}px";
            ZoomSlider.IsEnabled = true;
            SaveAsButton.IsEnabled = true;
            CopyButton.IsEnabled = true;
            if (viewMode == ViewMode.Capture)
            {
                ShowCursorButton.IsChecked = true;
                CapturePlayPauseButton.IsChecked = true;
            }
            else if (viewMode == ViewMode.Video)
            {
                VideoPlayPauseButton.IsChecked = true;
                var videoImage = (VideoImage)MainImageViewer.Image;
                videoImage.BindToSlider(VideoPlayerSeekSlider);
            }
        }

        private async Task SaveToFileAsync(StorageFile file)
        {
            if (MainImageViewer.Image != null)
            {
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await MainImageViewer.Image.SaveSnapshotToStreamAsync(stream);
                }
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
            if (MainImageViewer.Image != null)
            {
                var currentName = "image";
                if (MainImageViewer.Image is FileImage fileImage)
                {
                    currentName = fileImage.File.File.Name;
                    currentName = $"{currentName.Substring(0, currentName.LastIndexOf('.'))}.modified";
                }
                else if (MainImageViewer.Image is CaptureImage captureImage)
                {
                    currentName = "capture";
                }
                else if (MainImageViewer.Image is DiffImage diffImage)
                {
                    var modeString = diffImage.ViewMode == DiffViewMode.Color ? "Color" : "Alpha";
                    currentName = $"diff-{modeString}";
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

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer.Image != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.RequestedOperation = DataPackageOperation.Copy;

                var stream = new InMemoryRandomAccessStream();
                await MainImageViewer.Image.SaveSnapshotToStreamAsync(stream);

                dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                Clipboard.SetContent(dataPackage);
            }
        }

        private async void DiffImagesButton_Click(object sender, RoutedEventArgs e)
        {
            var diffSetup = await DiffSetupPage.ShowAsync(Frame);
            if (diffSetup != null)
            {
                await OpenDiffAsync(diffSetup);
            }
        }

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AboutDialog();
            await dialog.ShowAsync();
        }

        private void ColorDiffButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is DiffImage image)
            {
                image.ViewMode = DiffViewMode.Color;
            }
        }

        private void AlphaDiffButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is DiffImage image)
            {
                image.ViewMode = DiffViewMode.Alpha;
            }
        }

        private async void ScreenCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new GraphicsCapturePicker();
            var item = await picker.PickSingleItemAsync();
            if (item != null)
            {
                OpenImage(new CaptureImage(item, GraphicsManager.Current.CaptureDevice), ViewMode.Capture);
            }
        }

        private void ShowCursorButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is CaptureImage image)
            {
                image.ShowCursor = true;
            }
        }

        private void ShowCursorButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is CaptureImage image)
            {
                image.ShowCursor = false;
            }
        }

        private void CapturePlayPauseButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is CaptureImage image)
            {
                image.Play();
            }
        }

        private void CapturePlayPauseButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is CaptureImage image)
            {
                image.Pause();
            }
        }

        private async void CompactOverlayButton_Checked(object sender, RoutedEventArgs e)
        {
            var result = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
        }

        private async void CompactOverlayButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var result = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
        }

        private void DragButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null)
            {
                MainImageViewer.InputMode = InputMode.Drag;
            }
            NoneInputModeButton.IsChecked = false;
            if (MeasureInputModeButton != null)
            {
                MeasureInputModeButton.IsChecked = false;
            }
        }

        private void DragButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if ((!NoneInputModeButton.IsChecked.HasValue || !NoneInputModeButton.IsChecked.Value) &&
                (!DragInputModeButton.IsChecked.HasValue || !DragInputModeButton.IsChecked.Value) &&
                (!MeasureInputModeButton.IsChecked.HasValue || !MeasureInputModeButton.IsChecked.Value))
            {
                MainImageViewer.InputMode = InputMode.None;
                NoneInputModeButton.IsChecked = true;
            }
        }

        private void NoneInputModeButton_Checked(object sender, RoutedEventArgs e)
        {
            MainImageViewer.InputMode = InputMode.None;
            DragInputModeButton.IsChecked = false;
            MeasureInputModeButton.IsChecked = false;
        }

        private void NoneInputModeButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if ((!NoneInputModeButton.IsChecked.HasValue || !NoneInputModeButton.IsChecked.Value) &&
                (!DragInputModeButton.IsChecked.HasValue || !DragInputModeButton.IsChecked.Value) &&
                (!MeasureInputModeButton.IsChecked.HasValue || !MeasureInputModeButton.IsChecked.Value))
            {
                MainImageViewer.InputMode = InputMode.None;
                NoneInputModeButton.IsChecked = true;
            }
        }

        private void MeasureInputModeButton_Checked(object sender, RoutedEventArgs e)
        {
            MainImageViewer.InputMode = InputMode.Measure;
            DragInputModeButton.IsChecked = false;
            NoneInputModeButton.IsChecked = false;
            MeasureSizeTextBlocks.Visibility = Visibility.Visible;
            MeasureMenu.Visibility = Visibility.Visible;
        }

        private void MeasureInputModeButton_Unchecked(object sender, RoutedEventArgs e)
        {
            MeasureSizeTextBlocks.Visibility = Visibility.Collapsed;
            MeasureMenu.Visibility = Visibility.Collapsed;
            if ((!NoneInputModeButton.IsChecked.HasValue || !NoneInputModeButton.IsChecked.Value) &&
                (!DragInputModeButton.IsChecked.HasValue || !DragInputModeButton.IsChecked.Value) &&
                (!MeasureInputModeButton.IsChecked.HasValue || !MeasureInputModeButton.IsChecked.Value))
            {
                MainImageViewer.InputMode = InputMode.None;
                NoneInputModeButton.IsChecked = true;
            }
        }

        private async void CaptureBorderButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is CaptureImage image)
            {
                await image.SetBorderAsync(true);
            }
        }

        private async void CaptureBorderButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is CaptureImage image)
            {
                await image.SetBorderAsync(false);
            }
        }

        private void ClipboardButon_Click(object sender, RoutedEventArgs e)
        {
            ImportFromClipboard();
        }

        private async Task ImportFromDataPackageView(DataPackageView view, string name)
        {
            if (view.Contains(StandardDataFormats.Bitmap))
            {
                var bitmap = await view.GetBitmapAsync();
                using (var stream = await bitmap.OpenReadAsync())
                {
                    var canvasBitmap = await CanvasBitmap.LoadAsync(GraphicsManager.Current.CanvasDevice, stream, 96.0f);
                    OpenImage(new CanvasBitmapImage(canvasBitmap, name), ViewMode.Image);
                }
            }
        }

        private async void ImportFromClipboard()
        {
            var view = Clipboard.GetContent();
            await ImportFromDataPackageView(view, "Clipboard");
        }

        private void CoreWindow_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            var key = args.VirtualKey;
            var isControlDown = (sender.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            if (key == VirtualKey.V && isControlDown)
            {
                ImportFromClipboard();
            }
        }

        private Range ComputeSegmentLayoutRange(int i)
        {
            const int segmentWidth = 200;
            const int sliderSegmentWidth = 250;

            var min = (segmentWidth * (i + 1)) + sliderSegmentWidth;
            var max = min + segmentWidth;
            if (i == _bottomBarSegments.Length - 1)
            {
                max = int.MaxValue;
            }

            return new Range()
            {
                Min = min,
                Max = max,
            };
        }

        private void ComputeSegmentLayoutRanges()
        {
            var numSegments = _bottomBarSegments.Length;
            var ranges = new List<Range>();
            for (var i = 0; i < _bottomBarSegments.Length; i++)
            {
                ranges.Add(ComputeSegmentLayoutRange(i));
            }
            _bottomBarLayoutRanges = ranges.ToArray();
        }

        private void EvaluateBottomBarSegments()
        {
            for (var i = 0; i < _bottomBarSegments.Length; i++)
            {
                var segment = _bottomBarSegments[i];
                if (i <= _currentBottomBarSegmentLevel)
                {
                    segment.Control.Visibility = Visibility.Visible;
                }
                else
                {
                    segment.Control.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OnBottomBarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var width = e.NewSize.Width;
            for (var i = _bottomBarLayoutRanges.Length - 1; i >= 0; i--)
            {
                var range = _bottomBarLayoutRanges[i];

                if (width < range.Max && width >= range.Min)
                {
                    if (i != _currentBottomBarSegmentLevel)
                    {
                        _currentBottomBarSegmentLevel = i;
                        EvaluateBottomBarSegments();
                    }
                    break;
                }
            }
        }

        private async void VideoButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var image = await VideoImage.CreateAsync(file);
                OpenImage(image, ViewMode.Video);
            }
        }

        private void VideoPlayPauseButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is VideoImage image)
            {
                image.Play();
            }
        }

        private void VideoPlayPauseButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is VideoImage image)
            {
                image.Pause();
            }
        }

        private void VideoPreviousFrameButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is VideoImage image)
            {
                image.PreviousFrame();
            }
        }

        private void VideoNextFrameButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is VideoImage image)
            {
                image.NextFrame();
            }
        }

        private async void MainImageViewer_DragOver(object sender, DragEventArgs e)
        {
            var deferral = e.GetDeferral();
            bool valid = false;
            string caption = "";
            var view = e.DataView;
            
            // First check for storage items
            if (view.Contains(StandardDataFormats.StorageItems))
            {
                var items = await view.GetStorageItemsAsync();

                if (items.Count > 0)
                {
                    // If we open two files, try to diff them
                    if (items.Count == 2)
                    {
                        var item1 = items[0];
                        var item2 = items[1];
                        if (item1 is StorageFile file1 && item2 is StorageFile file2)
                        {
                            var type1 = GetFileType(file1);
                            var type2 = GetFileType(file2);
                            valid = type1 == FileType.Image && type2 == FileType.Image;
                            caption = "Diff";
                        }
                    }
                    else
                    {
                        var item = items.First();
                        if (item is StorageFile file)
                        {
                            var fileType = GetFileType(file);
                            valid = fileType != FileType.Unknown;
                            caption = "Open";
                        }
                    }
                }
            }
            else if (view.Contains(StandardDataFormats.Bitmap))
            {
                valid = true;
            }

            if (valid)
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = caption;
            }
            deferral.Complete();
        }

        private async void MainImageViewer_Drop(object sender, DragEventArgs e)
        {
            var view = e.DataView;
            if (view.Contains(StandardDataFormats.StorageItems))
            {
                var items = await view.GetStorageItemsAsync();
                await OpenStorageItemsAsync(items);
            }
            else if (view.Contains(StandardDataFormats.Bitmap))
            {
                await ImportFromDataPackageView(view, "Drag and Drop");
            }
        }

        private FileType GetFileType(StorageFile file)
        {
            var extension = file.FileType.ToLower();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".bin":
                    return FileType.Image;
                case ".mp4":
                    return FileType.Video;
                default:
                    return FileType.Unknown;
            }
        }
    }
}
