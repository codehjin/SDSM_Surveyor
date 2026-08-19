using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>로컬 앱 설정 저장소.</summary>
public interface ISettingsStore
{
    AppSettings Settings { get; }
    void Save();
}
