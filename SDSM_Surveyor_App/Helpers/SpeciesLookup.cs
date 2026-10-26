using SDSM_Models;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 종목록에서 국명(+ 학명)으로 종 하나를 찾는다 — 어류 · 저서의 붙여넣기 · 국명 칸 입력 · 예전 세션 불러오기가 같이 쓴다(v0.5.0).
/// ① 학명이 있으면 지침 표기로 읽고(<see cref="SpeciesNameMap"/>) 국명 + 학명이 같은 종
/// ② 국명이 같은 종이 하나면 그 종
/// ③ 여럿이면 예전 번들이 쓰던 행(<see cref="SpeciesNameMap.BenthosDefaultFor"/>) — 없으면 목록에서 먼저 나오는 종.
/// 국명은 원문 그대로 비교한다(Ordinal · CLAUDE.md §5).
/// </summary>
public static class SpeciesLookup
{
    public static FishSpeciesList? Fish(IEnumerable<FishSpeciesList> list, string? ko, string? en = null) =>
        Find(list, ko, SpeciesNameMap.Fish(en), s => s.SpeciesKo, s => s.SpeciesEn, _ => null);

    public static BenthosSpeciesList? Benthos(IEnumerable<BenthosSpeciesList> list, string? ko, string? en = null) =>
        Find(list, ko, SpeciesNameMap.Benthos(en), s => s.SpeciesKo, s => s.SpeciesEn, SpeciesNameMap.BenthosDefaultFor);

    private static T? Find<T>(IEnumerable<T> list, string? ko, string? en,
                              Func<T, string?> koOf, Func<T, string?> enOf, Func<string, string?> defaultEn) where T : class
    {
        if (string.IsNullOrEmpty(ko)) return null;
        var same = list.Where(s => string.Equals(koOf(s), ko, StringComparison.Ordinal)).ToList();
        if (same.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(en)
            && same.FirstOrDefault(s => string.Equals(enOf(s), en, StringComparison.Ordinal)) is { } exact)
            return exact;
        if (same.Count == 1) return same[0];
        var def = defaultEn(ko);
        return same.FirstOrDefault(s => def is not null && string.Equals(enOf(s), def, StringComparison.Ordinal)) ?? same[0];
    }
}
