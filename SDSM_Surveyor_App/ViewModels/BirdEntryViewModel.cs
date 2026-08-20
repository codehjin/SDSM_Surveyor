using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>조류 탭: 종별 관찰 입력(관리자 Bird 전 필드) + 총 종수/개체수.</summary>
public partial class BirdEntryViewModel : ObservableObject, ITransientService
{
    private const string TaxonKey = "Bird";
    private readonly ISpeciesListProvider _speciesProvider;
    private readonly ILocalDraftStore _draftStore;

    public BirdEntryViewModel(ISpeciesListProvider speciesProvider, ILocalDraftStore draftStore)
    {
        _speciesProvider = speciesProvider;
        _draftStore = draftStore;
        SpeciesListSource = _speciesProvider.GetBirdSpecies();
        FilteredQuick = SpeciesListSource.ToList();   // 콤보 초기 목록
        Entries.CollectionChanged += OnEntriesChanged;
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
    }

    // ── 공통 조사개황 : 모든 분류군 공유(SurveyOverviewControl에서 입력) ──
    // ※ 관리자 Bird 테이블에는 대권역/중권역/하천유형/조사기관 컬럼이 없다 → 내보내기 시 해당 열만 비운다.
    public SurveyMeta Meta { get; } = new();

    public string[] MigratoryTypes { get; } = { "텃새", "여름철새", "겨울철새", "나그네새", "길잃은새" };

    public RadObservableCollection<BirdEntry> Entries { get; } = new();

    // 종명 초성 자동완성용 공식 종목록(species.json)
    [ObservableProperty] private string[] _speciesListSource = Array.Empty<string>();

    // ── 빠른 추가 바(그리드 위) : 초성 검색(RadComboBox·VM에서 필터) → 개체수 → Enter/추가 ──
    [ObservableProperty] private List<string> _filteredQuick = new();   // 콤보 드롭다운(초성 필터 결과)
    [ObservableProperty] private string? _quickSearch;                  // 검색 입력(초성/이름)
    [ObservableProperty] private string? _quickSpecies;                 // 선택한 종
    [ObservableProperty] private string? _quickCount;                   // 개체수

    partial void OnQuickSearchChanged(string? value) => FilterQuick();

    /// <summary>입력한 초성/이름으로 종목록을 필터해 콤보 드롭다운을 갱신.</summary>
    private void FilterQuick()
    {
        var q = QuickSearch?.Trim();
        FilteredQuick = string.IsNullOrEmpty(q)
            ? SpeciesListSource.ToList()
            : SpeciesListSource.Where(s => Helpers.ChosungHelper.IsMatch(s, q)).Take(80).ToList();
    }

    /// <summary>빠른 추가 후 검색창으로 포커스 복귀(코드비하인드가 구독).</summary>
    public event EventHandler? QuickAddCompleted;

    /// <summary>종을 고르면 개체수 칸으로 포커스 이동(코드비하인드가 구독).</summary>
    public event EventHandler? QuickSpeciesPicked;
    partial void OnQuickSpeciesChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value)) QuickSpeciesPicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>빠른 추가: 선택 종 + 개체수로 행을 추가하고 입력칸을 비운다.</summary>
    [RelayCommand]
    private void AddQuick()
    {
        if (string.IsNullOrWhiteSpace(QuickSpecies)) return;   // 목록에서 종을 선택해야 추가(오타 방지)

        var entry = new BirdEntry { SpeciesKo = QuickSpecies };
        if (int.TryParse(QuickCount, out var n)) entry.IndividualCount = n;
        Entries.Add(entry);

        QuickSpecies = null;
        QuickCount = null;
        QuickSearch = null;   // 콤보 텍스트 비움 + 목록 원복
        QuickAddCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>엑셀에서 복사한 여러 줄을 그리드 행으로 추가(그리드 Ctrl+V에서 호출).
    /// 열 순서 = 그리드 순서 : 국명 · 개체수 · 학명 · 도래유형 · 대항목 · 세부항목 · 서식유형 · 위도 · 경도 · 특징 · 특이사항.
    /// (어류와 동일하게 [국명, 개체수] 2열만 붙여넣어도 된다)
    /// RadGridView 기본 붙여넣기가 커스텀 편집기 컬럼과 충돌해 한 칸에 뭉치므로 직접 파싱한다.</summary>
    public void PasteRows(string clipboard)
    {
        if (string.IsNullOrWhiteSpace(clipboard)) return;
        var lines = clipboard.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = line.Split('\t');
            var ko = cells[0].Trim();   // 종명은 원문 그대로(Trim만)
            if (string.IsNullOrEmpty(ko)) continue;

            Entries.Add(new BirdEntry
            {
                SpeciesKo = ko,
                IndividualCount = Pi(Cell(cells, 1)),
                SpeciesEn = Cell(cells, 2),
                MigratoryType = Cell(cells, 3),
                Category = Cell(cells, 4),
                CategoryDetail = Cell(cells, 5),
                HabitatType = Cell(cells, 6),
                Lat = Pd(Cell(cells, 7)),
                Lng = Pd(Cell(cells, 8)),
                Feature = Cell(cells, 9),
                Note = Cell(cells, 10),
            });
        }
    }

    private static string? Cell(string[] cells, int i)
    {
        if (i >= cells.Length) return null;
        var v = cells[i].Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }
    private static int? Pi(string? s) => int.TryParse(s, out var n) ? n : null;
    private static double? Pd(string? s) => double.TryParse(s, out var d) ? d : null;

    /// <summary>국명·개체수가 모두 빈 행 제거(붙여넣기·오클릭으로 생긴 빈 행 정리).</summary>
    [RelayCommand]
    private void PruneEmpty()
    {
        var empties = Entries
            .Where(r => string.IsNullOrWhiteSpace(r.SpeciesKo) && r.IndividualCount is null)
            .ToList();
        foreach (var e in empties) Entries.Remove(e);
    }

    [ObservableProperty] private int _totalSpeciesCount;
    [ObservableProperty] private int _totalIndividualCount;

    [ObservableProperty] private string _statusText = "임시 저장 없음";
    [ObservableProperty] private DateTime? _lastSavedTime;

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (BirdEntry r in e.OldItems) r.PropertyChanged -= OnRowChanged;
        if (e.NewItems is not null) foreach (BirdEntry r in e.NewItems) r.PropertyChanged += OnRowChanged;
        Recalculate();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BirdEntry.SpeciesKo) or nameof(BirdEntry.IndividualCount))
            Recalculate();
    }

    private void Recalculate()
    {
        TotalSpeciesCount = Entries.Where(x => !string.IsNullOrWhiteSpace(x.SpeciesKo))
                                   .Select(x => x.SpeciesKo).Distinct().Count();
        // 결측치(null)와 0 엄격 구분 : 미입력은 합계에서 0으로 캐스팅
        TotalIndividualCount = Entries.Sum(x => x.IndividualCount ?? 0);
        ExportExcelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand] private void AddRow() => Entries.Add(new BirdEntry());
    [RelayCommand] private void RemoveRow(BirdEntry? row) { if (row is not null) Entries.Remove(row); }

    [RelayCommand]
    private async Task SaveTemporary()
    {
        await _draftStore.SaveDraftAsync(TaxonKey, new
        {
            Meta.SurveyYear, Meta.YearChsu, Meta.SurveyDate, Meta.MajorRegion, Meta.MiddleRegion,
            Meta.River, Meta.RiverType, Meta.Site, Meta.Lat, Meta.Lng, Meta.Weather,
            Meta.SurveyAgency, Meta.Surveyor,
            Rows = Entries.ToList()
        });
        LastSavedTime = DateTime.Now;
        StatusText = $"임시 저장됨 · {LastSavedTime:HH:mm:ss}";
        WeakReferenceMessenger.Default.Send(new NotifyMessage(("임시 저장되었습니다.", true)));
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportExcel()
    {
        StatusText = "엑셀 내보내기: 다음 단계에서 연동 예정";
        return Task.CompletedTask;
    }

    private bool CanExport() => Entries.Any(x => !string.IsNullOrWhiteSpace(x.SpeciesKo));
}
