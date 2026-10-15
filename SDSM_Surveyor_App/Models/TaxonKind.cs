namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 담당 분류군 7종. 값 이름은 세션 파일의 분류군 키(<c>ITaxonSession.Key</c>)와 **글자까지 같다** —
/// 두 곳이 어긋나면 세션 복원이 조용히 실패한다.
/// </summary>
public enum TaxonKind
{
    Fish,
    Benthos,
    Bird,
    Mammal,
    AmphibianReptile,
    WaterQuality,
    HabitatWaterEdge
}

/// <summary>분류군 하나의 표시 정보. 화면이 분류군 문자열을 직접 적지 않게 한다.</summary>
/// <param name="Kind">분류군.</param>
/// <param name="Key">세션 파일 키(= <c>ITaxonSession.Key</c>).</param>
/// <param name="Name">분류군명(어류 · 저서동물 …).</param>
/// <param name="TypeCaption">화면 유형 설명(첫 실행 타일의 둘째 줄).</param>
/// <param name="DotBrushKey">색점 브러시 키 — **흰 배경**(타일 · 팝오버 · 본문)용.</param>
/// <param name="DotOnShellBrushKey">색점 브러시 키 — **딥네이비 셸**(캡션 칩)용. design.md §3.7 대비 계약.</param>
/// <param name="CountUnit">입력량 단위. 종 입력형·관찰형은 `종`, 폼형은 `항목`.</param>
public sealed record TaxonInfo(
    TaxonKind Kind,
    string Key,
    string Name,
    string TypeCaption,
    string DotBrushKey,
    string DotOnShellBrushKey,
    string CountUnit);

/// <summary>
/// 분류군 7종의 단일 출처. 종전에는 이름·색·순서가 셸 XAML·자료함·내보내기에 흩어져 있었다.
/// ⚠ 순서는 첫 실행 타일과 재선택 팝오버가 함께 쓴다(시안 3c · 3a).
/// </summary>
public static class TaxonCatalog
{
    public static IReadOnlyList<TaxonInfo> All { get; } = new[]
    {
        new TaxonInfo(TaxonKind.Fish,             "Fish",             "어류",      "종 입력형 · FAI",
                      "FishBrush",             "FishOnShellBrush",             "종"),
        new TaxonInfo(TaxonKind.Benthos,          "Benthos",          "저서동물",  "종 입력형 · BMI",
                      "BenthosBrush",          "BenthosOnShellBrush",          "종"),
        new TaxonInfo(TaxonKind.Bird,             "Bird",             "조류",      "관찰형 · 2단 행",
                      "BirdBrush",             "BirdOnShellBrush",             "종"),
        new TaxonInfo(TaxonKind.Mammal,           "Mammal",           "포유류",    "관찰형 · 흔적 12칸",
                      "MammalBrush",           "MammalOnShellBrush",           "종"),
        new TaxonInfo(TaxonKind.AmphibianReptile, "AmphibianReptile", "양서파충류","관찰형 · 2단 행",
                      "AmphibianReptileBrush", "AmphibianReptileOnShellBrush", "종"),
        new TaxonInfo(TaxonKind.WaterQuality,     "WaterQuality",     "수질",      "폼형 · 항목별 등급",
                      "WaterQualityBrush",     "WaterQualityOnShellBrush",     "항목"),
        new TaxonInfo(TaxonKind.HabitatWaterEdge, "HabitatWaterEdge", "서식수변",  "폼형 · 10항목 평가",
                      "HabitatWaterEdgeBrush", "HabitatWaterEdgeOnShellBrush", "항목"),
    };

    public static TaxonInfo Of(TaxonKind kind) => All.First(t => t.Kind == kind);

    /// <summary>세션 키(또는 enum 이름)로 찾는다. 못 찾으면 null.</summary>
    public static TaxonInfo? ByKey(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null
        : All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}
