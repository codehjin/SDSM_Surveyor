using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SDSM_Surveyor_App.Behaviors;

/// <summary>
/// 글상자에 <b>0 이상 정수만</b> 들어가게 한다(B-6). 숫자 외 키·붙여넣기를 막는다.
///
/// 관리자 모델이 정수(<c>int?</c>)라 소수·문자가 들어가면 일괄등록이 오류를 낸다.
/// 값은 지금처럼 문자열로 저장한다(세션 형식 그대로) — 여기서는 <b>새로 치는 것</b>만 막는다.
/// 불러온 세션에 이미 있던 소수·문자는 지우지 않는다(화면 경고는 VM 이 띄운다).
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

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        box.PreviewTextInput -= OnPreviewTextInput;
        box.PreviewKeyDown -= OnPreviewKeyDown;
        DataObject.RemovePastingHandler(box, OnPasting);
        if (e.NewValue is not true) return;

        box.PreviewTextInput += OnPreviewTextInput;
        box.PreviewKeyDown += OnPreviewKeyDown;
        DataObject.AddPastingHandler(box, OnPasting);
        // 한글 입력기가 켜져 있으면 조합 중인 글자가 막히지 않고 들어간다 — 숫자 칸에서는 끈다.
        InputMethod.SetIsInputMethodEnabled(box, false);
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox box && !IsAllowed(box, Proposed(box, e.Text))) e.Handled = true;
    }

    // 스페이스는 TextInput 으로 오지 않는다(TextBox 가 키로 처리한다) → 키에서 막는다.
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) e.Handled = true;
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
            return;
        }
        if (!ReferenceEquals(trimmed, text))
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, trimmed);
            e.DataObject = data;
        }
    }

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
