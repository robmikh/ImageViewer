using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ImageViewer.Controls
{
    public sealed partial class FileSelectionControl : UserControl
    {
        private static readonly DependencyProperty SelectedFileProperty = DependencyProperty.Register(nameof(SelectedFile), typeof(IImportedFile), typeof(FileSelectionControl), new PropertyMetadata(null, OnSelectedFileChanged));

        private static void OnSelectedFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (FileSelectionControl)d;
            var file = e.NewValue as IImportedFile;
            var path = "";
            if (file != null)
            {
                path = file.File.Path.ToString();
            }
            control.FilePathTextBox.Text = path;
            control.FireFileSelectedEvent();
        }

        public FileSelectionControl()
        {
            this.InitializeComponent();
        }

        public IImportedFile SelectedFile
        {
            get { return GetValue(SelectedFileProperty) as IImportedFile; }
            set { SetValue(SelectedFileProperty, value);  }
        }

        public event TypedEventHandler<object, IImportedFile> FileSelected;

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var file = await FileImporter.OpenFileAsync();
            if (file != null)
            {
                SelectedFile = file;
            }
        }

        private void FireFileSelectedEvent()
        {
            FileSelected?.Invoke(this, SelectedFile);
        }
    }
}
