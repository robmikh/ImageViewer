﻿using ImageViewer.Controls;
using ImageViewer.Dialogs;
using ImageViewer.System;
using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Capture;
using Windows.Storage;
using Windows.Storage.Pickers;
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
    }

    public sealed partial class MainPage : Page
    {
        private CanvasDevice _canvasDevice;
        private StorageFile _currentFile;

        enum ViewMode
        {
            Image,
            Diff,
            Capture,
        }
        private ViewMode _viewMode = ViewMode.Image;

        public MainPage()
        {
            var themeHelper = ThemeHelper.EnsureThemeHelper(this);
            this.InitializeComponent();

            var graphicsManager = GraphicsManager.Current;
            _canvasDevice = graphicsManager.CanvasDevice;

            var settings = ApplicationSettings.GetCachedSettings<Settings>();
            MainImageViewer.IsBorderVisible = settings.ShowImageBorder;
            MainImageViewer.BorderColor = settings.ImageBorderColor;
            MainImageViewer.AreGridLinesVisible = settings.ShowGridLines;
            MainImageViewer.GridLinesColor = settings.GridLinesColor;

            if (ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay))
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
            var fileBitmap = await file.ImportFileAsync(_canvasDevice);

            if (fileBitmap != null)
            {
                OpenImage(new CanvasBitmapImage(fileBitmap), ViewMode.Image);
                _currentFile = file.File;
            }
        }

        public void CacheCurrentSettings()
        {
            var settings = new Settings();
            settings.ShowImageBorder = MainImageViewer.IsBorderVisible;
            settings.ImageBorderColor = MainImageViewer.BorderColor;
            settings.ShowGridLines = MainImageViewer.AreGridLinesVisible;
            settings.GridLinesColor = MainImageViewer.GridLinesColor;
            ApplicationSettings.CacheSettings(settings);
        }

        private void OpenImage(IImage image, ViewMode viewMode)
        {
            MainImageViewer.Image = image;
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
                default:
                    return ViewMenu;
            }
        }

        private void OnBitmapOpened(ViewMode viewMode)
        {
            ViewMenu.Visibility = Visibility.Visible;
            DiffMenu.Visibility = viewMode == ViewMode.Diff ? Visibility.Visible : Visibility.Collapsed;
            CaptureMenu.Visibility = viewMode == ViewMode.Capture ? Visibility.Visible : Visibility.Collapsed;
            MainMenu.SelectedItem = GetMenuForViewMode(viewMode);
            var size = MainImageViewer.Image.Size;
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
            if (MainImageViewer.Image != null)
            {
                var bitmap = MainImageViewer.Image.GetSnapshot();
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await bitmap.SaveAsync(stream, CanvasBitmapFileFormat.Png);
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

        private async void DiffImagesButton_Click(object sender, RoutedEventArgs e)
        {
            var diffSetup = await DiffSetupPage.ShowAsync(Frame);
            if (diffSetup != null)
            {
                ContinueImageDiff(diffSetup);
            }
        }

        private async void ContinueImageDiff(DiffSetupResult diffSetup)
        {
            var diff = await ImageDiffer.GenerateDiff(_canvasDevice, diffSetup.SelectedFile1, diffSetup.SelectedFile2);
            OpenImage(new DiffImage(diff), ViewMode.Diff);
            _currentFile = null;
            ColorChannelsDiffStatus.IsChecked = diff.ColorChannelsMatch;
            AlphaChannelsDiffStatus.IsChecked = diff.AlphaChannelsMatch;
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
                // Use a seperate device so we don't have to deal
                // with synchronization with D2D
                OpenImage(new CaptureImage(item, new CanvasDevice()), ViewMode.Capture);
                _currentFile = null;
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

        private void PlayPauseButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainImageViewer != null && MainImageViewer.Image is CaptureImage image)
            {
                image.Play();
            }
        }

        private void PlayPauseButton_Unchecked(object sender, RoutedEventArgs e)
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

        private async void ImportFromClipboard()
        {
            var view = Clipboard.GetContent();
            if (view.Contains(StandardDataFormats.Bitmap))
            {
                var bitmap = await view.GetBitmapAsync();
                using (var stream = await bitmap.OpenReadAsync())
                {
                    var canvasBitmap = await CanvasBitmap.LoadAsync(_canvasDevice, stream, 96.0f);
                    OpenImage(new CanvasBitmapImage(canvasBitmap), ViewMode.Image);
                    _currentFile = null;
                }
            }
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
    }
}