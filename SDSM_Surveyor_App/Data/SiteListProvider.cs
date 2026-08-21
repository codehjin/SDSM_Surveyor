using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using SDSM_Models;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 지점 마스터 로더. `species.json` 과 동일한 규칙으로 읽는다.
/// ① %AppData%\SDSM_Surveyor\sites.json (관리자 배포·교체본)
/// ② 실행폴더 번들(sites.json)
/// 파일이 없거나 손상되면 빈 목록으로 동작한다(지점 드롭다운만 비고 앱은 계속 동작).
/// </summary>
public sealed class SiteListProvider : ISiteListProvider, ISingletonService
{
    private static readonly Regex StPattern = new(@"^\s*st\.?\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly SiteCatalog _cat;

    public SiteListProvider() => _cat = Load();

    public string? Version => string.IsNullOrEmpty(_cat.Version) ? null : _cat.Version;

    public IReadOnlyList<SurveySite> All => _cat.Sites;

    public List<SurveySite> ByProject(string? project) =>
        string.IsNullOrWhiteSpace(project)
            ? _cat.Sites.ToList()
            : _cat.Sites.Where(s => s.Project == project).ToList();

    /// <summary>
    /// 조사자가 친 값을 지점으로 해석한다. 우선순위:
    /// ① 지점명 정확일치 → ② 조사장소 번호(ST1·St.1·st 1) → ③ 연도별 표기·지도번호.
    /// 종명과 달리 지점명은 코드성 값이라 공백 제거·대문자 비교를 쓴다(CLAUDE.md §7.3).
    /// </summary>
    public SurveySite? Resolve(string? input, string? project = null)
    {
        var q = input?.Trim();
        if (string.IsNullOrEmpty(q)) return null;

        var pool = ByProject(project);
        if (pool.Count == 0) pool = _cat.Sites.ToList();

        static string Key(string? s) => (s ?? string.Empty).Replace(" ", "").ToUpperInvariant();
        var key = Key(q);

        // ① 지점명
        var hit = pool.FirstOrDefault(s => Key(s.SiteName) == key);
        if (hit is not null) return hit;

        // ② 조사장소 번호 — "ST1" "St.1" "st 1" 모두 St.1 로 본다
        var m = StPattern.Match(q);
        if (m.Success)
        {
            var no = m.Groups[1].Value;
            hit = pool.FirstOrDefault(s => Key(s.StNo) == Key($"St.{no}"));
            if (hit is not null) return hit;

            // St 번호가 없는 하천은 연도별 지도 번호가 그 역할을 한다(오산천 등)
            hit = pool.FirstOrDefault(s => s.MapNumbers.Values.Any(v => v == no));
            if (hit is not null) return hit;
        }

        // ③ 연도별 표기 이력(옛 보고서 지점명으로 찾는 경우)
        return pool.FirstOrDefault(s => s.YearAliases.Values.Any(v => Key(v) == key));
    }

    private static SiteCatalog Load()
    {
        foreach (var path in CandidatePaths())
        {
            try
            {
                if (!File.Exists(path)) continue;
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<SiteCatalog>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data is not null && data.Sites.Count > 0) return data;
            }
            catch { /* 다음 후보 시도 */ }
        }
        return new SiteCatalog();
    }

    private static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SDSM_Surveyor", "sites.json");
        yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sites.json");
    }
}
