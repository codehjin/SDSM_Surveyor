using System.Text.RegularExpressions;
using SDSM_Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 지점 정렬·St.번호 표시·St.번호 해석 — <b>하천별 혼합 규칙</b>(C-7 · 지시서 §5 C-7).
///
/// 배경: 발주처도 St.번호를 확인할 수 없다. <c>sites.json</c> 의 St.번호는 제출 엑셀 `조사지점` 시트의 연도별 열에서 가져와
/// 해마다 다른 번호가 섞였다(오산천: 결측 1 · 중복 7쌍, 원천리천: 전부 없음).
/// <list type="bullet">
/// <item><b>하천(River, 가나다)</b> → 그 하천의 St.번호가 <b>빠짐·중복 없이 모두 있으면</b> St.번호 숫자 순,
///       아니면 <b>지점명 자연 정렬</b>(숫자를 수로 비교: 오산천9 &lt; 오산천10, 오산천3 &lt; 오산천3_1 &lt; 오산천3_2 &lt; 오산천4).</item>
/// <item>하천마다 <b>자료로 판정</b>한다(하천 이름을 코드에 박지 않는다). 번호가 채워지면 저절로 St.번호 순이 된다.</item>
/// <item>St.번호 <b>표시</b>도 같은 판정을 따른다 — 번호를 믿을 수 있는 하천만 `곡교천1 (St.1)`, 나머지는 지점명만.</item>
/// </list>
/// ⚠ 공유 모델(<c>SurveySite.Display</c>)·공유 해석기(<c>SiteResolver</c>)는 고치지 않는다(관리자 앱도 쓴다). 여기서 감싼다.
/// ⚠ <c>sites.json</c> 의 St.번호 값도 고치지 않는다.
/// </summary>
public static class SiteOrdering
{
    /// <summary>`ST1` · `St.1` · `st 1` — 공유 해석기와 같은 형태.</summary>
    private static readonly Regex StInput = new(@"^\s*st\.?\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StValue = new(@"^\s*st\.?\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>St.번호 숫자(`St.12` → 12). 없거나 형식이 다르면 null.</summary>
    public static int? StNumber(SurveySite s)
    {
        var m = StValue.Match(s.StNo ?? string.Empty);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    private static string RiverKey(SurveySite s) => (s.Project ?? string.Empty) + "\u0001" + (s.River ?? string.Empty).Trim();

    /// <summary>이 지점들(같은 대분류·같은 하천)의 St.번호를 믿을 수 있는가 — 모두 있고 중복이 없다.</summary>
    public static bool TrustsStNo(IReadOnlyCollection<SurveySite> riverSites)
    {
        if (riverSites.Count == 0) return false;
        var nums = riverSites.Select(StNumber).ToList();
        return nums.All(n => n is not null) && nums.Distinct().Count() == nums.Count;
    }

    /// <summary>하천(가나다) → 하천별 규칙으로 정렬한다.</summary>
    public static List<SurveySite> Sort(IEnumerable<SurveySite> sites)
    {
        var result = new List<SurveySite>();
        foreach (var g in sites.GroupBy(s => (s.River ?? string.Empty).Trim()).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var list = g.ToList();
            result.AddRange(TrustsStNo(list)
                ? list.OrderBy(s => StNumber(s)!.Value).ThenBy(s => s.SiteName, NaturalComparer.Instance)
                : list.OrderBy(s => s.SiteName, NaturalComparer.Instance));
        }
        return result;
    }

    /// <summary>
    /// 화면 표기. 번호를 믿을 수 있는 하천이면 `곡교천1 (St.1)`, 아니면 지점명만.
    /// <paramref name="all"/> 은 판정에 쓰는 전체 지점(같은 대분류·같은 하천끼리 묶어 본다).
    /// </summary>
    public static string Display(SurveySite s, IEnumerable<SurveySite> all)
    {
        if (string.IsNullOrWhiteSpace(s.StNo)) return s.SiteName;
        var key = RiverKey(s);
        var river = all.Where(x => RiverKey(x) == key).ToList();
        return TrustsStNo(river) ? $"{s.SiteName} ({s.StNo!.Trim()})" : s.SiteName;
    }

    /// <summary>입력이 St.번호 형태(`St.4` · `ST4` · `st 4`)인가.</summary>
    public static bool IsStInput(string? input) => input is not null && StInput.IsMatch(input);

    /// <summary>
    /// St.번호 형태 입력에 맞는 후보 — 같은 St.번호이거나 같은 지도 번호인 지점 <b>모두</b>(정렬 순).
    /// 공유 해석기는 처음 맞는 지점 하나를 고른다(`St.4` 를 치면 하천과 상관없이 늘 곡교천4) — 그래서 여기서 모은다.
    /// St.번호 형태가 아니면 빈 목록.
    /// </summary>
    public static List<SurveySite> StCandidates(IEnumerable<SurveySite> pool, string? input)
    {
        var m = StInput.Match(input ?? string.Empty);
        if (!m.Success) return new List<SurveySite>();
        var no = int.Parse(m.Groups[1].Value);
        return Sort(pool.Where(s => StNumber(s) == no || s.MapNumbers.Values.Any(v => int.TryParse(v, out var k) && k == no)));
    }

    /// <summary>지점명 자연 정렬 — 숫자 덩어리는 수로, 나머지는 글자 순으로 비교한다.</summary>
    public sealed class NaturalComparer : IComparer<string?>
    {
        public static readonly NaturalComparer Instance = new();
        private static readonly Regex Chunk = new(@"\d+|\D+", RegexOptions.Compiled);

        public int Compare(string? a, string? b)
        {
            var x = Chunk.Matches(a ?? string.Empty);
            var y = Chunk.Matches(b ?? string.Empty);
            for (int i = 0; i < Math.Min(x.Count, y.Count); i++)
            {
                var p = x[i].Value; var q = y[i].Value;
                int c;
                if (char.IsDigit(p[0]) && char.IsDigit(q[0]))
                {
                    c = decimal.Parse(p).CompareTo(decimal.Parse(q));
                    if (c == 0) c = p.Length.CompareTo(q.Length);   // 03 과 3 이 섞여도 순서를 정한다
                }
                else c = string.CompareOrdinal(p, q);
                if (c != 0) return c;
            }
            return x.Count.CompareTo(y.Count);   // 앞이 같으면 짧은 쪽이 먼저(오산천3 < 오산천3_1)
        }
    }
}
