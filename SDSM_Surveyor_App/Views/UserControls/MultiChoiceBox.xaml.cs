using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 복수 선택 칸(B-8 신설 · C-4 에서 다시 쓴다).
///
/// 화면은 <see cref="Options"/>(양식 선택지)와 <see cref="Value"/>(저장 문자열)만 넘긴다.
/// 저장값은 고른 이름을 <b>양식 순서대로 공백 없이 `,` 로 잇는다</b>(`흐름,소`) — 일괄입력용 규칙과 같다.
/// 세션 형식은 그대로 문자열이다.
///
/// 예전 세션의 자유 입력 값은 `,`·`.` 로 나눠(관리자 Import 와 같은 구분자) 선택지와 맞는 것은 고른 상태로 보여 주고,
/// 맞지 않는 것은 <b>지우지 않고</b> '(양식 외)' 항목으로 붙여 선택된 채로 둔 뒤 경고한다.
/// 화면을 열기만 하고 아무것도 고치지 않으면 저장값을 다시 쓰지 않는다(공백·순서까지 그대로).
/// </summary>
public partial class MultiChoiceBox : UserControl
{
    /// <summary>드롭다운 한 줄.</summary>
    public sealed class Choice
    {
        public Choice(string name, bool isExtra) { Name = name; IsExtra = isExtra; }
        public string Name { get; }
        /// <summary>양식 선택지에 없는 예전 값.</summary>
        public bool IsExtra { get; }
        public string Display => IsExtra ? $"{Name} (양식 외)" : Name;
    }

    private static readonly char[] Separators = { ',', '.' };

    private readonly ObservableCollection<Choice> _items = new();
    private bool _syncing;

    public MultiChoiceBox()
    {
        InitializeComponent();
        Box.ItemsSource = _items;
    }

    /// <summary>양식 선택지(순서 = 저장 순서).</summary>
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options), typeof(IEnumerable<string>), typeof(MultiChoiceBox),
        new PropertyMetadata(null, (d, _) => ((MultiChoiceBox)d).Rebuild()));

    public IEnumerable<string>? Options
    {
        get => (IEnumerable<string>?)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>저장 문자열(VM 속성에 양방향으로 묶는다). 아무것도 고르지 않으면 null.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(MultiChoiceBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => { var c = (MultiChoiceBox)d; if (!c._syncing) c.Rebuild(); }));

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>아무것도 고르지 않았을 때 콤보에 보이는 글자.</summary>
    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(MultiChoiceBox), new PropertyMetadata("·"));

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public static readonly DependencyProperty ToolTipTextProperty = DependencyProperty.Register(
        nameof(ToolTipText), typeof(string), typeof(MultiChoiceBox), new PropertyMetadata("여러 개를 고를 수 있습니다"));

    public string ToolTipText
    {
        get => (string)GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
    }

    public static readonly DependencyProperty AutomationNameProperty = DependencyProperty.Register(
        nameof(AutomationName), typeof(string), typeof(MultiChoiceBox), new PropertyMetadata(null));

    public string? AutomationName
    {
        get => (string?)GetValue(AutomationNameProperty);
        set => SetValue(AutomationNameProperty, value);
    }

    /// <summary>선택지에 없는 예전 값이 남아 있는가(화면 경고와 같은 조건).</summary>
    public bool HasExtra => _items.Any(c => c.IsExtra && Box.SelectedItems.Contains(c));

    /// <summary>저장 문자열을 항목으로 나눈다(`,`·`.` · 앞뒤 공백 제거 · 빈 항목 무시).</summary>
    public static List<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(Separators).Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

    /// <summary>저장값 → 드롭다운 항목·선택 상태.</summary>
    private void Rebuild()
    {
        _syncing = true;
        try
        {
            var options = Options?.ToList() ?? new List<string>();
            var tokens = Split(Value);

            Box.SelectedItems.Clear();
            _items.Clear();
            foreach (var o in options) _items.Add(new Choice(o, false));
            foreach (var t in tokens.Where(t => !options.Contains(t, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal))
                _items.Add(new Choice(t, true));

            foreach (var c in _items.Where(c => tokens.Contains(c.Name, StringComparer.Ordinal)))
                Box.SelectedItems.Add(c);
        }
        finally { _syncing = false; }
        UpdateWarning();
    }

    /// <summary>조사자가 고르거나 해제했다 → 저장값을 다시 만든다(선택지 순서, 공백 없이 `,`).</summary>
    private void Box_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        var picked = _items.Where(c => Box.SelectedItems.Contains(c)).Select(c => c.Name).ToList();
        _syncing = true;
        try { Value = picked.Count == 0 ? null : string.Join(",", picked); }
        finally { _syncing = false; }
        UpdateWarning();
    }

    private void UpdateWarning()
    {
        var extras = _items.Where(c => c.IsExtra && Box.SelectedItems.Contains(c)).Select(c => c.Name).ToList();
        Warn.Text = extras.Count == 0
            ? string.Empty
            : $"양식 외 값 {string.Join(",", extras)} · 지우지 않음 — 확인하세요";
    }
}
