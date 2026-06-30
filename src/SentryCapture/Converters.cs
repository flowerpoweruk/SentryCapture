using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SentryCapture;

/// <summary>Visible when the bound int count is 0, otherwise collapsed (for empty-state panels).</summary>
public class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value is int i ? i : 0;
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Collapsed when the bound string is null/empty, otherwise visible.</summary>
public class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? s = value as string;
        return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
