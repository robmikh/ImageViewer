using ImageViewer.Dialogs;
using Microsoft.Graphics.Canvas;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace ImageViewer
{
    public interface IImportedFile
    {
        StorageFile File { get; }
        Task<CanvasBitmap> ImportFileAsync(CanvasDevice device);
    }

    class ImportedStorageFile : IImportedFile
    {
        public StorageFile File { get; }

        public ImportedStorageFile(StorageFile file)
        {
            File = file;
        }

        public async Task<CanvasBitmap> ImportFileAsync(CanvasDevice device)
        {
            // Open it with Win2D
            using (var stream = await File.OpenReadAsync())
            {
                return await CanvasBitmap.LoadAsync(device, stream);
            }
        }
    }

    class ImportedRawPixelsFile : IImportedFile
    {
        public StorageFile File { get; }
        public int Width { get; }
        public int Height { get; }
        public DirectXPixelFormat Format { get; }

        public ImportedRawPixelsFile(StorageFile file, int width, int height, DirectXPixelFormat format)
        {
            File = file;
            Width = width;
            Height = height;
            Format = format;
        }

        public async Task<CanvasBitmap> ImportFileAsync(CanvasDevice device)
        {
            var buffer = await FileIO.ReadBufferAsync(File);
            return CanvasBitmap.CreateFromBytes(device, buffer, Width, Height, Format);
        }
    }

    static class FileImporter
    {
        public static async Task<IImportedFile> OpenFileAsync()
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bin");

            IImportedFile result = null;
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                result = await ProcessStorageFileAsync(file);
            }
            return result;
        }

        public static async Task<IImportedFile> ProcessStorageFileAsync(StorageFile file)
        {
            IImportedFile result = null;
            // We need to collect additional information if the
            // file contains raw pixel data.
            var extension = file.FileType;
            switch (extension)
            {
                case ".bin":
                    var width = 0;
                    var height = 0;
                    var format = DirectXPixelFormat.B8G8R8A8UIntNormalized;

                    // If the image name ends in (width)x(height), then use that in the dialog
                    var fileName = file.Name;
                    var fileStem = fileName.Substring(0, fileName.LastIndexOf('.'));
                    var pattern = @".*[A-z](?<width>[0-9]+)x(?<height>[0-9]+)";
                    var match = Regex.Match(fileStem, pattern);
                    if (match.Success)
                    {
                        width = int.Parse(match.Groups["width"].Value);
                        height = int.Parse(match.Groups["height"].Value);
                    }

                    var dialog = new BinaryDetailsInputDialog(width, height);
                    var dialogResult = await dialog.ShowAsync();
                    if (dialogResult == ContentDialogResult.Primary &&
                        dialog.ParseBinaryDetailsSizeBoxes(out width, out height))
                    {
                        result = new ImportedRawPixelsFile(file, width, height, format);
                    }
                    break;
                default:
                    result = new ImportedStorageFile(file);
                    break;
            }
            return result;
        }
    }
}
