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
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>저서동물 탭: 초고속 입력 + 실시간 지수(DI/H'/R1/J'/BMI) + 임시저장/내보내기.
/// (자동계산 외 관리자 전체 필드 입력 · 빠른 추가 바 · 엑셀 붙여넣기)</summary>
public partial class BenthosEntryViewModel : ObservableObject, ITransientService
{
    private const string TaxonKey = "Benthos";

    private readonly ISpeciesListProvider _speciesProvider;
    private readonly ILocalDraftStore _draftStore;
    private readonly IReferenceRangeProvider _reference;

    public BenthosEntryViewModel(ISpeciesListProvider speciesProvider, ILocalDraftStore draftStore,
                                 IReferenceRangeProvider reference)
    {
        _speciesProvider = speciesProvider;
        _draftStore = draftStore;
        _reference = reference;

        SpeciesListSource = _speciesProvider.GetBenthosSpecies();
        FilteredQuick = SpeciesListSource;   // 콤보 초기 목록
        SpeciesEntries.CollectionChanged += OnEntriesChanged;
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
    }

    // ── 공통 조사개황 : 모든 분류군 공유(SurveyOverviewControl에서 입력) ──
    public SurveyMeta Meta { get; } = new();

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
    public RadObservableCollection<BenthosSpeciesEntry> SpeciesEntries { get; } = new();
    [ObservableProperty] private List<BenthosSpeciesList> _speciesListSource = new();

    // ── 빠른 추가 바(그리드 위) : 초성 검색(RadComboBox·VM에서 필터) → 개체수 → Enter/추가 ──
    [ObservableProperty] private List<BenthosSpeciesList> _filteredQuick = new();
    [ObservableProperty] private string? _quickSearch;
    [ObservableProperty] private BenthosSpeciesList? _quickSpecies;
    [ObservableProperty] private string? _quickCount;

    partial void OnQuickSearchChanged(string? value) => FilterQuick();

    private void FilterQuick()
    {
        var q = QuickSearch?.Trim();
        FilteredQuick = string.IsNullOrEmpty(q)
            ? SpeciesListSource
            : SpeciesListSource.Where(s => Helpers.ChosungHelper.IsMatch(s.SpeciesKo, q)).Take(80).ToList();
    }

    /// <summary>종을 고르면 개체수 칸으로 포커스 이동(코드비하인드가 구독).</summary>
    public event EventHandler? QuickSpeciesPicked;
    partial void OnQuickSpeciesChanged(BenthosSpeciesList? value)
    {
        if (value is not null) QuickSpeciesPicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>빠른 추가 후 검색 콤보로 포커스 복귀(코드비하인드가 구독).</summary>
    public event EventHandler? QuickAddCompleted;

    [RelayCommand]
    private void AddQuick()
    {
        if (QuickSpecies is null) return;

        var entry = new BenthosSpeciesEntry { SelectedSpecies = QuickSpecies };
        if (double.TryParse(QuickCount, out var n)) entry.IndividualCount = n;
        SpeciesEntries.Add(entry);

        QuickSpecies = null;
        QuickCount = null;
        QuickSearch = null;
        QuickAddCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>엑셀에서 복사한 [국명, 개체수] 여러 줄을 그리드 행으로 추가(그리드 Ctrl+V에서 호출).</summary>
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

            var entry = new BenthosSpeciesEntry();
            var match = SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko);
            if (match is not null) entry.SelectedSpecies = match;
            else entry.SpeciesKo = ko;

            if (cells.Length > 1 && double.TryParse(cells[1].Trim(), out var n))
                entry.IndividualCount = n;

            SpeciesEntries.Add(entry);
        }
    }

    /// <summary>국명·개체수가 모두 빈 행 제거.</summary>
    [RelayCommand]
    private void PruneEmpty()
    {
        var empties = SpeciesEntries
            .Where(r => string.IsNullOrWhiteSpace(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo) && r.IndividualCount is null)
            .ToList();
        foreach (var e in empties) SpeciesEntries.Remove(e);
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
    [ObservableProperty] private string _statusText = "임시 저장 없음";
    [ObservableProperty] private DateTime? _lastSavedTime;

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (BenthosSpeciesEntry r in e.OldItems) r.PropertyChanged -= OnRowChanged;
        if (e.NewItems is not null)
            foreach (BenthosSpeciesEntry r in e.NewItems) r.PropertyChanged += OnRowChanged;
        Recalculate();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 붙여넣기/직접입력으로 국명만 채워진 행 → 공식 종목록과 자동 매칭
        if (e.PropertyName == nameof(BenthosSpeciesEntry.SpeciesKo) && sender is BenthosSpeciesEntry row)
            ResolveSpecies(row);

        if (e.PropertyName is nameof(BenthosSpeciesEntry.IndividualCount)
                           or nameof(BenthosSpeciesEntry.SelectedSpecies)
                           or nameof(BenthosSpeciesEntry.SpeciesKo))
            Recalculate();
    }

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

    private void Recalculate()
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

        // 유효 개체가 하나도 없으면 지수를 계산하지 않는다(0건인데 DI 1.000 등으로 보이는 문제 방지)
        if (TotalSpeciesCount == 0)
        {
            IndexDI = IndexH = IndexR1 = IndexJ = null;
            BmiScore = null;
            BmiGrade = "-";
        }
        else
        {
            IndexDI = Math.Round(BenthosCalculator.GetDI(imports), 3, MidpointRounding.AwayFromZero);
            IndexH  = Math.Round(BenthosCalculator.GetH(imports),  3, MidpointRounding.AwayFromZero);
            IndexR1 = Math.Round(BenthosCalculator.GetR1(imports), 3, MidpointRounding.AwayFromZero);
            IndexJ  = Math.Round(BenthosCalculator.GetJ(imports),  3, MidpointRounding.AwayFromZero);

            var (score, grade) = BenthosCalculator.GetBMI(imports, SurveyUnavailableReason);
            BmiScore = score;
            BmiGrade = grade ?? "-";
        }

        ExportExcelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveTemporary()
    {
        var draft = new BenthosDraft
        {
            SurveyYear = Meta.SurveyYear, YearChsu = Meta.YearChsu, SurveyDate = Meta.SurveyDate,
            MajorRegion = Meta.MajorRegion, MiddleRegion = Meta.MiddleRegion, River = Meta.River, RiverType = Meta.RiverType,
            Site = Meta.Site, Lat = Meta.Lat, Lng = Meta.Lng, Weather = Meta.Weather, SurveyAgency = Meta.SurveyAgency, Surveyor = Meta.Surveyor,
            Surbernet30 = Surbernet30, Surbernet50 = Surbernet50, Dredge = Dredge, Ekman = Ekman,
            Watershed = Watershed, PollutionSource = PollutionSource, CanopyCover = CanopyCover,
            Floodplain = Floodplain, LeveeLeft = LeveeLeft, LeveeRight = LeveeRight,
            Bedrock = Bedrock, Concrete = Concrete, Mud = Mud, Sand = Sand,
            FineGravel = FineGravel, Gravel = Gravel, SmallStone = SmallStone, BigStone = BigStone,
            HabitatRiverType = HabitatRiverType, RiverWidth = RiverWidth, WaterWidth = WaterWidth,
            AverageDepth = AverageDepth, AverageVelocity = AverageVelocity,
            AirTemperature = AirTemperature, WaterTemperature = WaterTemperature,
            FlowState = FlowState, Transparency = Transparency, Smell = Smell,
            SurveyUnavailableReason = SurveyUnavailableReason, Note = Note,
            Rows = SpeciesEntries.Select(r => new BenthosDraftRow(r.SpeciesKo, r.IndividualCount)).ToList()
        };

        await _draftStore.SaveDraftAsync(TaxonKey, draft);

        LastSavedTime = DateTime.Now;
        StatusText = $"임시 저장됨 · {LastSavedTime:HH:mm:ss}";
        WeakReferenceMessenger.Default.Send(new NotifyMessage(("임시 저장되었습니다.", true)));
    }

    /// <summary>관리자 '입력'(저서) 일괄입력 양식으로 내보내기.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportExcel()
    {
        try
        {
            var saved = Export.BenthosExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"엑셀 내보내기 완료 · {System.IO.Path.GetFileName(saved)}";
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("엑셀을 내보냈습니다.", true)));
            }
        }
        catch (Exception ex)
        {
            StatusText = $"엑셀 내보내기 실패: {ex.Message}";
        }
    }

    // 이상치 경고는 알림일 뿐 제재가 아니므로 내보내기를 막지 않는다
    private bool CanExport() =>
        SpeciesEntries.Any(r => (r.IndividualCount ?? 0) > 0);
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
    // 종 목록
    public List<BenthosDraftRow> Rows { get; init; } = new();
}
