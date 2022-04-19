using System;
using System.Text;
using Windows.UI.Xaml.Data;

namespace ImageViewer.Converters
{
    class SecondsToTimestampConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var seconds = (double)value;
            var timeSpan = TimeSpan.FromSeconds(seconds);
            var builder = new StringBuilder();
            if (timeSpan.TotalHours > 1)
            {
                builder.Append(@"hh\:");
            }
            builder.Append(@"mm\:ss");
            return timeSpan.ToString(builder.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
