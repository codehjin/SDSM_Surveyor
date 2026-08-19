namespace SDSM_Surveyor_App.Data;

/// <summary>오프라인 로컬 임시 저장소(사용자 AppData).</summary>
public interface ILocalDraftStore
{
    /// <summary>분류군별 임시 자료를 로컬에 저장하고 저장 경로를 반환.</summary>
    Task<string> SaveDraftAsync<T>(string taxon, T payload);

    /// <summary>저장된 임시 자료를 불러옴(없으면 default).</summary>
    Task<T?> LoadDraftAsync<T>(string taxon);
}
