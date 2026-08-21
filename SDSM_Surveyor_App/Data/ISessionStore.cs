using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 조사 세션 저장소(`%AppData%\SDSM_Surveyor\sessions\`). 단일 슬롯 임시저장을 대체한다.
/// </summary>
public interface ISessionStore
{
    /// <summary>자료함 목록(최신 수정 순).</summary>
    Task<List<SessionIndexEntry>> ListAsync();

    /// <summary>세션 한 건을 읽는다(없으면 null).</summary>
    Task<SurveySession?> LoadAsync(string sessionId);

    /// <summary>세션을 저장하고 목록을 갱신한다. 저장 경로를 반환.</summary>
    Task<string> SaveAsync(SurveySession session, SessionIndexEntry index);

    /// <summary>세션을 지운다(파일 + 목록).</summary>
    Task DeleteAsync(string sessionId);

    /// <summary>세션 키를 바꾼다(지점·차수 변경). 새 키가 이미 있으면 덮어쓴다.</summary>
    Task<string> RenameAsync(string oldId, SurveySession session, SessionIndexEntry index);

    /// <summary>구버전 `drafts\{분류군}.json` 이 있으면 세션 하나로 편입한다(최초 1회). 편입 건수 반환.</summary>
    Task<int> MigrateLegacyDraftsAsync();
}
