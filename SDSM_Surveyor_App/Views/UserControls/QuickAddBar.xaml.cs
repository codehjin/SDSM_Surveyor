using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using SDSM_Surveyor_App.ViewModels.Base;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 종 빠른 추가 바. 5개 화면(어류·저서·조류·포유류·양서파충류)이 공유한다.
///
/// DataContext 는 호스트 화면의 ViewModel 그대로다 —
/// <c>FilteredQuick</c>·<c>QuickSearch</c>·<c>QuickSpecies</c>·<c>QuickCount</c>·<c>AddQuickCommand</c>
/// 는 <see cref="SpeciesEntryViewModelBase{TEntry, TSpecies}"/> 가 제공한다.
/// ⚠ 이 이름들을 바꾸지 않는다(06_DESIGN_REBUILD §6).
/// </summary>
public partial class QuickAddBar : UserControl
{
    private IQuickAddHost? _host;

    public QuickAddBar()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        // 콤보 안 글상자의 TextChanged 가 올라온다(편집형 콤보).
        SpeciesBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(SpeciesBox_TextChanged));
        Unloaded += (_, _) => Detach();
    }

    /// <summary>검색창 워터마크. 분류군별 초성 예시를 넣는다(예: `종명 초성 검색 (예: ㅊㅂㅇ)`).</summary>
    public static readonly DependencyProperty SearchWatermarkProperty = DependencyProperty.Register(
        nameof(SearchWatermark), typeof(string), typeof(QuickAddBar), new PropertyMetadata("종명 초성 검색"));

    public string SearchWatermark
    {
        get => (string)GetValue(SearchWatermarkProperty);
        set => SetValue(SearchWatermarkProperty, value);
    }

    /// <summary>수량 칸 워터마크. 관찰형은 `관찰 개체수`(= '관찰' 흔적에 들어간다).</summary>
    public static readonly DependencyProperty CountWatermarkProperty = DependencyProperty.Register(
        nameof(CountWatermark), typeof(string), typeof(QuickAddBar), new PropertyMetadata("개체수"));

    public string CountWatermark
    {
        get => (string)GetValue(CountWatermarkProperty);
        set => SetValue(CountWatermarkProperty, value);
    }

    /// <summary>
    /// 수량 칸이 소수 개체수(저서)인가 — 켜면 숫자 · 소수점 하나만 받는다(<see cref="Behaviors.DecimalInput"/> · v0.5.1).
    /// 종전에는 '3a' 를 치면 개체수 없이 행만 들어갔다(말없이 빈칸).
    /// </summary>
    public static readonly DependencyProperty DecimalCountProperty = DependencyProperty.Register(
        nameof(DecimalCount), typeof(bool), typeof(QuickAddBar), new PropertyMetadata(false));

    public bool DecimalCount
    {
        get => (bool)GetValue(DecimalCountProperty);
        set => SetValue(DecimalCountProperty, value);
    }

    /// <summary>
    /// 수량 칸이 정수 개체수(어류 · 조류 · 포유류 · 양서파충류)인가 — 켜면 다른 개체수 칸과 같은 <see cref="Behaviors.IntegerInput"/>(§24-3).
    /// 종전에는 '3a' · '1.5' 를 치면 개체수 없이 행만 들어갔다(말없이 빈칸).
    /// </summary>
    public static readonly DependencyProperty IntegerCountProperty = DependencyProperty.Register(
        nameof(IntegerCount), typeof(bool), typeof(QuickAddBar), new PropertyMetadata(false));

    public bool IntegerCount
    {
        get => (bool)GetValue(IntegerCountProperty);
        set => SetValue(IntegerCountProperty, value);
    }

    /// <summary>바 오른쪽 안내 문구.</summary>
    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
        nameof(Hint), typeof(string), typeof(QuickAddBar), new PropertyMetadata("종 선택 후 수량 입력 → Enter"));

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    // ── 포커스 연결 : 종 선택 → 수량 → Enter → 검색창 복귀 ──────────────────────
    // 종전에는 5개 화면 코드비하인드가 같은 구독을 각자 들고 있었다.
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        if (e.NewValue is not IQuickAddHost host) return;

        _host = host;
        _host.QuickSpeciesPicked += OnSpeciesPicked;
        _host.QuickAddCompleted += OnAddCompleted;
    }

    private void Detach()
    {
        if (_host is null) return;
        _host.QuickSpeciesPicked -= OnSpeciesPicked;
        _host.QuickAddCompleted -= OnAddCompleted;
        _host = null;
    }

    private void OnSpeciesPicked(object? sender, EventArgs e) => FocusLater(CountBox);
    private void OnAddCompleted(object? sender, EventArgs e) => FocusLater(SpeciesBox);

    private void FocusLater(IInputElement target) =>
        Dispatcher.BeginInvoke(new Action(() => target.Focus()), DispatcherPriority.Input);

    /// <summary>
    /// 검색어를 치면 드롭다운을 연다(B-3). 종전에는 포커스를 받을 때(<c>OpenDropDownOnFocus</c>)만 열려서
    /// 한 번 닫히면 글자를 쳐도 후보가 보이지 않았다.
    /// 목록에서 고른 직후에는 글자가 그 종명으로 바뀐다 — 이때 다시 열면 개체수 칸으로 넘어가는 흐름이 끊긴다.
    /// </summary>
    private void SpeciesBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!SpeciesBox.IsKeyboardFocusWithin || SpeciesBox.IsDropDownOpen) return;
        var text = SpeciesBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (SpeciesBox.SelectedItem is { } picked && text == DisplayName(picked)) return;
        SpeciesBox.IsDropDownOpen = true;
    }

    /// <summary>
    /// 검색창에서 Enter → 후보를 고른다(B-3 · `검색 → ↓/Enter → 개체수`).
    /// ↓ 는 콤보가 첫 후보(= 같은 이름 → 앞부분 → 포함 순)를 고른다 — 사용자가 고른 것이다.
    /// Enter 는 <b>친 글자와 이름이 똑같은 종</b>만 고른다(2026-10-18 결정 D1 — 후보가 하나뿐이어도 자동으로 고르지 않는다).
    /// 그 밖에는 아무것도 고르지 않는다. 빠른추가는 목록에서 고른 종만 넣는다(목록 밖 국명은 그리드 맨 아래 새 행에 친다).
    /// </summary>
    private void SpeciesBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || SpeciesBox.SelectedItem is not null) return;
        var text = SpeciesBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var items = SpeciesBox.Items.Cast<object>().ToList();
        var pick = items.FirstOrDefault(i => string.Equals(DisplayName(i)?.Trim(), text, StringComparison.Ordinal));
        if (pick is null) return;

        SpeciesBox.SelectedItem = pick;   // → QuickSpecies → 개체수 칸으로 이동(OnSpeciesPicked)
        SpeciesBox.IsDropDownOpen = false;
        e.Handled = true;
    }

    /// <summary>콤보에 보이는 종명(<c>DisplayMemberPath</c>). 종 타입이 분류군마다 달라 이름으로 읽는다.</summary>
    private string? DisplayName(object item) =>
        item.GetType().GetProperty(SpeciesBox.DisplayMemberPath)?.GetValue(item) as string;

    // 종목록 팝업 열기. 호스트를 넘겨 더블클릭한 종이 이 화면 야장으로 들어가게 한다.
    private void SpeciesCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (_host is null) return;
        new Views.Windows.SpeciesCatalogWindow(_host) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    // 수량 칸에서 Enter → 행 추가
    private void CountBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        // 방금 받지 않은 글자('3a' · '1.5')가 있어 안내가 떠 있으면 행을 넣지 않는다 — 개체수 빈 행이 몰래 생기지 않게(§24-3)
        if (Behaviors.IntegerInput.IsShowingNotice(CountBox) || Behaviors.DecimalInput.IsShowingNotice(CountBox))
        {
            e.Handled = true;
            return;
        }

        var cmd = _host?.AddQuickCommand;
        if (cmd?.CanExecute(null) == true) cmd.Execute(null);
        e.Handled = true;
    }
}
