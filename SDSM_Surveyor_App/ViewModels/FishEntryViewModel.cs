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

    private readonly IReferenceRangeProvider _reference;

    public FishEntryViewModel(ISpeciesListProvider speciesProvider, ISessionService sessions,
                 IReferenceRangeProvider reference, SurveyMeta meta)
        : base(sessions, meta)
    {
        _reference = reference;
        SpeciesListSource = speciesProvider.GetFishSpecies();
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
    }


    // ── 어류 고유 기본정보 ──
    [ObservableProperty] private string? _collectionTool;                 // 채집도구
    [ObservableProperty] private double? _riverChasu;                     // 하천차수(FAI 계산)
    [ObservableProperty] private string? _surveyUnavailableReason;        // 특이사항(조사불가→"-")

    // ── 관리자 전체 필드(자동계산 외 전부 입력) : 값은 숫자도 원문 문자열로 보관 후 내보내기 시 파싱 ──
    // 채집방법 확장
    [ObservableProperty] private string? _collectionTime;    // 채집소요시간(분)
    [ObservableProperty] private string? _collectionFlowState; // 흐름상태(채집)
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
    [ObservableProperty] private string? _habitatFlowState;  // 흐름상태(서식처)
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

    // 위 헤더 값 변경 시에도 FAI 재계산
    partial void OnRiverChasuChanged(double? value) => Recalculate();
    partial void OnSurveyUnavailableReasonChanged(string? value) => Recalculate();
    partial void OnDeCountChanged(string? value) => Recalculate();
    partial void OnEfCountChanged(string? value) => Recalculate();
    partial void OnLeCountChanged(string? value) => Recalculate();
    partial void OnTuCountChanged(string? value) => Recalculate();

    public string[] CollectionTools { get; } = { "투망/족대" };

    // ── 초고속 입력 그리드(Center-Left) ──
    /// <summary>종 행. 기반 클래스의 <c>Rows</c> 를 화면 바인딩 이름으로 노출한다.</summary>
    public RadObservableCollection<FishSpeciesEntry> SpeciesEntries => Rows;

    /// <summary>붙여넣기 한 줄 → 행. 열 순서 = [국명, 개체수].
    /// 국명이 공식 종목록에 있으면 학명·길드가 자동으로 연결된다.</summary>
    protected override FishSpeciesEntry CreateRowFromCells(string ko, string[] cells)
    {
        var entry = new FishSpeciesEntry();
        var match = SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko);
        if (match is not null) entry.SelectedSpecies = match;   // 학명·길드 자동 연결
        else entry.SpeciesKo = ko;

        if (cells.Length > 1 && int.TryParse(cells[1].Trim(), out var n))
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

    // ── 실시간 통계(Center-Right) ──
    [ObservableProperty] private int _totalSpeciesCount;
    [ObservableProperty] private int _totalIndividualCount;
    [ObservableProperty] private string _dominantSpecies = "-";
    [ObservableProperty] private double? _faiScore;
    [ObservableProperty] private string _faiGrade = "-";

    // ── 상태바(Bottom) ──


    /// <summary>국명 문자열을 공식 종목록과 대조해 SelectedSpecies(학명·길드·보호종)를 연결.
    /// 엑셀 복사·붙여넣기로 국명만 들어온 경우에도 학명/지수가 채워지도록 한다.</summary>
    private void ResolveSpecies(FishSpeciesEntry row)
    {
        var ko = row.SpeciesKo?.Trim();
        if (string.IsNullOrEmpty(ko)) return;
        if (row.SelectedSpecies?.SpeciesKo == ko) return;   // 이미 일치(자동완성 선택 등)
        var match = SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko);
        if (match is not null && !ReferenceEquals(match, row.SelectedSpecies))
            row.SelectedSpecies = match;                    // 미매칭이면 SelectedSpecies=null 유지(신규종 경고로 처리)
    }

    // 입력 이상치 경고 개수(상태바 표시)
    [ObservableProperty] private int _warningCount;

    partial void OnWarningCountChanged(int value)
    {
        OnPropertyChanged(nameof(WarningText));
        OnPropertyChanged(nameof(HasWarnings));
    }

    public string WarningText => WarningCount > 0 ? $"⚠ 이상치 경고 {WarningCount}건" : string.Empty;

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
            var range = _reference.GetFishRange(ko);
            if (range is null) e.ErrorContent = "과거 기록 없음 (신규 종 확인)";
            else if (cnt.Value > range.Max) e.ErrorContent = $"과거 최대 {range.Max:N0}개체 초과";
            else e.ErrorContent = string.Empty;

            if (e.HasWarning) warn++;
        }
        WarningCount = warn;

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
        DominantSpecies = imports.Where(x => (x.IndividualCount ?? 0) > 0)
                                 .OrderByDescending(x => x.IndividualCount)
                                 .FirstOrDefault()?.SpeciesKo ?? "-";

        // 0종 처리는 계산기가 판단한다(12_CALC_FIX §2)
        //  조사불가 → "-"  /  조사 수행 + 0종 선언 → 0·E  /  미입력 → "-"
        int abnormal = PiOrZero(DeCount) + PiOrZero(EfCount) + PiOrZero(LeCount) + PiOrZero(TuCount);
        var (score, grade) = EcologyCalculator.CalculateFai(
            imports, SurveyUnavailableReason, abnormalCount: abnormal,
            chasu: (int)(RiverChasu ?? 0), noSpeciesDeclared: NoSpeciesDeclared);
        FaiScore = score;
        FaiGrade = grade ?? "-";

        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
    }


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
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("보고서용 엑셀을 내보냈습니다.", true)));
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    /// <summary>[레거시] 관리자 '어류_입력' 전치 일괄입력 양식으로 내보내기(현행 관리자 Import 취합용).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportBulk()
    {
        try
        {
            var saved = Export.FishExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"일괄입력용 엑셀 완료 · {System.IO.Path.GetFileName(saved)}";
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("일괄입력용 엑셀을 내보냈습니다.", true)));
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
}

// ── 임시 저장용 DTO ──
public record FishDraftRow(string? SpeciesKo, int? IndividualCount);

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
    // 비정상종(개체수)
    public string? DeCount { get; init; }
    public string? EfCount { get; init; }
    public string? LeCount { get; init; }
    public string? TuCount { get; init; }
    // 종 목록
    public List<FishDraftRow> Rows { get; init; } = new();
}
