using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Poe2FilterEditor.Infrastructure
{
    /// <summary>
    /// bool → Visibility，参数为 "invert" 时取反。
    /// </summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// null / 空字符串 → Collapsed，参数为 "invert" 时反向。
    /// </summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var hasValue = value != null && !(value is string s && s.Length == 0);
            if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            {
                hasValue = !hasValue;
            }

            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => !(value is bool b && b);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => !(value is bool b && b);
    }

    /// <summary>
    /// Color → SolidColorBrush（用于色块显示）。
    /// </summary>
    public sealed class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return Colors.Transparent;
        }
    }

    /// <summary>
    /// 数值 → 是否等于 ConverterParameter（用于 RadioButton 绑定枚举）。
    /// </summary>
    public sealed class EnumMatchConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                return false;
            }

            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null && targetType.IsEnum)
            {
                return Enum.Parse(targetType, parameter.ToString()!, true);
            }

            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// bool → 画笔，ConverterParameter 形如 "#E0B94B|#6B6156"。
    /// </summary>
    public sealed class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            var spec = parameter as string;
            if (string.IsNullOrWhiteSpace(spec))
            {
                return flag ? Brushes.White : Brushes.Gray;
            }

            var parts = spec.Split('|');
            var text = flag ? parts[0] : (parts.Length > 1 ? parts[1] : parts[0]);
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(text)!);
            }
            catch
            {
                return Brushes.White;
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
