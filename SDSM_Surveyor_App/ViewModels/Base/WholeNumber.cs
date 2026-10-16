namespace SDSM_Surveyor_App.ViewModels.Base;

/// <summary>
/// 정수 칸(하상구성 %·채집기 횟수·채집시간)의 저장값 점검(B-6).
///
/// 값은 문자열로 저장한다(세션 형식 그대로). 새로 치는 값은 <c>Behaviors.IntegerInput</c> 이 막지만,
/// 예전 세션에 이미 들어 있던 소수·문자는 <b>지우지 않고</b> 화면 경고로만 알린다.
/// </summary>
public static class WholeNumber
{
    /// <summary>비었거나 0~<paramref name="max"/> 정수면 true.</summary>
    public static bool IsValid(string? value, int max = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var t = value.Trim();
        return t.Length <= 9 && t.All(char.IsAsciiDigit) && int.Parse(t) <= max;
    }

    /// <summary>규칙에 맞지 않는 칸을 `모래 12.5, 자갈 a` 처럼 적는다. 모두 맞으면 빈 문자열.</summary>
    public static string Describe(IEnumerable<(string Label, string? Value)> fields, int max = int.MaxValue) =>
        string.Join(", ", fields.Where(f => !IsValid(f.Value, max)).Select(f => $"{f.Label} {f.Value!.Trim()}"));
}
