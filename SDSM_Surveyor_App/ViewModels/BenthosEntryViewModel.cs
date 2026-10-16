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
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>저서동물 탭: 초고속 입력 + 실시간 지수(DI/H'/R1/J'/BMI) + 임시저장/내보내기.
/// (자동계산 외 관리자 전체 필드 입력 · 빠른 추가 바 · 엑셀 붙여넣기)</summary>
public partial class BenthosEntryViewModel : SpeciesEntryViewModelBase<BenthosSpeciesEntry, BenthosSpeciesList>, ISingletonService
{
    internal const string TaxonKey = "Benthos";

    private readonly IReferenceRangeProvider _reference;

    public BenthosEntryViewModel(ISpeciesListProvider speciesProvider, ISessionService sessions,
                 IReferenceRangeProvider reference, SurveyMeta meta)
        : base(sessions, meta)
    {
        _reference = reference;
        SpeciesListSource = speciesProvider.GetBenthosSpecies();
        _speciesCatalogVersion = speciesProvider.Version ?? "-";
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
    }


    // ── 채집방법(정량 채집기 회수) ──
    [ObservableProperty] private string? _surbernet30;      // Surber net 30×30
    [ObservableProperty] private string? _surbernet50;      // Surber net 50×50
    [ObservableProperty] private string? _dredge;           // 드렛지
    [ObservableProperty] private string? _ekman;            // 에크만

    // ── 서식처① ──
    [ObservableProperty] private string? _watershed;        // 유역이용
    [ObservableProperty] private string? _pollutionSource;  // 확인 가능 오염원
    [ObservableProperty] private string? _canopyCover;      // 식생 수피도
    [ObservableProperty] private string? _floodplain;       // 범람원의 이용
    [ObservableProperty] private string? _leveeLeft;        // 제방(좌안)
    [ObservableProperty] private string? _leveeRight;       // 제방(우안)

    // ── 서식지 하상구성(%) ──
    [ObservableProperty] private string? _bedrock;          // 암반
    [ObservableProperty] private string? _concrete;         // 콘크리트
    [ObservableProperty] private string? _mud;              // 진흙이하(<0.063mm)
    [ObservableProperty] private string? _sand;             // 모래(0.063-2mm)
    [ObservableProperty] private string? _fineGravel;       // 잔자갈(2-16mm)
    [ObservableProperty] private string? _gravel;           // 자갈(16-64mm)
    [ObservableProperty] private string? _smallStone;       // 작은돌(64-256mm)
    [ObservableProperty] private string? _bigStone;         // 큰돌(>256mm)

    // ── 서식처②(수리·환경) ──
    [ObservableProperty] private string? _habitatRiverType; // 하천유형
    [ObservableProperty] private string? _riverWidth;       // 하폭(m)
    [ObservableProperty] private string? _waterWidth;       // 수폭(m)
    [ObservableProperty] private string? _averageDepth;     // 평균수심(cm)
    [ObservableProperty] private string? _averageVelocity;  // 평균유속(cm/s)
    [ObservableProperty] private string? _airTemperature;   // 기온
    [ObservableProperty] private string? _waterTemperature; // 수온
    [ObservableProperty] private string? _flowState;        // 흐름상태
    [ObservableProperty] private string? _transparency;     // 투명도
    [ObservableProperty] private string? _smell;            // 냄새

    // ── 채집불가시 ──
    [ObservableProperty] private string? _surveyUnavailableReason;  // 채집불가시 → 등급 "-"
    [ObservableProperty] private string? _note;                     // 특이사항

    // ── 입력 그리드 ──
    /// <summary>종 행. 기반 클래스의 <c>Rows</c> 를 화면 바인딩 이름으로 노출한다.</summary>
    public RadObservableCollection<BenthosSpeciesEntry> SpeciesEntries => Rows;
    // ⚠ 종목록(SpeciesListSource)은 기반 클래스 것을 쓴다. 여기서 같은 이름을 다시 선언하면
    //    기반 클래스 속성을 가려 빠른추가 후보(FilteredQuick)가 늘 비어 있었다(B-3 · 2026-10-15 확인).

    /// <summary>붙여넣기 한 줄 → 행. 열 순서 = [국명, 개체수].
    /// 국명이 공식 종목록에 있으면 학명·길드가 자동으로 연결된다.</summary>
    protected override BenthosSpeciesEntry CreateRowFromCells(string ko, string[] cells)
    {
        var entry = new BenthosSpeciesEntry();
        var match = SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko);
        if (match is not null) entry.SelectedSpecies = match;   // 학명·길드 자동 연결
        else entry.SpeciesKo = ko;

        if (cells.Length > 1 && double.TryParse(cells[1].Trim(), out var n))
            entry.IndividualCount = n;

        return entry;
    }


    // ── 실시간 통계·지수 ──
    // ── 접힘 상태 (세션 저장 · 기본 접힘) ────────────────────────────────
    [ObservableProperty] private bool _isEnvironmentExpanded;
    [ObservableProperty] private bool _isHealthExpanded;

    [ObservableProperty] private int _totalSpeciesCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalShareText))]
    private double _totalIndividualCount;

    [ObservableProperty] private string _dominantSpecies = "-";
    [ObservableProperty] private double? _dominantShare;

    /// <summary>합계 행의 상대풍부도 칸. 개체가 없으면 100% 가 아니라 `·`(미산출).</summary>
    public string TotalShareText => TotalIndividualCount > 0 ? "100%" : "·";

    /// <summary>행이 하나도 없는가(빈 상태 안내 · README §11.4).</summary>
    public bool HasNoRows => SpeciesEntries.Count == 0;

    [ObservableProperty] private int _protectedCount;
    [ObservableProperty] private int _endemicCount;

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

    /// <summary>건강성 카드 머리글 옆 캡션 — 계산 출처를 밝힌다.</summary>
    public string HealthCaption =>
        $"관리자 SDSM_Core 와 동일 계산 · 종목록 버전 {_speciesCatalogVersion}";

    private readonly string _speciesCatalogVersion;

    // ── 조사환경 요약줄 ──────────────────────────────────────────────────
    /// <summary>하상구성 8칸 중 값이 들어간 칸 수.</summary>
    public string SubstrateSummary
    {
        get
        {
            var filled = new[] { Bedrock, Concrete, Mud, Sand, FineGravel, Gravel, SmallStone, BigStone }
                         .Count(v => !string.IsNullOrWhiteSpace(v));
            return filled == 0 ? "·" : $"{filled}칸 입력됨";
        }
    }

    /// <summary>정량 채집기 4종 중 사용한 것.</summary>
    public string GearSummary
    {
        get
        {
            var used = new List<string>();
            if (!string.IsNullOrWhiteSpace(Surbernet30)) used.Add("Surber 30");
            if (!string.IsNullOrWhiteSpace(Surbernet50)) used.Add("Surber 50");
            if (!string.IsNullOrWhiteSpace(Dredge)) used.Add("드렛지");
            if (!string.IsNullOrWhiteSpace(Ekman)) used.Add("에크만");
            return used.Count == 0 ? "·" : string.Join(" · ", used);
        }
    }

    /// <summary>하상구성 8칸(화면 이름, 값). 합계·정수 점검이 같이 쓴다.</summary>
    private (string Label, string? Value)[] SubstrateFields => new[]
    {
        ("암반", Bedrock), ("콘크리트", Concrete), ("진흙", Mud), ("모래", Sand),
        ("잔자갈", FineGravel), ("자갈", Gravel), ("작은돌", SmallStone), ("큰돌", BigStone),
    };

    /// <summary>하상구성 합계(%). 지침상 100 이어야 한다. 전부 비면 null. (어류와 같은 규칙 · B-6)</summary>
    public double? SubstrateTotal
    {
        get
        {
            var vals = SubstrateFields.Select(f => Pd(f.Value)).Where(v => v is not null).Select(v => v!.Value).ToList();
            return vals.Count == 0 ? null : vals.Sum();
        }
    }

    /// <summary>
    /// 하상구성 경고 — 합계가 100 이 아니거나, 0~100 정수가 아닌 칸이 있으면.
    /// 최종본 실제 제출 자료에 합계 90% 가 있었다(B-6). 막지 않고 알리기만 한다. 8칸이 모두 비면 경고하지 않는다.
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

    /// <summary>정량 채집기 횟수가 정수가 아니면 경고(B-6 · 예전 세션 값은 지우지 않는다).</summary>
    public string GearWarning
    {
        get
        {
            var bad = WholeNumber.Describe(new[]
            {
                ("Surber 30", Surbernet30), ("Surber 50", Surbernet50), ("드렛지", Dredge), ("에크만", Ekman),
            });
            return bad.Length == 0 ? string.Empty : $"채집기 횟수가 정수가 아닌 칸: {bad}";
        }
    }

    /// <summary>접힌 요약줄에 띄우는 조사환경 경고(채집기 + 하상구성).</summary>
    public string EnvironmentWarning =>
        string.Join(" · ", new[] { GearWarning, SubstrateWarning }.Where(s => s.Length > 0));

    private void RaiseEnvironmentSummary()
    {
        OnPropertyChanged(nameof(SubstrateSummary));
        OnPropertyChanged(nameof(GearSummary));
        OnPropertyChanged(nameof(SubstrateTotal));
        OnPropertyChanged(nameof(SubstrateWarning));
        OnPropertyChanged(nameof(GearWarning));
        OnPropertyChanged(nameof(EnvironmentWarning));
    }

    partial void OnBedrockChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnConcreteChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnMudChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnSandChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnFineGravelChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnGravelChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnSmallStoneChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnBigStoneChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnSurbernet30Changed(string? value) => RaiseEnvironmentSummary();
    partial void OnSurbernet50Changed(string? value) => RaiseEnvironmentSummary();
    partial void OnDredgeChanged(string? value) => RaiseEnvironmentSummary();
    partial void OnEkmanChanged(string? value) => RaiseEnvironmentSummary();

    /// <summary>빠른추가로 들어온 행을 1.2초간 강조한다(기반 클래스가 타이머를 돌린다).</summary>
    protected override void SetJustAdded(BenthosSpeciesEntry row, bool value) => row.IsJustAdded = value;
    // 자료 0건일 때는 null → 화면에 "-" 로 표시(0.000/1.000 오해 방지)
    [ObservableProperty] private double? _indexDI;
    [ObservableProperty] private double? _indexH;
    [ObservableProperty] private double? _indexR1;
    [ObservableProperty] private double? _indexJ;
    [ObservableProperty] private double? _bmiScore;
    [ObservableProperty] private string _bmiGrade = "-";

    // 상태바


    /// <summary>국명 문자열을 공식 종목록과 대조해 SelectedSpecies(학명·오탁치·가중치)를 연결.</summary>
    private void ResolveSpecies(BenthosSpeciesEntry row)
    {
        var ko = row.SpeciesKo?.Trim();
        if (string.IsNullOrEmpty(ko)) return;
        if (row.SelectedSpecies?.SpeciesKo == ko) return;
        var match = SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko);
        if (match is not null)
        {
            if (!ReferenceEquals(match, row.SelectedSpecies)) row.SelectedSpecies = match;
        }
        else if (row.SelectedSpecies is not null)
        {
            // 목록 밖 국명으로 고쳤다(B-4) — 예전 종의 오탁치·가중치가 남으면 BMI 가 그 종으로 된다.
            row.SelectedSpecies = null;
        }
    }

    // 입력 이상치 경고 개수(상태바 표시) — 이상치 + 같은 종 중복 행을 합친 수다.
    [ObservableProperty] private int _warningCount;

    // 상태바 문구를 두 갈래로 나눠 적으려고 따로 센다(이상치 n건 · 같은 종 중복 k종).
    private int _outlierRows;
    private int _unlistedRows;
    private int _duplicateSpecies;

    /// <summary>경고 영역 표시 여부(06_DESIGN_REBUILD §5-2-1). 경고는 비차단이다 — 저장을 막지 않는다.</summary>
    public bool HasWarnings => WarningCount > 0;

    /// <summary>경고가 걸린 종 이름. 종전에는 건수만 보여 어느 종인지 알 수 없었다(§5-2-1).</summary>
    [ObservableProperty] private string _warningDetail = string.Empty;
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

    private void ValidateRows()
    {
        // 같은 종이 여러 행 — 불러온 세션에 이미 있던 중복은 자동으로 합치지 않고 알리기만 한다(B-1).
        // BMI 는 행 단위라 두 행이면 오탁치·지표가중치가 이중으로 더해진다.
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
                var r = _reference.GetBenthosRange(ko);
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

    /// <summary>조사불가 사유가 선언되어 등급이 "-"가 되는 상태인지.</summary>
    private bool IsUnavailable =>
        !string.IsNullOrWhiteSpace(SurveyUnavailableReason)
        && UnavailableReasons.Contains(SurveyUnavailableReason.Replace(" ", ""));

    private static readonly string[] UnavailableReasons = { "접근불가", "건천화", "준설", "공사중" };

    /// <summary>출현종 없음(0종) 선언 — 조사는 수행했으나 한 종도 확인되지 않았다는 뜻.
    /// 종 목록이 비었다는 사실만으로는 "아직 미입력"과 구분할 수 없어 명시적으로 선언받는다(12_CALC_FIX §2-1).</summary>
    [ObservableProperty] private bool _noSpeciesDeclared;
    partial void OnNoSpeciesDeclaredChanged(bool value) => Recalculate();

    // 조사불가 사유는 등급을 "-"로 바꾸므로 입력 즉시 다시 계산한다(어류와 동일).
    partial void OnSurveyUnavailableReasonChanged(string? value) => Recalculate();

    protected override void Recalculate()
    {
        ValidateRows();

        var entries = SpeciesEntries.ToList();

        // 1) 개체수 배열 → 각 행 RankScorer 산정 (null=0 처리)
        int[] counts = entries.Select(e => e.IndividualCount.HasValue ? (int)e.IndividualCount.Value : 0).ToArray();
        foreach (var e in entries)
        {
            int v = e.IndividualCount.HasValue ? (int)e.IndividualCount.Value : 0;
            e.RankScorer = BenthosCalculator.GetRankScorer(counts, v);
        }

        // 2) 표준 모델로 변환 후 지수 계산
        var imports = entries.Select(e => e.ToImport()).ToList();

        TotalSpeciesCount = imports.Count(x => (x.IndividualCount ?? 0) > 0);
        TotalIndividualCount = imports.Sum(x => x.IndividualCount ?? 0);
        DominantSpecies = imports.Where(x => (x.IndividualCount ?? 0) > 0)
                                 .OrderByDescending(x => x.IndividualCount)
                                 .FirstOrDefault()?.SpeciesKo ?? "-";

        // 다양도 지수는 유효 개체가 있어야 의미가 있다(0건인데 DI 1.000 등으로 보이는 문제 방지)
        if (TotalSpeciesCount == 0)
        {
            IndexDI = IndexH = IndexR1 = IndexJ = null;
        }
        else
        {
            IndexDI = Math.Round(BenthosCalculator.GetDI(imports), 3, MidpointRounding.AwayFromZero);
            IndexH  = Math.Round(BenthosCalculator.GetH(imports),  3, MidpointRounding.AwayFromZero);
            IndexR1 = Math.Round(BenthosCalculator.GetR1(imports), 3, MidpointRounding.AwayFromZero);
            IndexJ  = Math.Round(BenthosCalculator.GetJ(imports),  3, MidpointRounding.AwayFromZero);
        }

        var dominant = imports.Where(x => (x.IndividualCount ?? 0) > 0)
                              .OrderByDescending(x => x.IndividualCount).FirstOrDefault();
        DominantShare = TotalIndividualCount > 0 && dominant is not null
            ? (dominant.IndividualCount ?? 0) / TotalIndividualCount * 100.0 : null;

        // 행 번호 · 상대풍부도 · 태그 집계
        // ⚠ 여기서 건드리는 속성은 AffectsRecalculation 에 없어야 한다(무한 재계산 방지).
        int n = 0, prot = 0, endemic = 0, missing = 0;
        foreach (var row in SpeciesEntries)
        {
            row.RowNumber = ++n;
            row.RelativeAbundance = TotalIndividualCount > 0 && row.IndividualCount is double c && c > 0
                ? c / TotalIndividualCount * 100.0 : null;

            if (string.IsNullOrWhiteSpace(row.SelectedSpecies?.SpeciesKo ?? row.SpeciesKo)) continue;
            if (row.IndividualCount is null) missing++;
            if ((row.IndividualCount ?? 0) <= 0) continue;
            if (row.IsProtected) prot++;
            if (row.IsEndemic) endemic++;
        }
        ProtectedCount = prot;
        EndemicCount = endemic;
        MissingCountRows = missing;
        OnPropertyChanged(nameof(HasNoRows));

        // 0종 처리는 계산기가 판단한다(12_CALC_FIX §2-3)
        //  조사불가 → "-"  /  조사 수행 + 0종 선언 → 0·E  /  미입력 → "-"
        var (bmi, bmiGrade) = BenthosCalculator.GetBMI(imports, SurveyUnavailableReason, NoSpeciesDeclared);
        BmiScore = bmi;
        BmiGrade = bmiGrade ?? "-";

        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
    }

    /// <summary>보고서·기록용 엑셀 내보내기(주력).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportExcel()
    {
        try
        {
            var saved = Export.BenthosReportExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"보고서용 엑셀 완료 · {System.IO.Path.GetFileName(saved)}";
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("보고서용 엑셀을 내보냈습니다.", true)));
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    /// <summary>[레거시] 관리자 일괄입력 양식으로 내보내기(현행 관리자 Import 취합용).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportBulk()
    {
        try
        {
            var saved = Export.BenthosExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"일괄입력용 완료 · {System.IO.Path.GetFileName(saved)}";
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("일괄입력용 엑셀을 내보냈습니다.", true)));
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    // 유효 개체가 하나라도 있거나, 0종 선언·조사불가가 선언되면 내보낼 수 있다(12_CALC_FIX §2-2).
    // 이상치 경고는 알림일 뿐 제재가 아니므로 내보내기를 막지 않는다.
    private bool CanExport() =>
        SpeciesEntries.Any(r => (r.IndividualCount ?? 0) > 0) || NoSpeciesDeclared || IsUnavailable;
// ── 기반 클래스가 요구하는 분류군 고유 동작 ──────────────────────────────

    protected override string? SpeciesKoOf(BenthosSpeciesList species) => species.SpeciesKo;

    protected override BenthosSpeciesEntry CreateRow(BenthosSpeciesList species, string? count)
    {
        var entry = new BenthosSpeciesEntry { SelectedSpecies = species };
        if (double.TryParse(count, out var n)) entry.IndividualCount = n;
        return entry;
    }

    protected override bool IsRowEmpty(BenthosSpeciesEntry row) =>
        string.IsNullOrWhiteSpace(row.SelectedSpecies?.SpeciesKo ?? row.SpeciesKo) && row.IndividualCount is null;

    protected override bool AffectsRecalculation(string? propertyName) =>
        propertyName is nameof(BenthosSpeciesEntry.IndividualCount)
                     or nameof(BenthosSpeciesEntry.SelectedSpecies)
                     or nameof(BenthosSpeciesEntry.SpeciesKo);

    /// <summary>
    /// 국명을 직접 친 경우에도 공식 종목록과 잇는다(B-4).
    /// ⚠ 종전에는 이 연결이 붙여넣기 경로에만 있었다 — 그리드에 친 국명은 오탁치·가중치가 비어 BMI 가 틀렸다.
    /// </summary>
    protected override void OnRowPropertyChanged(BenthosSpeciesEntry row, string? propertyName)
    {
        if (propertyName == nameof(BenthosSpeciesEntry.SpeciesKo)) ResolveSpecies(row);
    }

    // ── 같은 종 중복 : 합산 (B-1) ─────────────────────────────────────────────

    protected override DuplicateSpeciesMode DuplicateMode => DuplicateSpeciesMode.Merge;

    protected override string? SpeciesKeyOf(BenthosSpeciesEntry row) =>
        (row.SelectedSpecies?.SpeciesKo ?? row.SpeciesKo)?.Trim();

    protected override (string Before, string Added, string After) MergeInto(BenthosSpeciesEntry target, BenthosSpeciesEntry source)
    {
        var before = target.IndividualCount;
        var added = source.IndividualCount;
        target.IndividualCount = CountMath.Sum(before, added);   // 결측 보존 — null 을 0 으로 채우지 않는다
        target.Note = CountMath.JoinNote(target.Note, source.Note);
        return (CountMath.Show(before), CountMath.Show(added), CountMath.Show(target.IndividualCount));
    }
}

// ── 임시 저장용 DTO ──
/// <summary>세션에 저장되는 종 행. <c>Note</c> 는 선택 매개변수라 옛 세션 파일도 그대로 읽힌다.</summary>
public record BenthosDraftRow(string? SpeciesKo, double? IndividualCount, string? Note = null);

public sealed class BenthosDraft
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
    public string? Surbernet30 { get; init; }
    public string? Surbernet50 { get; init; }
    public string? Dredge { get; init; }
    public string? Ekman { get; init; }
    // 서식처①
    public string? Watershed { get; init; }
    public string? PollutionSource { get; init; }
    public string? CanopyCover { get; init; }
    public string? Floodplain { get; init; }
    public string? LeveeLeft { get; init; }
    public string? LeveeRight { get; init; }
    // 서식지 하상구성(%)
    public string? Bedrock { get; init; }
    public string? Concrete { get; init; }
    public string? Mud { get; init; }
    public string? Sand { get; init; }
    public string? FineGravel { get; init; }
    public string? Gravel { get; init; }
    public string? SmallStone { get; init; }
    public string? BigStone { get; init; }
    // 서식처②
    public string? HabitatRiverType { get; init; }
    public string? RiverWidth { get; init; }
    public string? WaterWidth { get; init; }
    public string? AverageDepth { get; init; }
    public string? AverageVelocity { get; init; }
    public string? AirTemperature { get; init; }
    public string? WaterTemperature { get; init; }
    public string? FlowState { get; init; }
    public string? Transparency { get; init; }
    public string? Smell { get; init; }
    // 채집불가시
    public string? SurveyUnavailableReason { get; init; }
    public string? Note { get; init; }
    /// <summary>출현종 없음(0종) 선언.</summary>
    public bool NoSpeciesDeclared { get; init; }
    // 화면 접힘 상태 — 다음 실행에도 유지된다. 옛 파일에는 없으므로 기본 false(접힘).
    public bool IsEnvironmentExpanded { get; init; }
    public bool IsHealthExpanded { get; init; }
    // 종 목록
    public List<BenthosDraftRow> Rows { get; init; } = new();
}
