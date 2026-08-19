namespace SDSM_Surveyor_App.Ecology;

/// <summary>
/// 서식·수변환경 평가점수/등급 산정. 관리자 GetGrade 로직 이식.
/// 평가항목 10개 합계 ÷ 2 → 점수, 접근불가면 등급 "-".
/// </summary>
public static class HabitatEvaluator
{
    public static (double? score, string? grade) Evaluate(
        IEnumerable<double?> items, string? surveyUnavailableReason)
    {
        double total = items.Where(x => x.HasValue).Sum(x => x!.Value);

        double? score = total != 0
            ? Math.Round(total / 2, 1, MidpointRounding.AwayFromZero)
            : null;

        var unavailable = (surveyUnavailableReason ?? string.Empty).Replace(" ", "");
        if (unavailable.Contains("접근불가"))
            return (score, "-");

        string? grade =
            score >= 80 ? "A" :
            score >= 60 ? "B" :
            score >= 40 ? "C" :
            score >= 20 ? "D" :
            score >= 0 ? "E" : null;

        return (score, grade);
    }
}
