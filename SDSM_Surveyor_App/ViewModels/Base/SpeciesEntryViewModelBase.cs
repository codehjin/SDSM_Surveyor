using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Core.Helpers;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels.Base;

/// <summary>
/// 종을 행으로 입력하는 5개 분류군(어류·저서동물·조류·포유류·양서파충류)의 공통 기반
/// (05_REFACTORING §1-1).
///
/// 공통으로 가져가는 것
///  · 빠른추가 바 — 초성 검색 · 종 선택 · Enter 추가 · 포커스 이벤트
///  · 클립보드 붙여넣기의 뼈대(줄·셀 분해)와 셀 파싱 헬퍼
///  · 빈 행 정리(<c>PruneEmpty</c>)
///  · 행 추가/삭제·행 내부 변경 구독 → 재계산 호출
///
/// 분류군마다 다른 것은 <b>추상 멤버</b>로 뺐다. 종 타입도 행 타입도 다르기 때문이다
/// (어류 <c>FishSpeciesList</c> · 저서 <c>BenthosSpeciesList</c> · 관찰형 <c>ObservedSpecies</c>).
///
/// ⚠ 행 컬렉션 이름은 분류군마다 다르다(<c>SpeciesEntries</c> / <c>Entries</c>).
///   XAML 바인딩이 그 이름을 쓰고 있어 바꾸지 않았다 — 파생 클래스가 <see cref="Rows"/> 를 그 이름으로 노출한다.
/// </summary>
/// <typeparam name="TEntry">화면 행 모델(FishSpeciesEntry·BirdEntry 등)</typeparam>
/// <typeparam name="TSpecies">공식 종목록 항목(FishSpeciesList·ObservedSpecies 등)</typeparam>
public abstract partial class SpeciesEntryViewModelBase<TEntry, TSpecies> : TaxonEntryViewModelBase
    where TEntry : class, INotifyPropertyChanged
    where TSpecies : class
{
    protected SpeciesEntryViewModelBase(ISessionService sessions, SurveyMeta meta)
        : base(sessions, meta)
    {
        Rows.CollectionChanged += OnRowsCollectionChanged;
    }

    /// <summary>입력된 종 행. 파생 클래스가 화면 이름(<c>SpeciesEntries</c>·<c>Entries</c>)으로 다시 노출한다.</summary>
    public RadObservableCollection<TEntry> Rows { get; } = new();

    /// <summary>종명 초성 자동완성용 공식 종목록(species.json).</summary>
    [ObservableProperty] private List<TSpecies> _speciesListSource = new();

    // ── 빠른 추가 바(그리드 위) : 초성 검색 → 개체수 → Enter/추가 ──────────────

    /// <summary>콤보 드롭다운(초성 필터 결과).</summary>
    [ObservableProperty] private List<TSpecies> _filteredQuick = new();

    /// <summary>검색 입력(초성/이름).</summary>
    [ObservableProperty] private string? _quickSearch;

    /// <summary>선택한 종.</summary>
    [ObservableProperty] private TSpecies? _quickSpecies;

    /// <summary>개체수.</summary>
    [ObservableProperty] private string? _quickCount;

    partial void OnQuickSearchChanged(string? value) => FilterQuick();

    partial void OnSpeciesListSourceChanged(List<TSpecies> value) => FilteredQuick = value.ToList();

    partial void OnQuickSpeciesChanged(TSpecies? value)
    {
        if (value is not null) QuickSpeciesPicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>빠른 추가 후 검색창으로 포커스 복귀(코드비하인드가 구독).</summary>
    public event EventHandler? QuickAddCompleted;

    /// <summary>종을 고르면 개체수 칸으로 포커스 이동(코드비하인드가 구독).</summary>
    public event EventHandler? QuickSpeciesPicked;

    /// <summary>입력한 초성/이름으로 종목록을 필터해 콤보 드롭다운을 갱신.</summary>
    private void FilterQuick()
    {
        var q = QuickSearch?.Trim();
        FilteredQuick = string.IsNullOrEmpty(q)
            ? SpeciesListSource.ToList()
            : SpeciesListSource.Where(s => ChosungHelper.IsMatch(SpeciesKoOf(s), q)).Take(80).ToList();
    }

    /// <summary>빠른 추가: 선택 종 + 개체수로 행을 추가하고 입력칸을 비운다.</summary>
    [RelayCommand]
    private void AddQuick()
    {
        if (QuickSpecies is null) return;   // 목록에서 종을 선택해야 추가(오타 방지)

        Rows.Add(CreateRow(QuickSpecies, QuickCount));

        QuickSpecies = null;
        QuickCount = null;
        QuickSearch = null;   // 콤보 텍스트 비움 + 목록 원복
        QuickAddCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>국명·개체수가 모두 빈 행 제거(붙여넣기·오클릭으로 생긴 빈 행 정리).</summary>
    [RelayCommand]
    private void PruneEmpty()
    {
        var empties = Rows.Where(IsRowEmpty).ToList();
        foreach (var e in empties) Rows.Remove(e);
    }

    /// <summary>
    /// 엑셀에서 복사한 여러 줄을 그리드 행으로 추가(그리드 Ctrl+V에서 호출).
    /// RadGridView 기본 붙여넣기가 커스텀 편집기 컬럼과 충돌해 한 칸에 뭉치므로 직접 파싱한다.
    /// 열 해석은 분류군마다 달라 <see cref="CreateRowFromCells"/> 가 맡는다.
    /// </summary>
    public void PasteRows(string clipboard)
    {
        if (string.IsNullOrWhiteSpace(clipboard)) return;

        var lines = clipboard.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cells = line.Split('\t');
            var ko = cells[0].Trim();   // 종명은 원문 그대로(Trim만) — CLAUDE.md §7.3
            if (string.IsNullOrEmpty(ko)) continue;

            var row = CreateRowFromCells(ko, cells);
            if (row is not null) Rows.Add(row);
        }
    }

    // ── 파생 클래스가 채우는 것 ────────────────────────────────────────────────

    /// <summary>종목록 항목의 국명(초성 검색·매칭 기준).</summary>
    protected abstract string? SpeciesKoOf(TSpecies species);

    /// <summary>빠른추가 바에서 행 하나를 만든다.</summary>
    protected abstract TEntry CreateRow(TSpecies species, string? count);

    /// <summary>붙여넣은 한 줄(셀 배열)에서 행 하나를 만든다. 만들지 않으려면 null.</summary>
    protected abstract TEntry? CreateRowFromCells(string speciesKo, string[] cells);

    /// <summary>국명·개체수가 모두 비어 지워도 되는 행인지.</summary>
    protected abstract bool IsRowEmpty(TEntry row);

    /// <summary>행이 늘거나 줄거나 값이 바뀌면 호출된다. 통계·지수 재계산을 여기서 한다.</summary>
    protected abstract void Recalculate();

    /// <summary>재계산을 유발하는 행 속성인지(불필요한 재계산을 막는다).</summary>
    protected abstract bool AffectsRecalculation(string? propertyName);

    /// <summary>행 값이 바뀔 때 재계산 전에 할 일(국명 → 공식 종목록 매칭 등).</summary>
    protected virtual void OnRowPropertyChanged(TEntry row, string? propertyName) { }

    // ── 행 구독 ────────────────────────────────────────────────────────────────

    private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (TEntry r in e.OldItems) r.PropertyChanged -= OnRowChanged;
        if (e.NewItems is not null)
            foreach (TEntry r in e.NewItems) r.PropertyChanged += OnRowChanged;

        Recalculate();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is TEntry row) OnRowPropertyChanged(row, e.PropertyName);
        if (AffectsRecalculation(e.PropertyName)) Recalculate();
    }

    // ── 붙여넣기 셀 파싱 헬퍼 ──────────────────────────────────────────────────

    /// <summary>셀 값(없거나 비면 null).</summary>
    protected static string? Cell(string[] cells, int i)
    {
        if (i >= cells.Length) return null;
        var v = cells[i].Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    protected static int? Pi(string? s) => int.TryParse(s, out var n) ? n : null;
    protected static double? Pd(string? s) => double.TryParse(s, out var d) ? d : null;
}
