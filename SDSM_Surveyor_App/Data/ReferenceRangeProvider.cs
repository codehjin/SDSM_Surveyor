using System.IO;
using System.Text.Json;
using SDSM_Models;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 기준자료(reference.json) 로더.
/// ① %AppData%\SDSM_Surveyor\reference.json (관리자 배포본) → ② 실행폴더 번들 순으로 로드.
/// </summary>
public sealed class ReferenceRangeProvider : IReferenceRangeProvider, ISingletonService
{
    private readonly ReferenceData _data;

    public ReferenceRangeProvider() => _data = Load();

    public string? Version => string.IsNullOrEmpty(_data.Version) ? null : _data.Version;

    public SpeciesRange? GetFishRange(string? ko)
        => ko is not null && _data.Fish.TryGetValue(ko, out var r) ? r : null;

    public SpeciesRange? GetBenthosRange(string? ko)
        => ko is not null && _data.Benthos.TryGetValue(ko, out var r) ? r : null;

    private static ReferenceData Load()
    {
        foreach (var path in CandidatePaths())
        {
            try
            {
                if (!File.Exists(path)) continue;
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<ReferenceData>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data is not null) return data;
            }
            catch { /* 다음 후보 시도 */ }
        }
        return new ReferenceData();
    }

    private static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SDSM_Surveyor", "reference.json");
        yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reference.json");
    }
}
