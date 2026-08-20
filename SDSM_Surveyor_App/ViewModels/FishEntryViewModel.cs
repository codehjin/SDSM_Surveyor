using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Models;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Ecology;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Data;   // RadObservableCollection

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>어류 탭: 초고속 입력 + 실시간 통계/FAI + 임시저장/내보내기.</summary>
public partial class FishEntryViewModel : ObservableObject, ITransientService
{
    private const string TaxonKey = "Fish";

    private readonly ISpeciesListProvider _speciesProvider;
    private readonly ILocalDraftStore _draftStore;
    private readonly IReferenceRangeProvider _reference;

    public FishEntryViewModel(ISpeciesListProvider speciesProvider, ILocalDraftStore draftStore,
                              IReferenceRangeProvider reference)
    {
        _speciesProvider = speciesProvider;
        _draftStore = draftStore;
        _reference = reference;

        SpeciesListSource = _speciesProvider.GetFishSpecies();
        FilteredQuick = SpeciesListSource;   // 콤보 초기 목록
        SpeciesEntries.CollectionChanged += OnEntriesChanged;
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
    }

    // ── 공통 조사개황 : 모든 분류군 공유(SurveyOverviewControl에서 입력) ──
    public SurveyMeta Meta { get; } = new();

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

    private static int Pi(string? s) => int.TryParse(s, out var n) ? n : 0;

    // 위 헤더 값 변경 시에도 FAI 재계산
    partial void OnRiverChasuChanged(double? v) => Recalculate();
    partial void OnSurveyUnavailableReasonChanged(string? v) => Recalculate();
    partial void OnDeCountChanged(string? v) => Recalculate();
    partial void OnEfCountChanged(string? v) => Recalculate();
    partial void OnLeCountChanged(string? v) => Recalculate();
    partial void OnTuCountChanged(string? v) => Recalculate();

    public string[] CollectionTools { get; } = { "투망/족대" };

    // ── 초고속 입력 그리드(Center-Left) ──
    public RadObservableCollection<FishSpeciesEntry> SpeciesEntries { get; } = new();

    // 종명 초성 자동완성용 공식 종목록
    [ObservableProperty] private List<FishSpeciesList> _speciesListSource = new();

    // ── 빠른 추가 바(그리드 위) : 초성 검색(RadComboBox·VM에서 필터) → 개체수 → Enter/추가 ──
    [ObservableProperty] private List<FishSpeciesList> _filteredQuick = new();  // 콤보 드롭다운(초성 필터 결과)
    [ObservableProperty] private string? _quickSearch;           // 검색 입력(초성/이름)
    [ObservableProperty] private FishSpeciesList? _quickSpecies; // 선택한 종
    [ObservableProperty] private string? _quickCount;            // 개체수

    partial void OnQuickSearchChanged(string? value) => FilterQuick();

    /// <summary>입력한 초성/이름으로 종목록을 필터해 콤보 드롭다운을 갱신.</summary>
    private void FilterQuick()
    {
        var q = QuickSearch?.Trim();
        FilteredQuick = string.IsNullOrEmpty(q)
            ? SpeciesListSource
            : SpeciesListSource.Where(s => Helpers.ChosungHelper.IsMatch(s.SpeciesKo, q)).Take(80).ToList();
    }

    /// <summary>빠른 추가 후 검색창으로 포커스 복귀(코드비하인드가 구독).</summary>
    public event EventHandler? QuickAddCompleted;

    /// <summary>종을 고르면 개체수 칸으로 포커스 이동(코드비하인드가 구독).</summary>
    public event EventHandler? QuickSpeciesPicked;
    partial void OnQuickSpeciesChanged(FishSpeciesList? value)
    {
        if (value is not null) QuickSpeciesPicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>빠른 추가: 선택 종(또는 입력 문자열) + 개체수로 행을 추가하고 입력칸을 비운다.</summary>
    [RelayCommand]
    private void AddQuick()
    {
        if (QuickSpecies is null) return;   // 목록에서 종을 선택해야 추가(오타 방지)

        var entry = new FishSpeciesEntry { SelectedSpecies = QuickSpecies };
        if (int.TryParse(QuickCount, out var n)) entry.IndividualCount = n;
        SpeciesEntries.Add(entry);

        QuickSpecies = null;
        QuickCount = null;
        QuickSearch = null;   // 콤보 텍스트 비움 + 목록 원복
        QuickAddCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>엑셀에서 복사한 [국명, 개체수] 여러 줄을 그리드 행으로 추가(그리드 Ctrl+V에서 호출).
    /// RadGridView 기본 붙여넣기가 커스텀 편집기 컬럼과 충돌해 한 칸에 뭉치므로 직접 파싱한다.</summary>
    public void PasteRows(string clipboard)
    {
        if (string.IsNullOrWhiteSpace(clipboard)) return;
        var lines = clipboard.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = line.Split('\t');
            var ko = cells[0].Trim();
            if (string.IsNullOrEmpty(ko)) continue;

            var entry = new FishSpeciesEntry();
            var match = SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko);
            if (match is not null) entry.SelectedSpecies = match;   // 학명·길드 자동 연결
            else entry.SpeciesKo = ko;

            if (cells.Length > 1 && int.TryParse(cells[1].Trim(), out var n))
                entry.IndividualCount = n;

            SpeciesEntries.Add(entry);
        }
    }

    /// <summary>국명·개체수가 모두 빈 행 제거(붙여넣기·오클릭으로 생긴 빈 행 정리).</summary>
    [RelayCommand]
    private void PruneEmpty()
    {
        var empties = SpeciesEntries
            .Where(r => string.IsNullOrWhiteSpace(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo) && r.IndividualCount is null)
            .ToList();
        foreach (var e in empties) SpeciesEntries.Remove(e);
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
    [ObservableProperty] private string _statusText = "임시 저장 없음";
    [ObservableProperty] private DateTime? _lastSavedTime;

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (FishSpeciesEntry r in e.OldItems) r.PropertyChanged -= OnRowChanged;
        if (e.NewItems is not null)
            foreach (FishSpeciesEntry r in e.NewItems) r.PropertyChanged += OnRowChanged;
        Recalculate();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 붙여넣기/직접입력으로 국명만 채워진 행 → 공식 종목록과 자동 매칭
        if (e.PropertyName == nameof(FishSpeciesEntry.SpeciesKo) && sender is FishSpeciesEntry row)
            ResolveSpecies(row);

        if (e.PropertyName is nameof(FishSpeciesEntry.IndividualCount)
                           or nameof(FishSpeciesEntry.SelectedSpecies)
                           or nameof(FishSpeciesEntry.SpeciesKo))
            Recalculate();
    }

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
    partial void OnWarningCountChanged(int value) => OnPropertyChanged(nameof(WarningText));
    public string WarningText => WarningCount > 0 ? $"⚠ 이상치 경고 {WarningCount}건" : string.Empty;

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
    }

    /// <summary>입력 즉시 실시간 통계·FAI 재계산.</summary>
    private void Recalculate()
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
        int abnormal = Pi(DeCount) + Pi(EfCount) + Pi(LeCount) + Pi(TuCount);
        var (score, grade) = EcologyCalculator.CalculateFai(
            imports, SurveyUnavailableReason, abnormalCount: abnormal,
            chasu: (int)(RiverChasu ?? 0), noSpeciesDeclared: NoSpeciesDeclared);
        FaiScore = score;
        FaiGrade = grade ?? "-";

        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddRow() => SpeciesEntries.Add(new FishSpeciesEntry());

    [RelayCommand]
    private void RemoveRow(FishSpeciesEntry? row)
    {
        if (row is not null) SpeciesEntries.Remove(row);
    }

    /// <summary>오프라인 로컬 임시 저장(AppData JSON).</summary>
    [RelayCommand]
    private async Task SaveTemporary()
    {
        var draft = new FishDraft
        {
            SurveyYear = Meta.SurveyYear, YearChsu = Meta.YearChsu, SurveyDate = Meta.SurveyDate,
            MajorRegion = Meta.MajorRegion, MiddleRegion = Meta.MiddleRegion, River = Meta.River, RiverType = Meta.RiverType,
            Site = Meta.Site, Lat = Meta.Lat, Lng = Meta.Lng, Weather = Meta.Weather, SurveyAgency = Meta.SurveyAgency, Surveyor = Meta.Surveyor,
            CollectionTime = CollectionTime, CollectionTool = CollectionTool,
            CollectionFlowState = CollectionFlowState, RiverChasu = RiverChasu,
            Bedrock = Bedrock, Concrete = Concrete, Mud = Mud, Sand = Sand,
            FineGravel = FineGravel, Gravel = Gravel, SmallStone = SmallStone, BigStone = BigStone,
            HabitatRiverType = HabitatRiverType, HabitatFlowState = HabitatFlowState,
            SurveyUnavailableReason = SurveyUnavailableReason, Note = Note,
            NoSpeciesDeclared = NoSpeciesDeclared,
            DeCount = DeCount, EfCount = EfCount, LeCount = LeCount, TuCount = TuCount,
            Rows = SpeciesEntries.Select(r => new FishDraftRow(r.SpeciesKo, r.IndividualCount)).ToList()
        };

        await _draftStore.SaveDraftAsync(TaxonKey, draft);

        LastSavedTime = DateTime.Now;
        StatusText = $"임시 저장됨 · {LastSavedTime:HH:mm:ss}";
        WeakReferenceMessenger.Default.Send(new NotifyMessage(("임시 저장되었습니다.", true)));
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
