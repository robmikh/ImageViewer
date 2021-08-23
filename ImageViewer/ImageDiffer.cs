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
        private int _width;
        private int _height;

        public Color[] ColorDiffPixels { get; }
        public Color[] AlphaDiffPixels { get; }
        public CanvasBitmap ColorDiffBitmap { get; private set; }
        public CanvasBitmap AlphaDiffBitmap { get; private set; }
        public bool ColorChannelsMatch { get; }
        public bool AlphaChannelsMatch { get; }

        public DiffResult(CanvasDevice device, Color[] colorPixels, Color[] alphaPixels, int width, int height, bool colorsMatch, bool alphasMatch)
        {
            _width = width;
            _height = height;
            ColorDiffPixels = colorPixels;
            AlphaDiffPixels = alphaPixels;
            ReplaceDeviceResources(device);
            ColorChannelsMatch = colorsMatch;
            AlphaChannelsMatch = alphasMatch;
        }

        public void ReplaceDeviceResources(CanvasDevice device)
        {
            ColorDiffBitmap?.Dispose();
            AlphaDiffBitmap?.Dispose();
            ColorDiffBitmap = CanvasBitmap.CreateFromColors(device, ColorDiffPixels, _width, _height);
            AlphaDiffBitmap = CanvasBitmap.CreateFromColors(device, AlphaDiffPixels, _width, _height);
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

            var colorDiffPixels = new List<Color>();
            var identicalColors = true;
            var alphaDiffPixels = new List<Color>();
            var identicalAlpha = true;
            for (var i = 0; i < pixels1.Length; i++)
            {
                var pixel1 = pixels1[i];
                var pixel2 = pixels2[i];

                var diffB = pixel1.B - pixel2.B;
                var diffG = pixel1.G - pixel2.G;
                var diffR = pixel1.R - pixel2.R;
                var diffA = pixel1.A - pixel2.A;

                if (diffB != 0 || diffG != 0 || diffR != 0)
                {
                    identicalColors = false;
                }

                if (diffA != 0)
                {
                    identicalAlpha = false;
                }

                var newColorPixel = new Color
                {
                    B = (byte)diffB,
                    G = (byte)diffG,
                    R = (byte)diffR,
                    A = 255
                };
                colorDiffPixels.Add(newColorPixel);
                var newAlphaPixel = new Color
                {
                    B = (byte)diffA,
                    G = (byte)diffA,
                    R = (byte)diffA,
                    A = 255
                };
                alphaDiffPixels.Add(newAlphaPixel);
            }

            var size = image1.SizeInPixels;
            var colorDiff = colorDiffPixels.ToArray();
            var alphaDiff = alphaDiffPixels.ToArray();
            return new DiffResult(device, colorDiff, alphaDiff, (int)size.Width, (int)size.Height, identicalColors, identicalAlpha);
        }
    }
}
