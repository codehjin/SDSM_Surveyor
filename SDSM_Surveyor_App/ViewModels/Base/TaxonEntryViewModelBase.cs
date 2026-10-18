using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.ViewModels.Base;

/// <summary>
/// 7개 분류군 입력 ViewModel 의 공통 기반 (05_REFACTORING §1-1).
///
/// 여기 두는 것은 **분류군과 무관하게 똑같은 것**뿐이다.
///  · 공통 조사개황(<see cref="Meta"/>) — 7개 탭이 같은 인스턴스를 본다
///  · 임시저장 상태(<see cref="StatusText"/>·<see cref="LastSavedTime"/>)
///  · [임시 저장] — 분류군 하나가 아니라 **세션 전체**를 저장한다
///
/// 분류군 고유 필드·계산은 파생 클래스에 남긴다.
/// </summary>
public abstract partial class TaxonEntryViewModelBase : ObservableObject
{
    private readonly ISessionService _sessions;

    protected TaxonEntryViewModelBase(ISessionService sessions, SurveyMeta meta)
    {
        _sessions = sessions;
        Meta = meta;
        lock (Instances) Instances.Add(new WeakReference<TaxonEntryViewModelBase>(this));
    }

    // ── 저장·내보내기 때 함께 알릴 것(2026-10-18 결정 D3·D5~D7) ─────────────────────
    // 7개 분류군 VM 은 싱글턴이다. [임시 저장]은 세션 전체를 저장하므로 모든 분류군의 알림을 모은다.
    private static readonly List<WeakReference<TaxonEntryViewModelBase>> Instances = new();

    /// <summary>알림에 쓰는 분류군 이름(관찰형만 쓴다).</summary>
    protected virtual string NoticeTaxon => string.Empty;

    /// <summary>개체수·흔적이 빈 관찰 행 수(D3 · 관찰형만).</summary>
    public virtual int EmptyObservationRows => 0;

    /// <summary>양식에 없는 값 알림(D5·D6 · 예전 세션 호환). 없으면 null.</summary>
    public virtual string? OutOfFormNotice() => null;

    /// <summary>이 분류군 양식에 조사개황 하천유형이 있는가(D7 · 어류·저서·수질·서식수변).</summary>
    protected virtual bool UsesRiverType => false;

    private static string EmptyRowsText(IEnumerable<TaxonEntryViewModelBase> vms)
    {
        var parts = vms.Where(v => v.EmptyObservationRows > 0).Select(v => $"{v.NoticeTaxon} {v.EmptyObservationRows}").ToList();
        return parts.Count == 0 ? string.Empty
            : $"개체수·흔적이 빈 행 {vms.Sum(v => v.EmptyObservationRows)}개({string.Join(" · ", parts)}) — 빈칸 그대로 저장·내보냄";
    }

    private string Compose(string head, IReadOnlyList<TaxonEntryViewModelBase> vms, bool riverType)
    {
        var notes = new List<string>();
        var empty = EmptyRowsText(vms);
        if (empty.Length > 0) notes.Add(empty);
        if (riverType && Meta.RiverTypeNotice is string r) notes.Add(r);
        notes.AddRange(vms.Select(v => v.OutOfFormNotice()).Where(s => !string.IsNullOrEmpty(s))!);
        return notes.Count == 0 ? head : $"{head} · 확인: {string.Join(" · ", notes)}";
    }

    /// <summary>[임시 저장] 알림 글자 — 모든 분류군의 빈 행·양식에 없는 값(경고만 · 막지 않는다). 하네스가 읽는다.</summary>
    public string SaveNoticeText()
    {
        List<TaxonEntryViewModelBase> all;
        lock (Instances) all = Instances.Select(w => w.TryGetTarget(out var v) ? v : null).Where(v => v is not null).Distinct().ToList()!;
        return Compose("자료함에 저장되었습니다.", all, riverType: true);
    }

    /// <summary>내보내기 알림 — 이 분류군의 빈 행·양식에 없는 값을 함께(경고만).</summary>
    protected void NotifyExported(string head)
    {
        var text = Compose(head, new[] { this }, UsesRiverType);
        WeakReferenceMessenger.Default.Send(new NotifyMessage((text, text == head)));
    }

    /// <summary>공통 조사개황 : 모든 분류군 공유(SurveyOverviewControl에서 입력).</summary>
    public SurveyMeta Meta { get; }

    [ObservableProperty] private string _statusText = "임시 저장 없음";
    [ObservableProperty] private DateTime? _lastSavedTime;

    /// <summary>
    /// 오프라인 저장. 분류군 하나가 아니라 세션(조사개황 + 7개 분류군) 전체를 저장한다.
    /// 어느 탭에서 눌러도 같은 세션 파일이 갱신되므로 지점을 옮겨도 이전 자료가 사라지지 않는다.
    /// </summary>
    [RelayCommand]
    private async Task SaveTemporary()
    {
        var idx = await _sessions.SaveCurrentAsync();

        LastSavedTime = DateTime.Now;
        StatusText = $"자료함 저장됨 · {idx.Site} {idx.YearChsu} · {LastSavedTime:HH:mm:ss}";
        var notice = SaveNoticeText();   // 개체수·흔적이 빈 행 · 양식에 없는 값 — 알리기만 한다(D3·D5~D7)
        WeakReferenceMessenger.Default.Send(new NotifyMessage((notice, notice == "자료함에 저장되었습니다.")));
    }
}
