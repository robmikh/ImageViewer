using Windows.Foundation;
using Windows.UI.Xaml;

namespace ImageViewer
{
    class ThemeChangedEventArgs
    {
        public ThemeChangedEventArgs(EffectiveTheme theme)
        {
            Theme = theme;
        }

        public EffectiveTheme Theme { get; }
    }

    enum EffectiveTheme
    {
        Light,
        Dark
    }

    class ThemeHelper
    {
        private static ThemeHelper _helper;

        public static ThemeHelper Current => _helper;
        public static ThemeHelper EnsureThemeHelper(FrameworkElement element)
        {
            if (_helper == null)
            {
                _helper = new ThemeHelper(element);
            }
            else
            {
                _helper.ReplaceCurrentSource(element);
            }
            return _helper;
        }

        private FrameworkElement _source;
        private EffectiveTheme _theme = EffectiveTheme.Light;

        private ThemeHelper(FrameworkElement element)
        {
            _source = element;
            _source.ActualThemeChanged += OnActualThemeChanged;
            _theme = EvaluteTheme();
        }

        public EffectiveTheme CurrentTheme => _theme;
        public event TypedEventHandler<object, ThemeChangedEventArgs> ThemeChanged;

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            _theme = EvaluteTheme();
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(_theme));
        }

        private EffectiveTheme EvaluteTheme()
        {
            switch (_source.ActualTheme)
            {
                case ElementTheme.Dark:
                    return EffectiveTheme.Dark;
                case ElementTheme.Default:
                    var theme = Application.Current.RequestedTheme;
                    switch (theme)
                    {
                        case ApplicationTheme.Dark:
                            return EffectiveTheme.Dark;
                        default:
                            return EffectiveTheme.Light;
                    }
                default:
                    return EffectiveTheme.Light;
            }
        }

        public void ReplaceCurrentSource(FrameworkElement element)
        {
            _source.ActualThemeChanged -= OnActualThemeChanged;
            _source = element;
            _source.ActualThemeChanged += OnActualThemeChanged;
        }

        public void ReevaluteTheme()
        {
            var currentTheme = EvaluteTheme();
            if (currentTheme != _theme)
            {
                _theme = currentTheme;
                ThemeChanged?.Invoke(this, null);
            }
        }
    }
}
