using System;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI;

namespace ImageViewer.System
{
    static class ApplicationSettings
    {
        public static T GetCachedSettings<T>() where T : new()
        {
            var settings = new T();

            var localSettings = ApplicationData.Current.LocalSettings;
            var type = typeof(T);
            foreach (var field in type.GetFields())
            {
                if (localSettings.Values.TryGetValue(field.Name, out var fieldValue))
                {
                    var value = DeserializeSpecialField(field.FieldType, fieldValue);
                    field.SetValue(settings, value);
                }
            }

            return settings;
        }

        public static void CacheSettings<T>(T settings) where T : new()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var type = typeof(T);
            foreach (var field in type.GetFields())
            {
                var fieldValue = field.GetValue(settings);
                var value = SerializeSpecialField(field.FieldType, fieldValue);
                localSettings.Values[field.Name] = value;
            }
        }

        private static object DeserializeSpecialField(Type type, object serializedValue)
        {
            if (type == typeof(Color))
            {
                var stringValue = serializedValue.ToString();
                Debug.Assert(stringValue[0] == '#');
                var alpha = Convert.ToByte(stringValue.Substring(1, 2), 16);
                var red = Convert.ToByte(stringValue.Substring(3, 2), 16);
                var green = Convert.ToByte(stringValue.Substring(5, 2), 16);
                var blue = Convert.ToByte(stringValue.Substring(7, 2), 16);
                return new Color() { A = alpha, R = red, G = green, B = blue };
            }
            return serializedValue;
        }

        private static object SerializeSpecialField(Type type, object deserializedValue)
        {
            if (type == typeof(Color))
            {
                var color = (Color)deserializedValue;
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return deserializedValue;
        }
    }
}
