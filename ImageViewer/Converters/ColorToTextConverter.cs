using System;
using Windows.UI;
using Windows.UI.Xaml.Data;

namespace ImageViewer.Converters
{
    class ColorToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var color = value as Color?;
            if (color.HasValue)
            {
                return $"A: {color.Value.A} R: {color.Value.R} G: {color.Value.G} B: {color.Value.B}";
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
