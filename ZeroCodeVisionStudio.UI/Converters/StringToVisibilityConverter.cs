using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZeroCodeVisionStudio.UI.Converters
{
    /// <summary>
    /// 字符串到 Visibility 的转换器。
    /// 当字符串为空或 null 时返回 Collapsed，否则返回 Visible。
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
