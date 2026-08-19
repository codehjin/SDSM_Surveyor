using System.IO;
using System.Text.Json;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>%AppData%\SDSM_Surveyor\settings.json 로컬 설정 저장소.</summary>
public sealed class SettingsStore : ISettingsStore, ISingletonService
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SDSM_Surveyor", "settings.json");

    public AppSettings Settings { get; }

    public SettingsStore() => Settings = Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* 손상 시 기본값 */ }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
