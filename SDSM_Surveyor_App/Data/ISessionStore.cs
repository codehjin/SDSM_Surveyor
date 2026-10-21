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

    /// <summary>
    /// 자료함이 비어 있고 실행폴더에 <c>sessions_seed\</c> 가 있으면 예시 세션을 채운다. 채운 건수 반환.
    /// 테스트 배포본에만 그 폴더가 들어간다(<c>-p:IncludeTestData=true</c>).
    /// </summary>
    Task<int> SeedTestDataIfEmptyAsync();

    // ── 오류 기록 · 복구본(2026-10-20 연계 시험 2차 N1) ─────────────────────────
    // 자료함 아래 logs\ · _recovery\ 에 둔다. 정식 세션 파일과 목록(index.json)은 건드리지 않는다.
    // ⚠ 동기 메서드다 — 미처리 예외 처리기(앱이 곧 닫힐 수 있음)에서 끝까지 쓰고 돌아와야 한다.

    /// <summary>자료함 위치(안내 문구용).</summary>
    string Location { get; }

    /// <summary>오류를 <c>logs\error_yyMMdd.log</c> 에 덧붙인다. 쓴 파일 경로를 반환.</summary>
    string AppendErrorLog(string text);

    /// <summary>지금 화면 세션을 <c>_recovery\</c> 에 세션 형식 그대로 쓴다(정식 세션 파일은 덮지 않음). 쓴 파일 경로를 반환.</summary>
    string SaveRecovery(SurveySession session);

    /// <summary>아직 처리하지 않은 복구본 이름(최신 순).</summary>
    IReadOnlyList<string> ListRecovery();

    /// <summary>복구본 한 건을 읽는다(없거나 읽지 못하면 null).</summary>
    SurveySession? LoadRecovery(string name);

    /// <summary>불러온 복구본을 <c>_recovery\loaded\</c> 으로 옮긴다(지우지 않음 · 다음 실행에 다시 묻지 않음).</summary>
    void SetRecoveryAside(string name);
}
