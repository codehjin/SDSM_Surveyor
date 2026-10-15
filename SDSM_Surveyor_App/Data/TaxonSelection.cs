using CommunityToolkit.Mvvm.ComponentModel;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 담당 분류군 하나(단일 분류군 모드). 첫 실행 화면과 셸이 같은 인스턴스를 본다.
///
/// ⚠ **표시 상태일 뿐이다.** 7개 <c>EntryViewModel</c> 싱글턴과
/// 세션 저장 단위(<c>대분류_연도차수_지점</c>)는 그대로다 — 분류군을 바꿔도
/// 세션 하나가 7개 분류군 자료를 계속 함께 담는다(README §상태).
/// 담당자가 조류로 바꿨다가 어류로 돌아와도 어류 입력이 남아 있어야 한다.
/// </summary>
public partial class TaxonSelection : ObservableObject, ISingletonService
{
    private readonly ISettingsStore _store;

    public TaxonSelection(ISettingsStore store)
    {
        _store = store;

        var remembered = TaxonCatalog.ByKey(store.Settings.SelectedTaxon);
        // 생성자에서는 필드에 직접 넣는다 — 세터를 타면 아직 고르지도 않은 값이 설정에 저장된다.
        _current = remembered?.Kind ?? TaxonKind.Fish;
        _rememberOnStartup = store.Settings.RememberTaxonOnStartup;
        SkipPicker = remembered is not null && store.Settings.RememberTaxonOnStartup;
    }

    /// <summary>담당 분류군.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Info))]
    [NotifyPropertyChangedFor(nameof(Name))]
    [NotifyPropertyChangedFor(nameof(DotOnShellBrushKey))]
    private TaxonKind _current;

    /// <summary>다음 실행에도 이 분류군으로 시작할지.</summary>
    [ObservableProperty] private bool _rememberOnStartup;

    // 팝오버 체크박스는 누르는 즉시 설정에 반영한다(따로 저장 버튼이 없다).
    // ⚠ 생성자는 필드에 직접 넣으므로 이 훅을 타지 않는다.
    partial void OnRememberOnStartupChanged(bool value) => Commit();

    public TaxonInfo Info => TaxonCatalog.Of(Current);
    public string Name => Info.Name;

    /// <summary>캡션 칩 색점용 브러시 키(딥네이비 위 변형).</summary>
    public string DotOnShellBrushKey => Info.DotOnShellBrushKey;

    /// <summary>기억된 분류군이 있어 첫 실행 화면을 건너뛸 수 있는지(앱 시작 시점 판단값).</summary>
    public bool SkipPicker { get; }

    /// <summary>현재 선택을 설정 파일에 저장한다.</summary>
    public void Commit()
    {
        _store.Settings.SelectedTaxon = Info.Key;
        _store.Settings.RememberTaxonOnStartup = RememberOnStartup;
        _store.Save();
    }
}
