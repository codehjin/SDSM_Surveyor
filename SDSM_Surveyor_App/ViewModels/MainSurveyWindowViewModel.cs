using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>분류군 재선택 팝오버의 항목 한 줄.</summary>
public partial class TaxonMenuItemViewModel : ObservableObject
{
    public TaxonMenuItemViewModel(TaxonInfo info) => Info = info;

    public TaxonInfo Info { get; }
    public string Name => Info.Name;

    /// <summary>팝오버는 흰 배경이므로 기본 7색을 쓴다(셸 변형이 아니다).</summary>
    public string DotBrushKey => Info.DotBrushKey;

    /// <summary>「8종 입력됨」 / 미입력은 `·`.</summary>
    [ObservableProperty] private string _countText = "·";

    /// <summary>현재 담당 분류군(배경·글자색·굵기로 표시).</summary>
    [ObservableProperty] private bool _isCurrent;
}

/// <summary>
/// 메인 창(셸) ViewModel.
///
/// 단일 분류군 모드에서 셸이 지는 것 :
///  · 캡션 바(앱 마크 · 앱 이름 · 버전 · 분류군 칩 · 세션 요약 · 자료함/동기화)
///  · 조사개황 요약 카드(48px) — <see cref="Meta"/> 는 싱글턴이라 내용이 원래 하나다
///  · 분류군 재선택 팝오버
///
/// ⚠ 상단 분류군 탭 행(44px)은 더 이상 표시하지 않는다. 재선택은 캡션 칩에서 한다.
/// ⚠ 7개 EntryViewModel 싱글턴과 세션 저장 단위는 그대로다 — 바뀌는 것은 '보이는 화면'뿐이다.
/// </summary>
public partial class MainSurveyWindowViewModel : ObservableObject, ISingletonService
{
    private readonly IServiceProvider _provider;
    private ITaxonSession[]? _taxaCache;

    public MainSurveyWindowViewModel(IServiceProvider provider, TaxonSelection taxon, SurveyMeta meta)
    {
        _provider = provider;
        Taxon = taxon;
        Meta = meta;

        TaxonMenu = TaxonCatalog.All.Select(t => new TaxonMenuItemViewModel(t)).ToList();

        // 캡션 바 세션 요약은 조사개황을 그대로 따라간다.
        Meta.PropertyChanged += OnMetaChanged;
        Taxon.PropertyChanged += OnTaxonChanged;
        RefreshCurrentMark();
        Meta.ApplyTaxonForm(Taxon.Current);   // 하천유형 선택지 = 지금 분류군 양식(D7)
    }

    /// <summary>담당 분류군(표시 상태). 7개 VM 싱글턴은 그대로다.</summary>
    public TaxonSelection Taxon { get; }

    /// <summary>공통 조사개황 — 셸이 한 번만 소유한다(design.md §5.1).</summary>
    public SurveyMeta Meta { get; }

    public IReadOnlyList<TaxonMenuItemViewModel> TaxonMenu { get; }

    /// <summary>앱 이름. **한글만 표기한다** — 영문명·삼성 로고·브랜드 남색은 쓰지 않는다.</summary>
    [ObservableProperty] private string _windowTitle = Helpers.AppInfo.ProgramName;

    [ObservableProperty] private string _version = Helpers.AppInfo.VersionText;   // 판 표시 한 곳(v0.5.0)

    /// <summary>분류군 재선택 팝오버가 열려 있는지(칩 테두리·배경이 이 값을 따라간다).</summary>
    [ObservableProperty] private bool _isTaxonPopupOpen;

    /// <summary>
    /// 업로드 대기 건수. 업로드 큐는 아직 없다(design.md §11.3 — 🚧 부분 구현).
    /// 값이 0 이면 숫자를 지어내지 않고 「동기화」로만 표기한다.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SyncPillText))]
    private int _pendingUploadCount;

    public string SyncPillText => PendingUploadCount > 0 ? $"대기 {PendingUploadCount}" : "동기화";

    /// <summary>캡션 바 세션 요약 — `방류하천 · 곡교천1 (St.1) · 2026반기1차`.</summary>
    public string SessionSummary => Meta.ShellSessionSummary;

    /// <summary>칩 안 보조 문구.</summary>
    public string TaxonChipCaption => "담당 분류군 · 변경";

    // ── 팝오버 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 칩을 누르면 팝오버를 연다. 열 때마다 분류군별 입력량을 다시 센다 —
    /// 자료가 어디에 얼마나 있는지 보고 나서 바꾸게 하려는 것이다.
    /// </summary>
    [RelayCommand]
    private void ToggleTaxonPopup()
    {
        if (!IsTaxonPopupOpen) RefreshCounts();
        IsTaxonPopupOpen = !IsTaxonPopupOpen;
    }

    /// <summary>
    /// 담당 분류군을 바꾼다. **입력 중인 자료는 그대로 유지된다** —
    /// 세션 하나가 7개 분류군을 함께 담는 기존 구조를 건드리지 않기 때문이다.
    /// </summary>
    [RelayCommand]
    private void PickTaxon(TaxonMenuItemViewModel? item)
    {
        if (item is null) return;
        Taxon.Current = item.Info.Kind;
        Taxon.Commit();
        IsTaxonPopupOpen = false;
    }

    /// <summary>팝오버 안의 「다음 실행에도 이 분류군으로 시작」.</summary>
    [RelayCommand]
    private void ToggleRememberTaxon()
    {
        Taxon.RememberOnStartup = !Taxon.RememberOnStartup;
        Taxon.Commit();
    }

    // ── 분류군별 입력량 ───────────────────────────────────────────────────────

    /// <summary>
    /// 7개 분류군 VM. <see cref="SessionService"/> 와 같은 방식으로 <b>늦게</b> 가져온다 —
    /// 생성자 주입은 순환 의존이 되어 앱이 아예 시작되지 않는다(CLAUDE.md §2).
    /// </summary>
    private ITaxonSession[] Taxa => _taxaCache ??= new ITaxonSession[]
    {
        _provider.GetRequiredService<FishEntryViewModel>(),
        _provider.GetRequiredService<BenthosEntryViewModel>(),
        _provider.GetRequiredService<BirdEntryViewModel>(),
        _provider.GetRequiredService<MammalEntryViewModel>(),
        _provider.GetRequiredService<AmphibianReptileEntryViewModel>(),
        _provider.GetRequiredService<HabitatWaterEdgeEntryViewModel>(),
        _provider.GetRequiredService<WaterQualityEntryViewModel>(),
    };

    private void RefreshCounts()
    {
        foreach (var item in TaxonMenu)
        {
            var session = Taxa.FirstOrDefault(t => t.Key == item.Info.Key);
            var n = session?.EntryCount ?? 0;
            // 미입력은 `·` — 0 을 적으면 '조사해서 0' 과 구분되지 않는다(CLAUDE.md §4).
            item.CountText = n > 0 ? $"{n}{item.Info.CountUnit} 입력됨" : "·";
        }
    }

    private void RefreshCurrentMark()
    {
        foreach (var item in TaxonMenu)
            item.IsCurrent = item.Info.Kind == Taxon.Current;
    }

    private void OnTaxonChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TaxonSelection.Current))
        {
            RefreshCurrentMark();
            Meta.ApplyTaxonForm(Taxon.Current);   // 분류군 양식이 바뀌면 하천유형 선택지도(D7)
        }
    }

    private void OnMetaChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SurveyMeta.ShellSessionSummary) or null)
            OnPropertyChanged(nameof(SessionSummary));
    }
}
