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

    /// <summary>
    /// 새 지점으로 시작(C-2) — 7개 분류군 입력을 비우고, 지점(<paramref name="site"/> 가 null 이면 지점·하천·좌표·권역을 비움)을 바꾼다.
    /// 그 밖의 조사개황(압정 6칸 포함)은 그대로. 저장 전 새 세션이 된다. 저장 확인(B-5)은 부르는 쪽이 먼저 한다.
    /// </summary>
    void StartNewSite(SurveySite? site);

    /// <summary>7개 분류군 중 입력한 자료가 하나라도 있는가(C-2 지점 직접 변경 확인).</summary>
    bool HasInputData();

    /// <summary>
    /// 지금 화면(조사개황·분류군 자료·열린 세션·저장 안 한 변경 기준)을 보존한 채 일을 하고 끝나면 되돌린다(C-8 일괄 출력).
    /// </summary>
    Task<T> PreserveScreenAsync<T>(Func<Task<T>> work);

    /// <summary>이전 차수 복사(종 목록까지 복제 후 연도차수 교체).</summary>
    Task<bool> CopyFromPreviousAsync(string sessionId, string newYearChsu, DateTime? newDate);

    /// <summary>구버전 임시저장 파일을 세션으로 편입(최초 1회).</summary>
    Task<int> MigrateLegacyDraftsAsync();

    /// <summary>자료함이 비어 있으면 테스트 배포본의 예시 세션을 채운다. 채운 건수 반환.</summary>
    Task<int> SeedTestDataIfEmptyAsync();

    /// <summary>지금 화면을 '저장된 상태' 로 기록한다(앱 시작 직후 창이 부른다 · B-5).</summary>
    void MarkClean();

    /// <summary>마지막 저장·불러오기·복사 이후 저장될 내용이 바뀌었는가(B-5).</summary>
    bool HasUnsavedChanges();

    /// <summary>저장 키를 만드는 칸(대분류·연도차수·지점) 중 비어 있는 것.</summary>
    IReadOnlyList<string> MissingKeyFields();
}
