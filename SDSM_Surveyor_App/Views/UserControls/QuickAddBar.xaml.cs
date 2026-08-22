using System.Windows;
using System.Windows.Controls;
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

    // 수량 칸에서 Enter → 행 추가
    private void CountBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var cmd = _host?.AddQuickCommand;
        if (cmd?.CanExecute(null) == true) cmd.Execute(null);
        e.Handled = true;
    }
}
