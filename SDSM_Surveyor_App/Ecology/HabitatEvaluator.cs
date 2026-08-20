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
        var d = EvaluateDetail(items, surveyUnavailableReason);
        return (d.Score, d.Grade);
    }

    /// <summary>
    /// 평가점수를 산출하면서 **항목별 점수와 합계까지 노출**한다(보고서 엑셀의 계산과정 수록용).
    /// 계산식은 <see cref="Evaluate"/>와 하나의 구현을 공유한다.
    /// </summary>
    public static HriResult EvaluateDetail(
        IEnumerable<double?> items, string? surveyUnavailableReason)
    {
        var arr = items.ToArray();
        double total = arr.Where(x => x.HasValue).Sum(x => x!.Value);

        double? score = total != 0
            ? Math.Round(total / 2, 1, MidpointRounding.AwayFromZero)
            : null;

        var unavailable = (surveyUnavailableReason ?? string.Empty).Replace(" ", "");
        string? grade;
        if (unavailable.Contains("접근불가"))
        {
            grade = "-";
        }
        else
        {
            grade =
                score >= 80 ? "A" :
                score >= 60 ? "B" :
                score >= 40 ? "C" :
                score >= 20 ? "D" :
                score >= 0 ? "E" : null;
        }

        return new HriResult { Items = arr, Total = total, Score = score, Grade = grade };
    }
}

/// <summary>서식·수변환경(HRI) 상세 결과(계산과정 포함). 보고서 엑셀에 그대로 수록한다.</summary>
public sealed class HriResult
{
    /// <summary>평가항목 1~10 점수(좌/우안 항목은 평균). 미선택은 null.</summary>
    public double?[] Items { get; set; } = Array.Empty<double?>();

    /// <summary>항목 합계.</summary>
    public double Total { get; set; }

    /// <summary>평가점수 = 합계 ÷ 2.</summary>
    public double? Score { get; set; }

    /// <summary>평가등급 A80·B60·C40·D20·E. 접근불가 시 "-".</summary>
    public string? Grade { get; set; }
}
