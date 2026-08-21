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

    public SurveySite? Resolve(string? input, string? project = null)
        => SiteResolver.Resolve(_cat.Sites, input, project);

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
