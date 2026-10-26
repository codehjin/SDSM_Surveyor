using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace SDSM_Surveyor_App.Behaviors;

/// <summary>
/// 글상자에 <b>소수(숫자 · 소수점 하나)만</b> 들어가게 한다 — <see cref="IntegerInput"/> 의 소수판(v0.5.1 · 5차 연계 시험 새 문제 1).
///
/// 저서 환경 6칸(하천폭 · 수폭 · 평균수심 · 평균유속 · 기온 · 수온)과 수질 측정값은 관리자 모델이 <c>double?</c> 이라
/// 글자가 섞이면 관리자 일괄등록이 그 기록 전체를 '형변환실패'로 막는다(5차 시험에서 '18.3Z' 로 실제로 일어났다).
/// 값은 지금처럼 문자열로 저장한다(세션 형식 그대로) — 여기서는 <b>새로 치는 것</b>만 막는다.
/// 불러온 세션에 이미 있던 글자는 지우지 않는다(화면 경고 · [일괄입력용] 막힘은 VM 이 맡는다 — <c>DecimalNumber</c>).
///
/// 막는 방식은 N2(정수 칸)와 같다 — 숫자 · 소수점 · (허용한 칸만) 맨 앞 '-' 가 아닌 글자를 치면
/// ① 받지 않고 ② 칸을 이 칸에 들어왔을 때의 값으로 되돌리고 ③ 안내가 떠 있는 동안 이어 치는 글자도 받지 않는다.
/// 글자만 몰래 빼고 넣지 않는다. 소수점을 두 번 치는 것도 같은 방식으로 막는다.
/// 붙여넣기는 들어간 뒤의 글자가 소수가 아니면 통째로 받지 않고 같은 안내를 보인다.
/// 한글 입력기는 끈다(조합 중인 글자가 막히지 않고 들어가는 것을 막는다 · IntegerInput 과 같음).
///
/// <code>
/// &lt;telerik:RadWatermarkTextBox core:DecimalInput.IsEnabled="True" core:DecimalInput.AllowNegative="True" …/&gt;
/// </code>
/// </summary>
public static class DecimalInput
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(DecimalInput), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    /// <summary>맨 앞 '-' 를 받는가 — 음수가 뜻이 있는 칸(기온)만 켠다.</summary>
    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.RegisterAttached(
        "AllowNegative", typeof(bool), typeof(DecimalInput), new PropertyMetadata(false));

    public static bool GetAllowNegative(DependencyObject d) => (bool)d.GetValue(AllowNegativeProperty);
    public static void SetAllowNegative(DependencyObject d, bool value) => d.SetValue(AllowNegativeProperty, value);

    // 자릿수 막기(부호 · 소수점 포함) — double 을 끝자리까지 적은 값('3.9619888663636367' · 2026-03 최종본 수질 일당유량)도 들어가게 넉넉히
    private const int MaxLength = 24;

    /// <summary>안내를 보이는 시간. 이 동안은 이어 치는 글자도 받지 않는다(Backspace 등으로 바로 풀림).</summary>
    internal static TimeSpan NoticeDuration = TimeSpan.FromMilliseconds(2500);

    public const string NotDecimalText = "숫자만 넣습니다 — 글자 · 쉼표는 받지 않습니다(소수점은 하나)";
    public const string NotDecimalNegativeText = "숫자만 넣습니다 — 글자 · 쉼표는 받지 않습니다(소수점은 하나 · 맨 앞 - 가능)";

    /// <summary>마지막 안내 글자(시험이 읽는다).</summary>
    public static string? LastNotice { get; private set; }

    /// <summary>이 칸에 '받지 않음' 안내가 떠 있는가 — 빠른추가 개체수 칸은 이동안 Enter 로 행을 넣지 않는다(§24-3).</summary>
    public static bool IsShowingNotice(TextBox box) => States.TryGetValue(box, out var s) && s.Popup?.IsOpen == true;

    /// <summary>
    /// 칸마다 안내 글을 바꾼다(비우면 기본 글). 수질 측정 칸은 검출한계 미만을 적는 방법을 함께 보인다(<see cref="WaterNdText"/> · §24-4).
    /// </summary>
    public static readonly DependencyProperty NoticeProperty = DependencyProperty.RegisterAttached(
        "Notice", typeof(string), typeof(DecimalInput), new PropertyMetadata(null));

    public static string? GetNotice(DependencyObject d) => (string?)d.GetValue(NoticeProperty);
    public static void SetNotice(DependencyObject d, string? value) => d.SetValue(NoticeProperty, value);

    /// <summary>수질 측정 칸 안내 — 검출한계 미만('&lt;0.5' · 'ND')은 칸을 비우고 비고(특이사항)에 적는다(§24-4).</summary>
    public const string WaterNdText = "숫자만 넣습니다 — 검출한계 미만은 칸을 비우고 비고(특이사항)에 ND 또는 <값을 적으세요";

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
        if (e.NewValue is not true) { InputMethod.SetIsInputMethodEnabled(box, true); return; }

        box.PreviewTextInput += OnPreviewTextInput;
        box.PreviewKeyDown += OnPreviewKeyDown;
        box.GotKeyboardFocus += OnGotFocus;
        box.LostKeyboardFocus += OnLostFocus;
        box.PreviewMouseLeftButtonDown += OnMouseDown;
        DataObject.AddPastingHandler(box, OnPasting);
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
            // 방금 받지 않는 글자를 쳤다 — '18.3Z5' 의 '5' 처럼 이어 치는 글자도 받지 않는다
            e.Handled = true;
            ShowNotice(box, NoticeText(box));
            return;
        }
        if (!IsTyping(Proposed(box, e.Text), GetAllowNegative(box)))
        {
            e.Handled = true;
            Reject(box);
        }
    }

    // 스페이스는 TextInput 으로 오지 않는다(TextBox 가 키로 처리한다) → 키에서 막는다.
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            Reject(box);
            return;
        }
        // 지우기 · 이동 · 확정 키나 Ctrl 조합(Ctrl+A 등)을 누르면 막힘을 푼다(안내를 본 뒤 고치는 동작)
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
        // 엑셀 셀을 복사하면 끝에 줄바꿈이 붙는다 — 앞뒤 공백만 걷어 보고, 그래도 소수가 아니면 통째로 막는다.
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !IsTyping(Proposed(box, trimmed), GetAllowNegative(box)))
        {
            e.CancelCommand();
            ShowNotice(box, NoticeText(box));
            return;
        }
        if (!ReferenceEquals(trimmed, text))
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, trimmed);
            e.DataObject = data;
        }
    }

    /// <summary>받지 않는 글자 — 이 칸에 들어왔을 때의 값으로 되돌리고, 안내하는 동안 이어 치는 글자도 막는다.</summary>
    private static void Reject(TextBox box)
    {
        var s = Of(box);
        if (!string.Equals(box.Text, s.FocusText, StringComparison.Ordinal))
        {
            box.Text = s.FocusText;
            box.CaretIndex = box.Text.Length;
        }
        s.Blocked = true;
        ShowNotice(box, NoticeText(box));
    }

    private static string NoticeText(TextBox box) =>
        GetNotice(box) is { Length: > 0 } custom ? custom : GetAllowNegative(box) ? NotDecimalNegativeText : NotDecimalText;

    /// <summary>
    /// 치는 중의 글자로 받을 수 있는가 — 숫자 · 소수점 하나 · (허용한 칸만) 맨 앞 '-'.
    /// 치는 도중의 '-' · '5.' · '.' 은 받는다(다 친 값의 점검은 <c>DecimalNumber.IsValid</c>).
    /// </summary>
    public static bool IsTyping(string text, bool allowNegative)
    {
        if (text.Length == 0) return true;
        if (text.Length > MaxLength) return false;
        var body = allowNegative && text[0] == '-' ? text[1..] : text;
        return body.All(c => char.IsAsciiDigit(c) || c == '.') && body.Count(c => c == '.') <= 1;
    }

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
            System.Windows.Automation.AutomationProperties.SetAutomationId(border, "DecimalInputNotice");
            s.Popup = new Popup
            {
                PlacementTarget = box, Placement = PlacementMode.Bottom, AllowsTransparency = true,
                StaysOpen = true, Focusable = false, Child = border,
            };
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

    /// <summary>입력이 들어간 뒤의 글자(선택 영역은 바뀐다).</summary>
    private static string Proposed(TextBox box, string input)
    {
        var t = box.Text ?? string.Empty;
        var start = Math.Min(box.SelectionStart, t.Length);
        var len = Math.Min(box.SelectionLength, t.Length - start);
        return t.Remove(start, len).Insert(start, input);
    }
}
