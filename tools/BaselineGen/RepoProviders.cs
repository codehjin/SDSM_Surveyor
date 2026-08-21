using System.IO;
using System.Text.Json;
using SDSM_Core.Data;
using SDSM_Models;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Models;

namespace BaselineGen;

/// <summary>
/// 기준 파일 생성 전용 제공자. **저장소 안 파일만 읽는다.**
///
/// 운영 제공자(`SpeciesListProvider`·`ReferenceRangeProvider`·`SiteListProvider`)는
/// `%AppData%\SDSM_Surveyor\` 를 먼저 본다. 그러면 기준 파일이 그 PC의 AppData 상태에 따라
/// 달라져 회귀 대조가 무의미해지고, 05_REFACTORING §0-3 의 "AppData 를 읽지도 쓰지도 않는다" 에도 어긋난다.
/// 그래서 여기서는 저장소의 번들 JSON 을 직접 읽는다.
/// </summary>
internal static class RepoJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static T Load<T>(string path) where T : new()
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"  ! 파일 없음 — 빈 값으로 진행: {path}");
            return new T();
        }
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
    }
}

/// <summary>저장소 번들 `species.json` 만 읽는 종목록 제공자.</summary>
internal sealed class RepoSpeciesProvider(string path) : ISpeciesListProvider
{
    private readonly SpeciesCatalog _cat = RepoJson.Load<SpeciesCatalog>(path);

    public string? Version => _cat.Version;
    public List<FishSpeciesList> GetFishSpecies() => _cat.Fish;
    public List<BenthosSpeciesList> GetBenthosSpecies() => _cat.Benthos;
    public List<ObservedSpecies> GetBirdSpecies() => _cat.Bird;
    public List<ObservedSpecies> GetMammalSpecies() => _cat.Mammal;
    public List<ObservedSpecies> GetAmphibianSpecies() => _cat.Amphibian;
}

/// <summary>저장소 번들 `reference.json` 만 읽는 기준자료 제공자.</summary>
internal sealed class RepoReferenceProvider(string path) : IReferenceRangeProvider
{
    private readonly ReferenceData _data = RepoJson.Load<ReferenceData>(path);

    public string? Version => _data.Version;
    public SpeciesRange? GetFishRange(string? ko)
        => ko is not null && _data.Fish.TryGetValue(ko, out var r) ? r : null;
    public SpeciesRange? GetBenthosRange(string? ko)
        => ko is not null && _data.Benthos.TryGetValue(ko, out var r) ? r : null;
}

/// <summary>
/// 저장소 번들 `sites.json` 만 읽는 지점 제공자.
/// 해석 규칙(대분류 격리 포함)은 운영과 **같은 코드**(<see cref="SiteResolver"/>)를 쓴다 —
/// 여기 복제해 두면 운영 규칙이 바뀌었을 때 기준 파일만 옛 규칙으로 만들어진다.
/// </summary>
internal sealed class RepoSiteProvider(string path) : ISiteListProvider
{
    private readonly SiteCatalog _cat = RepoJson.Load<SiteCatalog>(path);

    public string? Version => string.IsNullOrEmpty(_cat.Version) ? null : _cat.Version;
    public IReadOnlyList<SurveySite> All => _cat.Sites;

    public List<SurveySite> ByProject(string? project) => SiteResolver.ByProject(_cat.Sites, project);

    public SurveySite? Resolve(string? input, string? project = null)
        => SiteResolver.Resolve(_cat.Sites, input, project);
}

/// <summary>
/// 세션 저장은 기준 파일 생성과 무관하다. 실수로 호출되면 곧바로 드러나게 예외를 던진다.
/// (조용히 넘어가면 AppData 에 쓰는 코드가 섞여도 알아채지 못한다.)
/// </summary>
internal sealed class NoSessionService : ISessionService
{
    private static InvalidOperationException Fail([System.Runtime.CompilerServices.CallerMemberName] string m = "")
        => new($"기준 파일 생성에서는 세션 저장을 쓰지 않는다. 호출됨: {m}");

    public string? CurrentSessionId => null;
    public string CurrentKey => "baseline";
    public Task<List<SessionIndexEntry>> ListAsync() => throw Fail();
    public Task<SessionIndexEntry> SaveCurrentAsync() => throw Fail();
    public Task<bool> LoadAsync(string sessionId) => throw Fail();
    public Task DeleteAsync(string sessionId) => throw Fail();
    public void CopyToSite(SurveySite site) => throw Fail();
    public Task<bool> CopyFromPreviousAsync(string sessionId, string newYearChsu, DateTime? newDate) => throw Fail();
    public Task<int> MigrateLegacyDraftsAsync() => throw Fail();
}
