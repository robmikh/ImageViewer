using ImageViewer.Dialogs;
using ImageViewer.FileFormats;
using Microsoft.Graphics.Canvas;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
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
        public BinaryImportPixelFormat Format { get; }

        public ImportedRawPixelsFile(StorageFile file, int width, int height, BinaryImportPixelFormat format)
        {
            File = file;
            Width = width;
            Height = height;
            Format = format;
        }

        public async Task<CanvasBitmap> ImportFileAsync(CanvasDevice device)
        {
            var buffer = await FileIO.ReadBufferAsync(File);

            switch (Format)
            {
                // Win2D supported formats
                case BinaryImportPixelFormat.BGRA8:
                    {
                        return CanvasBitmap.CreateFromBytes(device, buffer, Width, Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                    }
                // Other formats
                case BinaryImportPixelFormat.RGB8:
                    {
                        var bytes = buffer.ToArray();
                        var bgraBytes = new byte[Width * Height * 4];
                        for (var i = 0; i < Width * Height; i++)
                        {
                            var sourceIndex = i * 3;
                            var destIndex = i * 4;

                            bgraBytes[destIndex + 0] = bytes[sourceIndex + 2];
                            bgraBytes[destIndex + 1] = bytes[sourceIndex + 1];
                            bgraBytes[destIndex + 2] = bytes[sourceIndex + 0];
                            bgraBytes[destIndex + 3] = 255;
                        }
                        return CanvasBitmap.CreateFromBytes(device, bgraBytes, Width, Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                    }
                default:
                    throw new ArgumentException();
            }
            
        }
    }

    class ImportedRmRawFile : IImportedFile
    {
        public StorageFile File { get; }
        public RmRawImage RawImage { get; }
        public int Width { get; }
        public int Height { get; }
        public RmRawPixelFormat Format { get; }

        public ImportedRmRawFile(StorageFile file, RmRawImage image)
        {
            File = file;
            RawImage = image;
            Width = (int)image.Width;
            Height = (int)image.Height;

            switch (image.PixelFormat)
            {
                case RmRawPixelFormat.BGRA8:
                case RmRawPixelFormat.RGB8:
                case RmRawPixelFormat.R8:
                    Format = image.PixelFormat;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public async Task<CanvasBitmap> ImportFileAsync(CanvasDevice device)
        {
            var bytes = RawImage.PixelBytes;

            switch (Format)
            {
                // Win2D supported formats
                case RmRawPixelFormat.BGRA8:
                    {
                        return CanvasBitmap.CreateFromBytes(device, bytes, Width, Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                    }
                // Other formats
                case RmRawPixelFormat.RGB8:
                    {
                        var bgraBytes = new byte[Width * Height * 4];
                        for (var i = 0; i < Width * Height; i++)
                        {
                            var sourceIndex = i * 3;
                            var destIndex = i * 4;

                            bgraBytes[destIndex + 0] = bytes[sourceIndex + 2];
                            bgraBytes[destIndex + 1] = bytes[sourceIndex + 1];
                            bgraBytes[destIndex + 2] = bytes[sourceIndex + 0];
                            bgraBytes[destIndex + 3] = 255;
                        }
                        return CanvasBitmap.CreateFromBytes(device, bgraBytes, Width, Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                    }
                case RmRawPixelFormat.R8:
                    {
                        var bgraBytes = new byte[Width * Height * 4];
                        for (var i = 0; i < Width * Height; i++)
                        {
                            var sourceIndex = i;
                            var destIndex = i * 4;

                            bgraBytes[destIndex + 0] = bytes[sourceIndex];
                            bgraBytes[destIndex + 1] = bytes[sourceIndex];
                            bgraBytes[destIndex + 2] = bytes[sourceIndex];
                            bgraBytes[destIndex + 3] = 255;
                        }
                        return CanvasBitmap.CreateFromBytes(device, bgraBytes, Width, Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                    }
                default:
                    throw new ArgumentException();
            }
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
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".bin");
            picker.FileTypeFilter.Add(".rmraw");

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
                    {
                        var width = 0;
                        var height = 0;
                        var format = BinaryImportPixelFormat.BGRA8;

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

                        // Guess the format based on the size
                        if (width > 0 && height > 0)
                        {
                            var basiProperties = await file.GetBasicPropertiesAsync();
                            var size = basiProperties.Size;
                            var pixels = (ulong)(width * height);
                            if (pixels * 4 == size)
                            {
                                format = BinaryImportPixelFormat.BGRA8;
                            }
                            else if (pixels * 3 == size)
                            {
                                format = BinaryImportPixelFormat.RGB8;
                            }
                        }

                        var dialog = new BinaryDetailsInputDialog(width, height, format);
                        var dialogResult = await dialog.ShowAsync();
                        if (dialogResult == ContentDialogResult.Primary &&
                            dialog.ParseBinaryDetailsSizeBoxes(out width, out height, out format))
                        {
                            result = new ImportedRawPixelsFile(file, width, height, format);
                        }
                    }
                    break;
                case ".rmraw":
                    {
                        RmRawImage rawImage;
                        using (var stream = await file.OpenReadAsync())
                        {
                            rawImage = await RmRaw.ReadImageAsync(stream);
                        }
                        result = new ImportedRmRawFile(file, rawImage);
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
