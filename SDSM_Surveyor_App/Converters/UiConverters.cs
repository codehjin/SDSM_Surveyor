using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SDSM_Surveyor_App.Converters;

/// <summary>
/// 담당 분류군(<c>TaxonKind</c>) == ConverterParameter 이면 Visible.
/// 단일 분류군 모드에서 7개 EntryControl 중 하나만 보이게 한다.
/// ⚠ 7개 VM 싱글턴은 그대로 살아 있다 — 보이는 것만 좁힌다(README §상태).
/// </summary>
public sealed class TaxonToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && string.Equals(value.ToString(), parameter as string, StringComparison.Ordinal)
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 담당 분류군 == ConverterParameter 이면 true (팝오버 현재 항목 강조 등).
/// </summary>
public sealed class TaxonToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && string.Equals(value.ToString(), parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 미입력(null · 빈 문자열)을 가운뎃점 <c>·</c> 으로 바꾼다.
///
/// ⚠ 이 <c>·</c> 는 장식이 아니라 **null ≠ 0 을 뜻하는 의미 있는 문자**다(CLAUDE.md §4).
///   개체수 미기입과 실측 0 은 저장·계산이 서로 다르므로 화면에서도 갈려야 한다.
///   색은 가장 진한 보조색(<c>SecondaryForegroundBrush</c> · 합성 #444 · 9.74:1)을 쓴다 —
///   시안 1차본의 #aaa(2.3:1)로 되돌아가지 말 것(README 대비 교정 이력).
/// </summary>
public sealed class EmptyToDotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string ?? value?.ToString();
        return string.IsNullOrWhiteSpace(s) ? "·" : s;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value as string is { Length: > 0 } s && s != "·" ? s : null!;
}

/// <summary>미입력이면 true (미입력 표시의 색·굵기를 가르는 데 쓴다).</summary>
public sealed class IsEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string ?? value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// FAI 메트릭 점수(0 · 6.25 · 12.5)를 막대 높이(px)로. 최대 34px(<c>GridRowHeight</c>와 같은 눈금).
/// 색만으로 구분하지 않도록 낮은 항목 이름을 글자로도 함께 적는다(design.md §11).
/// </summary>
public sealed class MetricToBarHeightConverter : IValueConverter
{
    /// <summary>M1~M8 만점(12.5)일 때의 막대 높이.</summary>
    public double MaxHeight { get; set; } = 34;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is double d ? d : 0;
        var h = Math.Max(0, Math.Min(1, v / 12.5)) * MaxHeight;
        return Math.Max(3, h);   // 0 점도 자리를 남겨 8칸이 있다는 것이 보이게 한다
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 리소스 키 문자열 → Brush. <c>TaxonCatalog</c> 가 색을 키로만 들고 있게 해
/// ViewModel 이 WPF Brush 를 직접 만들지 않도록 한다(design.md §1 — 리터럴 금지).
/// </summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || Application.Current is null) return null;
        return Application.Current.TryFindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>bool? → Visibility (반대). 접힘 요약줄처럼 '펼쳐지지 않았을 때만' 보이는 것에 쓴다.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}
