using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SDSM_Surveyor_App.Converters;

/// <summary>
/// bool?(RadioButton.IsChecked) → Visibility.
/// null/false 를 안전하게 Collapsed 로 처리한다(기본 BooleanToVisibilityConverter 는 null 에서 예외).
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
