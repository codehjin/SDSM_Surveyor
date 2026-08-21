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
    [ObservableProperty] private List<BenthosSpeciesList> _speciesListSource = new();

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
    [ObservableProperty] private int _totalSpeciesCount;
    [ObservableProperty] private double _totalIndividualCount;
    [ObservableProperty] private string _dominantSpecies = "-";
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
        if (match is not null && !ReferenceEquals(match, row.SelectedSpecies))
            row.SelectedSpecies = match;
    }

    // 입력 이상치 경고 개수(상태바 표시)
    [ObservableProperty] private int _warningCount;
    partial void OnWarningCountChanged(int value) => OnPropertyChanged(nameof(WarningText));
    public string WarningText => WarningCount > 0 ? $"⚠ 이상치 경고 {WarningCount}건" : string.Empty;

    private void ValidateRows()
    {
        int warn = 0;
        foreach (var e in SpeciesEntries)
        {
            var ko = e.SelectedSpecies?.SpeciesKo ?? e.SpeciesKo;
            var cnt = e.IndividualCount;
            if (string.IsNullOrWhiteSpace(ko) || cnt is null || cnt <= 0)
            {
                e.ErrorContent = string.Empty;
                continue;
            }
            var range = _reference.GetBenthosRange(ko);
            if (range is null) e.ErrorContent = "과거 기록 없음 (신규 종 확인)";
            else if (cnt.Value > range.Max) e.ErrorContent = $"과거 최대 {range.Max:N0}개체 초과";
            else e.ErrorContent = string.Empty;

            if (e.HasWarning) warn++;
        }
        WarningCount = warn;
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
}

// ── 임시 저장용 DTO ──
public record BenthosDraftRow(string? SpeciesKo, double? IndividualCount);

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
    // 종 목록
    public List<BenthosDraftRow> Rows { get; init; } = new();
}
