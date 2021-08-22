using System;
using Windows.UI.Xaml.Data;

namespace ImageViewer.Converters
{
    class FloatToPercentageConverter: IValueConverter
    {
        private static string FormatPercentage(float percentage)
        {
            var s = string.Format("{0:0.00}", percentage);

            if (s.EndsWith("00"))
            {
                return ((int)percentage).ToString();
            }
            else
            {
                return s;
            }
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var factor = (float)(double)value;
            var percentage = factor * 100.0f;
            var result = FormatPercentage(percentage);
            return $"{result}%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var rawString = (string)value;
            var percentageString = rawString.Remove(rawString.Length - 1); // Remove '%'
            return float.Parse(percentageString);
        }
    }
}
