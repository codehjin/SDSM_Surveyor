namespace SDSM_Surveyor_App.Data;

/// <summary>같은 키 세션이 이미 있을 때의 선택(2026-10-20 결정 · 문제 1).</summary>
public enum SessionConflictChoice
{
    /// <summary>지금 화면에 자료가 있는 분류군만 새 값, 나머지 분류군은 기존 자료 유지(기본).</summary>
    Merge,
    /// <summary>기존 세션을 지금 화면으로 통째로 바꾼다.</summary>
    Replace,
    /// <summary>저장하지 않는다.</summary>
    Cancel,
}

/// <summary>
/// 저장하려는 키(대분류·연도차수·지점)를 다른 세션이 이미 쓰고 있다 — 새로 만든 화면([새 지점]·[지점 복사]·[차수 복제])이거나,
/// 열어 둔 세션의 지점·차수를 바꿔 다른 세션 키로 옮기는 경우다.
/// </summary>
/// <param name="SessionId">저장 키.</param>
/// <param name="ExistingTaxa">이미 있는 세션의 분류군(한글 이름 · 7개 분류군 순서).</param>
/// <param name="CurrentTaxa">지금 화면에 자료가 있는 분류군(한글 이름).</param>
/// <param name="ExistingUpdatedAt">이미 있는 세션을 마지막으로 저장한 때.</param>
/// <param name="MergeNotes">[합치기]를 고르면 생기는 일(N3) — '포유류 조사일 2025-09-06 유지' · '날씨 · 맑음 → 흐림' 등. 없으면 빈 목록.</param>
public sealed record SessionSaveConflict(string SessionId, string Display,
                                         IReadOnlyList<string> ExistingTaxa, IReadOnlyList<string> CurrentTaxa,
                                         DateTime ExistingUpdatedAt, IReadOnlyList<string>? MergeNotes = null);

/// <summary>
/// 불러온 세션의 대분류 · 연도차수 · 지점(세션 이름을 정하는 칸)이 바뀐 채 저장할 때의 선택(§32 · S4 · 작업보고 v052 8-4 #2).
/// 종전에는 묻지 않고 새 이름으로 저장한 뒤 원래 세션을 지웠다.
/// </summary>
public enum SessionRenameChoice
{
    /// <summary>지금 화면을 새 이름으로 저장하고 원래 세션은 그대로 둔다(기본).</summary>
    SaveAsNew,
    /// <summary>원래 세션을 지우고 새 이름으로 옮긴다.</summary>
    Replace,
    /// <summary>저장하지 않는다.</summary>
    Cancel,
}

/// <param name="OldId">원래 세션 키.</param>
/// <param name="OldDisplay">원래 세션(대분류 · 연도차수 · 지점).</param>
/// <param name="NewDisplay">새 이름(대분류 · 연도차수 · 지점).</param>
/// <param name="OldTaxa">원래 세션의 분류군(한글 이름).</param>
/// <param name="OldUpdatedAt">원래 세션을 마지막으로 저장한 때.</param>
public sealed record SessionRename(string OldId, string OldDisplay, string NewId, string NewDisplay,
                                   IReadOnlyList<string> OldTaxa, DateTime OldUpdatedAt);
