using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Behaviors;

/// <summary>
/// 점수 콤보에서 <b>점수 숫자를 치면 그 점수의 선택지를 바로 고른다</b>(10-17 화면 검토 · 서식수변 평가항목).
/// <list type="bullet">
/// <item>여러 자리는 약 1초(<see cref="Window"/>) 안에 친 숫자를 이어서 본다 — `1` → 1점, 곧이어 `0` → 10점.</item>
/// <item><b>정확히 같은 점수</b>만 고른다. 없는 점수(예: `2`·`20` 이 없는 항목)는 무시한다(지금 선택을 바꾸지 않는다).</item>
/// <item>같은 항목 안에서는 점수가 겹치지 않으므로(10·5·3·1·0 등) 숫자만으로 하나가 정해진다.</item>
/// <item>콤보의 기본 글자 찾기(첫 글자로 항목 이동)가 숫자를 먼저 가로채지 않게 숫자 입력은 여기서 처리하고 끝낸다.</item>
/// </list>
/// Tab → 숫자 → Tab 으로 마우스 없이 입력한다.
/// <code>&lt;telerik:RadComboBox core:ScoreKeySelect.ScorePath="Score" …/&gt;</code>
/// </summary>
public static class ScoreKeySelect
{
    /// <summary>이어 치기로 보는 간격.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(1000);

    /// <summary>선택지에서 점수를 읽을 속성 이름(예: <c>Score</c>). 정하면 동작이 켜진다.</summary>
    public static readonly DependencyProperty ScorePathProperty = DependencyProperty.RegisterAttached(
        "ScorePath", typeof(string), typeof(ScoreKeySelect), new PropertyMetadata(null, OnScorePathChanged));

    public static string? GetScorePath(DependencyObject d) => (string?)d.GetValue(ScorePathProperty);
    public static void SetScorePath(DependencyObject d, string? value) => d.SetValue(ScorePathProperty, value);

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(State), typeof(ScoreKeySelect), new PropertyMetadata(null));

    private sealed class State
    {
        public string Buffer = string.Empty;
        public DateTime Last = DateTime.MinValue;
    }

    private static void OnScorePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RadComboBox box) return;
        box.PreviewTextInput -= OnPreviewTextInput;
        if (e.NewValue is string { Length: > 0 })
        {
            box.SetValue(StateProperty, new State());
            box.PreviewTextInput += OnPreviewTextInput;
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not RadComboBox box || string.IsNullOrEmpty(e.Text) || !e.Text.All(char.IsDigit)) return;
        e.Handled = true;   // 숫자는 점수 고르기로만 쓴다(기본 글자 찾기와 겹치지 않게)

        var st = (State)box.GetValue(StateProperty);
        var now = DateTime.Now;
        st.Buffer = (now - st.Last <= Window ? st.Buffer : string.Empty) + e.Text;
        st.Last = now;

        if (!double.TryParse(st.Buffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wanted)) return;
        var hit = Pick(box, GetScorePath(box)!, wanted);
        if (hit is not null) box.SelectedItem = hit;   // 없는 점수는 무시한다
    }

    /// <summary>점수가 정확히 같은 선택지(없으면 null).</summary>
    public static object? Pick(RadComboBox box, string path, double score)
    {
        foreach (var item in box.Items)
        {
            var p = item?.GetType().GetProperty(path);
            if (p?.GetValue(item) is IConvertible c && Math.Abs(Convert.ToDouble(c, CultureInfo.InvariantCulture) - score) < 1e-9)
                return item;
        }
        return null;
    }
}
