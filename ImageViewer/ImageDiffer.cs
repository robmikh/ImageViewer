using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Popups;

namespace ImageViewer
{
    public class DiffResult
    {
        public CanvasBitmap Bitmap { get; }
        public bool ColorChannelsMatch { get; }
        public bool AlphaChannelsMatch { get; }

        public DiffResult(CanvasBitmap bitmap, bool colorsMatch, bool alphasMatch)
        {
            Bitmap = bitmap;
            ColorChannelsMatch = colorsMatch;
            AlphaChannelsMatch = alphasMatch;
        }
    }

    static class ImageDiffer
    {
        public static async Task<DiffResult> GenerateDiff(CanvasDevice device, IImportedFile file1, IImportedFile file2)
        {
            var image1 = await file1.ImportFileAsync(device);
            var image2 = await file2.ImportFileAsync(device);

            // Make sure they're the same size
            var size1 = image1.SizeInPixels;
            var size2 = image2.SizeInPixels;
            if (size1.Width != size2.Width || size1.Height != size2.Height)
            {
                var dialog = new MessageDialog("Sizes do not match!");
                await dialog.ShowAsync();
                return null;
            }

            var result = GenerateDiffBitmap(device, image1, image2);
            return result;
        }

        private static DiffResult GenerateDiffBitmap(CanvasDevice device, CanvasBitmap image1, CanvasBitmap image2)
        {
            var pixels1 = image1.GetPixelColors();
            var pixels2 = image2.GetPixelColors();
            Debug.Assert(pixels1.Length == pixels2.Length);

            var newPixels = new List<Color>();
            var identicalColors = true;
            var identicalAlpha = true;
            for (var i = 0; i < pixels1.Length; i++)
            {
                var pixel1 = pixels1[i];
                var pixel2 = pixels2[i];

                var diffB = pixel1.B - pixel2.B;
                var diffG = pixel1.G - pixel2.G;
                var diffR = pixel1.R - pixel2.R;

                if (diffB != 0 || diffG != 0 || diffR != 0)
                {
                    identicalColors = false;
                }

                if (pixel1.A != pixel2.A)
                {
                    identicalAlpha = false;
                }

                var newPixel = new Color
                {
                    B = (byte)diffB,
                    G = (byte)diffG,
                    R = (byte)diffR,
                    A = 255
                };
                newPixels.Add(newPixel);
            }

            var size = image1.SizeInPixels;
            var bitmap = CanvasBitmap.CreateFromColors(device, newPixels.ToArray(), (int)size.Width, (int)size.Height);
            return new DiffResult(bitmap, identicalColors, identicalAlpha);
        }
    }
}
