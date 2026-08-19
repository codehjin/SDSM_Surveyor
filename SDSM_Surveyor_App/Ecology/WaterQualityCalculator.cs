namespace SDSM_Surveyor_App.Ecology;

/// <summary>
/// 수질 항목별 등급 산정. 관리자 GB 로직을 그대로 이식(결과 동일).
/// 값이 null(미측정)이면 등급도 null.
/// </summary>
public static class WaterQualityCalculator
{
    public static int? PhGrade(double? v)
        => v is null ? null : (v >= 6.5 && v <= 8.5 ? 1 : 4);

    public static string? BodGrade(double? v) => v switch
    {
        null => null,
        <= 1 => "1a",
        <= 2 => "1b",
        <= 3 => "2",
        <= 5 => "3",
        <= 8 => "4",
        <= 10 => "5",
        _ => "6"
    };

    public static string? CodGrade(double? v) => v switch
    {
        null => null,
        <= 2 => "1a",
        <= 4 => "1b",
        <= 5 => "2",
        <= 7 => "3",
        <= 9 => "4",
        <= 11 => "5",
        _ => "6"
    };

    public static string? TocGrade(double? v) => v switch
    {
        null => null,
        <= 2 => "1a",
        <= 3 => "1b",
        <= 4 => "2",
        <= 5 => "3",
        <= 6 => "4",
        <= 8 => "5",
        _ => "6"
    };

    public static int? SsGrade(double? v) => v switch
    {
        null => null,
        <= 25 => 1,
        <= 100 => 4,
        _ => 5
    };

    public static string? DoGrade(double? v) => v switch
    {
        null => null,
        >= 7.5 => "1a",
        >= 5 => "1b",
        >= 2 => "4",
        _ => "6"
    };

    public static string? TpGrade(double? v) => v switch
    {
        null => null,
        <= 0.02 => "1a",
        <= 0.04 => "1b",
        <= 0.1 => "2",
        <= 0.2 => "3",
        <= 0.3 => "4",
        <= 0.5 => "5",
        _ => "6"
    };

    public static string? EColiGrade(double? v) => v switch
    {
        null => null,
        <= 50 => "1a",
        <= 500 => "1b",
        <= 1000 => "2",
        <= 5000 => "3",
        _ => "4"
    };
}
