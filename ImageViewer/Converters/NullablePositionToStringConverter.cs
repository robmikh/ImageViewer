using ImageViewer.Controls;
using System;
using Windows.UI.Xaml.Data;

namespace ImageViewer.Converters
{
    class NullablePositionToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var positionNullable = value as PositionInt32?;
            if (positionNullable.HasValue)
            {
                var position = positionNullable.Value;
                return $"{position.X}, {position.Y}px";
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
