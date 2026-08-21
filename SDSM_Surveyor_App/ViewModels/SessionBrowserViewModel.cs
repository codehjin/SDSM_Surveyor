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

    public SessionBrowserViewModel(ISessionService sessions, SurveyMeta meta)
    {
        _sessions = sessions;
        _meta = meta;
        SiteOptions = meta.SiteOptions;
    }

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

    partial void OnCopyTargetSiteChanged(SurveySite? value) => CopyToSiteCommand.NotifyCanExecuteChanged();
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

    /// <summary>선택 세션을 7개 탭에 복원한다.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task LoadSession()
    {
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
        await RefreshAsync();
        Selected = Sessions.FirstOrDefault(e => e.SessionId == idx.SessionId);
        Notify("현재 화면을 세션으로 저장했습니다.");
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
    private void CopyToSite()
    {
        if (CopyTargetSite is null) return;
        _sessions.CopyToSite(CopyTargetSite);
        Notify($"{CopyTargetSite.Display} 지점으로 조사개황을 복사했습니다(종 목록은 비움).");
        LoadCompleted?.Invoke(this, EventArgs.Empty);
    }

    private bool CanCopyFromPrevious() => Selected is not null && !string.IsNullOrWhiteSpace(NewYearChsu);

    /// <summary>이전 차수 복사 — 같은 지점의 지난 회차를 종 목록까지 복제한 뒤 연도차수만 바꾼다.</summary>
    [RelayCommand(CanExecute = nameof(CanCopyFromPrevious))]
    private async Task CopyFromPrevious()
    {
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
