using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SDSM_Surveyor_App.Behaviors;

/// <summary>
/// 글상자에 <b>0 이상 정수만</b> 들어가게 한다(B-6). 숫자 외 키·붙여넣기를 막는다.
///
/// 관리자 모델이 정수(<c>int?</c>)라 소수·문자가 들어가면 일괄등록이 오류를 낸다.
/// 값은 지금처럼 문자열로 저장한다(세션 형식 그대로) — 여기서는 <b>새로 치는 것</b>만 막는다.
/// 불러온 세션에 이미 있던 소수·문자는 지우지 않는다(화면 경고는 VM 이 띄운다).
///
/// N2(2026-10-20 연계 시험 2차) — 예전에는 글자 단위로 '.' 만 막혀 '1.5' 를 치면 '15' 가 들어갔다(알림 없음).
/// 이제 숫자가 아닌 글자(소수점·쉼표·문자·공백)를 치면 ① 받지 않고 ② 칸을 이 칸에 들어왔을 때의 값으로 되돌리고
/// ③ 안내가 떠 있는 동안(약 2.5초 · Backspace·Delete·Tab·Enter·Esc·방향키·Ctrl 조합을 누르거나 칸을 누르거나 떠나면 바로 풀림) 이어 치는 숫자도 받지 않는다.
/// 그래서 '1.5' 를 빠르게 쳐도 '15'·'1' 이 남지 않고 값이 그대로다. 칸 아래에 '정수만 넣습니다' 를 잠깐 보인다.
/// 붙여넣기는 글자가 정수가 아니면 통째로 받지 않고 같은 안내를 보인다.
///
/// <code>
/// &lt;telerik:RadWatermarkTextBox core:IntegerInput.IsEnabled="True" core:IntegerInput.Maximum="100" …/&gt;
/// </code>
/// </summary>
public static class IntegerInput
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(IntegerInput), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    /// <summary>최댓값(포함). 하상구성 % 는 100. 정하지 않으면 자릿수(9자리)만 막는다.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.RegisterAttached(
        "Maximum", typeof(int), typeof(IntegerInput), new PropertyMetadata(int.MaxValue));

    public static int GetMaximum(DependencyObject d) => (int)d.GetValue(MaximumProperty);
    public static void SetMaximum(DependencyObject d, int value) => d.SetValue(MaximumProperty, value);

    private const int MaxDigits = 9;   // int 범위 안

    /// <summary>안내를 보이는 시간. 이 동안은 이어 치는 숫자도 받지 않는다(Backspace 등으로 바로 풀림).</summary>
    internal static TimeSpan NoticeDuration = TimeSpan.FromMilliseconds(2500);

    public const string NotIntegerText = "정수만 넣습니다 — 소수점·글자는 받지 않습니다";

    /// <summary>마지막 안내 글자(시험이 읽는다).</summary>
    public static string? LastNotice { get; private set; }

    /// <summary>이 칸에 '받지 않음' 안내가 떠 있는가 — 빠른추가 개체수 칸은 이동안 Enter 로 행을 넣지 않는다(v0.5.1 §24-3).</summary>
    public static bool IsShowingNotice(TextBox box) => States.TryGetValue(box, out var s) && s.Popup?.IsOpen == true;

    /// <summary>칸마다의 상태 — 칸에 들어왔을 때의 값 · 막힘 · 떠 있는 안내.</summary>
    private sealed class State
    {
        public string FocusText = string.Empty;
        public bool Blocked;
        public Popup? Popup;
        public DispatcherTimer? Timer;
    }

    private static readonly ConditionalWeakTable<TextBox, State> States = new();
    private static State Of(TextBox box) => States.GetValue(box, b => new State { FocusText = b.Text ?? string.Empty });

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        box.PreviewTextInput -= OnPreviewTextInput;
        box.PreviewKeyDown -= OnPreviewKeyDown;
        box.GotKeyboardFocus -= OnGotFocus;
        box.LostKeyboardFocus -= OnLostFocus;
        box.PreviewMouseLeftButtonDown -= OnMouseDown;
        DataObject.RemovePastingHandler(box, OnPasting);
        if (e.NewValue is not true) return;

        box.PreviewTextInput += OnPreviewTextInput;
        box.PreviewKeyDown += OnPreviewKeyDown;
        box.GotKeyboardFocus += OnGotFocus;
        box.LostKeyboardFocus += OnLostFocus;
        box.PreviewMouseLeftButtonDown += OnMouseDown;
        DataObject.AddPastingHandler(box, OnPasting);
        // 한글 입력기가 켜져 있으면 조합 중인 글자가 막히지 않고 들어간다 — 숫자 칸에서는 끈다.
        InputMethod.SetIsInputMethodEnabled(box, false);
    }

    private static void OnGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var s = Of(box);
        s.FocusText = box.Text ?? string.Empty;
        s.Blocked = false;
    }

    // 칸을 누르면(다시 고치려는 동작) 막힘을 푼다
    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox box && States.TryGetValue(box, out var s)) s.Blocked = false;
    }

    private static void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox box) HideNotice(box);
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box || string.IsNullOrEmpty(e.Text)) return;
        var s = Of(box);
        if (s.Blocked)
        {
            // 방금 숫자가 아닌 글자를 쳤다 — '1.5' 의 '5' 처럼 이어 치는 숫자도 받지 않는다
            e.Handled = true;
            ShowNotice(box, NotIntegerText);
            return;
        }
        if (!e.Text.All(char.IsAsciiDigit))
        {
            e.Handled = true;
            RejectNotInteger(box);
            return;
        }
        if (!IsAllowed(box, Proposed(box, e.Text)))
        {
            e.Handled = true;
            ShowNotice(box, RangeText(box));
        }
    }

    // 스페이스는 TextInput 으로 오지 않는다(TextBox 가 키로 처리한다) → 키에서 막는다.
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            RejectNotInteger(box);
            return;
        }
        // 지우기·이동·확정 키나 Ctrl 조합(Ctrl+A 등)을 누르면 막힘을 푼다(안내를 본 뒤 고치는 동작)
        if (e.Key is Key.Back or Key.Delete or Key.Tab or Key.Enter or Key.Escape
                  or Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End
            || (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            Of(box).Blocked = false;
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box) return;
        var text = e.DataObject.GetDataPresent(DataFormats.UnicodeText)
            ? e.DataObject.GetData(DataFormats.UnicodeText) as string
            : null;
        // 엑셀 셀을 복사하면 끝에 줄바꿈이 붙는다 — 앞뒤 공백만 걷어 보고, 그래도 숫자가 아니면 막는다.
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !IsAllowed(box, Proposed(box, trimmed)))
        {
            e.CancelCommand();
            ShowNotice(box, !string.IsNullOrEmpty(trimmed) && trimmed.All(char.IsAsciiDigit) ? RangeText(box) : NotIntegerText);
            return;
        }
        if (!ReferenceEquals(trimmed, text))
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, trimmed);
            e.DataObject = data;
        }
    }

    /// <summary>숫자가 아닌 글자 — 받지 않고, 이 칸에 들어왔을 때의 값으로 되돌리고, 안내하는 동안 이어 치는 숫자도 막는다.</summary>
    private static void RejectNotInteger(TextBox box)
    {
        var s = Of(box);
        if (!string.Equals(box.Text, s.FocusText, StringComparison.Ordinal))
        {
            box.Text = s.FocusText;
            box.CaretIndex = box.Text.Length;
        }
        s.Blocked = true;
        ShowNotice(box, NotIntegerText);
    }

    private static string RangeText(TextBox box) =>
        GetMaximum(box) is var max && max < int.MaxValue ? $"0~{max} 사이 정수만 넣습니다" : $"정수만 넣습니다({MaxDigits}자리까지)";

    /// <summary>칸 아래에 안내를 잠깐 보인다. 시간이 지나면 닫고 막힘도 푼다.</summary>
    private static void ShowNotice(TextBox box, string text)
    {
        LastNotice = text;
        var s = Of(box);
        if (s.Popup is null)
        {
            var label = new TextBlock { Text = text, Margin = new Thickness(8, 4, 8, 4), FontSize = 12 };
            label.SetResourceReference(TextBlock.ForegroundProperty, "DangerTextBrush");
            var border = new Border { Child = label, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
            border.SetResourceReference(Border.BackgroundProperty, "InputBackBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "DangerTextBrush");
            s.Popup = new Popup
            {
                PlacementTarget = box, Placement = PlacementMode.Bottom, AllowsTransparency = true,
                StaysOpen = true, Focusable = false, Child = border,
            };
            AutomationPropertiesHelper(border);
        }
        if (s.Popup.Child is Border { Child: TextBlock tb }) tb.Text = text;
        s.Popup.IsOpen = true;

        s.Timer ??= new DispatcherTimer(DispatcherPriority.Normal, box.Dispatcher) { Interval = NoticeDuration };
        s.Timer.Stop();
        s.Timer.Interval = NoticeDuration;
        s.Timer.Tick -= OnTimer;
        s.Timer.Tag = box;
        s.Timer.Tick += OnTimer;
        s.Timer.Start();
    }

    private static void OnTimer(object? sender, EventArgs e)
    {
        if (sender is DispatcherTimer { Tag: TextBox box }) HideNotice(box);
    }

    private static void HideNotice(TextBox box)
    {
        if (!States.TryGetValue(box, out var s)) return;
        s.Blocked = false;
        s.Timer?.Stop();
        if (s.Popup is not null) s.Popup.IsOpen = false;
    }

    private static void AutomationPropertiesHelper(FrameworkElement e) =>
        System.Windows.Automation.AutomationProperties.SetAutomationId(e, "IntegerInputNotice");

    /// <summary>입력이 들어간 뒤의 글자(선택 영역은 바뀐다).</summary>
    private static string Proposed(TextBox box, string input)
    {
        var t = box.Text ?? string.Empty;
        var start = Math.Min(box.SelectionStart, t.Length);
        var len = Math.Min(box.SelectionLength, t.Length - start);
        return t.Remove(start, len).Insert(start, input);
    }

    private static bool IsAllowed(TextBox box, string text)
    {
        if (text.Length == 0) return true;
        if (text.Length > MaxDigits || !text.All(char.IsAsciiDigit)) return false;
        return int.Parse(text) <= GetMaximum(box);
    }
}
