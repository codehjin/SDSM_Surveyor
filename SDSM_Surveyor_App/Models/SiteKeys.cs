using System.Text.RegularExpressions;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 지점 자리 값 · 조사연도 — 세션 키·파일명·지점 목록 거르기가 같은 규칙을 쓴다(2026-10-18 결정 D4).
/// </summary>
public static class SiteKeys
{
    /// <summary>목록 밖 지점을 직접 입력할 수 있는 대분류(D4 ②). 방류하천은 목록에서만 고른다.</summary>
    public const string EcoStatus = "생태현황";

    /// <summary>
    /// 세션 키·파일명의 지점 자리. 지점명이 있으면 지점명.
    /// 생태현황에서 지점명이 비고 하천명(대상지명)만 있으면 `평택(지점명 없음)` — 원본 양서파충류는 지점명 칸이 비어 있다.
    /// 그냥 `평택` 으로 쓰지 않는 까닭: 생태현황 포유류에 `평택` 이라는 지점이 있어 두 세션이 한 파일로 겹친다.
    /// 그 밖에는 null(저장 키가 비었다).
    /// </summary>
    public static string? Of(string? project, string? site, string? river)
    {
        if (!string.IsNullOrWhiteSpace(site)) return site.Trim();
        if (string.Equals(project?.Trim(), EcoStatus, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(river))
            return $"{river.Trim()}(지점명 없음)";
        return null;
    }

    private static readonly Regex Year4 = new(@"^\s*(\d{4})", RegexOptions.Compiled);

    /// <summary>`2025` · `2025분기1차` → 2025. 앞 네 자리가 연도가 아니면 null.</summary>
    public static int? YearOf(string? text)
    {
        var m = Year4.Match(text ?? string.Empty);
        return m.Success && int.TryParse(m.Groups[1].Value, out var y) && y is >= 1990 and <= 2100 ? y : null;
    }
}
