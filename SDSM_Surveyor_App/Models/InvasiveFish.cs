namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 생태계교란 어류 판정.
///
/// ⚠ **외래종(<c>Exotic</c>)과 생태계교란생물은 다르다.**
/// FAI 는 외래종만 쓴다(M1 국내종 종수 · M7 국내종 개체수 · M6 충식종 비율에서 외래종 차감 —
/// <c>SDSM_Core/Ecology/EcologyCalculator</c>). 반면 화면의 「⚠ 교란종」 뱃지는
/// 「생태계교란 생물 지정 고시」(환경부) 대상만 가리켜야 한다.
/// 떡붕어·이스라엘잉어 등은 외래종이지만 교란종이 아니므로 뱃지를 켜면 안 된다.
///
/// 관리자와 공유하는 <c>FishSpeciesList</c> 에는 <c>Invasive</c> 필드가 없다.
/// 종목록·관리자 모델·계산식을 건드리지 않기 위해 판정만 여기서 한다 —
/// 고시가 갱신되면 <b>이 목록 한 줄만</b> 고친다.
/// (저서·조류·포유류·양서파충류는 <c>ObservedSpecies.Invasive</c> 를 그대로 쓴다.)
/// </summary>
public static class InvasiveFish
{
    /// <summary>생태계교란 어류(환경부 고시). 국명 원문 그대로 적는다.</summary>
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "배스",
        "큰입배스",
        "블루길",
        "파랑볼우럭",
        "브라운송어",
    };

    public static bool IsInvasive(string? speciesKo) =>
        !string.IsNullOrWhiteSpace(speciesKo) && Names.Contains(speciesKo.Trim());
}
