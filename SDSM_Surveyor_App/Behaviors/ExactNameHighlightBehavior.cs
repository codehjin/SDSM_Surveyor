using System.Collections;              // IList
using System.Reflection;
using Telerik.Windows.Controls;        // IHighlightBehavior, TextSearchMode

namespace SDSM_Surveyor_App.Behaviors;

/// <summary>
/// 그리드 국명 칸(RadAutoCompleteBox) 강조 규칙 — 2026-10-18 결정 D1.
///
/// Telerik 기본 강조는 친 글자를 포함하는 <b>첫 후보</b>를 강조하고, Enter 를 누르면 그 후보를 고른다.
/// 그래서 종목록에 있는 '방울새'를 정확히 쳐도 앞에 있는 '홍방울새'가 저장됐다(5개 그리드 · 192종).
/// 여기서는 <b>친 글자와 이름이 똑같은 종만</b> 강조한다. 없으면 강조하지 않는다(-1).
///   ① 같은 이름 → Enter 로 그 종
///   ② 방향키·마우스로 후보를 고르면 → 그 종(Telerik 기본 동작 그대로)
///   ③ 그 밖 → 아무것도 고르지 않음 → 친 글자 그대로(목록 밖 국명 · <see cref="Helpers.GridSpeciesCommit"/>)
/// 후보 순서(같음 → 앞부분 → 포함)는 <see cref="ChosungFilteringBehavior"/> 가 정한다.
/// (인터페이스 시그니처: Telerik.Windows.Controls.Input 2026.3.1007 메타데이터 확인)
/// </summary>
public sealed class ExactNameHighlightBehavior : IHighlightBehavior
{
    private static readonly Dictionary<(Type, string), PropertyInfo?> _cache = new();

    public int FindHighlightedIndex(string searchText, IList filteredItems, IEnumerable<object> escapedItems,
                                    string textSearchPath, TextSearchMode textSearchMode)
    {
        var q = searchText?.Trim();
        if (string.IsNullOrEmpty(q) || filteredItems is null) return -1;
        for (int i = 0; i < filteredItems.Count; i++)
        {
            var item = filteredItems[i];
            if (item is null) continue;
            if (string.Equals(TextOf(item, textSearchPath)?.Trim(), q, StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    private static string? TextOf(object item, string path)
    {
        if (string.IsNullOrEmpty(path)) return item.ToString();
        var key = (item.GetType(), path);
        if (!_cache.TryGetValue(key, out var prop)) _cache[key] = prop = item.GetType().GetProperty(path);
        return prop?.GetValue(item)?.ToString();
    }
}
