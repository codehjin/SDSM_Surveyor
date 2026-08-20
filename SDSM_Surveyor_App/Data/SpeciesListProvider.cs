using System.IO;
using System.Text.Json;
using SDSM_Models;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 종목록 로더. 하드코딩이 아니라 species.json(관리자 마스터 내보내기)에서 로드한다.
/// ① %AppData%\SDSM_Surveyor\species.json (관리자 배포·교체본)
/// ② 실행폴더 번들(species.json) 순으로 로드.
/// 파일이 없거나 손상 시 최소 시드로 대체(앱은 계속 동작).
/// ※ 종목록 관리(추가·수정)는 관리자 프로그램에서 하고 내보내면 이 파일 교체로 반영된다.
/// </summary>
public sealed class SpeciesListProvider : ISpeciesListProvider, ISingletonService
{
    private readonly SpeciesCatalog _cat;

    public SpeciesListProvider() => _cat = Load();

    /// <summary>로드된 종목록 버전(없으면 null). 상태표시·검증에 사용 가능.</summary>
    public string? Version => string.IsNullOrEmpty(_cat.Version) ? null : _cat.Version;

    public List<FishSpeciesList> GetFishSpecies() => _cat.Fish;
    public List<BenthosSpeciesList> GetBenthosSpecies() => _cat.Benthos;
    public List<ObservedSpecies> GetBirdSpecies() => _cat.Bird;
    public List<ObservedSpecies> GetMammalSpecies() => _cat.Mammal;
    public List<ObservedSpecies> GetAmphibianSpecies() => _cat.Amphibian;

    private static SpeciesCatalog Load()
    {
        foreach (var path in CandidatePaths())
        {
            try
            {
                if (!File.Exists(path)) continue;
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<SpeciesCatalog>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data is not null && (data.Fish.Count > 0 || data.Benthos.Count > 0
                                         || data.Bird.Count > 0 || data.Mammal.Count > 0 || data.Amphibian.Count > 0))
                    return data;
            }
            catch { /* 다음 후보 시도 */ }
        }
        return Fallback();
    }

    private static IEnumerable<string> CandidatePaths()
    {
        // AppData(관리자 배포·교체본) 우선 → 실행폴더 번들
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SDSM_Surveyor", "species.json");
        yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "species.json");
    }

    // species.json이 전혀 없을 때만 쓰는 최소 시드(정상 배포 시 파일이 대체).
    private static SpeciesCatalog Fallback() => new()
    {
        Version = "seed",
        Fish = new()
        {
            new() { SpeciesKo = "피라미", SpeciesEn = "Zacco platypus",  ToleranceGuild = "TS", FeedingGuild = "I" },
            new() { SpeciesKo = "붕어",   SpeciesEn = "Carassius auratus", ToleranceGuild = "TS", FeedingGuild = "O" },
        },
        Benthos = new()
        {
            new() { SpeciesKo = "깔따구류", SpeciesEn = "Chironomidae sp.", SaprobicValue = 3.5, IndicatorWeight = 5 },
        },
        Bird = new()
        {
            new() { SpeciesKo = "흰뺨검둥오리", SpeciesEn = "Anas zonorhyncha", OrderKo = "기러기목", FamilyKo = "오리과" },
            new() { SpeciesKo = "왜가리",       SpeciesEn = "Ardea cinerea",    OrderKo = "사다새목", FamilyKo = "백로과" },
        },
        Mammal = new()
        {
            new() { SpeciesKo = "고라니", SpeciesEn = "Hydropotes inermis", OrderKo = "우제목", FamilyKo = "사슴과" },
            new() { SpeciesKo = "수달",   SpeciesEn = "Lutra lutra",        OrderKo = "식육목", FamilyKo = "족제비과",
                    Endangered1 = "O", NaturalMonument = "O" },
        },
        Amphibian = new()
        {
            new() { SpeciesKo = "참개구리",   SpeciesEn = "Pelophylax nigromaculatus", OrderKo = "무미목", FamilyKo = "개구리과" },
            new() { SpeciesKo = "황소개구리", SpeciesEn = "Lithobates catesbeianus",   OrderKo = "무미목", FamilyKo = "개구리과",
                    Invasive = "O" },
        },
    };
}
