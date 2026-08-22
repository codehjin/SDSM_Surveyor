using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using SDSM_Surveyor_App.ViewModels.Base;
using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 관찰형 그리드 (06_DESIGN_REBUILD §5-2-2 · §5-3). 조류·포유류·양서파충류가 공유한다.
///
/// 화면은 <see cref="Columns"/>(1행 열)와 <see cref="RowDetails"/>(2행 묶음)만 넘긴다.
/// 그리드 설정·Ctrl+V·보호종 강조는 여기 한 곳에 있다.
///
/// 열을 이 컨트롤이 소유하는 이유 — 세 화면이 같은 그리드 설정을 각자 들고 있으면
/// 한 곳만 고쳤을 때 화면끼리 어긋난다. 실제로 종전에 그랬다.
/// </summary>
[ContentProperty(nameof(Columns))]
public partial class ObservationGrid : UserControl
{
    private IQuickAddHost? _host;

    public ObservationGrid()
    {
        InitializeComponent();
        Columns.CollectionChanged += OnColumnsChanged;
        Loaded += (_, _) => SyncColumns();
        DataContextChanged += (_, e) => _host = e.NewValue as IQuickAddHost;
    }

    /// <summary>
    /// 1행 열. XAML 에서 이 컨트롤의 자식으로 바로 적는다(기본 콘텐츠 속성).
    /// ⚠ 폭 합계가 <b>1,104 px</b>(가용 1,224 − 특징 최소 120)를 넘으면
    ///   Telerik 이 가로 스크롤 대신 **열을 뭉갠다**. 넘치면 2행으로 더 내릴 것(§5-1-1).
    /// </summary>
    public ObservableCollection<Telerik.Windows.Controls.GridViewColumn> Columns { get; } = new();

    /// <summary>표시할 행 컬렉션. 화면 VM 의 <c>Entries</c> 를 그대로 넘긴다.</summary>
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(IEnumerable), typeof(ObservationGrid), new PropertyMetadata(null));

    public IEnumerable? Rows
    {
        get => (IEnumerable?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>
    /// 2행 묶음 템플릿. 1행에 다 못 넣는 열을 여기 담는다.
    /// <b>상시 표시된다</b> — 클릭해서 펼치는 구조가 아니므로 항목을 숨기는 것이 아니다(§3-6).
    /// </summary>
    public static readonly DependencyProperty RowDetailsProperty = DependencyProperty.Register(
        nameof(RowDetails), typeof(DataTemplate), typeof(ObservationGrid),
        new PropertyMetadata(null, OnRowDetailsChanged));

    public DataTemplate? RowDetails
    {
        get => (DataTemplate?)GetValue(RowDetailsProperty);
        set => SetValue(RowDetailsProperty, value);
    }

    private static void OnRowDetailsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ObservationGrid)d).Grid.RowDetailsTemplate = e.NewValue as DataTemplate;

    /// <summary>내부 그리드. 검증 하네스가 열 폭을 재려고 들여다본다.</summary>
    public RadGridView InnerGrid => Grid;

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncColumns();

    private void SyncColumns()
    {
        if (Grid.Columns.Count == Columns.Count) return;
        Grid.Columns.Clear();
        foreach (var c in Columns) Grid.Columns.Add(c);
    }

    /// <summary>
    /// Ctrl+V : 엑셀 여러 줄을 VM 이 직접 파싱해 행으로 추가한다.
    /// RadGridView 기본 붙여넣기는 커스텀 편집기 컬럼과 충돌해 한 칸에 뭉친다(90_TECH_NOTES §2).
    /// </summary>
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
        if (_host is null || !Clipboard.ContainsText()) return;

        // 인서트 행이 편집 상태면 컬렉션에 추가돼도 화면이 안 그려진다
        // → 편집 종료 후 다음 레이아웃 사이클에서 추가한다.
        Grid.CancelEdit();
        var text = Clipboard.GetText();
        var host = _host;
        Dispatcher.BeginInvoke(new Action(() => host.PasteRows(text)), DispatcherPriority.Background);
        e.Handled = true;
    }
}
