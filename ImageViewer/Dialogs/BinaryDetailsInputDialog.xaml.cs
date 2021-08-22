using Windows.UI.Xaml.Controls;

namespace ImageViewer.Dialogs
{
    public sealed partial class BinaryDetailsInputDialog : ContentDialog
    {
        public BinaryDetailsInputDialog(int width, int height)
        {
            this.InitializeComponent();
            ResetBinaryDetailsInputDialog(width, height);
        }

        public void ResetBinaryDetailsInputDialog()
        {
            ResetBinaryDetailsInputDialog(0, 0);
        }

        public void ResetBinaryDetailsInputDialog(int width, int height)
        {
            // Reset the state
            IsPrimaryButtonEnabled = width > 0 && height > 0;
            BinaryDetailsWidthTextBox.Text = $"{width}";
            BinaryDetailsHeightTextBox.Text = $"{height}";
        }

        public bool ParseBinaryDetailsSizeBoxes(out int width, out int height)
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

        private void BinaryDetailsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var width = 0;
                var height = 0;
                if (ParseBinaryDetailsSizeBoxes(out width, out height))
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
