using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Composition;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;

namespace ImageViewer
{
    static class TiledBrushRenderer
    {
        public static void Render(CompositionVirtualDrawingSurface surface, ICanvasBrush brush, int tileWidth, int tileHeight)
        {
            var size = surface.SizeInt32;
            var surfaceWidth = size.Width;
            var surfaceHeight = size.Height;
            var widthInTiles = surfaceWidth / tileWidth;
            if (surfaceWidth % tileWidth > 0)
            {
                widthInTiles += 1;
            }
            var heightInTiles = surfaceHeight / tileHeight;
            if (surfaceHeight % tileHeight > 0)
            {
                heightInTiles += 1;
            }
            for (var i = 0; i < widthInTiles; i++)
            {
                var tileLeft = i * tileWidth;
                for (var j = 0; j < heightInTiles; j++)
                {
                    var tileTop = j * tileHeight;
                    var currentTileWidth = tileWidth;
                    if (tileLeft + tileWidth > surfaceWidth)
                    {
                        currentTileWidth = surfaceWidth - tileLeft;
                    }
                    var currentTileHeight = tileHeight;
                    if (tileTop + tileHeight > surfaceHeight)
                    {
                        currentTileHeight = surfaceHeight - tileTop;
                    }
                    var updateRect = new Rect(tileLeft, tileTop, currentTileWidth, currentTileHeight);
                    using (var drawingSession = CanvasComposition.CreateDrawingSession(surface, updateRect))
                    {
                        drawingSession.Clear(Colors.Transparent);
                        drawingSession.FillRectangle(0, 0, (float)currentTileWidth, (float)currentTileHeight, brush);
                    }
                }
            }
        }
    }
}
