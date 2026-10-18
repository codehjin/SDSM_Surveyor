using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 단일 선택 칸(C-4 신설). 화면은 <see cref="Options"/>(양식 선택지)와 <see cref="Value"/>(저장 문자열)만 넘긴다.
///
/// 저장값은 선택지의 <see cref="FormChoice.Value"/> — 이름(하천유형), 코드 숫자(식생 수피도 `1`), 정수 글자(투명도 `3`).
/// 세션 형식은 그대로 문자열이다.
/// 예전 세션 값이 선택지에 없으면 <b>지우지 않고</b> '(양식 외)' 항목으로 붙여 고른 채로 두고 경고한다.
/// 화면을 열기만 하고 아무것도 고치지 않으면 저장값을 다시 쓰지 않는다(앞뒤 공백까지 그대로).
/// </summary>
public partial class SingleChoiceBox : UserControl
{
    /// <summary>드롭다운 한 줄.</summary>
    public sealed class Choice
    {
        public Choice(string value, string display, bool isExtra) { Value = value; Display = display; IsExtra = isExtra; }
        public string Value { get; }
        public string Display { get; }
        /// <summary>양식 선택지에 없는 예전 값.</summary>
        public bool IsExtra { get; }
    }

    private readonly ObservableCollection<Choice> _items = new();
    private bool _syncing;

    public SingleChoiceBox()
    {
        InitializeComponent();
        Box.ItemsSource = _items;
    }

    /// <summary>양식 선택지.</summary>
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options), typeof(IEnumerable<FormChoice>), typeof(SingleChoiceBox),
        new PropertyMetadata(null, (d, _) => ((SingleChoiceBox)d).Rebuild()));

    public IEnumerable<FormChoice>? Options
    {
        get => (IEnumerable<FormChoice>?)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>저장 문자열(VM 속성에 양방향으로 묶는다). 비우면 null.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(SingleChoiceBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => { var c = (SingleChoiceBox)d; if (!c._syncing) c.Rebuild(); }));

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>칸 아래 경고 줄을 보일지(그리드 2행처럼 높이가 고정된 곳에서는 끈다 — '(양식 외)' 표시는 콤보에 남는다).</summary>
    public static readonly DependencyProperty ShowWarningProperty = DependencyProperty.Register(
        nameof(ShowWarning), typeof(bool), typeof(SingleChoiceBox),
        new PropertyMetadata(true, (d, e) => ((SingleChoiceBox)d).Warn.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    public bool ShowWarning
    {
        get => (bool)GetValue(ShowWarningProperty);
        set => SetValue(ShowWarningProperty, value);
    }

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(SingleChoiceBox), new PropertyMetadata("·"));

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public static readonly DependencyProperty ToolTipTextProperty = DependencyProperty.Register(
        nameof(ToolTipText), typeof(string), typeof(SingleChoiceBox), new PropertyMetadata(null));

    public string? ToolTipText
    {
        get => (string?)GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
    }

    public static readonly DependencyProperty AutomationNameProperty = DependencyProperty.Register(
        nameof(AutomationName), typeof(string), typeof(SingleChoiceBox), new PropertyMetadata(null));

    public string? AutomationName
    {
        get => (string?)GetValue(AutomationNameProperty);
        set => SetValue(AutomationNameProperty, value);
    }

    /// <summary>선택지에 없는 예전 값이 고른 상태인가(화면 경고와 같은 조건).</summary>
    public bool HasExtra => Box.SelectedItem is Choice { IsExtra: true };

    private void Rebuild()
    {
        _syncing = true;
        try
        {
            _items.Clear();
            foreach (var o in Options ?? Array.Empty<FormChoice>()) _items.Add(new Choice(o.Value, o.Label, false));

            var v = Value?.Trim();
            Choice? pick = null;
            if (!string.IsNullOrEmpty(v))
            {
                pick = _items.FirstOrDefault(c => c.Value == v);
                if (pick is null)
                {
                    // 원문 그대로 보관한다(앞뒤 공백 포함) — 고치지 않는 한 저장값이 바뀌지 않는다.
                    pick = new Choice(Value!, $"{v} (양식 외)", true);
                    _items.Add(pick);
                }
            }
            Box.SelectedItem = pick;
        }
        finally { _syncing = false; }
        UpdateWarning();
    }

    private void Box_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        _syncing = true;
        try { Value = (Box.SelectedItem as Choice)?.Value; }
        finally { _syncing = false; }
        UpdateWarning();
    }

    private void UpdateWarning() =>
        Warn.Text = Box.SelectedItem is Choice { IsExtra: true } c
            ? $"양식 외 값 {c.Value.Trim()} · 지우지 않음 — 확인하세요"
            : string.Empty;
}
