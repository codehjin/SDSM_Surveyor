using System.IO;
using System.Text.Json;
using SDSM_Core.Data;
using SDSM_Models;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 지점 마스터 로더. `species.json` 과 동일한 규칙으로 읽는다.
/// ① %AppData%\SDSM_Surveyor\sites.json (관리자 배포·교체본)
/// ② 실행폴더 번들(sites.json)
/// 파일이 없거나 손상되면 빈 목록으로 동작한다(지점 드롭다운만 비고 앱은 계속 동작).
///
/// 해석 규칙(대분류 격리 포함)은 <see cref="SiteResolver"/> 에 있다 — 여기 다시 쓰지 말 것.
/// </summary>
public sealed class SiteListProvider : ISiteListProvider, ISingletonService
{
    private readonly SiteCatalog _cat;

    public SiteListProvider() => _cat = Load();

    public string? Version => string.IsNullOrEmpty(_cat.Version) ? null : _cat.Version;

    public IReadOnlyList<SurveySite> All => _cat.Sites;

    public List<SurveySite> ByProject(string? project) => SiteResolver.ByProject(_cat.Sites, project);

    /// <summary>
    /// 공유 해석기(<see cref="SiteResolver"/>)를 감싼다(C-7). 공유 해석기는 St.번호를 치면 대분류 안에서 <b>처음 맞는 지점</b>을 고른다 —
    /// `St.4` 가 하천과 상관없이 늘 곡교천4 가 되고, 오산천의 중복 번호도 한쪽으로만 간다.
    /// ① 지점명 정확 일치는 지금처럼 바로 고른다.
    /// ② St.번호 형태면 후보(같은 St.번호·지도 번호)를 모두 모아 <b>하나일 때만</b> 고른다. 둘 이상이면 null — 화면이 후보를 보여 준다.
    /// ③ 후보가 없으면 공유 해석기 나머지 규칙(연도별 표기)을 그대로 쓴다.
    /// </summary>
    public SurveySite? Resolve(string? input, string? project = null)
    {
        if (SiteOrdering.IsStInput(input))
        {
            var pool = ByProject(project);
            var exact = pool.FirstOrDefault(s => Key(s.SiteName) == Key(input));
            if (exact is not null) return exact;
            var cands = SiteOrdering.StCandidates(pool, input);
            if (cands.Count == 1) return cands[0];
            if (cands.Count > 1) return null;
        }
        return SiteResolver.Resolve(_cat.Sites, input, project);
    }

    private static string Key(string? s) => (s ?? string.Empty).Replace(" ", "").ToUpperInvariant();

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
