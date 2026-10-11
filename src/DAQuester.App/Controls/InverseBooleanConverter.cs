using System.Globalization;
using Avalonia.Data.Converters;

namespace DAQuester.App.Controls;

public sealed class InverseBooleanConverter : IValueConverter
{
 public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
 public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public sealed class ObjectPresenceConverter : IValueConverter
{
 public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;
 public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
