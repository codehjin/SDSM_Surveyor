using SDSM_Models;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>화면 ↔ 조사 세션(자료함)을 잇는 서비스.</summary>
public interface ISessionService
{
    /// <summary>현재 열려 있는 세션 키(저장 전이면 null).</summary>
    string? CurrentSessionId { get; }

    /// <summary>현재 조사개황으로 만들어지는 세션 키.</summary>
    string CurrentKey { get; }

    Task<List<SessionIndexEntry>> ListAsync();

    /// <summary>현재 화면 전체를 세션으로 저장.</summary>
    Task<SessionIndexEntry> SaveCurrentAsync();

    /// <summary>세션을 7개 탭에 복원.</summary>
    Task<bool> LoadAsync(string sessionId);

    Task DeleteAsync(string sessionId);

    /// <summary>지점 간 값 복사(조사개황 유지 · 지점 교체 · 종 목록 비움).</summary>
    void CopyToSite(SurveySite site);

    /// <summary>이전 차수 복사(종 목록까지 복제 후 연도차수 교체).</summary>
    Task<bool> CopyFromPreviousAsync(string sessionId, string newYearChsu, DateTime? newDate);

    /// <summary>구버전 임시저장 파일을 세션으로 편입(최초 1회).</summary>
    Task<int> MigrateLegacyDraftsAsync();
}
