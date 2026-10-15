namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 종목록 팝업 그리드의 한 행.
///
/// 종목록 타입이 분류군마다 다르다(<c>FishSpeciesList</c> · <c>BenthosSpeciesList</c> ·
/// <c>ObservedSpecies</c>). 화면과 엑셀이 같은 모양으로 다루도록 표시 전용으로 평평하게 편 DTO다.
/// 값이 없는 칸은 <c>null</c> 로 둔다 — 화면이 알아서 비운다(결측을 0/빈 문자열로 만들지 않는다).
///
/// <see cref="Source"/> 는 원본 종목록 항목이다. 야장에 넣을 때 이 값을 그대로 넘긴다.
/// </summary>
public sealed class SpeciesCatalogRow
{
    /// <summary>원본 종목록 항목(FishSpeciesList · BenthosSpeciesList · ObservedSpecies).</summary>
    public object Source { get; init; } = default!;

    /// <summary>화면 표시용 일련번호(1부터). 필터해도 다시 매긴다.</summary>
    public int No { get; set; }

    public string? PhylumKo { get; init; }      // 문   — 저서동물만
    public string? ClassKo { get; init; }       // 강   — 어류·저서동물
    public string? OrderKo { get; init; }       // 목
    public string? FamilyKo { get; init; }      // 과
    public string? SpeciesKo { get; init; }     // 국명 (원문 그대로 — '-' 도 그대로 보여 준다)
    public string? SpeciesEn { get; init; }     // 학명

    public string? Saprobic { get; init; }      // 오탁치 s      — 저서동물
    public string? Weight { get; init; }        // 지표가중치 g  — 저서동물

    public string? Tolerance { get; init; }     // 내성도 길드   — 어류
    public string? Feeding { get; init; }       // 섭식 길드     — 어류
    public string? Habitat { get; init; }       // 서식지 길드   — 어류

    public string? Ktsn { get; init; }          // 국가생물종목록 번호 — 관찰형

    /// <summary>보호·고유·교란 표기를 한 칸에 모은 것(`멸종위기Ⅰ · 고유종`). 없으면 null.</summary>
    public string? Tags { get; init; }

    /// <summary>국명 검색용. 국명이 <c>-</c> 인 행은 빈 문자열이라 `-` 로는 검색되지 않는다.</summary>
    public string SearchKo { get; init; } = string.Empty;

    /// <summary>학명 검색용.</summary>
    public string SearchEn { get; init; } = string.Empty;
}
