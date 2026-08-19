using System.Text;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 한글 초성 검색 유틸. 'ㅋㅈㄴ' 같은 초성 입력으로 종명을 매칭한다.
/// (검증됨: 참붕어→ㅊㅂㅇ, 돌고기→ㄷㄱ, 큰지느러미얼룩동사리→ㅋㅈㄴ)
/// 추후 SDSM_Core로 이관 예정.
/// </summary>
public static class ChosungHelper
{
    // 19 초성 (한글 호환 자모)
    private static readonly char[] Chosung =
    {
        'ㄱ','ㄲ','ㄴ','ㄷ','ㄸ','ㄹ','ㅁ','ㅂ','ㅃ','ㅅ',
        'ㅆ','ㅇ','ㅈ','ㅉ','ㅊ','ㅋ','ㅌ','ㅍ','ㅎ'
    };

    private const int HangulBase = 0xAC00;    // '가'
    private const int HangulLast = 0xD7A3;    // '힣'
    private const int JamoPerChosung = 588;   // 21(중성) * 28(종성)

    /// <summary>문자열을 초성 문자열로 변환(한글 음절만 초성으로, 그 외는 그대로).</summary>
    public static string ToChosung(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch >= HangulBase && ch <= HangulLast)
                sb.Append(Chosung[(ch - HangulBase) / JamoPerChosung]);
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>입력이 모두 초성(한글 호환 자모 자음)인지 판단.</summary>
    public static bool IsChosungQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;

        foreach (var c in query)
        {
            if (c == ' ') continue;
            if (c < 0x3131 || c > 0x314E) return false; // 자음 호환 자모 범위 밖
        }
        return true;
    }

    /// <summary>text가 query에 매칭되는지: (1) 일반 부분일치 또는 (2) 초성 매칭.</summary>
    public static bool IsMatch(string? text, string? query)
    {
        if (string.IsNullOrEmpty(query)) return true;
        if (string.IsNullOrEmpty(text)) return false;

        var t = text.Trim();
        var q = query.Trim();

        // (1) 일반 부분일치 — 완성형 한글/영문 모두 처리(대소문자 무시)
        if (t.Contains(q, StringComparison.OrdinalIgnoreCase))
            return true;

        // (2) 초성 검색 — 입력이 초성 자음으로만 이루어진 경우
        if (IsChosungQuery(q))
        {
            var choText = ToChosung(t).Replace(" ", string.Empty);
            var choQuery = q.Replace(" ", string.Empty);
            return choText.Contains(choQuery, StringComparison.Ordinal);
        }

        return false;
    }
}
