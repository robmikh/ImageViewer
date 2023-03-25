using System.Collections.ObjectModel;
using Windows.UI.Xaml.Controls;

namespace ImageViewer.Dialogs
{
    public enum BinaryImportPixelFormat
    {
        Unknown,
        BGRA8,
        RGB8
    }

    public sealed partial class BinaryDetailsInputDialog : ContentDialog
    {
        ObservableCollection<BinaryImportPixelFormat> _supportedFormats;  

        public BinaryDetailsInputDialog(int width, int height, BinaryImportPixelFormat format)
        {
            this.InitializeComponent();
            _supportedFormats = new ObservableCollection<BinaryImportPixelFormat>()
            {
                BinaryImportPixelFormat.BGRA8,
                BinaryImportPixelFormat.RGB8,
            };
            BinaryDetailsPixelFormatComboBox.ItemsSource = _supportedFormats;
            ResetBinaryDetailsInputDialog(width, height, format);
        }

        public void ResetBinaryDetailsInputDialog()
        {
            ResetBinaryDetailsInputDialog(0, 0, BinaryImportPixelFormat.BGRA8);
        }

        public void ResetBinaryDetailsInputDialog(int width, int height, BinaryImportPixelFormat format)
        {
            // Reset the state
            IsPrimaryButtonEnabled = width > 0 && height > 0;
            BinaryDetailsWidthTextBox.Text = $"{width}";
            BinaryDetailsHeightTextBox.Text = $"{height}";

            if (format == BinaryImportPixelFormat.Unknown)
            {
                format = BinaryImportPixelFormat.BGRA8;
            }
            var index = _supportedFormats.IndexOf(format);
            BinaryDetailsPixelFormatComboBox.SelectedIndex = index;
        }

        public bool ParseBinaryDetailsSizeBoxes(out int width, out int height, out BinaryImportPixelFormat format)
        {
            width = 0;
            height = 0;
            format = BinaryImportPixelFormat.Unknown;

            var selectedItem = BinaryDetailsPixelFormatComboBox.SelectedItem;
            if (selectedItem is BinaryImportPixelFormat selectedFormat)
            {
                format = selectedFormat;
            }
            else
            {
                return false;
            }

            var widthText = BinaryDetailsWidthTextBox.Text;
            var heightText = BinaryDetailsHeightTextBox.Text;
            if (!int.TryParse(widthText, out width) || width == 0 ||
                !int.TryParse(heightText, out height) || height == 0)
            {
                return false;
            }
            return true;
        }

        private void BinaryDetailsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var width = 0;
                var height = 0;
                var format = BinaryImportPixelFormat.Unknown;
                if (ParseBinaryDetailsSizeBoxes(out width, out height, out format))
                {
                    IsPrimaryButtonEnabled = true;
                }
                else
                {
                    IsPrimaryButtonEnabled = false;
                }
            }
        }
    }
}
