using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Imaging;

namespace ImageViewer
{
    static class SizeExtensions
    {
        public static SizeInt32 ToSizeInt32(this BitmapSize size)
        {
            return new SizeInt32() { Width = (int)size.Width, Height = (int)size.Height };
        }
    }
}
