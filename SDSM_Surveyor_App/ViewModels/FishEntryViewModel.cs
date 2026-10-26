using System.Collections.Specialized;
using SDSM_Core.Helpers;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Models;
using SDSM_Surveyor_App.Data;
using SDSM_Core.Ecology;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels.Base;
using Telerik.Windows.Data;   // RadObservableCollection

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>어류 탭: 초고속 입력 + 실시간 통계/FAI + 임시저장/내보내기.</summary>
public partial class FishEntryViewModel : SpeciesEntryViewModelBase<FishSpeciesEntry, FishSpeciesList>, ISingletonService
{
    internal const string TaxonKey = "Fish";

    /// <summary>양식에 조사개황 하천유형이 있다 — 내보내기 때 '양식에 없는 값'을 알린다(D7).</summary>
    protected override bool UsesRiverType => true;

    private readonly IReferenceRangeProvider _reference;

    public FishEntryViewModel(ISpeciesListProvider speciesProvider, ISessionService sessions,
                 IReferenceRangeProvider reference, SurveyMeta meta)
        : base(sessions, meta)
    {
        _reference = reference;
        SpeciesListSource = speciesProvider.GetFishSpecies();
        _speciesCatalogVersion = speciesProvider.PartVersion("Fish") ?? speciesProvider.Version ?? "-";   // 이 분류군 부분의 판(v0.5.0)
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
        meta.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SurveyMeta.SelectedSite)) PrefillRiverChasu(Meta.SelectedSite);
        };
    }

    // ── 하천차수 = 지점 고정값(2026-10-20 결정 · 문제 7·10) ─────────────────────
    // sites.json 의 지점 하천차수로 미리 채우고 고칠 수 있게 둔다. 지점을 바꾸면 — 조사자가 직접 고친 값이 아니면 — 새 지점 값으로 바꾼다.

    /// <summary>마지막으로 지점에서 채운 값(조사자가 고친 값인지 가리는 데 쓴다).</summary>
    private double? _siteChasu;

    /// <summary>지점 하천차수로 채운다. 비어 있거나 앞서 지점에서 채운 그대로일 때만(조사자가 고친 값은 둔다).</summary>
    private void PrefillRiverChasu(SurveySite? site)
    {
        double? next = site?.Chasu is int c && c > 0 ? c : null;
        if (RiverChasu is null || RiverChasu == _siteChasu) RiverChasu = next;
        _siteChasu = next;
    }

    /// <summary>
    /// 하천차수가 비어 FAI 를 낼 수 없는가 — FAI M1·M2·M3·M7 은 하천차수별 구간표라 0차로 계산하면 낮게 나온다(문제 10).
    /// 조사불가 사유가 있거나 출현종 없음(0종) 선언이면 하천차수 없이도 결과가 정해지므로 묻지 않는다.
    /// </summary>
    public bool NeedsRiverChasu => RiverChasu is not (> 0) && !IsUnavailable && !NoSpeciesDeclared
                                   && SpeciesEntries.Any(r => (r.IndividualCount ?? 0) > 0);   // 낼 자료가 없는 빈 탭에는 묻지 않는다

    public const string RiverChasuNeededText = "하천차수 필요 — 조사환경에 하천차수를 넣으세요(지점 하천차수가 없음)";

    public string RiverChasuNeeded => NeedsRiverChasu ? RiverChasuNeededText : string.Empty;

    /// <summary>
    /// 일괄입력용을 만들 수 없는 까닭(없으면 빈 문자열) — 관리자 일괄등록이 막는 값을 미리 막는다.
    /// 하천차수 빈칸(문제 7) · 비정상종 개체수가 정수가 아님(문제 8·11).
    /// </summary>
    public string BulkBlockReason
    {
        get
        {
            var parts = new List<string>();
            if (NeedsRiverChasu) parts.Add("하천차수가 비어 있습니다(관리자 일괄등록 필수 칸) — 조사환경에 하천차수를 넣으세요");
            if (AbnormalInvalid.Length > 0) parts.Add($"비정상종 개체수가 정수가 아닙니다: {AbnormalInvalid} — 정수로 고치고 종명은 비고에 적으세요");
            return string.Join("\n", parts);
        }
    }


    // ── 어류 고유 기본정보 ──
    [ObservableProperty] private string? _collectionTool;                 // 채집도구
    [ObservableProperty] private double? _riverChasu;                     // 하천차수(FAI 계산)
    [ObservableProperty] private string? _surveyUnavailableReason;        // 특이사항(조사불가→"-")

    // ── 관리자 전체 필드(자동계산 외 전부 입력) : 값은 숫자도 원문 문자열로 보관 후 내보내기 시 파싱 ──
    // 채집방법 확장
    [ObservableProperty] private string? _collectionTime;    // 채집소요시간(분)
    [ObservableProperty] private string? _collectionFlowState; // 흐름상태(채집) — 여울/흐름/소 복수(B-8)
    // 서식지 하상구성(%)
    [ObservableProperty] private string? _bedrock;           // 암반
    [ObservableProperty] private string? _concrete;          // 콘크리트
    [ObservableProperty] private string? _mud;               // 진흙이하(<0.063mm)
    [ObservableProperty] private string? _sand;              // 모래(0.063-2mm)
    [ObservableProperty] private string? _fineGravel;        // 잔자갈(2-16mm)
    [ObservableProperty] private string? _gravel;            // 자갈(16-64mm)
    [ObservableProperty] private string? _smallStone;        // 작은돌(64-256mm)
    [ObservableProperty] private string? _bigStone;          // 큰돌(>256mm)
    // 서식처
    [ObservableProperty] private string? _habitatRiverType;  // 하천형태
    [ObservableProperty] private string? _habitatFlowState;  // 흐름상태(유속) — 화면 라벨 '유속'(B-8)
    // 채집불가시
    [ObservableProperty] private string? _note;              // 비고

    // 비정상종 개체수 (FAI M8) : 기형·지느러미손상·피부손상·종양
    [ObservableProperty] private string? _deCount;   // 기형 deformity
    [ObservableProperty] private string? _efCount;   // 지느러미손상 erosion of fin
    [ObservableProperty] private string? _leCount;   // 피부손상 lesions
    [ObservableProperty] private string? _tuCount;   // 종양 tumors

    /// <summary>비정상종 개체수 파싱 — 미입력은 **0**으로 본다(합산 대상).
    /// 기반 클래스의 <c>Pi</c> 는 미입력을 null 로 두므로 이름을 갈라 둔다.</summary>
    private static int PiOrZero(string? s) => int.TryParse(s, out var n) ? n : 0;

    /// <summary>
    /// 비정상종(DE·EF·LE·TU) 칸 중 0 이상 정수가 아닌 것(예전 세션의 '붕어(2)' · '1.5' 등) — 지우지 않고 '양식에 없는 값'으로 알린다(문제 8·11).
    /// 관리자 일괄등록은 정수만 받는다(형변환실패). FAI 는 지금처럼 그 칸을 0 으로 본다.
    /// </summary>
    public string AbnormalInvalid => string.Join(" · ", new[] { ("기형(DE)", DeCount), ("지느러미손상(EF)", EfCount), ("피부손상(LE)", LeCount), ("종양(TU)", TuCount) }
        .Where(f => !string.IsNullOrWhiteSpace(f.Item2) && !(int.TryParse(f.Item2.Trim(), out var n) && n >= 0))
        .Select(f => $"{f.Item1} '{f.Item2!.Trim()}'"));

    public string AbnormalWarning => AbnormalInvalid.Length == 0 ? string.Empty : $"양식에 없는 값(개체수 정수만): {AbnormalInvalid}";

    // 위 헤더 값 변경 시에도 FAI 재계산
    partial void OnRiverChasuChanged(double? value) { RaiseEnvironmentSummary(); Recalculate(); }
    partial void OnSurveyUnavailableReasonChanged(string? value) => Recalculate();
    partial void OnDeCountChanged(string? value) { RaiseEnvironmentSummary(); Recalculate(); }
    partial void OnEfCountChanged(string? value) { RaiseEnvironmentSummary(); Recalculate(); }
    partial void OnLeCountChanged(string? value) { RaiseEnvironmentSummary(); Recalculate(); }
    partial void OnTuCountChanged(string? value) { RaiseEnvironmentSummary(); Recalculate(); }

    // 하상구성 8칸 — 합계 100 검증과 요약줄이 함께 따라간다
    partial void OnBedrockChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnConcreteChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnMudChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnSandChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnFineGravelChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnGravelChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnSmallStoneChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnBigStoneChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnCollectionTimeChanged(string? value) => RaiseEnvironmentSummary();

    /// <summary>
    /// 흐름상태(채집) 선택지 — 최종본 양식 16행 '1 여울, 2 흐름, 3 소 (복수)'(B-8).
    /// 저장은 이름을 공백 없이 `,` 로 잇는다(`흐름,소`). 양식 28행 '흐름상태(유속)' 은 <see cref="HabitatFlowState"/> 다.
    /// 다른 선택지(하천형태·유속·채집도구)는 화면이 <see cref="FormChoices"/> 를 직접 쓴다(C-4).
    /// </summary>
    public IReadOnlyList<string> CollectionFlowStates => FormChoices.FlowState;

    // C-4 : 선택지로 바꾼 칸의 양식 외 값(예전 세션) — 지우지 않고 접힌 요약줄에도 알린다
    partial void OnHabitatRiverTypeChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnHabitatFlowStateChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnCollectionFlowStateChanged(string? value) => RaiseEnvironmentSummary();

    /// <summary>선택지 밖 값 경고(C-4). 채집도구는 직접 입력을 허용하므로 보지 않는다.</summary>
    public string ChoiceWarning => FormChoices.Describe(
        ("하천형태", FormChoices.OutOfForm(HabitatRiverType, FormChoices.FishRiverForm)),
        ("흐름상태(유속)", FormChoices.OutOfForm(HabitatFlowState, FormChoices.FishVelocity) is string v ? new[] { v } : Array.Empty<string>()),
        ("흐름상태(채집)", FormChoices.OutOfForm(CollectionFlowState, FormChoices.FlowState)));

    // ── 초고속 입력 그리드(Center-Left) ──
    /// <summary>종 행. 기반 클래스의 <c>Rows</c> 를 화면 바인딩 이름으로 노출한다.</summary>
    public RadObservableCollection<FishSpeciesEntry> SpeciesEntries => Rows;

    /// <summary>붙여넣기 한 줄 → 행. 열 순서 = [국명, 개체수].
    /// 국명이 공식 종목록에 있으면 학명·길드가 자동으로 연결된다.</summary>
    protected override FishSpeciesEntry CreateRowFromCells(string ko, string[] cells)
    {
        var entry = new FishSpeciesEntry();
        var match = Helpers.SpeciesLookup.Fish(SpeciesListSource, ko);
        if (match is not null) entry.SelectedSpecies = match;   // 학명·길드 자동 연결
        else entry.SpeciesKo = ko;

        if (PasteInt(cells, 1) is int n)   // N2 : 정수가 아니면 넣지 않고 끝에 행·열을 알린다
            entry.IndividualCount = n;

        return entry;
    }


    /// <summary>조사불가 사유가 선언되어 등급이 "-"가 되는 상태인지.</summary>
    private bool IsUnavailable =>
        !string.IsNullOrWhiteSpace(SurveyUnavailableReason)
        && UnavailableReasons.Contains(SurveyUnavailableReason.Replace(" ", ""));

    private static readonly string[] UnavailableReasons = { "접근불가", "건천화", "준설", "공사중" };

    /// <summary>출현종 없음(0종) 선언 — 조사는 수행했으나 한 종도 확인되지 않았다는 뜻.
    /// 종 목록이 비었다는 사실만으로는 "아직 미입력"과 구분할 수 없어 명시적으로 선언받는다(12_CALC_FIX §2-1).</summary>
    [ObservableProperty] private bool _noSpeciesDeclared;
    partial void OnNoSpeciesDeclaredChanged(bool value) => Recalculate();

    // ── 접힘 상태 (README §상태 · 세션에 저장한다) ──────────────────────────
    // 기본은 접힘이다. 조사환경은 지점당 1회만 적고, 그다음부터는 종 그리드가 20행이어야 한다.
    [ObservableProperty] private bool _isEnvironmentExpanded;
    [ObservableProperty] private bool _isHealthExpanded;

    // ── 실시간 통계 (본문 상단 스트립 · 종전 우측 결과 레일) ──
    [ObservableProperty] private int _totalSpeciesCount;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalShareText))]
    private int _totalIndividualCount;
    [ObservableProperty] private string _dominantSpecies = "-";
    [ObservableProperty] private double? _faiScore;
    [ObservableProperty] private string _faiGrade = "-";

    /// <summary>우점종 점유율(%). 총 개체수가 0이면 null.</summary>
    [ObservableProperty] private double? _dominantShare;

    /// <summary>합계 행의 상대풍부도 칸. 개체가 하나도 없으면 100% 가 아니라 `·` 다(미산출).</summary>
    public string TotalShareText => TotalIndividualCount > 0 ? "100%" : "·";

    /// <summary>행이 하나도 없는가(빈 상태 안내 · README §11.4).</summary>
    public bool HasNoRows => SpeciesEntries.Count == 0;

    /// <summary>법정보호종 종수(멸종위기Ⅰ·Ⅱ급·천연기념물).</summary>
    [ObservableProperty] private int _protectedCount;

    /// <summary>생태계교란생물 종수. ⚠ 외래종 종수와 다르다(<see cref="ExoticCount"/>).</summary>
    [ObservableProperty] private int _invasiveCount;

    /// <summary>외래종 종수. FAI M1·M7 이 국내종을 셀 때 빼는 값이다.</summary>
    [ObservableProperty] private int _exoticCount;

    /// <summary>종명은 적혔는데 개체수가 비어 있는 행 수. **미입력과 0 은 다르게 저장된다.**</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMissingCounts))]
    [NotifyPropertyChangedFor(nameof(MissingCountText))]
    [NotifyPropertyChangedFor(nameof(MissingCountDetail))]
    private int _missingCountRows;

    public bool HasMissingCounts => MissingCountRows > 0;
    public string MissingCountText => MissingCountRows > 0 ? $"{MissingCountRows}행 개체수 미입력" : string.Empty;
    public string MissingCountDetail =>
        MissingCountRows > 0 ? $"{MissingCountRows}행 개체수 미입력 — 미입력과 0 은 다르게 저장됩니다" : string.Empty;

    // ── FAI 상세 (M1~M8) ─────────────────────────────────────────────────────
    // ⚠ 계산은 SDSM_Core 가 한다. 여기서는 **결과만 읽는다** — 관리자와 같은 코드다.
    //    CalculateFaiDetail 은 CalculateFai 가 내부에서 쓰는 바로 그 함수다(값이 어긋날 수 없다).
    [ObservableProperty] private FaiResult? _faiDetail;

    public double M1 => FaiDetail?.M1 ?? 0;
    public double M2 => FaiDetail?.M2 ?? 0;
    public double M3 => FaiDetail?.M3 ?? 0;
    public double M4 => FaiDetail?.M4 ?? 0;
    public double M5 => FaiDetail?.M5 ?? 0;
    public double M6 => FaiDetail?.M6 ?? 0;
    public double M7 => FaiDetail?.M7 ?? 0;
    public double M8 => FaiDetail?.M8 ?? 0;

    /// <summary>
    /// 낮은 항목(M1~M8). 색만으로 구분하지 않도록 **글자로도** 적는다(design.md §11).
    /// 판정 : 0점 항목이 있으면 그것들, 없으면 6.25점 항목들, 둘 다 없으면 「없음」.
    /// </summary>
    public string LowMetricsText
    {
        get
        {
            if (FaiDetail is null || FaiDetail.Score is null) return "·";
            var m = new[] { M1, M2, M3, M4, M5, M6, M7, M8 };
            var zero = Enumerable.Range(0, 8).Where(i => m[i] <= 0).ToList();
            var half = Enumerable.Range(0, 8).Where(i => m[i] > 0 && m[i] < 12.5).ToList();
            var pick = zero.Count > 0 ? zero : half;
            return pick.Count == 0 ? "없음" : string.Join(" · ", pick.Select(i => $"M{i + 1}"));
        }
    }

    /// <summary>
    /// M1~M8 막대 하나. 낮은 항목은 색이 다르고 <b>이름도 함께</b> 적힌다
    /// (색만으로 구분하지 않는다 · design.md §11).
    /// </summary>
    /// <param name="Name">M1~M8.</param>
    /// <param name="Score">0 · 6.25 · 12.5.</param>
    /// <param name="IsLow">낮은 항목인지.</param>
    /// <param name="Tooltip">그 지표가 무엇을 재는지.</param>
    public sealed record FaiMetricBar(string Name, double Score, bool IsLow, string Tooltip);

    /// <summary>M1~M8 막대. 값은 <c>SDSM_Core</c> 계산 결과를 그대로 옮긴 것이다.</summary>
    [ObservableProperty] private IReadOnlyList<FaiMetricBar> _metricBars = Array.Empty<FaiMetricBar>();

    /// <summary>M1~M8 이 각각 무엇을 재는지(툴팁 · 지침 표기).</summary>
    private static readonly string[] MetricTooltips =
    {
        "M1 국내종 총 종수",
        "M2 여울성 저서종수",
        "M3 민감종수",
        "M4 내성종 개체수 비율",
        "M5 잡식종 개체수 비율",
        "M6 충식종 개체수 비율",
        "M7 국내종 총 개체수",
        "M8 비정상종 개체수 비율",
    };

    /// <summary>건강성 카드 머리글 옆 캡션 — 계산 출처를 밝힌다.</summary>
    public string HealthCaption =>
        $"관리자 SDSM_Core 와 동일 계산 · 종목록 버전 {_speciesCatalogVersion}";

    private readonly string _speciesCatalogVersion;

    // ── 조사환경 요약줄 (접힘 36px) ──────────────────────────────────────────
    // 접어도 값이 계속 보여야 한다 — 접기가 '숨기기'가 되지 않게.

    /// <summary>하상구성 8칸 중 값이 들어간 칸 수. 미입력이면 `·`.</summary>
    public string SubstrateSummary
    {
        get
        {
            var filled = new[] { Bedrock, Concrete, Mud, Sand, FineGravel, Gravel, SmallStone, BigStone }
                         .Count(v => !string.IsNullOrWhiteSpace(v));
            return filled == 0 ? "·" : $"{filled}칸 입력됨";
        }
    }

    /// <summary>하상구성 8칸(화면 이름, 값). 합계·정수 점검이 같이 쓴다.</summary>
    private (string Label, string? Value)[] SubstrateFields => new[]
    {
        ("암반", Bedrock), ("콘크리트", Concrete), ("진흙", Mud), ("모래", Sand),
        ("잔자갈", FineGravel), ("자갈", Gravel), ("작은돌", SmallStone), ("큰돌", BigStone),
    };

    /// <summary>하상구성 합계(%). 지침상 100 이어야 한다. 전부 비면 null.</summary>
    public double? SubstrateTotal
    {
        get
        {
            var vals = SubstrateFields.Select(f => Pd(f.Value)).Where(v => v is not null).Select(v => v!.Value).ToList();
            return vals.Count == 0 ? null : vals.Sum();
        }
    }

    /// <summary>
    /// 하상구성 경고 — 합계가 100 이 아니거나, 0~100 정수가 아닌 칸이 있으면(B-6 · 예전 세션 값).
    /// 막지 않고 알리기만 한다. 8칸이 모두 비면 경고하지 않는다.
    /// </summary>
    public string SubstrateWarning
    {
        get
        {
            var parts = new List<string>();
            if (SubstrateTotal is double t && Math.Abs(t - 100) > 0.01)
                parts.Add($"하상구성 합계 {t:0.#}% — 100 이 되어야 합니다");
            var bad = WholeNumber.Describe(SubstrateFields, 100);
            if (bad.Length > 0) parts.Add($"0~100 정수가 아닌 칸: {bad}");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>채집소요시간(분)이 정수가 아니면 경고(B-6 · 예전 세션 값은 지우지 않는다).</summary>
    public string CollectionTimeWarning =>
        WholeNumber.IsValid(CollectionTime) ? string.Empty : $"채집소요시간 {CollectionTime!.Trim()} — 분 단위 정수로 고치세요";

    /// <summary>하천차수가 정수가 아니면 경고(10-17 (4) · 칸은 정수로만 보이므로 예전 세션의 소수 값을 지우지 않고 알린다).
    /// FAI 는 소수점 아래를 버린 값으로 계산한다(<c>(int)RiverChasu</c>).</summary>
    public string RiverChasuWarning =>
        RiverChasu is double c && c != Math.Floor(c) ? $"하천차수 {c:0.###} — 정수로 고치세요(지수 계산은 {(int)c}차)" : string.Empty;

    /// <summary>접힌 요약줄에 띄우는 조사환경 경고(하상구성 + 채집소요시간 + 하천차수).</summary>
    public string EnvironmentWarning =>
        string.Join(" · ", new[] { RiverChasuNeeded, SubstrateWarning, CollectionTimeWarning, RiverChasuWarning, AbnormalWarning, ChoiceWarning }.Where(s => s.Length > 0));

    /// <summary>비정상종(DE·EF·LE·TU) 합계 개체수. FAI M8 의 분자다.</summary>
    public int AbnormalTotal => PiOrZero(DeCount) + PiOrZero(EfCount) + PiOrZero(LeCount) + PiOrZero(TuCount);

    public string AbnormalSummary => AbnormalTotal > 0 ? $"{AbnormalTotal}개체" : "·";

    /// <summary>하천차수 표기. FAI M1·M2·M3·M7 의 구간표를 고르는 값이라 비면 지수가 안 나온다.</summary>
    public string RiverChasuText => RiverChasu is double c and > 0 ? $"{c:0}차" : "·";

    private void RaiseEnvironmentSummary()
    {
        OnPropertyChanged(nameof(SubstrateSummary));
        OnPropertyChanged(nameof(SubstrateTotal));
        OnPropertyChanged(nameof(SubstrateWarning));
        OnPropertyChanged(nameof(CollectionTimeWarning));
        OnPropertyChanged(nameof(RiverChasuWarning));
        OnPropertyChanged(nameof(ChoiceWarning));
        OnPropertyChanged(nameof(EnvironmentWarning));
        OnPropertyChanged(nameof(AbnormalSummary));
        OnPropertyChanged(nameof(RiverChasuText));
        OnPropertyChanged(nameof(AbnormalInvalid));
        OnPropertyChanged(nameof(AbnormalWarning));
        OnPropertyChanged(nameof(NeedsRiverChasu));
        OnPropertyChanged(nameof(RiverChasuNeeded));
        OnPropertyChanged(nameof(BulkBlockReason));
    }

    // ── 상태바(Bottom) ──


    /// <summary>국명 문자열을 공식 종목록과 대조해 SelectedSpecies(학명·길드·보호종)를 연결.
    /// 엑셀 복사·붙여넣기로 국명만 들어온 경우에도 학명/지수가 채워지도록 한다.</summary>
    private void ResolveSpecies(FishSpeciesEntry row)
    {
        var ko = row.SpeciesKo?.Trim();
        if (string.IsNullOrEmpty(ko)) return;
        if (row.SelectedSpecies?.SpeciesKo == ko) return;   // 이미 일치(자동완성 선택 등)
        var match = Helpers.SpeciesLookup.Fish(SpeciesListSource, ko);
        if (match is not null)
        {
            if (!ReferenceEquals(match, row.SelectedSpecies)) row.SelectedSpecies = match;
        }
        else if (row.SelectedSpecies is not null)
        {
            // 목록 밖 국명으로 고쳤다(B-4) — 예전 종의 학명·길드가 남으면 계산이 그 종으로 된다.
            // 비우면 국명만 남고 '과거 기록 없음' 경고 경로를 탄다. SpeciesKo 는 건드리지 않는다.
            row.SelectedSpecies = null;
        }
    }

    // 입력 이상치 경고 개수(상태바 표시) — 이상치 + 같은 종 중복 행을 합친 수다.
    [ObservableProperty] private int _warningCount;

    // 상태바 문구를 두 갈래로 나눠 적으려고 따로 센다(이상치 n건 · 같은 종 중복 k종).
    private int _outlierRows;
    private int _unlistedRows;
    private int _duplicateSpecies;

    partial void OnWarningCountChanged(int value)
    {
        OnPropertyChanged(nameof(WarningText));
        OnPropertyChanged(nameof(HasWarnings));
    }

    public string WarningText
    {
        get
        {
            var parts = new List<string>();
            if (_outlierRows > 0) parts.Add($"이상치 경고 {_outlierRows}건");
            if (_unlistedRows > 0) parts.Add($"목록 밖 국명 {_unlistedRows}건");
            if (_duplicateSpecies > 0) parts.Add($"같은 종 중복 {_duplicateSpecies}종");
            return parts.Count == 0 ? string.Empty : "⚠ " + string.Join(" · ", parts);
        }
    }

    /// <summary>경고 영역 표시 여부(06_DESIGN_REBUILD §5-2-1). 경고는 비차단이다 — 저장을 막지 않는다.</summary>
    public bool HasWarnings => WarningCount > 0;

    /// <summary>
    /// 경고가 걸린 종 이름. 종전에는 건수만 보여 **어느 종인지 알 수 없었다**(§5-2-1).
    /// 3종까지 적고 나머지는 개수로 줄인다.
    /// </summary>
    [ObservableProperty] private string _warningDetail = string.Empty;

    /// <summary>기준자료 대비 이상치 검증(비차단 경고). 종별 과거 최대 초과 / 신규 종.</summary>
    private void ValidateRows()
    {
        // 같은 종이 여러 행 — 불러온 세션에 이미 있던 중복은 자동으로 합치지 않고 알리기만 한다(B-1).
        // 입력 중에 생긴 중복은 빠른추가·붙여넣기·그리드 확정 단계에서 이미 합쳐진다.
        var dupCount = SpeciesEntries
            .Select(SpeciesKeyOf).Where(k => !string.IsNullOrEmpty(k))
            .GroupBy(k => k!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        int warn = 0, outlier = 0, unlisted = 0;
        foreach (var e in SpeciesEntries)
        {
            var ko = e.SelectedSpecies?.SpeciesKo ?? e.SpeciesKo;
            var cnt = e.IndividualCount;

            var key = SpeciesKeyOf(e);
            var dup = key is not null && dupCount.TryGetValue(key, out var n)
                ? $"같은 종이 {n}행 — 합쳐야 계산이 맞습니다"
                : null;

            // 종목록에 없는 국명(B-4) — 지우지 않고 그대로 저장한다. 오타인지 확인만 요청한다.
            // 이 경우 기준자료에도 없으므로 아래 '과거 기록 없음' 은 겹쳐 적지 않는다.
            string? listed = e.SelectedSpecies is null && !string.IsNullOrWhiteSpace(e.SpeciesKo)
                ? "종목록에 없는 국명 — 오타인지 확인하세요"
                : null;
            if (listed is not null) unlisted++;

            string? range = null;
            if (listed is null && !string.IsNullOrWhiteSpace(ko) && cnt is > 0)
            {
                var r = _reference.GetFishRange(ko);
                if (r is null) range = "과거 기록 없음 (신규 종 확인)";
                else if (cnt.Value > r.Max) range = $"과거 최대 {r.Max:N0}개체 초과";
            }
            if (range is not null) outlier++;

            e.ErrorContent = string.Join(" · ", new[] { dup, listed, range }.Where(s => s is not null));
            if (e.HasWarning) warn++;
        }
        _outlierRows = outlier;
        _unlistedRows = unlisted;
        _duplicateSpecies = dupCount.Count;
        WarningCount = warn;
        OnPropertyChanged(nameof(WarningText));

        var names = SpeciesEntries.Where(x => x.HasWarning)
                                  .Select(x => x.SelectedSpecies?.SpeciesKo ?? x.SpeciesKo)
                                  .Where(x => !string.IsNullOrWhiteSpace(x))
                                  .ToList();
        WarningDetail = names.Count switch
        {
            0 => string.Empty,
            <= 3 => string.Join(" · ", names),
            _ => string.Join(" · ", names.Take(3)) + $" 외 {names.Count - 3}종",
        };
    }

    /// <summary>입력 즉시 실시간 통계·FAI 재계산.</summary>
    protected override void Recalculate()
    {
        ValidateRows();

        var imports = SpeciesEntries.Select(r => r.ToImport()).ToList();

        // 결측치(null)와 0 엄격 구분 : 0 초과 개체만 집계
        TotalSpeciesCount = imports.Count(x => (x.IndividualCount ?? 0) > 0);
        TotalIndividualCount = imports.Sum(x => x.IndividualCount ?? 0);

        var dominant = imports.Where(x => (x.IndividualCount ?? 0) > 0)
                              .OrderByDescending(x => x.IndividualCount)
                              .FirstOrDefault();
        DominantSpecies = dominant?.SpeciesKo ?? "-";
        DominantShare = TotalIndividualCount > 0 && dominant is not null
            ? (double)(dominant.IndividualCount ?? 0) / TotalIndividualCount * 100.0
            : null;

        // 행 번호 · 상대풍부도(읽기전용 파생) · 태그 집계
        //  ⚠ 여기서 건드리는 속성들은 AffectsRecalculation 에 없다 — 있으면 무한 재계산이 된다.
        int n = 0, prot = 0, invasive = 0, exotic = 0, missing = 0;
        foreach (var row in SpeciesEntries)
        {
            row.RowNumber = ++n;

            row.RelativeAbundance = TotalIndividualCount > 0 && row.IndividualCount is int c && c > 0
                ? (double)c / TotalIndividualCount * 100.0
                : null;

            var named = !string.IsNullOrWhiteSpace(row.SelectedSpecies?.SpeciesKo ?? row.SpeciesKo);
            if (!named) continue;

            // 종명은 적혔는데 개체수가 비었다 — 0 으로 치환하지 않고 건수만 알린다
            if (row.IndividualCount is null) missing++;

            if ((row.IndividualCount ?? 0) <= 0) continue;
            if (row.IsProtected) prot++;
            if (row.IsInvasive) invasive++;
            if (row.IsExotic) exotic++;
        }
        ProtectedCount = prot;
        InvasiveCount = invasive;
        ExoticCount = exotic;
        MissingCountRows = missing;

        // 0종 처리는 계산기가 판단한다(12_CALC_FIX §2)
        //  조사불가 → "-"  /  조사 수행 + 0종 선언 → 0·E  /  미입력 → "-"
        // ⚠ CalculateFaiDetail 은 CalculateFai 가 내부에서 호출하는 바로 그 함수다.
        //    M1~M8 을 화면에 그리려고 결과를 더 받을 뿐, 계산식은 손대지 않았다.
        int abnormal = AbnormalTotal;
        var detail = EcologyCalculator.CalculateFaiDetail(
            imports, SurveyUnavailableReason, abnormalCount: abnormal,
            chasu: (int)(RiverChasu ?? 0), noSpeciesDeclared: NoSpeciesDeclared);

        if (NeedsRiverChasu)
        {
            // 문제 10 : 하천차수 없이 낸 FAI 는 M1·M2·M3·M7 이 0 이 되어 낮게 나온다 — 값을 보이지 않고 '하천차수 필요'를 알린다.
            //   계산식(SDSM_Core)은 그대로다. 화면·보고서에서 쓰지 않을 뿐이다.
            FaiDetail = null;
            FaiScore = null;
            FaiGrade = "-";
        }
        else
        {
            FaiDetail = detail;
            FaiScore = detail.Score;
            FaiGrade = detail.Grade ?? "-";
        }

        RaiseHealthSummary();
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(NeedsRiverChasu));
        OnPropertyChanged(nameof(RiverChasuNeeded));
        OnPropertyChanged(nameof(BulkBlockReason));
        OnPropertyChanged(nameof(EnvironmentWarning));

        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
    }

    private void RaiseHealthSummary()
    {
        var m = new[] { M1, M2, M3, M4, M5, M6, M7, M8 };
        var scored = FaiDetail?.Score is not null;
        // 낮은 항목 판정은 LowMetricsText 와 같은 규칙을 쓴다(0점 우선, 없으면 6.25점).
        var lowSet = LowMetricsText;
        MetricBars = Enumerable.Range(0, 8)
            .Select(i => new FaiMetricBar(
                $"M{i + 1}",
                scored ? m[i] : 0,
                scored && lowSet.Contains($"M{i + 1}", StringComparison.Ordinal),
                MetricTooltips[i]))
            .ToList();

        OnPropertyChanged(nameof(M1)); OnPropertyChanged(nameof(M2));
        OnPropertyChanged(nameof(M3)); OnPropertyChanged(nameof(M4));
        OnPropertyChanged(nameof(M5)); OnPropertyChanged(nameof(M6));
        OnPropertyChanged(nameof(M7)); OnPropertyChanged(nameof(M8));
        OnPropertyChanged(nameof(LowMetricsText));
    }

    /// <summary>빠른추가로 들어온 행을 1.2초간 강조한다(기반 클래스가 타이머를 돌린다).</summary>
    protected override void SetJustAdded(FishSpeciesEntry row, bool value) => row.IsJustAdded = value;


    /// <summary>보고서·기록용 엑셀 내보내기(조사개황·출현종·FAI 건강성평가 3개 시트).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportExcel()
    {
        try
        {
            var saved = Export.FishReportExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"보고서용 엑셀 완료 · {System.IO.Path.GetFileName(saved)}";
                NotifyExported("보고서용 엑셀을 내보냈습니다.");
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    /// <summary>[레거시] 관리자 '어류_입력' 전치 일괄입력 양식으로 내보내기(현행 관리자 Import 취합용).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportBulk()
    {
        // 관리자 일괄등록이 막을 값(하천차수 빈칸 · 비정상종 개체수가 정수 아님)이면 만들지 않고 까닭을 알린다(문제 7·8·11)
        if (BulkBlockReason is { Length: > 0 } block)
        {
            StatusText = "일괄입력용을 만들지 않았습니다 — " + block.Replace("\n", " · ");
            WeakReferenceMessenger.Default.Send(new NotifyMessage(($"일괄입력용을 만들지 않았습니다.\n{block}", false)));
            return;
        }
        try
        {
            var saved = Export.FishExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"일괄입력용 엑셀 완료 · {System.IO.Path.GetFileName(saved)}";
                NotifyExported("일괄입력용 엑셀을 내보냈습니다.");
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    // 내보내기 게이트: 유효 개체가 하나라도 있거나, 0종 선언·조사불가가 선언되면 가능.
    // (조사했는데 0종인 지점도 결과를 전달할 수 있어야 한다 — 12_CALC_FIX §2-2)
    // 이상치 경고는 알림일 뿐 제재가 아니므로 내보내기를 막지 않는다.
    private bool CanExport() =>
        SpeciesEntries.Any(r => (r.IndividualCount ?? 0) > 0) || NoSpeciesDeclared || IsUnavailable;
// ── 기반 클래스가 요구하는 분류군 고유 동작 ──────────────────────────────

    protected override string? SpeciesKoOf(FishSpeciesList species) => species.SpeciesKo;

    protected override FishSpeciesEntry CreateRow(FishSpeciesList species, string? count)
    {
        var entry = new FishSpeciesEntry { SelectedSpecies = species };
        if (int.TryParse(count, out var n)) entry.IndividualCount = n;
        return entry;
    }

    protected override bool IsRowEmpty(FishSpeciesEntry row) =>
        string.IsNullOrWhiteSpace(row.SelectedSpecies?.SpeciesKo ?? row.SpeciesKo) && row.IndividualCount is null;

    protected override bool AffectsRecalculation(string? propertyName) =>
        propertyName is nameof(FishSpeciesEntry.IndividualCount)
                     or nameof(FishSpeciesEntry.SelectedSpecies)
                     or nameof(FishSpeciesEntry.SpeciesKo);

    /// <summary>
    /// 국명을 직접 친 경우에도 공식 종목록과 잇는다(B-4).
    /// ⚠ 종전에는 이 연결이 붙여넣기 경로에만 있었다 — 그리드에 친 국명은 학명·길드가 비어 FAI 가 틀렸다.
    /// </summary>
    protected override void OnRowPropertyChanged(FishSpeciesEntry row, string? propertyName)
    {
        if (propertyName == nameof(FishSpeciesEntry.SpeciesKo)) ResolveSpecies(row);
    }

    // ── 같은 종 중복 : 합산 (B-1) ─────────────────────────────────────────────
    // 한 지점·한 회차에 종당 한 행이 양식 구조다. 관리자 Import 도 종명 중복을 오류로 막는다.

    protected override DuplicateSpeciesMode DuplicateMode => DuplicateSpeciesMode.Merge;

    protected override string? SpeciesKeyOf(FishSpeciesEntry row) =>
        (row.SelectedSpecies?.SpeciesKo ?? row.SpeciesKo)?.Trim();

    protected override (string Before, string Added, string After) MergeInto(FishSpeciesEntry target, FishSpeciesEntry source)
    {
        var before = target.IndividualCount;
        var added = source.IndividualCount;
        target.IndividualCount = CountMath.Sum(before, added);   // 결측 보존 — null 을 0 으로 채우지 않는다
        target.Note = CountMath.JoinNote(target.Note, source.Note);
        return (CountMath.Show(before), CountMath.Show(added), CountMath.Show(target.IndividualCount));
    }
}

// ── 임시 저장용 DTO ──
/// <summary>
/// 세션에 저장되는 종 행. <c>Note</c> 는 나중에 붙였으므로 **선택 매개변수**로 둔다 —
/// 옛 세션 파일(2개짜리)도 그대로 읽힌다(없으면 null).
/// </summary>
/// <summary>종 행. <c>SpeciesEn</c> 은 v0.5.0 에 더한 칸(추가만 · 예전 세션은 없음 = 국명으로 찾음) — 옛 학명도 지침 표기로 읽는다.</summary>
public record FishDraftRow(string? SpeciesKo, int? IndividualCount, string? Note = null, string? SpeciesEn = null);

public sealed class FishDraft
{
    // 조사개황
    public string? SurveyYear { get; init; }
    public string? YearChsu { get; init; }
    public DateTime? SurveyDate { get; init; }
    public string? MajorRegion { get; init; }
    public string? MiddleRegion { get; init; }
    public string? River { get; init; }
    public string? RiverType { get; init; }
    public string? Site { get; init; }
    public string? Lat { get; init; }
    public string? Lng { get; init; }
    public string? Weather { get; init; }
    public string? SurveyAgency { get; init; }
    public string? Surveyor { get; init; }
    // 채집방법
    public string? CollectionTime { get; init; }
    public string? CollectionTool { get; init; }
    public string? CollectionFlowState { get; init; }
    public double? RiverChasu { get; init; }
    // 서식지 하상구성(%)
    public string? Bedrock { get; init; }
    public string? Concrete { get; init; }
    public string? Mud { get; init; }
    public string? Sand { get; init; }
    public string? FineGravel { get; init; }
    public string? Gravel { get; init; }
    public string? SmallStone { get; init; }
    public string? BigStone { get; init; }
    // 서식처
    public string? HabitatRiverType { get; init; }
    public string? HabitatFlowState { get; init; }
    // 채집불가시
    public string? SurveyUnavailableReason { get; init; }
    public string? Note { get; init; }
    /// <summary>출현종 없음(0종) 선언.</summary>
    public bool NoSpeciesDeclared { get; init; }
    // 화면 접힘 상태 — 다음 실행에도 유지된다(README §상태). 옛 파일에는 없으므로 기본 false(접힘).
    public bool IsEnvironmentExpanded { get; init; }
    public bool IsHealthExpanded { get; init; }
    // 비정상종(개체수)
    public string? DeCount { get; init; }
    public string? EfCount { get; init; }
    public string? LeCount { get; init; }
    public string? TuCount { get; init; }
    // 종 목록
    public List<FishDraftRow> Rows { get; init; } = new();
}
