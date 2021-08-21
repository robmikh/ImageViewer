using Windows.UI.Xaml.Controls;

namespace ImageViewer
{
    public sealed partial class DiffImagesDialog : ContentDialog
    {
        private bool _image1Selected = false;
        private bool _image2Selected = false;

        public DiffImagesDialog()
        {
            this.InitializeComponent();
        }

        public IImportedFile SelectedFile1 { get { return ImageFile1.SelectedFile; } }
        public IImportedFile SelectedFile2 { get { return ImageFile2.SelectedFile; } }

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

        private void EvaluatePrimaryButtonState()
        {
            IsPrimaryButtonEnabled = _image1Selected && _image2Selected;
        }
    }
}
