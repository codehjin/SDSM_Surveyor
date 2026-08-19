using System.IO;
using System.Text.Json;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// %AppData%\SDSM_Surveyor\drafts\{taxon}.json 에 임시 저장하는 오프라인 저장소.
/// (업데이트/재설치에도 데이터가 보존되도록 실행폴더가 아닌 AppData 사용)
/// </summary>
public sealed class LocalDraftStore : ILocalDraftStore, ISingletonService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private static string RootDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SDSM_Surveyor", "drafts");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public async Task<string> SaveDraftAsync<T>(string taxon, T payload)
    {
        var path = Path.Combine(RootDir, $"{taxon}.json");
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, payload, Options);
        return path;
    }

    public async Task<T?> LoadDraftAsync<T>(string taxon)
    {
        var path = Path.Combine(RootDir, $"{taxon}.json");
        if (!File.Exists(path)) return default;

        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(fs);
    }
}
