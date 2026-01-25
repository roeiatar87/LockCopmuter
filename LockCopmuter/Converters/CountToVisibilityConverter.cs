using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LockCopmuter.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = 0;

        if (value is int i) count = i;
        else if (value is System.Collections.ICollection col) count = col.Count;

        var visible = count > 0;
        if (Invert) visible = !visible;

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
