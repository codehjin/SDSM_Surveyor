using System.Globalization;

namespace SDSM_Surveyor_App.ViewModels.Base;

/// <summary>
/// 소수 칸(저서 환경 6칸 · 수질 측정값)의 저장값 점검(v0.5.1 · 5차 연계 시험 새 문제 1).
///
/// 값은 문자열로 저장한다(세션 형식 그대로). 새로 치는 값은 <c>Behaviors.DecimalInput</c> 이 막지만,
/// 예전 세션에 이미 들어 있던 글자('18.3Z' 등)는 <b>지우지 않고</b> 칸 경고 · [일괄입력용] 막힘으로 알린다.
/// 판정은 일괄입력용 파일이 그 값을 <b>숫자로 쓰는가</b>(<c>Export.BulkTemplate.Put</c> 과 같은 규칙)다 —
/// 숫자로 못 쓰면 글자로 들어가 관리자 일괄등록이 그 기록을 '형변환실패'로 막는다.
/// </summary>
public static class DecimalNumber
{
    /// <summary>비었거나 숫자로 읽히면 true(쉼표가 있으면 숫자로 보지 않는다 — 일괄입력용과 같음).</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var v = value.Trim();
        return !v.Contains(',')
               && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
               && double.IsFinite(d);
    }

    /// <summary>숫자가 아닌 칸을 `저서 수온 「18.3Z」` 처럼 적는다(한 칸 한 줄). 모두 맞으면 빈 목록.</summary>
    public static IReadOnlyList<string> Invalid(string taxon, IEnumerable<(string Label, string? Value)> fields) =>
        fields.Where(f => !IsValid(f.Value)).Select(f => $"{taxon} {f.Label} 「{f.Value!.Trim()}」").ToList();

    /// <summary>[일괄입력용] 막힘 까닭 — 칸마다 '… — 숫자가 아닙니다'. 없으면 빈 문자열.</summary>
    public static string BlockReason(IReadOnlyList<string> invalid) =>
        string.Join("\n", invalid.Select(x => $"{x} — 숫자가 아닙니다"));

    /// <summary>칸 경고 줄 — '숫자가 아닌 칸: 저서 수온 「18.3Z」 · …'. 없으면 빈 문자열.</summary>
    public static string Warning(IReadOnlyList<string> invalid) =>
        invalid.Count == 0 ? string.Empty : "숫자가 아닌 칸: " + string.Join(" · ", invalid) + " — 고치면 [일괄입력용]을 만들 수 있습니다";
}
