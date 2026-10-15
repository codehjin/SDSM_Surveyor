namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 어류 길드 코드 → 화면 문구.
///
/// 코드는 종목록(관리자 마스터)에 그대로 들어 있고 FAI 가 그 코드를 직접 읽는다
/// (<c>SDSM_Core/Ecology/EcologyCalculator</c>) — 그래서 **코드를 바꾸지 않고 표시만 푼다.**
/// 조사자가 종만 고르면 이 값들은 종목록에서 자동 상속된다.
///
/// <code>
///  내성도 SS 민감종 → M3     TS 내성종 → M4
///  섭식   I  충식종 → M6     O  잡식종 → M5
///  서식지 RB 여울성 저서종 → M2
/// </code>
/// </summary>
public static class FishGuildText
{
    /// <summary>내성도 길드(민감종 · 중간종 · 내성종). 수생태계 건강성 평가 지침 표기.</summary>
    public static string Tolerance(string? code) => (code ?? string.Empty).Trim() switch
    {
        "SS" => "민감종",
        "IS" => "중간종",
        "TS" => "내성종",
        ""   => "·",
        var v => v,
    };

    /// <summary>섭식 길드(충식 · 잡식 · 육식 · 초식).</summary>
    public static string Feeding(string? code) => (code ?? string.Empty).Trim() switch
    {
        "I" => "충식종",
        "O" => "잡식종",
        "C" => "육식종",
        "H" => "초식종",
        ""  => "·",
        var v => v,
    };

    /// <summary>서식지 길드 — 종목록에 실제로 들어오는 값은 `RB`(여울성 저서종)와 공백뿐이다.</summary>
    public static string Habitat(string? code) => (code ?? string.Empty).Trim() switch
    {
        "RB" => "여울성",
        ""   => "·",
        var v => v,
    };
}
