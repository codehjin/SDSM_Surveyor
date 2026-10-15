using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SDSM_Surveyor_App.Converters;

/// <summary>
/// 압정(값 고정) 켜짐 → 입력칸 배경 브러시.
///
/// ⚠ Telerik 입력 컨트롤에 <c>Style</c> 을 통째로 주면 테마 템플릿이 벗겨진다(90_TECH_NOTES §2).
///   그래서 <c>Background</c> 를 **개별 속성**으로만 바꾼다. 이 컨버터가 그 값을 만든다.
///
/// 색은 <c>Colors.xaml</c> 의 토큰에서만 가져온다(design.md §1 — XAML 안 색 리터럴 금지).
/// </summary>
public sealed class PinnedBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "PinnedInputBackBrush" : "InputBackBrush";
        return Application.Current?.TryFindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
