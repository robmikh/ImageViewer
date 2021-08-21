using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ImageViewer
{
    public class DiffSetupResult
    {
        public IImportedFile SelectedFile1 { get; }
        public IImportedFile SelectedFile2 { get; }

        public DiffSetupResult(IImportedFile file1, IImportedFile file2)
        {
            SelectedFile1 = file1;
            SelectedFile2 = file2;
        }
    }

    public sealed partial class DiffSetupPage : Page
    {
        private bool _image1Selected = false;
        private bool _image2Selected = false;

        public DiffSetupPage()
        {
            this.InitializeComponent();
        }

        private void ImageFile1_FileSelected(object sender, IImportedFile file)
        {
            _image1Selected = file != null;
            EvaluatePrimaryButtonState();
        }

        private void ImageFile2_FileSelected(object sender, IImportedFile file)
        {
            _image2Selected = file != null;
            EvaluatePrimaryButtonState();
        }

        private void DiffButton_Click(object sender, RoutedEventArgs e)
        {
            var result = new DiffSetupResult(ImageFile1.SelectedFile, ImageFile2.SelectedFile);
            Frame.Navigate(typeof(MainPage), result);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private void EvaluatePrimaryButtonState()
        {
            DiffButton.IsEnabled = _image1Selected && _image2Selected;
        }
    }
}
