namespace SDSM_Surveyor_App.ViewModels.Base;

/// <summary>
/// 같은 종을 또 넣었을 때의 처리(검토요청 B-1 · `docs\CLAUDE_CODE_TASK_261015.md` §4).
/// 종이 들어가는 5개 분류군 모두 <b>입력 시점</b>에 처리한다 — 계산 코드(SDSM_Core)는 그대로 둔다.
/// </summary>
public enum DuplicateSpeciesMode
{
    /// <summary>
    /// 한 행으로 합친다(어류·저서동물). 한 지점·한 회차에 종당 한 행이 양식 구조이고,
    /// SDSM_Core 계산이 행 단위라 두 행이면 종수가 늘고 저서 BMI 는 오탁치·가중치가 이중으로 더해진다.
    /// </summary>
    Merge,

    /// <summary>
    /// 행은 그대로 넣고 알리기만 한다(조류·포유류·양서파충류). 한 행이 관찰 1건이라
    /// 위치·흔적·서식유형이 다르면 같은 종 여러 행이 정상이다. 종수 집계는 이미 `Distinct()` 다.
    /// </summary>
    Warn,
}

/// <summary>같은 종 합산에 쓰는 개체수 규칙. 결측(null)을 0 으로 채우지 않는다(CLAUDE.md §4).</summary>
public static class CountMath
{
    /// <summary>`null+null=null` · `null+x=x` · `x+null=x` · `x+y=x+y`.</summary>
    public static int? Sum(int? a, int? b) => a is null && b is null ? null : (a ?? 0) + (b ?? 0);

    /// <inheritdoc cref="Sum(int?, int?)"/>
    public static double? Sum(double? a, double? b) => a is null && b is null ? null : (a ?? 0) + (b ?? 0);

    /// <summary>칩 표기. 결측은 `·`(화면의 미입력 표시와 같다).</summary>
    public static string Show(int? v) => v?.ToString() ?? "·";

    /// <inheritdoc cref="Show(int?)"/>
    public static string Show(double? v) => v?.ToString("0.###") ?? "·";

    /// <summary>
    /// 비고 잇기. 둘 다 있으면 ` / ` 로 잇는다 — 버리지 않는다.
    /// 글자가 완전히 같으면 한 번만 둔다(같은 메모가 두 번 붙는 것을 막을 뿐, 내용은 잃지 않는다).
    /// </summary>
    public static string? JoinNote(string? a, string? b)
    {
        var x = string.IsNullOrWhiteSpace(a) ? null : a;
        var y = string.IsNullOrWhiteSpace(b) ? null : b;
        if (x is null) return y;
        if (y is null || string.Equals(x, y, StringComparison.Ordinal)) return x;
        return $"{x} / {y}";
    }
}
