using System.Collections;              // IList
using System.Reflection;
using SDSM_Core.Helpers;               // ChosungHelper
using Telerik.Windows.Controls;        // IFilteringBehavior, TextSearchMode

namespace SDSM_Surveyor_App.Behaviors;

/// <summary>
/// RadAutoCompleteBox용 초성 검색 필터.
/// 일반 부분일치 + 한글 초성(ㅋㅈㄴ) 매칭을 함께 지원한다.
/// (인터페이스 시그니처: Telerik.Windows.Controls.Input 2026.2.520 기준 검증)
/// </summary>
public sealed class ChosungFilteringBehavior : IFilteringBehavior
{
    // (타입, 속성경로) → PropertyInfo 캐시
    private static readonly Dictionary<(Type, string), PropertyInfo?> _cache = new();

    public IEnumerable<object> FindMatchingItems(
        string searchText,
        IList items,
        IEnumerable<object> escapedItems,
        string textSearchPath,
        TextSearchMode textSearchMode)
    {
        var result = new List<object>();
        if (items is null) return result;

        // 이미 선택(토큰)된 항목 제외 — 단일 선택 모드에선 보통 비어 있음
        var escaped = escapedItems as IList<object> ?? escapedItems?.ToList();

        foreach (var item in items)
        {
            if (item is null) continue;
            if (escaped != null && escaped.Contains(item)) continue;

            var text = GetItemText(item, textSearchPath);
            if (ChosungHelper.IsMatch(text, searchText))
                result.Add(item);
        }
        return result;
    }

    // textSearchPath(예: "SpeciesKo") 속성 값을 리플렉션으로 읽어옴(캐시 사용)
    private static string GetItemText(object item, string path)
    {
        if (string.IsNullOrEmpty(path))
            return item.ToString() ?? string.Empty;

        var key = (item.GetType(), path);
        if (!_cache.TryGetValue(key, out var prop))
        {
            prop = item.GetType().GetProperty(path);
            _cache[key] = prop;
        }
        return prop?.GetValue(item)?.ToString() ?? string.Empty;
    }
}
