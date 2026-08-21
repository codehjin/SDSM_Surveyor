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
        WeakReferenceMessenger.Default.Send(new NotifyMessage(("자료함에 저장되었습니다.", true)));
    }
}
