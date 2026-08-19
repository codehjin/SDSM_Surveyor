using System.IO;
using System.Text.Json;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Data;

/// <summary>파일 하나의 버전 정보(교환소/설치본 비교용).</summary>
public sealed record FileVersionInfo(bool Exists, string? Version, DateTime? GeneratedAt);

/// <summary>동기화 상태(설치본 vs 교환소).</summary>
public sealed record SyncStatus(
    FileVersionInfo InstalledSpecies, FileVersionInfo ExchangeSpecies,
    FileVersionInfo InstalledReference, FileVersionInfo ExchangeReference);

public interface ISyncService
{
    /// <summary>설치본(AppData→번들)과 교환소의 species.json·reference.json 버전을 비교.</summary>
    SyncStatus GetStatus();
    /// <summary>교환소의 파일을 AppData로 복사(가져오기). 복사한 파일 수 반환. 다음 실행부터 반영.</summary>
    int ImportFromExchange();
}

/// <summary>
/// 비동기 동기화(교환소 폴더 기반). 서버 없이 공유/클라우드 폴더로 파일을 주고받는다.
/// 가져오기: 교환소 → %AppData%\SDSM_Surveyor (다음 실행 시 로더가 자동 사용).
/// </summary>
public sealed class SyncService : ISyncService, ISingletonService
{
    private static readonly string[] Files = { "species.json", "reference.json" };

    private readonly ISettingsStore _settings;
    public SyncService(ISettingsStore settings) => _settings = settings;

    private static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SDSM_Surveyor");
    private static string BundleDir => AppDomain.CurrentDomain.BaseDirectory;

    public SyncStatus GetStatus()
    {
        var ex = _settings.Settings.ExchangeFolder;
        return new SyncStatus(
            ReadHeader(InstalledPath("species.json")),
            ReadHeader(ExchangePath(ex, "species.json")),
            ReadHeader(InstalledPath("reference.json")),
            ReadHeader(ExchangePath(ex, "reference.json")));
    }

    public int ImportFromExchange()
    {
        var ex = _settings.Settings.ExchangeFolder;
        if (string.IsNullOrWhiteSpace(ex) || !Directory.Exists(ex)) return 0;

        Directory.CreateDirectory(AppDataDir);
        int n = 0;
        foreach (var name in Files)
        {
            var src = Path.Combine(ex, name);
            if (File.Exists(src))
            {
                File.Copy(src, Path.Combine(AppDataDir, name), overwrite: true);
                n++;
            }
        }
        return n;
    }

    // 설치본: AppData 우선, 없으면 실행폴더 번들
    private static string? InstalledPath(string name)
    {
        var appData = Path.Combine(AppDataDir, name);
        if (File.Exists(appData)) return appData;
        var bundle = Path.Combine(BundleDir, name);
        return File.Exists(bundle) ? bundle : null;
    }

    private static string? ExchangePath(string? folder, string name)
        => string.IsNullOrWhiteSpace(folder) ? null : Path.Combine(folder, name);

    private static FileVersionInfo ReadHeader(string? path)
    {
        if (path is null || !File.Exists(path)) return new FileVersionInfo(false, null, null);
        try
        {
            using var s = File.OpenRead(path);
            var h = JsonSerializer.Deserialize<Header>(s, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new FileVersionInfo(true, h?.Version, h?.GeneratedAt);
        }
        catch { return new FileVersionInfo(true, null, null); }
    }

    private sealed class Header
    {
        public string? Version { get; set; }
        public DateTime? GeneratedAt { get; set; }
    }
}
