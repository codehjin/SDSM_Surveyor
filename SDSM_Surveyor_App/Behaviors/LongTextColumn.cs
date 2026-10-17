using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Behaviors;

/// <summary>
/// 그리드의 긴 글 열(관찰형 특징·특이사항, 종 입력형 비고 · 10-17 화면 검토). <b>열 너비는 바꾸지 않는다.</b>
/// <list type="bullet">
/// <item>보기 : 칸보다 긴 글은 말줄임(…)으로 보이고, 마우스를 올리면 툴팁으로 전체가 보인다.</item>
/// <item>편집 : 편집칸 바로 아래에 전체 글을 줄바꿈해 보여 준다(치는 대로 따라 바뀐다).
///   그리드 행 높이가 고정이라 편집칸 자체를 늘리지 않는다 — 편집·저장은 지금과 같은 편집칸이 한다.</item>
/// </list>
/// <code>
/// &lt;telerik:RadGridView core:LongTextColumn.Enable="True" …&gt;
///   &lt;telerik:GridViewDataColumn core:LongTextColumn.IsLongText="True" DataMemberBinding="{Binding Note}" …/&gt;
/// </code>
/// 열에 <c>CellTemplate</c> 이 이미 있으면 그대로 둔다(그 템플릿에 툴팁을 직접 단다).
/// </summary>
public static class LongTextColumn
{
    public static readonly DependencyProperty IsLongTextProperty = DependencyProperty.RegisterAttached(
        "IsLongText", typeof(bool), typeof(LongTextColumn), new PropertyMetadata(false));

    public static bool GetIsLongText(DependencyObject d) => (bool)d.GetValue(IsLongTextProperty);
    public static void SetIsLongText(DependencyObject d, bool value) => d.SetValue(IsLongTextProperty, value);

    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable", typeof(bool), typeof(LongTextColumn), new PropertyMetadata(false, OnEnableChanged));

    public static bool GetEnable(DependencyObject d) => (bool)d.GetValue(EnableProperty);
    public static void SetEnable(DependencyObject d, bool value) => d.SetValue(EnableProperty, value);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RadGridView grid) return;
        grid.PreparedCellForEdit -= OnPreparedCellForEdit;
        grid.Loaded -= OnLoaded;
        if (e.NewValue is true)
        {
            grid.PreparedCellForEdit += OnPreparedCellForEdit;
            grid.Loaded += OnLoaded;
            ApplyTemplates(grid);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => ApplyTemplates((RadGridView)sender);

    /// <summary>긴 글 열 중 템플릿이 없는 열에 '말줄임 + 툴팁' 보기 템플릿을 단다.</summary>
    public static void ApplyTemplates(RadGridView grid)
    {
        foreach (var col in grid.Columns.OfType<GridViewDataColumn>())
        {
            if (!GetIsLongText(col) || col.CellTemplate is not null) continue;
            var path = (col.DataMemberBinding as Binding)?.Path?.Path;
            if (string.IsNullOrEmpty(path)) continue;
            col.CellTemplate = (DataTemplate)XamlReader.Parse(
                "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
                $"<TextBlock Text='{{Binding {path}}}' Style='{{StaticResource LongTextCellStyle}}'/>" +
                "</DataTemplate>");
        }
    }

    // ── 편집 : 편집칸 아래 전체 글 미리보기 ─────────────────────────────────
    private static void OnPreparedCellForEdit(object? sender, GridViewPreparingCellForEditEventArgs e)
    {
        if (!GetIsLongText(e.Column) || e.EditingElement is not TextBox box) return;
        AttachPreview(box);
    }

    /// <summary>편집칸 아래에 전체 글을 줄바꿈해 보여 주는 팝업을 붙인다(포커스를 가져가지 않는다).</summary>
    public static Popup AttachPreview(TextBox box)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBox.Text)) { Source = box });
        if (box.TryFindResource("LongTextPreviewTextStyle") is Style ts) text.Style = ts;

        var card = new Border { Child = text };
        if (box.TryFindResource("LongTextPreviewBorderStyle") is Style bs) card.Style = bs;

        var popup = new Popup
        {
            PlacementTarget = box,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            AllowsTransparency = true,
            StaysOpen = true,
            Focusable = false,
            Child = card,
        };
        double min = box.TryFindResource("LongTextPreviewMinWidth") is double m ? m : 320;

        void Update()
        {
            popup.Width = Math.Max(box.ActualWidth, min);
            popup.IsOpen = box.IsKeyboardFocusWithin && box.IsVisible && !string.IsNullOrEmpty(box.Text);
        }
        box.TextChanged += (_, _) => Update();
        box.GotKeyboardFocus += (_, _) => Update();
        box.LostKeyboardFocus += (_, _) => popup.IsOpen = false;
        box.IsVisibleChanged += (_, _) => Update();
        box.Unloaded += (_, _) => popup.IsOpen = false;
        box.SetValue(PreviewProperty, popup);
        box.Dispatcher.BeginInvoke(new Action(Update), System.Windows.Threading.DispatcherPriority.Input);
        return popup;
    }

    /// <summary>붙인 미리보기 팝업(자동 검증이 찾는다).</summary>
    public static readonly DependencyProperty PreviewProperty = DependencyProperty.RegisterAttached(
        "Preview", typeof(Popup), typeof(LongTextColumn), new PropertyMetadata(null));

    public static Popup? GetPreview(DependencyObject d) => (Popup?)d.GetValue(PreviewProperty);
}
