using System.Threading.Tasks;
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
        private TaskCompletionSource<DiffSetupResult> _task;
        private bool _image1Selected = false;
        private bool _image2Selected = false;

        public DiffSetupPage(TaskCompletionSource<DiffSetupResult> task)
        {
            this.InitializeComponent();
            _task = task;
        }

        // This is terrible and hacky, but I needed something that behaved like
        // a ContentDialog without being a ContentDialog. The FileSelectionControl
        // will show a ContentDialog if it needs more information when picking
        // a file.
        public static async Task<DiffSetupResult> ShowAsync(Frame frame)
        {
            var currentPage = frame.Content;
            var task = new TaskCompletionSource<DiffSetupResult>();
            var setupPage = new DiffSetupPage(task);
            frame.Content = setupPage;
            var result = await task.Task;
            frame.Content = currentPage;
            return result;
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
            _task.SetResult(result);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _task.SetResult(null);
        }

        private void EvaluatePrimaryButtonState()
        {
            DiffButton.IsEnabled = _image1Selected && _image2Selected;
        }
    }
}
