using System;
using Windows.Graphics;
using Windows.UI.Xaml.Data;

namespace ImageViewer.Converters
{
    class NullableMeasureSizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var sizeNullable = value as SizeInt32?;
            if (sizeNullable.HasValue)
            {
                var size = sizeNullable.Value;
                return $"{size.Width} x {size.Height}px";
            }
            else
            {
                return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
