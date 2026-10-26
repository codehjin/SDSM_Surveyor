namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 옛 학명 → 지침(국립환경과학원고시 제2026-30호) 표기 대응표 — 어류 · 저서 한 곳(2026-10-26 v0.5.0 · §21-1 · §22-1).
/// **관리자 `SDSM_App\Helpers\SpeciesNameMap.cs` 와 같은 표다**(어류 3 · 저서 9 · 비교 규칙도 같음) — 고치면 둘 다 고친다.
/// 같은지는 UI 하네스 v06 이 관리자 파일을 읽어 확인한다.
/// 조사자 앱에서는 예전 세션의 종 행 학명(v0.5.0 부터 저장) · 일괄입력 양식 내장본 종 행(tools\TemplateGen)을 읽을 때 쓴다.
/// tools\TemplateGen 도 이 표로 양식 종 행의 옛 학명을 읽는다(앱 프로젝트 참조).
/// </summary>
public static class SpeciesNameMap
{
    private static readonly Dictionary<string, string> FishOldToGuideline = new()
    {
        ["Acanthorhodeus chankaensis"] = "Acheilognathus chankaensis",   // 가시납지리
        ["Gobiobotia nakdongensis"] = "Gobiobotia naktongensis",         // 흰수마자
        ["Furcina oshimai"] = "Furcina osimae",                          // 무늬횟대(옛 값은 줄바꿈 없는 공백 U+00A0 이 섞여 있다)
    };

    private static readonly Dictionary<string, string> BenthosOldToGuideline = new()
    {
        ["Koreanomelania nodifila"] = "Koreoleptoxis nodifila",                                          // 13 염주알다슬기
        ["Koreoleptoxis globus ovalis"] = "Koreoleptoxis globus",                                        // 14 띠구슬다슬기
        ["Koreanomelania paucicincta"] = "Semisulcospira calculus",                                      // 15 주머니알다슬기
        ["Corbicula (Corbicula) fluminalis"] = "Corbicula fluminalis",                                   // 38 대륙재첩
        ["Corbicula (Corbiculina) insularis"] = "Corbicula insularis",                                   // 40 섬재첩
        ["Corbicula (Corbiculina) leana"] = "Corbicula leana",                                           // 42 참재첩
        ["Corbicula (Corbiculina) portentosa"] = "Corbicula portentosa",                                 // 44 점박이재첩
        ["Hesperocorixa mandsshurica"] = "Hesperocorixa mandshurica",                                    // 293 닮은물벌레(옛 값 오타)
        ["Tipula (Yamatotipula) latemarginata latemarginata"] = "Tipula (Yamatotipula) latemarginata",   // 341 애아이노각다귀
    };

    /// <summary>시험용 — 표를 그대로(옛 표기 → 지침 표기).</summary>
    public static IReadOnlyDictionary<string, string> FishTable => FishOldToGuideline;

    /// <summary>시험용 — 표를 그대로(옛 표기 → 지침 표기).</summary>
    public static IReadOnlyDictionary<string, string> BenthosTable => BenthosOldToGuideline;

    /// <summary>어류 — 옛 표기면 지침 표기를, 아니면 받은 값을 그대로 돌려준다(종명은 원문 그대로 · 비교할 때만 공백을 고른다).</summary>
    public static string? Fish(string? speciesEn) => Map(FishOldToGuideline, speciesEn);

    /// <summary>저서 — 옛 표기면 지침 표기를, 아니면 받은 값 그대로.</summary>
    public static string? Benthos(string? speciesEn) => Map(BenthosOldToGuideline, speciesEn);

    private static string? Map(Dictionary<string, string> table, string? speciesEn)
    {
        if (string.IsNullOrWhiteSpace(speciesEn)) return speciesEn;

        // 비교할 때만 U+00A0 을 보통 공백으로 · 연속 공백을 하나로(관리자와 같은 규칙)
        var key = string.Join(' ', speciesEn.Replace(' ', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return table.TryGetValue(key, out var guideline) ? guideline : speciesEn;
    }

    // ── 저서 — 국명이 같은 종 ─────────────────────────────────────────────────
    // 관리자 목록(823)에는 국명이 같은 종이 있다(예 '-' 9종 · '물벌레류' 2종). 예전 번들(nibr-20260820 · 803)은
    // 국명마다 관리자 번호가 가장 작은 행 하나만 두었다. 국명만 있는 자료(예전 세션 종 행 · 붙여넣기 · 국명 칸 입력)는
    // 그 행으로 읽어야 예전과 같은 종 · 같은 지수가 나온다.
    private static readonly Dictionary<string, string> BenthosNameDefault = new(StringComparer.Ordinal)
    {
        ["-"] = "Theromyzon sp.",
        ["참갯지렁이류"] = "Hediste sp.",
        ["물벌레류"] = "Hesperocorixa sp.",
        ["깨알소금쟁이류"] = "Microvelia sp.",
        ["소금쟁이류"] = "Gerris sp.",
        ["물방개류"] = "Cybister sp.",
        ["각다귀류"] = "Tipula sp.",
        ["나방파리류"] = "Psychoda sp.",
        ["별모기류"] = "Dixa sp.",
        ["등에류"] = "Tabanus sp.",
        ["나비날도래류"] = "Ceraclea sp.",
        ["깃털이끼벌레류 (휴면아)"] = "Plumatella sp.",
    };

    /// <summary>저서 — 국명이 같은 종이 여럿일 때 국명만으로 고를 학명(예전 번들 행). 없으면 null.</summary>
    public static string? BenthosDefaultFor(string? speciesKo) =>
        speciesKo is not null && BenthosNameDefault.TryGetValue(speciesKo, out var en) ? en : null;

    /// <summary>시험용 — 국명 → 예전 번들 학명.</summary>
    public static IReadOnlyDictionary<string, string> BenthosDefaults => BenthosNameDefault;
}
