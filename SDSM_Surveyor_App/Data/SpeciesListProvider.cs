using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using SDSM_Models;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 종목록 로더. 하드코딩이 아니라 species.json 에서 로드한다.
/// ① %AppData%\SDSM_Surveyor\species.json (관리자 배포·교체본) ② 실행폴더 번들(species.json).
/// 부분(어류 · 저서 · 조류 · 포유류 · 양서파충류)마다 고른다(2026-10-26 v0.5.0 · §22-3 ②):
///   · 교체본에 그 부분이 있고, 그 부분의 판이 번들보다 오래되지 않았으면 교체본 — 아니면 번들.
///     관리자 [종목록 내보내기] 파일은 어류 · 저서만 있으므로 그대로 두어도 조류 · 포유류 · 양서파충류는 번들로 채운다.
///   · 판 비교: 어류 · 저서 = Version 의 'admin-yyMMdd', 관찰형 = 'nibr-yyyyMMdd'(표기가 없으면 가장 오래된 판).
/// 파일이 없거나 손상 시 최소 시드로 대체(앱은 계속 동작).
/// ※ 종목록 관리(추가·수정)는 관리자 프로그램에서 하고 내보내면 이 파일 교체로 반영된다.
/// </summary>
public sealed class SpeciesListProvider : ISpeciesListProvider, ISingletonService
{
    /// <summary>교체본 위치(%AppData%\SDSM_Surveyor\species.json).</summary>
    public static string DefaultAppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SDSM_Surveyor", "species.json");

    /// <summary>번들 위치(실행폴더 species.json).</summary>
    public static string DefaultBundlePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "species.json");

    private readonly SpeciesCatalog _cat;
    private readonly List<SpeciesListPart> _parts = new();

    public SpeciesListProvider() : this(DefaultAppDataPath, DefaultBundlePath) { }

    /// <summary>경로를 정해 읽는다(시험이 가짜 교체본으로 부른다 — 실자료 AppData 를 건드리지 않게).</summary>
    public SpeciesListProvider(string? appDataPath, string? bundlePath) => _cat = Load(appDataPath, bundlePath);

    /// <summary>
    /// 쓰고 있는 종목록 판. 모든 부분이 한 파일이면 그 파일의 Version,
    /// 섞였으면 '어류·저서 판+관찰형 판'(예: jichim2026-30+admin-261022+nibr-20260820).
    /// </summary>
    public string? Version => string.IsNullOrEmpty(_cat.Version) ? null : _cat.Version;

    public IReadOnlyList<SpeciesListPart> Parts => _parts;

    public string? PartVersion(string taxonKey) => _parts.FirstOrDefault(p => p.Key == taxonKey)?.Version;

    public List<FishSpeciesList> GetFishSpecies() => _cat.Fish;
    public List<BenthosSpeciesList> GetBenthosSpecies() => _cat.Benthos;
    public List<ObservedSpecies> GetBirdSpecies() => _cat.Bird;
    public List<ObservedSpecies> GetMammalSpecies() => _cat.Mammal;
    public List<ObservedSpecies> GetAmphibianSpecies() => _cat.Amphibian;

    public const string FromAppData = "교체본(AppData)";
    public const string FromBundle = "번들(실행폴더)";
    public const string FromSeed = "최소 시드";

    private SpeciesCatalog Load(string? appDataPath, string? bundlePath)
    {
        var app = Read(appDataPath);
        var bundle = Read(bundlePath);
        if (app is null && bundle is null)
        {
            var seed = Fallback();
            AddParts(seed, seed, seed, FromSeed);
            return seed;
        }

        // 부분마다 고른다 — 어류 · 저서는 관리자 판, 관찰형 3종은 국가생물종목록 판으로 비교
        SpeciesCatalog Pick(Func<SpeciesCatalog, int> count, Func<string?, long> stamp, out string origin)
        {
            bool appHas = app is not null && count(app) > 0, bundleHas = bundle is not null && count(bundle) > 0;
            if (appHas && (!bundleHas || stamp(app!.Version) >= stamp(bundle!.Version))) { origin = FromAppData; return app!; }
            if (bundleHas) { origin = FromBundle; return bundle!; }
            origin = app is not null ? FromAppData : FromBundle;   // 둘 다 비었다 — 빈 목록
            return app ?? bundle!;
        }

        var fishSrc = Pick(c => c.Fish.Count, AdminStamp, out var fishFrom);
        var benthosSrc = Pick(c => c.Benthos.Count, AdminStamp, out var benthosFrom);
        var birdSrc = Pick(c => c.Bird.Count, ObservedStamp, out var birdFrom);
        var mammalSrc = Pick(c => c.Mammal.Count, ObservedStamp, out var mammalFrom);
        var amphSrc = Pick(c => c.Amphibian.Count, ObservedStamp, out var amphFrom);

        var cat = new SpeciesCatalog
        {
            Fish = fishSrc.Fish, Benthos = benthosSrc.Benthos,
            Bird = birdSrc.Bird, Mammal = mammalSrc.Mammal, Amphibian = amphSrc.Amphibian,
            GeneratedAt = fishSrc.GeneratedAt,
        };
        _parts.Add(new("Fish", "어류", cat.Fish.Count, AdminPart(fishSrc.Version), fishFrom));
        _parts.Add(new("Benthos", "저서동물", cat.Benthos.Count, AdminPart(benthosSrc.Version), benthosFrom));
        _parts.Add(new("Bird", "조류", cat.Bird.Count, ObservedPart(birdSrc.Version), birdFrom));
        _parts.Add(new("Mammal", "포유류", cat.Mammal.Count, ObservedPart(mammalSrc.Version), mammalFrom));
        _parts.Add(new("Amphibian", "양서파충류", cat.Amphibian.Count, ObservedPart(amphSrc.Version), amphFrom));

        var sources = new[] { fishSrc, benthosSrc, birdSrc, mammalSrc, amphSrc }.Distinct().ToList();
        cat.Version = sources.Count == 1
            ? fishSrc.Version
            : string.Join("+", new[] { AdminPart(fishSrc.Version), ObservedPart(birdSrc.Version) }.Where(s => s.Length > 0).Distinct());
        return cat;
    }

    private void AddParts(SpeciesCatalog fish, SpeciesCatalog benthos, SpeciesCatalog observed, string origin)
    {
        _parts.Add(new("Fish", "어류", fish.Fish.Count, fish.Version, origin));
        _parts.Add(new("Benthos", "저서동물", benthos.Benthos.Count, benthos.Version, origin));
        _parts.Add(new("Bird", "조류", observed.Bird.Count, observed.Version, origin));
        _parts.Add(new("Mammal", "포유류", observed.Mammal.Count, observed.Version, origin));
        _parts.Add(new("Amphibian", "양서파충류", observed.Amphibian.Count, observed.Version, origin));
    }

    private static SpeciesCatalog? Read(string? path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var data = JsonSerializer.Deserialize<SpeciesCatalog>(
                File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return data is not null && (data.Fish.Count > 0 || data.Benthos.Count > 0
                                        || data.Bird.Count > 0 || data.Mammal.Count > 0 || data.Amphibian.Count > 0)
                ? data : null;
        }
        catch { return null; /* 손상 — 다른 후보를 쓴다 */ }
    }

    // ── 판 표기 ──────────────────────────────────────────────────────────────
    // 어류 · 저서 : 'admin-yyMMdd'(예: jichim2026-30+admin-261022) · 관찰형 : 'nibr-yyyyMMdd'. 표기가 없으면 0(가장 오래됨).
    private static readonly Regex AdminToken = new(@"admin-(\d{6})", RegexOptions.CultureInvariant);
    private static readonly Regex ObservedToken = new(@"nibr-(\d{8})", RegexOptions.CultureInvariant);

    internal static long AdminStamp(string? version) =>
        AdminToken.Match(version ?? string.Empty) is { Success: true } m ? long.Parse(m.Groups[1].Value) : 0;

    internal static long ObservedStamp(string? version) =>
        ObservedToken.Match(version ?? string.Empty) is { Success: true } m ? long.Parse(m.Groups[1].Value) : 0;

    /// <summary>Version 에서 어류 · 저서 판(관찰형 'nibr-…' 를 뺀 앞부분). 관리자 표기가 없으면 Version 그대로.</summary>
    internal static string AdminPart(string? version)
    {
        var v = version ?? string.Empty;
        if (!AdminToken.IsMatch(v)) return v;
        return string.Join("+", v.Split('+').Where(t => !ObservedToken.IsMatch(t)));
    }

    /// <summary>Version 에서 관찰형 판('nibr-…'). 없으면 Version 그대로.</summary>
    internal static string ObservedPart(string? version)
    {
        var v = version ?? string.Empty;
        var m = ObservedToken.Match(v);
        return m.Success ? m.Value : v;
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

/// <summary>종목록 한 부분(분류군) — 정보 창 · 종목록 창에 보이는 판 · 종 수 · 출처.</summary>
/// <param name="Key">Fish · Benthos · Bird · Mammal · Amphibian</param>
/// <param name="Name">화면 이름(어류 · 저서동물 …)</param>
/// <param name="Count">종 수(국명이 같은 종도 따로 센다 — 관리자 목록 그대로)</param>
/// <param name="Version">그 부분의 판</param>
/// <param name="Origin">교체본(AppData) · 번들(실행폴더) · 최소 시드</param>
public sealed record SpeciesListPart(string Key, string Name, int Count, string Version, string Origin);
