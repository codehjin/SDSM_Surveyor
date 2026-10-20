using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Models;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>
/// 자료함 — 저장된 조사 세션 목록을 보고 불러오기·삭제·복제한다.
/// (04_FEATURE_SITE_SESSION §B-3-2·§B-3-3)
/// </summary>
public partial class SessionBrowserViewModel : ObservableObject, ITransientService
{
    private readonly ISessionService _sessions;
    private readonly SurveyMeta _meta;

    private List<SessionIndexEntry> _all = new();

    private readonly SessionArchive _archive;

    public SessionBrowserViewModel(ISessionService sessions, SurveyMeta meta, SessionArchive archive)
    {
        _sessions = sessions;
        _meta = meta;
        _archive = archive;
        SiteOptions = meta.SiteOptions;
    }

    // ── 여러 세션 고르기(C-9 내보내기 · C-8 일괄 출력) ─────────────────────────────

    /// <summary>목록에서 고른 세션들(Ctrl·Shift 로 여럿). View 가 그리드 선택이 바뀔 때 넣는다.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportSessionsCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchBulkCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchReportCommand))]
    [NotifyPropertyChangedFor(nameof(SelectionText))]
    private IReadOnlyList<SessionIndexEntry> _selectedSessions = Array.Empty<SessionIndexEntry>();

    /// <summary>고른 세션 수 안내 — `3건 고름`.</summary>
    public string SelectionText => SelectedSessions.Count <= 1 ? string.Empty : $"{SelectedSessions.Count}건 고름";

    // View 가 붙이는 대화상자(파일 고르기·충돌 확인·결과 요약). null 이면 그 동작을 하지 않는다.
    public Func<int, string?>? PickExportPath { get; set; }
    public Func<string?>? PickImportPath { get; set; }
    public Func<SurveySession, SessionArchive.Conflict>? AskConflict { get; set; }
    public Action<string, string>? ShowSummary { get; set; }

    private bool CanExport() => SelectedSessions.Count > 0;

    // ── C-8 : 여러 지점 일괄 출력 ────────────────────────────────────────────────
    public Func<string?>? PickFolder { get; set; }
    /// <summary>보고서 일괄 파일 고르기(기본 이름을 받는다).</summary>
    public Func<string, string?>? PickReportPath { get; set; }
    /// <summary>보고서 일괄 결과 안내(파일 · 본문). View 가 [파일 열기]·[폴더 열기]를 붙인다.</summary>
    public Action<string, string>? ShowReportResult { get; set; }

    /// <summary>
    /// [보고서 일괄](C-8 · 사용자 선택 C안) — 고른 세션의 보고서를 모든 분류군을 담은 통합문서 하나로 만든다.
    /// 시트 = 분류군 × 시트 종류, 표 시트는 지점 행을 이어 붙이고 조사개황은 항목 × 지점 가로 표.
    /// 시작 전 저장 확인(B-5) · 끝나면 보던 화면을 그대로 되돌린다(일괄입력용 일괄과 같다).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task BatchReport()
    {
        var entries = SelectedSessions.ToList();
        if (entries.Count == 0) return;
        if (ConfirmUnsaved is not null
            && !await ConfirmUnsaved("일괄 출력은 고른 세션을 차례로 불러와 파일을 만듭니다. 끝나면 지금 화면으로 되돌립니다.")) return;

        var path = PickReportPath?.Invoke(Export.BatchExporter.ReportFileName(entries));
        if (string.IsNullOrEmpty(path)) return;

        var previousCursor = System.Windows.Input.Mouse.OverrideCursor;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        Export.BatchExporter.Result r;
        try
        {
            r = await _sessions.PreserveScreenAsync(() => Export.BatchExporter.WriteReportAsync(_sessions, entries, path));
        }
        catch (Exception ex)
        {
            System.Windows.Input.Mouse.OverrideCursor = previousCursor;
            StatusText = $"보고서를 만들지 못했습니다 — {ex.Message}";
            ShowSummary?.Invoke("보고서 일괄 실패", $"{path}\n\n{ex.Message}\n같은 이름 파일이 엑셀에 열려 있는지 확인해 주세요.");
            return;
        }
        finally { System.Windows.Input.Mouse.OverrideCursor = previousCursor; }

        var taxa = r.Written.Select(w => w.Split(" · ")[0]).Distinct().ToList();
        var lines = new List<string> { $"세션 {r.Sessions}건 · 분류군 {taxa.Count}개({string.Join(", ", taxa)})를 통합문서 하나에 담았습니다." };
        if (r.Failed.Count > 0) lines.Add("빠진 것: " + string.Join(" / ", r.Failed));
        lines.Add("지금 화면은 일괄 출력 전 그대로입니다.");
        StatusText = lines[0];
        ShowReportResult?.Invoke(path, string.Join("\n", lines));
    }
    /// <summary>이미 있는 파일 목록을 보여 주고 덮어쓸지 묻는다. false 면 멈춘다.</summary>
    public Func<IReadOnlyList<string>, bool>? ConfirmOverwriteFiles { get; set; }
    /// <summary>결과 안내(폴더 · 본문). View 가 [폴더 열기]를 붙인다.</summary>
    public Action<string, string>? ShowBatchResult { get; set; }
    /// <summary>일괄입력용 일괄에서 빠지는 종(개체수 0·빈칸) 요약을 한 번 묻는다(문제 5). false 면 멈춘다.</summary>
    public Func<string, bool>? ConfirmSkippedSpecies { get; set; }

    /// <summary>
    /// [일괄입력용 일괄](C-8) — 고른 세션 × 자료가 있는 분류군의 일괄입력용 파일을 고른 폴더에 만든다.
    /// 세션을 차례로 화면에 불러와 쓰므로 시작 전에 저장 확인(B-5)을 거치고, 끝나면 보던 화면을 그대로 되돌린다.
    /// 파일명에 연도차수가 들어가므로(문제 14) 하위 폴더로 나누지 않는다. 개체수 0·빈칸 종이 빠지면 요약을 한 번 묻고(문제 5),
    /// 관리자 일괄등록이 막을 어류(하천차수 빈칸 등)는 그 파일만 만들지 않고 까닭을 알린다(문제 7·8).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task BatchBulk()
    {
        var entries = SelectedSessions.ToList();
        if (entries.Count == 0) return;
        if (ConfirmUnsaved is not null
            && !await ConfirmUnsaved("일괄 출력은 고른 세션을 차례로 불러와 파일을 만듭니다. 끝나면 지금 화면으로 되돌립니다.")) return;

        var folder = PickFolder?.Invoke();
        if (string.IsNullOrEmpty(folder)) return;

        var plan = Export.BatchExporter.PlanBulk(entries);
        var exists = plan.Select(p => p.RelativePath).Where(r => System.IO.File.Exists(System.IO.Path.Combine(folder, r))).ToList();
        if (exists.Count > 0 && ConfirmOverwriteFiles is not null && !ConfirmOverwriteFiles(exists)) return;

        var previousCursor = System.Windows.Input.Mouse.OverrideCursor;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        Export.BatchExporter.Result r;
        try
        {
            r = await _sessions.PreserveScreenAsync(() => Export.BatchExporter.WriteBulkAsync(_sessions, plan, folder, ConfirmSkippedSpecies));
        }
        finally { System.Windows.Input.Mouse.OverrideCursor = previousCursor; }
        if (r.Cancelled) { StatusText = "일괄입력용 일괄을 멈췄습니다(내보내지 않는 종 확인에서 [취소])."; return; }

        var lines = new List<string> { $"세션 {r.Sessions}건 · 파일 {r.Written.Count}개" + (r.Blocked.Count > 0 ? $" · 만들지 않음 {r.Blocked.Count}개" : "") + (r.Failed.Count > 0 ? $" · 실패 {r.Failed.Count}개" : "") };
        if (r.Blocked.Count > 0) lines.Add("만들지 않은 파일(관리자 일괄등록이 막는 값): " + string.Join(" / ", r.Blocked));
        if (r.Skipped.Count > 0) lines.Add("내보내지 않은 종(개체수 0·빈칸): " + string.Join(" / ", r.Skipped));
        if (r.Notes.Count > 0) lines.Add("안내: " + string.Join(" / ", r.Notes));
        if (r.Failed.Count > 0) lines.Add("실패: " + string.Join(" / ", r.Failed));
        lines.Add("지금 화면은 일괄 출력 전 그대로입니다.");
        StatusText = lines[0];
        ShowBatchResult?.Invoke(folder, string.Join("\n", lines));
    }

    /// <summary>[내보내기](C-9) — 고른 세션을 zip 하나로. 다른 PC·다른 조사자에게 넘길 때 쓴다.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportSessions()
    {
        var ids = SelectedSessions.Select(e => e.SessionId).ToList();
        var path = PickExportPath?.Invoke(ids.Count);
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var n = await _archive.ExportAsync(ids, path);
            Notify($"세션 {n}건을 내보냈습니다 — {System.IO.Path.GetFileName(path)}");
            ShowSummary?.Invoke("내보내기 완료", $"{path}\n\n세션 {n}건을 담았습니다. 이 파일을 받은 사람은 자료함 [가져오기]로 넣을 수 있습니다.");
        }
        catch (Exception ex)
        {
            StatusText = $"내보내지 못했습니다 — {ex.Message}";
            ShowSummary?.Invoke("내보내기 실패", $"{path}\n\n{ex.Message}\n같은 이름 파일이 열려 있는지, 저장 위치가 살아 있는지 확인해 주세요.");
        }
    }

    /// <summary>
    /// [가져오기](C-9) — zip 을 검사한 뒤 세션을 더한다. 같은 세션 키(대분류·연도차수·지점)가 있으면 묻는다(기본 건너뜀).
    /// 깨진 zip·JSON 은 그 건만 건너뛰고 결과를 요약한다. 지금 화면은 바꾸지 않는다(필요하면 [불러오기]).
    /// </summary>
    [RelayCommand]
    private async Task ImportSessions()
    {
        var path = PickImportPath?.Invoke();
        if (string.IsNullOrEmpty(path)) return;
        var r = await _archive.ImportAsync(path, AskConflict);
        await RefreshAsync();

        var text = r.Summary;
        var current = _sessions.CurrentSessionId;
        if (current is not null && r.Overwritten.Count > 0
            && (await _sessions.ListAsync()).Any(e => e.SessionId == current && r.Overwritten.Contains($"{e.Site ?? "(지점 없음)"} · {e.YearChsu ?? "(차수 없음)"}")))
            text += "\n\n⚠ 지금 화면에 열려 있는 세션을 덮어썼습니다. 가져온 내용을 보려면 그 세션을 다시 [불러오기] 하세요.";
        StatusText = r.Summary.Split('\n')[0];
        ShowSummary?.Invoke("가져오기 결과", text);
    }

    /// <summary>
    /// 지점 복사 콤보 항목 — 조사개황 콤보와 같은 정렬·표기(C-7 · St.번호는 믿을 수 있는 하천만).
    /// </summary>
    public List<SurveyMeta.SiteChoice> SiteChoices => _meta.ToChoices(SiteOptions);

    /// <summary>지점 복사 콤보의 선택(= <see cref="CopyTargetSite"/>).</summary>
    public SurveyMeta.SiteChoice? CopyTargetChoice
    {
        get => CopyTargetSite is null ? null : SiteChoices.FirstOrDefault(c => ReferenceEquals(c.Site, CopyTargetSite));
        set => CopyTargetSite = value?.Site;
    }

    partial void OnSiteOptionsChanged(List<SurveySite> value) => OnPropertyChanged(nameof(SiteChoices));

    /// <summary>불러오기가 끝나 창을 닫아야 할 때 발생.</summary>
    public event EventHandler? LoadCompleted;

    public RadObservableCollection<SessionIndexEntry> Sessions { get; } = new();

    /// <summary>지점 간 값 복사 대상 후보(현재 대분류의 등록 지점).</summary>
    [ObservableProperty] private List<SurveySite> _siteOptions = new();

    [ObservableProperty] private SessionIndexEntry? _selected;

    /// <summary>지점·하천·연도차수 부분일치 검색.</summary>
    [ObservableProperty] private string? _search;

    /// <summary>지점 간 값 복사에서 옮겨 갈 지점.</summary>
    [ObservableProperty] private SurveySite? _copyTargetSite;

    /// <summary>이전 차수 복사에서 새로 매길 연도차수.</summary>
    [ObservableProperty] private string? _newYearChsu;

    [ObservableProperty] private string _statusText = "";

    partial void OnSearchChanged(string? value) => ApplyFilter();

    partial void OnSelectedChanged(SessionIndexEntry? value)
    {
        LoadSessionCommand.NotifyCanExecuteChanged();
        DeleteSessionCommand.NotifyCanExecuteChanged();
        CopyFromPreviousCommand.NotifyCanExecuteChanged();
    }

    partial void OnCopyTargetSiteChanged(SurveySite? value)
    {
        CopyToSiteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CopyTargetChoice));
    }
    partial void OnNewYearChsuChanged(string? value) => CopyFromPreviousCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        _all = await _sessions.ListAsync();
        SiteOptions = _meta.SiteOptions;
        ApplyFilter();
        StatusText = _all.Count == 0
            ? "저장된 조사 세션이 없습니다. 각 탭에서 [임시 저장]을 누르면 여기에 쌓입니다."
            : $"조사 세션 {_all.Count}건";
    }

    private void ApplyFilter()
    {
        var q = Search?.Trim();
        var view = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(e =>
                  (e.Site ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                  (e.River ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                  (e.YearChsu ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                  (e.Project ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                  (e.Workplace ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        Sessions.SuspendNotifications();
        Sessions.Clear();
        foreach (var e in view) Sessions.Add(e);
        Sessions.ResumeNotifications();
    }

    private bool HasSelection() => Selected is not null;

    /// <summary>
    /// 저장 안 한 변경 확인(View 가 붙인다 · B-5). 인자는 질문 아래 붙는 한 줄.
    /// false 를 돌려주면 동작을 멈춘다. null 이면 묻지 않는다.
    /// </summary>
    public Func<string, Task<bool>>? ConfirmUnsaved { get; set; }

    /// <summary>
    /// 지금 화면을 덮어쓰기 전에 묻는다. [예]로 저장했으면 목록을 새로 읽는다 —
    /// 그러면 선택이 풀리므로 고르고 있던 세션을 다시 고른다.
    /// </summary>
    private async Task<bool> ConfirmOverwriteAsync(string consequence)
    {
        if (ConfirmUnsaved is null) return true;
        var keep = Selected?.SessionId;
        if (!await ConfirmUnsaved(consequence)) return false;
        await RefreshAsync();
        if (keep is not null) Selected = Sessions.FirstOrDefault(e => e.SessionId == keep);
        return true;
    }

    /// <summary>선택 세션을 7개 탭에 복원한다.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task LoadSession()
    {
        if (Selected is null) return;
        if (!await ConfirmOverwriteAsync("불러오면 지금 화면이 이 세션으로 바뀝니다.")) return;
        if (Selected is null) return;
        var ok = await _sessions.LoadAsync(Selected.SessionId);
        if (!ok) { StatusText = "세션 파일을 찾지 못했습니다."; return; }

        Notify($"{Selected.Site} · {Selected.YearChsu} 자료를 불러왔습니다.");
        LoadCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>현재 화면 전체를 세션으로 저장한다(자료함에서도 저장할 수 있게).</summary>
    [RelayCommand]
    private async Task SaveCurrent()
    {
        var idx = await _sessions.SaveCurrentAsync();
        if (idx is null) { Notify("저장하지 않았습니다(같은 자료가 있어 취소)."); return; }
        await RefreshAsync();
        Selected = Sessions.FirstOrDefault(e => e.SessionId == idx.SessionId);
        Notify(_sessions.LastSaveNotice ?? "현재 화면을 세션으로 저장했습니다.");
    }

    /// <summary>선택 세션을 지운다. 확인은 View에서 받는다(<see cref="ConfirmDelete"/>).</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSession()
    {
        if (Selected is null) return;
        if (ConfirmDelete is not null && !ConfirmDelete(Selected)) return;

        await _sessions.DeleteAsync(Selected.SessionId);
        await RefreshAsync();
        Notify("세션을 삭제했습니다.");
    }

    /// <summary>삭제 확인 대화상자(View가 붙인다). null이면 확인 없이 지운다.</summary>
    public Func<SessionIndexEntry, bool>? ConfirmDelete { get; set; }

    private bool CanCopyToSite() => CopyTargetSite is not null;

    /// <summary>
    /// 지점 간 값 복사 — 현재 화면의 조사개황을 유지한 채 지점만 바꾸고 종 목록은 비운다.
    /// 같은 날 여러 지점을 도는 현장 흐름에 맞춘 기능이다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyToSite))]
    private async Task CopyToSite()
    {
        if (CopyTargetSite is null) return;
        var site = CopyTargetSite;
        if (!await ConfirmOverwriteAsync("지점 복사를 하면 지금 화면의 종 목록이 비워집니다.")) return;
        CopyTargetSite = site;   // 목록을 새로 읽어도 옮길 지점은 그대로
        _sessions.CopyToSite(CopyTargetSite);
        Notify($"{_meta.DisplayOf(CopyTargetSite)} 지점으로 조사개황을 복사했습니다(종 목록은 비움).");
        LoadCompleted?.Invoke(this, EventArgs.Empty);
    }

    private bool CanCopyFromPrevious() => Selected is not null && !string.IsNullOrWhiteSpace(NewYearChsu);

    /// <summary>이전 차수 복사 — 같은 지점의 지난 회차를 종 목록까지 복제한 뒤 연도차수만 바꾼다.</summary>
    [RelayCommand(CanExecute = nameof(CanCopyFromPrevious))]
    private async Task CopyFromPrevious()
    {
        if (Selected is null || string.IsNullOrWhiteSpace(NewYearChsu)) return;
        if (!await ConfirmOverwriteAsync("차수 복제를 하면 지금 화면이 고른 세션의 복제본으로 바뀝니다.")) return;
        if (Selected is null || string.IsNullOrWhiteSpace(NewYearChsu)) return;

        var ok = await _sessions.CopyFromPreviousAsync(Selected.SessionId, NewYearChsu.Trim(), DateTime.Today);
        if (!ok) { StatusText = "세션 파일을 찾지 못했습니다."; return; }

        Notify($"{Selected.Site} 의 {Selected.YearChsu} 자료를 {NewYearChsu.Trim()} 로 복제했습니다.");
        LoadCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void Notify(string text)
    {
        StatusText = text;
        WeakReferenceMessenger.Default.Send(new NotifyMessage((text, true)));
    }
}
