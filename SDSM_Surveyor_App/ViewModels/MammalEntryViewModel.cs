using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Models;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>포유류 탭: 종별 관찰(흔적 12종 개체수) + 총 종수/개체수/건수.</summary>
public partial class MammalEntryViewModel : ObservableObject, ITransientService
{
    private const string TaxonKey = "Mammal";
    private readonly ISpeciesListProvider _speciesProvider;
    private readonly ILocalDraftStore _draftStore;

    public MammalEntryViewModel(ISpeciesListProvider speciesProvider, ILocalDraftStore draftStore)
    {
        _speciesProvider = speciesProvider;
        _draftStore = draftStore;
        SpeciesListSource = _speciesProvider.GetMammalSpecies();
        FilteredQuick = SpeciesListSource.ToList();   // 콤보 초기 목록
        Entries.CollectionChanged += OnEntriesChanged;
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
    }

    // ── 공통 조사개황 : 모든 분류군 공유(SurveyOverviewControl에서 입력) ──
    public SurveyMeta Meta { get; } = new();

    public RadObservableCollection<MammalEntry> Entries { get; } = new();

    // 종명 초성 자동완성용 공식 종목록(species.json)
    [ObservableProperty] private List<ObservedSpecies> _speciesListSource = new();

    // ── 빠른 추가 바(그리드 위) : 초성 검색 → 관찰 개체수 → Enter/추가 ──
    [ObservableProperty] private List<ObservedSpecies> _filteredQuick = new();
    [ObservableProperty] private string? _quickSearch;
    [ObservableProperty] private ObservedSpecies? _quickSpecies;
    [ObservableProperty] private string? _quickCount;   // '관찰'(Trace2) 개체수로 들어간다

    partial void OnQuickSearchChanged(string? value) => FilterQuick();

    /// <summary>입력한 초성/이름으로 종목록을 필터해 콤보 드롭다운을 갱신.</summary>
    private void FilterQuick()
    {
        var q = QuickSearch?.Trim();
        FilteredQuick = string.IsNullOrEmpty(q)
            ? SpeciesListSource.ToList()
            : SpeciesListSource.Where(s => Helpers.ChosungHelper.IsMatch(s.SpeciesKo, q)).Take(80).ToList();
    }

    /// <summary>빠른 추가 후 검색창으로 포커스 복귀(코드비하인드가 구독).</summary>
    public event EventHandler? QuickAddCompleted;

    /// <summary>종을 고르면 개체수 칸으로 포커스 이동(코드비하인드가 구독).</summary>
    public event EventHandler? QuickSpeciesPicked;
    partial void OnQuickSpeciesChanged(ObservedSpecies? value)
    {
        if (value is not null) QuickSpeciesPicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>빠른 추가: 선택 종 + 개체수(관찰)로 행을 추가하고 입력칸을 비운다.
    /// 현장에서 가장 흔한 '관찰' 흔적에 넣고, 나머지 흔적은 그리드에서 직접 입력한다.</summary>
    [RelayCommand]
    private void AddQuick()
    {
        if (QuickSpecies is null) return;

        var entry = new MammalEntry { SpeciesKo = QuickSpecies.SpeciesKo };
        entry.ApplySpecies(QuickSpecies);   // 학명·목·과 자동 채움
        if (int.TryParse(QuickCount, out var n)) entry.Trace2 = n;   // 관찰
        Entries.Add(entry);

        QuickSpecies = null;
        QuickCount = null;
        QuickSearch = null;
        QuickAddCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>엑셀에서 복사한 여러 줄을 그리드 행으로 추가(그리드 Ctrl+V에서 호출).
    /// 열 순서 = 그리드 순서 : 국명 · 학명 · 목 · 과 · 관찰지유형 · 흔적12(포획·관찰·울음·사체·족적·털·식흔·굴·번식지·배설물·카메라·기타)
    /// · 위도 · 경도 · 특징 · 특이사항.
    /// 국명으로 공식 종목록을 조회해 학명·목·과를 채우고, 붙여넣은 셀이 있으면 그 값이 우선한다.</summary>
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

            var entry = new MammalEntry
            {
                SpeciesKo = ko,
                ObservationSite = Cell(cells, 4),
                Trace1 = Pi(Cell(cells, 5)),
                Trace2 = Pi(Cell(cells, 6)),
                Trace3 = Pi(Cell(cells, 7)),
                Trace4 = Pi(Cell(cells, 8)),
                Trace5 = Pi(Cell(cells, 9)),
                Trace6 = Pi(Cell(cells, 10)),
                Trace7 = Pi(Cell(cells, 11)),
                Trace8 = Pi(Cell(cells, 12)),
                Trace9 = Pi(Cell(cells, 13)),
                Trace10 = Pi(Cell(cells, 14)),
                Trace11 = Pi(Cell(cells, 15)),
                Trace12 = Pi(Cell(cells, 16)),
                Lat = Pd(Cell(cells, 17)),
                Lng = Pd(Cell(cells, 18)),
                Feature = Cell(cells, 19),
                Note = Cell(cells, 20),
            };
            entry.ApplySpecies(SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
            entry.SpeciesEn = Cell(cells, 1) ?? entry.SpeciesEn;   // 붙여넣은 값이 있으면 우선
            entry.OrderKo = Cell(cells, 2) ?? entry.OrderKo;
            entry.FamilyKo = Cell(cells, 3) ?? entry.FamilyKo;
            Entries.Add(entry);
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

    /// <summary>국명을 공식 종목록과 대조해 학명·목·과·보호종 표기를 연결한다.
    /// 직접 입력·붙여넣기로 국명만 들어온 경우에도 자동으로 채워진다. 미매칭이면 국명만 남는다.</summary>
    private void ResolveSpecies(MammalEntry row)
    {
        var ko = row.SpeciesKo?.Trim();
        if (string.IsNullOrEmpty(ko)) return;
        if (row.MatchedSpecies?.SpeciesKo == ko) return;   // 이미 일치(자동완성 선택 등)
        row.ApplySpecies(SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
    }

    /// <summary>국명·흔적이 모두 빈 행 제거(붙여넣기·오클릭으로 생긴 빈 행 정리).</summary>
    [RelayCommand]
    private void PruneEmpty()
    {
        var empties = Entries
            .Where(r => string.IsNullOrWhiteSpace(r.SpeciesKo) && r.TraceSum == 0)
            .ToList();
        foreach (var e in empties) Entries.Remove(e);
    }

    [ObservableProperty] private int _totalSpeciesCount;
    [ObservableProperty] private int _totalIndividualCount;
    [ObservableProperty] private int _totalObservations;

    [ObservableProperty] private string _statusText = "임시 저장 없음";
    [ObservableProperty] private DateTime? _lastSavedTime;

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (MammalEntry r in e.OldItems) r.PropertyChanged -= OnRowChanged;
        if (e.NewItems is not null) foreach (MammalEntry r in e.NewItems) r.PropertyChanged += OnRowChanged;
        Recalculate();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 국명만 채워진 행(직접 입력)도 공식 종목록과 자동 매칭
        if (e.PropertyName == nameof(MammalEntry.SpeciesKo) && sender is MammalEntry row)
            ResolveSpecies(row);

        // 흔적 12종은 TraceSum 하나로 묶어 통보되므로 그것만 보면 된다.
        if (e.PropertyName is nameof(MammalEntry.SpeciesKo) or nameof(MammalEntry.TraceSum))
            Recalculate();
    }

    private void Recalculate()
    {
        var named = Entries.Where(x => !string.IsNullOrWhiteSpace(x.SpeciesKo)).ToList();
        TotalSpeciesCount = named.Select(x => x.SpeciesKo).Distinct().Count();
        TotalObservations = named.Count;
        // 결측치(null)와 0 엄격 구분 : 미입력은 합계에서 0으로 캐스팅(TraceSum)
        TotalIndividualCount = named.Sum(x => x.TraceSum);
        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand] private void AddRow() => Entries.Add(new MammalEntry());
    [RelayCommand] private void RemoveRow(MammalEntry? row) { if (row is not null) Entries.Remove(row); }

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

    /// <summary>보고서·기록용 엑셀 내보내기(주력).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportExcel()
    {
        try
        {
            var saved = Export.MammalReportExporter.Export(this);
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
            var saved = Export.MammalExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"일괄입력용 완료 · {System.IO.Path.GetFileName(saved)}";
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("일괄입력용 엑셀을 내보냈습니다.", true)));
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    private bool CanExport() => Entries.Any(x => !string.IsNullOrWhiteSpace(x.SpeciesKo));
}
