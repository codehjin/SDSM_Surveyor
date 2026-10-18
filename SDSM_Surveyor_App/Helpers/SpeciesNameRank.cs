using SDSM_Core.Helpers;   // ChosungHelper

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 국명 후보 순위(2026-10-18 결정 D1) — 그리드 국명 칸·빠른추가가 같은 순서로 후보를 보인다.
/// <c>0</c> 같은 이름 · <c>1</c> 앞부분 일치(글자 또는 초성) · <c>2</c> 포함(글자 또는 초성) · <c>-1</c> 안 맞음.
/// 맞는지 여부는 <see cref="ChosungHelper.IsMatch"/> 와 같다(순서만 정한다).
/// </summary>
public static class SpeciesNameRank
{
    public static int Of(string? name, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return 2;
        if (string.IsNullOrEmpty(name)) return -1;
        var t = name.Trim();
        var q = query.Trim();

        if (string.Equals(t, q, StringComparison.Ordinal)) return 0;
        if (t.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 1;

        if (ChosungHelper.IsChosungQuery(q))
        {
            var ct = ChosungHelper.ToChosung(t).Replace(" ", string.Empty);
            var cq = q.Replace(" ", string.Empty);
            if (ct.StartsWith(cq, StringComparison.Ordinal)) return 1;
            if (ct.Contains(cq, StringComparison.Ordinal)) return 2;
        }
        return t.Contains(q, StringComparison.OrdinalIgnoreCase) ? 2 : -1;
    }

    /// <summary>순위대로 거르고 정렬한다(같은 순위는 원래 순서 — 종목록 순).</summary>
    public static IEnumerable<T> Order<T>(IEnumerable<T> items, Func<T, string?> name, string? query) =>
        items.Select(i => (i, r: Of(name(i), query))).Where(x => x.r >= 0).OrderBy(x => x.r).Select(x => x.i);
}
