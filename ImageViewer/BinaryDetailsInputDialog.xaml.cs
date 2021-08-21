using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace ImageViewer
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

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
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
