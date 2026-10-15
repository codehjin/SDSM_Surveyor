using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Core.Helpers;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Models;
using System.Windows.Threading;
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
public abstract partial class SpeciesEntryViewModelBase<TEntry, TSpecies> : TaxonEntryViewModelBase, IQuickAddHost
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

        var label = $"{SpeciesKoOf(QuickSpecies)} {QuickCount}".Trim();
        var row = CreateRow(QuickSpecies, QuickCount);
        Rows.Add(row);
        MarkJustAdded(row, label);

        QuickSpecies = null;
        QuickCount = null;
        QuickSearch = null;   // 콤보 텍스트 비움 + 목록 원복
        QuickAddCompleted?.Invoke(this, EventArgs.Empty);
    }

    // ── 종목록 팝업에서 넣기 ──────────────────────────────────────────────────

    /// <summary>
    /// 종목록 팝업(<c>SpeciesCatalogWindow</c>)에서 고른 종을 야장에 넣는다.
    /// 개체수는 <b>비워 둔다</b> — 결측(null)과 실측 0 을 구분해야 한다(CLAUDE.md §4).
    /// 이미 있는 종이면 행을 새로 만들지 않고 그 행을 '방금 추가' 로 표시한다.
    /// </summary>
    public void AddFromCatalog(object species)
    {
        if (species is not TSpecies s) return;

        var ko = SpeciesKoOf(s)?.Trim();
        var existing = string.IsNullOrEmpty(ko)
            ? null
            : Rows.FirstOrDefault(r => string.Equals(RowSpeciesKo(r)?.Trim(), ko, StringComparison.Ordinal));

        if (existing is not null)
        {
            MarkJustAdded(existing, $"{ko} · 이미 있음");
            return;
        }

        var row = CreateRow(s, null);
        Rows.Add(row);
        MarkJustAdded(row, ko ?? string.Empty);
    }

    // ── 행 삭제 ────────────────────────────────────────────────────────────────
    // 종목록에서 개체수 없이 넣은 뒤 같은 종을 수동으로 또 넣으면 중복이 생긴다.
    // 그때 지울 수단이 필요하다 — 빠른추가 바의 [행 삭제] 와 그리드 우클릭이 같은 명령을 쓴다.

    /// <summary>마지막으로 누른 행(그리드 코드비하인드가 넣는다). 삭제 대상이다.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteRowCommand))]
    private object? _lastTouchedRow;

    /// <summary>
    /// 삭제 확인 대화상자. 화면이 바꿔 끼울 수 있다(<c>SessionBrowserViewModel.ConfirmDelete</c> 와 같은 방식).
    /// 비워 두면 <see cref="DefaultConfirmDelete"/> 가 쓰인다 — <b>확인 없이 지우는 경로는 없다</b>.
    /// 인자는 지울 행의 종명(비어 있으면 안내 문구).
    /// </summary>
    public Func<string, bool>? ConfirmDeleteRow { get; set; }

    /// <summary>
    /// 행을 지우기 직전에 그리드 편집을 끝낸다(코드비하인드가 채운다).
    /// 붙여넣기 경로가 <c>CancelEdit()</c> 를 부르는 것과 같은 이유다 —
    /// 셀 편집(인서트 행 포함)이 열린 채로 원본 컬렉션을 건드리면 Telerik 이 예외를 던진다.
    /// </summary>
    public Action? EndGridEdit { get; set; }

    // 인서트 행(맨 아래 새 행)의 DataContext 도 TEntry 일 수 있다. 아직 Rows 에 없으므로
    // Contains 로 걸러야 한다 — 편집 중인 인서트 행을 지우려 들면 그리드가 예외를 던진다.
    private bool CanDeleteRow() => LastTouchedRow is TEntry row && Rows.Contains(row);

    /// <summary>마지막으로 누른 행을 지운다. 되돌릴 수 없으므로 먼저 확인을 받는다.</summary>
    [RelayCommand(CanExecute = nameof(CanDeleteRow))]
    private void DeleteRow()
    {
        if (LastTouchedRow is not TEntry row || !Rows.Contains(row))
        {
            LastTouchedRow = null;
            return;
        }

        // ⚠ 종명만 보여 주면 이 기능의 본래 용도(같은 종이 두 줄 있을 때)를 확인해 줄 수 없다.
        //    화면 '#' 열과 같은 1-기준 번호를 함께 붙인다.
        var ko = RowSpeciesKo(row)?.Trim();
        var no = Rows.IndexOf(row) + 1;
        var label = string.IsNullOrEmpty(ko) ? $"{no}번 행 (종명 없음)" : $"{no}번 행 · {ko}";

        var confirm = ConfirmDeleteRow ?? DefaultConfirmDelete;
        if (!confirm(label)) return;

        EndGridEdit?.Invoke();
        Rows.Remove(row);
        LastTouchedRow = null;
    }

    // ⚠ 기본 버튼을 [취소]로 둔다. 이 앱은 Enter 로 행을 추가하는 흐름이라
    //    습관적인 Enter 한 번이 되돌릴 수 없는 삭제가 되면 안 된다.
    private static bool DefaultConfirmDelete(string label) =>
        System.Windows.MessageBox.Show(
            $"「{label}」 을(를) 지울까요?\n\n되돌릴 수 없습니다.",
            "행 삭제",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.OK;

    // 행 모델 5종(FishSpeciesEntry·BenthosSpeciesEntry·BirdEntry·MammalEntry·AmphibianReptileEntry)이
    // 모두 `SpeciesKo` 를 갖지만 공통 인터페이스가 없다(IObservationRow 는 관찰형 3종뿐).
    // 파생 5곳에 추상 멤버를 새로 강요하는 대신 여기서 한 번만 읽는다 — 타입당 1회 캐시다.
    private static readonly Dictionary<Type, System.Reflection.PropertyInfo?> KoProps = new();

    private static string? RowSpeciesKo(TEntry row)
    {
        var t = row.GetType();
        if (!KoProps.TryGetValue(t, out var p))
        {
            p = t.GetProperty("SpeciesKo");
            KoProps[t] = p;
        }
        return p?.GetValue(row) as string;
    }

    // ── 방금 추가 강조 (1.2초) ────────────────────────────────────────────────
    // 연속 입력 중 "지금 들어간 게 이거다"를 알려 준다. 그리드 행 애니메이션은 끄고
    // (design.md §2.9 — 행이 밀려 내려가면 입력이 막힌다) 배경 강조로만 표시한다.

    private const double JustAddedSeconds = 1.2;
    private DispatcherTimer? _justAddedTimer;
    private TEntry? _justAddedRow;

    /// <summary>빠른추가 바 우측 칩 문구(`밀어 41`). 비어 있으면 칩이 숨는다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastAdded))]
    private string? _lastAddedLabel;

    public bool HasLastAdded => !string.IsNullOrWhiteSpace(LastAddedLabel);

    /// <summary>파생 클래스가 행의 '방금 추가' 플래그를 켠다(행 모델마다 속성이 다르다).</summary>
    protected virtual void SetJustAdded(TEntry row, bool value) { }

    private void MarkJustAdded(TEntry row, string label)
    {
        if (_justAddedRow is not null) SetJustAdded(_justAddedRow, false);

        _justAddedRow = row;
        SetJustAdded(row, true);
        LastAddedLabel = label;

        _justAddedTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(JustAddedSeconds) };
        _justAddedTimer.Tick -= OnJustAddedElapsed;
        _justAddedTimer.Tick += OnJustAddedElapsed;
        _justAddedTimer.Stop();
        _justAddedTimer.Start();
    }

    private void OnJustAddedElapsed(object? sender, EventArgs e)
    {
        _justAddedTimer?.Stop();
        if (_justAddedRow is not null) SetJustAdded(_justAddedRow, false);
        _justAddedRow = null;
        LastAddedLabel = null;
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

        // 세션을 바꾸거나 행이 빠지면 '마지막으로 누른 행'은 더 이상 화면에 없다.
        // 남겨 두면 [행 삭제]가 켜진 채로 엉뚱한 행을 겨눈다.
        if (LastTouchedRow is TEntry touched && !Rows.Contains(touched)) LastTouchedRow = null;
        else if (e.Action == NotifyCollectionChangedAction.Reset) LastTouchedRow = null;

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
