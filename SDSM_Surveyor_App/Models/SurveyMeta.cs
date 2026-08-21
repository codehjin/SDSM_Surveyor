using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Models;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Helpers;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 모든 분류군이 공유하는 "조사개황"(공통 식별·맥락 정보).
/// 각 분류군 ViewModel은 이 객체 하나를 <c>Meta</c> 로 가진다.
/// 형식·드롭다운 등 규칙 변경은 <c>SurveyOverviewControl</c> 한 곳에서 하면 전 분류군에 반영된다.
/// ※ 계산에 영향을 주는 값(하천차수·조사불가 사유 등)은 분류군 고유이므로 여기 두지 않는다.
/// </summary>
public partial class SurveyMeta : ObservableObject, ISingletonService
{
    /// <summary>DI 생성자. 7개 분류군이 이 인스턴스 하나를 공유한다(조사개황은 세션당 1벌).</summary>
    public SurveyMeta(ISiteListProvider sites) => UseSiteList(sites);


    private ISiteListProvider? _sites;
    private bool _applyingSite;   // 지점 자동 채움 중 재진입 방지

    [ObservableProperty] private string? _project;                        // 대분류(방류하천/생태현황) — 파일명·구분(SiteDivision)
    [ObservableProperty] private string? _surveyYear;                     // 연도
    [ObservableProperty] private string _yearChsu = string.Empty;         // 연도차수
    [ObservableProperty] private DateTime? _surveyDate = DateTime.Today;  // 조사일자
    [ObservableProperty] private string? _majorRegion;                    // 대권역명
    [ObservableProperty] private string? _middleRegion;                   // 중권역명
    [ObservableProperty] private string? _river;                          // 하천명(과업대표하천)
    [ObservableProperty] private string? _riverType;                      // 하천유형
    [ObservableProperty] private string? _workplace;                      // 사업장(기흥/화성/온양) — 내보내기 구분 컬럼
    [ObservableProperty] private string? _site;                           // 지점명(DB상 지점명)
    [ObservableProperty] private string? _lat;                            // 위도
    [ObservableProperty] private string? _lng;                            // 경도
    [ObservableProperty] private string? _weather;                        // 날씨
    [ObservableProperty] private string? _surveyAgency;                   // 조사기관
    [ObservableProperty] private string? _surveyor;                       // 조사자

    // 목록형 옵션(현재는 날씨만 목록). 대권역·하천유형 등을 드롭다운으로 바꿀 때
    // 여기(또는 별도 옵션 제공자)에 목록을 채우고 컨트롤을 콤보로 교체하면 전 분류군 일괄 반영.
    public string[] Weathers { get; } = { "맑음", "흐림", "비(눈)" };
    public string[] Projects { get; } = { "방류하천", "생태현황" };

    // ── 조사지점 : 등록된 지점만 고르게 한다(04_FEATURE_SITE_SESSION §A-2) ──────

    /// <summary>대분류로 거른 지점 목록(드롭다운 항목).</summary>
    [ObservableProperty] private List<SurveySite> _siteOptions = new();

    /// <summary>선택된 지점. 고르면 하천·사업장·좌표가 자동으로 채워진다.</summary>
    [ObservableProperty] private SurveySite? _selectedSite;

    /// <summary>지점 마스터를 연결한다(각 분류군 VM이 생성자에서 호출).</summary>
    public void UseSiteList(ISiteListProvider provider)
    {
        _sites = provider;
        RefreshSiteOptions();
    }

    /// <summary>지점 마스터 버전(상태 표시·검증용).</summary>
    public string? SiteCatalogVersion => _sites?.Version;

    private void RefreshSiteOptions()
    {
        if (_sites is null) return;
        SiteOptions = _sites.ByProject(Project);

        // 대분류가 바뀌어 현재 지점이 목록에서 빠지면 선택을 비운다(잘못된 조합 방지)
        if (SelectedSite is not null && !SiteOptions.Contains(SelectedSite))
            SelectedSite = null;
    }

    partial void OnProjectChanged(string? value) => RefreshSiteOptions();

    partial void OnSelectedSiteChanged(SurveySite? value)
    {
        if (value is null || _applyingSite) return;
        _applyingSite = true;
        try
        {
            Site = value.SiteName;                       // ST 번호로 골랐어도 DB상 지점명으로 저장
            if (!string.IsNullOrWhiteSpace(value.River)) River = value.River;
            if (!string.IsNullOrWhiteSpace(value.Workplace)) Workplace = value.Workplace;
            if (value.Lat is double la) Lat = la.ToString("0.######");
            if (value.Lng is double ln) Lng = ln.ToString("0.######");
            OnPropertyChanged(nameof(SiteDisplay));
            OnPropertyChanged(nameof(HasMap));
            RaiseSiteFlags();
        }
        finally { _applyingSite = false; }
    }

    /// <summary>
    /// 조사자가 친 값(`ST1`·`St.1`·`곡교천1`·옛 보고서 표기)을 지점으로 해석해 선택한다.
    /// 편집형 콤보의 텍스트 변경에서 호출한다. 못 찾으면 아무것도 바꾸지 않는다
    /// (자유 입력을 지우면 오타를 못 알아채므로, 화면에 미등록 표시만 남긴다).
    /// </summary>
    public void ResolveSiteText(string? text)
    {
        if (_sites is null || string.IsNullOrWhiteSpace(text)) return;
        if (_applyingSite) return;

        var hit = _sites.Resolve(text, Project);
        if (hit is not null && !ReferenceEquals(hit, SelectedSite))
            SelectedSite = hit;
        else
            RaiseSiteFlags();
    }

    /// <summary>화면 표기 — `곡교천1 (St.1)`. 조사자가 번호를 함께 인지하도록.</summary>
    public string SiteDisplay => SelectedSite?.Display ?? Site ?? string.Empty;

    /// <summary>지점 마스터에 등록된 지점인지(미등록이면 화면에 경고를 띄운다).</summary>
    public bool IsSiteRegistered => SelectedSite is not null || string.IsNullOrWhiteSpace(Site);

    /// <summary>미등록 지점 경고 표시용(BoolToVisibility 재사용).</summary>
    public bool IsSiteUnregistered => !IsSiteRegistered;

    /// <summary>선택 지점의 위치 설명(예: 배방야구장). 없으면 빈 문자열.</summary>
    public string SiteDesc => SelectedSite?.Desc ?? string.Empty;

    partial void OnSiteChanged(string? value)
    {
        OnPropertyChanged(nameof(SiteDisplay));
        OnPropertyChanged(nameof(IsSiteRegistered));
        OnPropertyChanged(nameof(IsSiteUnregistered));
        OnPropertyChanged(nameof(SiteDesc));
    }

    private void RaiseSiteFlags()
    {
        OnPropertyChanged(nameof(IsSiteRegistered));
        OnPropertyChanged(nameof(IsSiteUnregistered));
        OnPropertyChanged(nameof(SiteDesc));
    }

    /// <summary>좌표가 있어 지도를 열 수 있는지.</summary>
    public bool HasMap => CoordinateHelper.MapUrl(
        CoordinateHelper.ToDecimal(Lat), CoordinateHelper.ToDecimal(Lng)) is not null;

    partial void OnLatChanged(string? value) => OnPropertyChanged(nameof(HasMap));
    partial void OnLngChanged(string? value) => OnPropertyChanged(nameof(HasMap));

    /// <summary>좌표를 기본 브라우저 지도에서 연다.</summary>
    [RelayCommand]
    private void OpenMap()
    {
        var url = CoordinateHelper.MapUrl(CoordinateHelper.ToDecimal(Lat), CoordinateHelper.ToDecimal(Lng));
        if (url is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 기본 브라우저가 없거나 정책으로 막힌 환경 — 조사에 지장이 없으므로 조용히 넘어간다.
        }
    }
}
