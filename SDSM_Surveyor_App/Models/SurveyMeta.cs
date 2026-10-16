using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Models;
using SDSM_Surveyor_App.Data;
using SDSM_Core.Helpers;
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
    public SurveyMeta(ISiteListProvider sites, SurveyPins pins)
    {
        Pins = pins;
        UseSiteList(sites);

        // 앱을 켜서 빈 야장이 처음 만들어지는 순간이다 — 고정(압정)된 칸을 여기서 채운다.
        // 세션 복원은 항상 이보다 나중이라 저장된 세션이 정상적으로 덮어쓴다.
        BeginBulkApply();
        try { Pins.Attach(this); }
        finally { EndBulkApply(); }
    }

    /// <summary>조사개황 값 고정(압정) 6개. 화면이 <c>Pins.Project.IsOn</c> 식으로 본다.</summary>
    public SurveyPins Pins { get; }

    private ISiteListProvider? _sites;
    private bool _applyingSite;   // 지점 자동 채움 중 재진입 방지
    private int _bulkDepth;       // 세션 복원·고정값 주입 중 압정 갱신 억제(중첩 가능)

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

    /// <summary>대분류로 거른 지점 목록(드롭다운 항목). 대분류가 비면 빈 목록이다(§8-2).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSiteSelectable))]
    [NotifyPropertyChangedFor(nameof(SiteHint))]
    private List<SurveySite> _siteOptions = new();

    /// <summary>선택된 지점. 고르면 하천·사업장·좌표가 자동으로 채워진다.</summary>
    [ObservableProperty] private SurveySite? _selectedSite;

    /// <summary>
    /// 지점 콤보 한 줄 — 지점 + 조사자 앱 표기(C-7). 공유 모델의 <c>SurveySite.Display</c> 는 St.번호를 늘 붙이므로 쓰지 않는다.
    /// record 라 같은 지점·같은 표기면 같은 항목으로 본다(목록을 바꿔 끼워도 선택이 풀리지 않는다).
    /// </summary>
    public sealed record SiteChoice(SurveySite Site, string Display, string DisplayLong)
    {
        public override string ToString() => Display;
    }

    /// <summary>지점 콤보 항목(정렬 · 표기 규칙 적용). St.번호 후보를 보여 주는 동안은 그 후보만 담는다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSiteChoice))]
    private List<SiteChoice> _siteChoices = new();

    private bool _swappingChoices;

    /// <summary>지점 콤보의 선택(= <see cref="SelectedSite"/>). 목록을 바꿔 끼우는 동안 들어오는 null 은 무시한다.</summary>
    public SiteChoice? SelectedSiteChoice
    {
        get => SelectedSite is null ? null : SiteChoices.FirstOrDefault(c => ReferenceEquals(c.Site, SelectedSite));
        set
        {
            if (_swappingChoices && value is null) return;
            if (value is not null && TryGuardSiteChange(value.Site))
            {
                OnPropertyChanged(nameof(SelectedSiteChoice));   // 확인이 끝날 때까지 콤보는 지금 지점을 보인다
                return;
            }
            SelectedSite = value?.Site;
        }
    }

    /// <summary>
    /// 지점 직접 변경 확인(C-2). 화면(창)이 붙인다 — 입력한 자료가 있어 물어야 하면 true 를 돌려주고
    /// 확인 뒤 스스로 지점을 바꾼다(유지면 <see cref="SelectedSite"/> 대입 · 새 지점이면 <c>ISessionService.StartNewSite</c>).
    /// null 이면 묻지 않는다(자동 검증·기준 파일 생성).
    /// </summary>
    public Func<SurveySite, bool>? SiteChangeGuard { get; set; }

    /// <summary>
    /// 조사자가 지점을 <b>다른</b> 등록 지점으로 바꾸려 한다 — 확인이 필요하면 확인에 넘기고 true.
    /// 세션 불러오기·복사(대량 적용 구간)와 첫 지점 선택은 묻지 않는다.
    /// </summary>
    private bool TryGuardSiteChange(SurveySite next)
    {
        if (_bulkDepth > 0 || SiteChangeGuard is null) return false;
        if (ReferenceEquals(next, SelectedSite)) return false;
        if (SelectedSite is null && string.IsNullOrWhiteSpace(Site)) return false;   // 처음 고르는 지점
        if (SelectedSite is null && string.Equals(Site?.Trim(), next.SiteName, StringComparison.Ordinal)) return false;
        return SiteChangeGuard(next);
    }

    /// <summary>지점과 그에 딸린 값(하천·사업장·좌표·권역)을 비운다(C-2 [새 지점]). 그 밖의 조사개황은 그대로.</summary>
    public void ClearSite()
    {
        SelectedSite = null;
        Site = null;
        River = null;
        Workplace = null;
        Lat = null;
        Lng = null;
        MajorRegion = null;
        MiddleRegion = null;
        if (IsShowingCandidates) SetChoices(SiteOptions, null);
        OnPropertyChanged(nameof(SiteDisplay));
        OnPropertyChanged(nameof(HasMap));
        RaiseSiteFlags();
    }

    /// <summary>St.번호 입력에 후보가 여럿이라 그 후보만 보여 주는 중인가(C-7). 안내 문구가 <see cref="SiteHint"/> 에 나온다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SiteHint))]
    private string? _candidateHint;

    public bool IsShowingCandidates => CandidateHint is not null;

    /// <summary>지점 표기 — 번호를 믿을 수 있는 하천만 `곡교천1 (St.1)`(C-7 · <see cref="SiteOrdering.Display"/>).</summary>
    public string DisplayOf(SurveySite s) => SiteOrdering.Display(s, _sites?.All ?? (IEnumerable<SurveySite>)SiteOptions);

    /// <summary>지점 목록 → 콤보 항목(조사자 앱 표기). 자료함의 지점 복사 콤보도 쓴다.</summary>
    public List<SiteChoice> ToChoices(IEnumerable<SurveySite> sites) =>
        sites.Select(s =>
        {
            var d = DisplayOf(s);
            return new SiteChoice(s, d, string.IsNullOrWhiteSpace(s.Desc) ? d : $"{d} · {s.Desc}");
        }).ToList();

    private void SetChoices(IEnumerable<SurveySite> sites, string? hint)
    {
        _swappingChoices = true;
        try
        {
            SiteChoices = ToChoices(sites);
            CandidateHint = hint;
        }
        finally { _swappingChoices = false; }
        OnPropertyChanged(nameof(SelectedSiteChoice));
        OnPropertyChanged(nameof(IsShowingCandidates));
    }

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
        // C-7 : 하천(가나다) → 하천별 혼합 규칙(St.번호를 믿을 수 있으면 번호 순, 아니면 지점명 자연 정렬)
        SiteOptions = SiteOrdering.Sort(_sites.ByProject(Project));
        SetChoices(SiteOptions, null);
        OnPropertyChanged(nameof(IsSiteSelectable));
        OnPropertyChanged(nameof(SiteHint));
    }

    /// <summary>
    /// 대분류가 바뀌면 **선택된 지점을 즉시 해제**한다.
    /// 방류하천과 생태현황에 같은 이름의 다른 지점이 있어, 이전 선택이 남으면
    /// 다른 대분류의 지점으로 자료가 조용히 섞인다(_ecostatus_sites_extracted §8-2).
    /// </summary>
    partial void OnProjectChanged(string? value)
    {
        SelectedSite = null;
        Site = null;
        River = null;
        Workplace = null;
        Lat = null;
        Lng = null;
        MajorRegion = null;     // C-6 : 권역도 지점(하천)에 딸린 값이다
        MiddleRegion = null;

        RefreshSiteOptions();
        RaiseSummary();
        CapturePin(nameof(Project), value);
    }

    /// <summary>대분류를 고르기 전에는 지점 드롭다운을 쓸 수 없다(§8-2).</summary>
    public bool IsSiteSelectable => !string.IsNullOrWhiteSpace(Project) && SiteOptions.Count > 0;

    /// <summary>지점 드롭다운 아래 안내 문구.</summary>
    public string SiteHint =>
        string.IsNullOrWhiteSpace(Project) ? "대분류를 먼저 고르세요."
        : SiteOptions.Count == 0 ? $"'{Project}' 에 등록된 지점이 없습니다. 관리자에게 지점 등록을 요청하세요."
        : CandidateHint ?? SiteDesc;

    partial void OnSelectedSiteChanged(SurveySite? value)
    {
        OnPropertyChanged(nameof(SelectedSiteChoice));
        // 후보를 보여 주던 중에 하나를 골랐다 — 전체 목록으로 되돌린다(선택은 유지된다).
        if (value is not null && IsShowingCandidates) SetChoices(SiteOptions, null);
        if (value is null || _applyingSite) return;
        _applyingSite = true;
        try
        {
            Site = value.SiteName;                       // ST 번호로 골랐어도 DB상 지점명으로 저장
            if (!string.IsNullOrWhiteSpace(value.River)) River = value.River;
            if (!string.IsNullOrWhiteSpace(value.Workplace)) Workplace = value.Workplace;
            if (value.Lat is double la) Lat = la.ToString("0.######");
            if (value.Lng is double ln) Lng = ln.ToString("0.######");

            // C-6 : 대권역·중권역은 하천 단위 고정표로 채운다(표에 없는 하천이면 비운다).
            // ⚠ 세션 복원(대량 적용 구간)에서는 저장된 값을 그대로 둔다 — 복원은 '지점을 고른 것'이 아니다.
            if (_bulkDepth == 0)
            {
                var region = RegionTable.Find(value.River);
                MajorRegion = region?.Major;
                MiddleRegion = region?.Middle;
            }
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
        if (hit is null && _bulkDepth == 0)
        {
            // C-7 : St.번호 후보가 둘 이상 — 자동으로 고르지 않고 지점 콤보에 그 후보만 보여 준다.
            var cands = _sites.Candidates(text, Project);
            if (cands.Count > 1)
            {
                SetChoices(cands, $"'{text.Trim()}' 에 맞는 지점이 {cands.Count}곳입니다 — 하나를 고르세요: {string.Join(" · ", cands.Select(DisplayOf))}");
                RaiseSiteFlags();
                return;
            }
        }
        if (IsShowingCandidates) SetChoices(SiteOptions, null);   // 다른 값을 쳤다 — 전체 목록으로

        if (hit is not null && !ReferenceEquals(hit, SelectedSite))
        {
            if (TryGuardSiteChange(hit)) { OnPropertyChanged(nameof(SelectedSiteChoice)); return; }   // C-2 확인 뒤 바꾼다
            SelectedSite = hit;
        }
        else
            RaiseSiteFlags();
    }

    /// <summary>
    /// 화면 표기 — 번호를 믿을 수 있는 하천만 `곡교천1 (St.1)`, 나머지는 지점명만(C-7).
    /// 공유 모델의 <c>SurveySite.Display</c>(늘 번호를 붙인다)는 고치지 않고 여기서 만든다.
    /// </summary>
    public string SiteDisplay => SelectedSite is not null ? DisplayOf(SelectedSite) : Site ?? string.Empty;

    /// <summary>지점 마스터에 등록된 지점인지(미등록이면 화면에 경고를 띄운다).</summary>
    public bool IsSiteRegistered => SelectedSite is not null || string.IsNullOrWhiteSpace(Site);

    /// <summary>미등록 지점 경고 표시용(BoolToVisibility 재사용).</summary>
    public bool IsSiteUnregistered => !IsSiteRegistered;

    /// <summary>선택 지점의 위치 설명(예: 배방야구장). 없으면 빈 문자열.</summary>
    public string SiteDesc => SelectedSite?.Desc ?? string.Empty;

    partial void OnSiteChanged(string? value)
    {
        OnPropertyChanged(nameof(SiteDisplay));
        RaiseSiteFlags();
    }

    private void RaiseSiteFlags()
    {
        OnPropertyChanged(nameof(IsSiteRegistered));
        OnPropertyChanged(nameof(IsSiteUnregistered));
        OnPropertyChanged(nameof(SiteDesc));
        OnPropertyChanged(nameof(SiteHint));
        RaiseSummary();
    }

    // ── 값 고정(압정) 연결 ────────────────────────────────────────────────────
    // 사람이 친 값과 프로그램이 넣은 값을 구분해야 한다. 구분하지 않으면
    // 세션을 불러오는 순간 옛 세션의 조사일·조사자가 고정값을 덮어쓴다.
    // `_applyingSite`(지점 자동 채움 재진입 방지)와 같은 관례다.

    /// <summary>
    /// 세션 복원·고정값 주입처럼 프로그램이 여러 칸을 한꺼번에 넣는 구간의 시작.
    /// bool 이 아니라 카운터다 — 구간이 겹쳐도 안쪽이 바깥 구간을 조기에 풀지 않는다.
    /// </summary>
    public void BeginBulkApply() => _bulkDepth++;

    /// <summary>
    /// 대량 적용 구간의 끝. 구간이 끝나는 순간 압정 안내를 다시 계산한다 —
    /// 세션을 불러오면 야장 값이 고정값과 어긋나는데, 그 구간에서는 <c>Capture</c> 가
    /// 억제되어 있어 아무도 띠를 갱신해 주지 않는다.
    /// </summary>
    public void EndBulkApply()
    {
        if (_bulkDepth > 0) _bulkDepth--;
        if (_bulkDepth == 0) Pins.RefreshNotice();
    }

    /// <summary>사람이 그 칸을 고쳤을 때만 고정값을 갱신한다.</summary>
    private void CapturePin(string key, object? value)
    {
        if (_bulkDepth > 0) return;
        Pins.Capture(key, value);
    }

    // ── 접힌 조사개황 요약줄 (06_DESIGN_REBUILD §5-1-5) ─────────────────────
    // 조사개황을 접어도 **무엇을 고르고 있는지**가 항상 보여야 한다.
    // 대분류를 바꿔 지점·하천·좌표가 비워지는 것이 눈에 보여야 자료가 조용히 섞이지 않았음을
    // 조사자가 확인할 수 있다(§3-8).

    /// <summary>대분류를 골랐는가. 안 골랐으면 요약줄이 경고로 바뀌고 카드가 펼쳐진 채 고정된다.</summary>
    public bool HasProject => !string.IsNullOrWhiteSpace(Project);

    /// <summary>요약줄 앞의 대분류 표기. 색점과 **함께** 쓴다(색만으로 구분하지 않는다).</summary>
    public string ProjectText => HasProject ? Project! : "대분류 미선택";

    /// <summary>접힌 조사개황 한 줄 요약 — `곡교천1 (St.1) · 2026반기1차 · 05-14`(St.번호는 C-7 규칙).</summary>
    public string OverviewSummary
    {
        get
        {
            if (!HasProject) return "⚠ 대분류를 먼저 고르세요";

            var parts = new List<string>();
            if (SiteDisplay.Length > 0) parts.Add(SiteDisplay);
            else parts.Add("지점 미선택");
            if (!string.IsNullOrWhiteSpace(YearChsu)) parts.Add(YearChsu);
            if (SurveyDate is DateTime d) parts.Add(d.ToString("MM-dd"));
            return string.Join("  ·  ", parts);
        }
    }

    // ── 조사개황 요약 카드(48px)의 칸별 값 (README §2.3) ────────────────────
    // 접힌 상태에서도 「지점 │ 날짜 │ 좌표 │ 조사자·날씨」가 계속 보인다.
    // 미입력은 전부 `·` 다 — null 과 0/빈값을 눈으로 구분하기 위한 규칙(CLAUDE.md §4).

    /// <summary>조사일 — `2026-05-14`. 미입력이면 `·`.</summary>
    public string SurveyDateText => SurveyDate is DateTime d ? d.ToString("yyyy-MM-dd") : "·";

    /// <summary>좌표 — `36.8123, 127.0451`. 한쪽이라도 비면 `·`.</summary>
    public string CoordinateText =>
        string.IsNullOrWhiteSpace(Lat) || string.IsNullOrWhiteSpace(Lng) ? "·" : $"{Lat}, {Lng}";

    /// <summary>조사자 · 날씨 — 둘 다 비면 `·`.</summary>
    public string SurveyorWeatherText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Surveyor)) parts.Add(Surveyor!);
            if (!string.IsNullOrWhiteSpace(Weather)) parts.Add(Weather!);
            return parts.Count == 0 ? "·" : string.Join(" · ", parts);
        }
    }

    /// <summary>캡션 바 세션 요약 — `방류하천 · 곡교천1 (St.1) · 2026반기1차`.</summary>
    public string ShellSessionSummary
    {
        get
        {
            var parts = new List<string>();
            if (HasProject) parts.Add(Project!);
            if (SiteDisplay.Length > 0) parts.Add(SiteDisplay);
            if (!string.IsNullOrWhiteSpace(YearChsu)) parts.Add(YearChsu);
            return parts.Count == 0 ? "조사개황 미입력" : string.Join(" · ", parts);
        }
    }

    private void RaiseSummary()
    {
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(ProjectText));
        OnPropertyChanged(nameof(OverviewSummary));
        OnPropertyChanged(nameof(ShellSessionSummary));
        OnPropertyChanged(nameof(SurveyDateText));
    }

    partial void OnYearChsuChanged(string value)
    {
        RaiseSummary();
        CapturePin(nameof(YearChsu), value);
    }

    partial void OnSurveyDateChanged(DateTime? value)
    {
        RaiseSummary();
        CapturePin(nameof(SurveyDate), value);
    }

    partial void OnSurveyorChanged(string? value)
    {
        OnPropertyChanged(nameof(SurveyorWeatherText));
        CapturePin(nameof(Surveyor), value);
    }

    partial void OnWeatherChanged(string? value)
    {
        OnPropertyChanged(nameof(SurveyorWeatherText));
        CapturePin(nameof(Weather), value);
    }

    partial void OnSurveyAgencyChanged(string? value) => CapturePin(nameof(SurveyAgency), value);

    /// <summary>좌표가 있어 지도를 열 수 있는지.</summary>
    public bool HasMap => Coordinate.MapUrl(
        Coordinate.ToDecimal(Lat), Coordinate.ToDecimal(Lng)) is not null;

    partial void OnLatChanged(string? value)
    {
        OnPropertyChanged(nameof(HasMap));
        OnPropertyChanged(nameof(CoordinateText));
    }

    partial void OnLngChanged(string? value)
    {
        OnPropertyChanged(nameof(HasMap));
        OnPropertyChanged(nameof(CoordinateText));
    }

    /// <summary>좌표를 기본 브라우저 지도에서 연다.</summary>
    [RelayCommand]
    private void OpenMap()
    {
        var url = Coordinate.MapUrl(Coordinate.ToDecimal(Lat), Coordinate.ToDecimal(Lng));
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
