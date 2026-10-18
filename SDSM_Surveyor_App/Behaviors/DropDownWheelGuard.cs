using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Behaviors;

/// <summary>
/// 드롭다운 목록이 열렸을 때의 휠 규칙 — 앱의 모든 창·모든 드롭다운에 한 번에 건다(<see cref="Register"/> · App 생성자).
///
/// · 목록 <b>안</b>에서 굴린 휠은 목록만 움직인다. 목록 끝에 닿아도 바깥 화면으로 넘기지 않는다.
/// · 목록 <b>밖</b>에서 굴린 휠은 목록을 닫는다(그 한 칸은 소비한다 — 바깥이 움직이면 목록창이 칸에서 떨어져 보인다).
/// · 닫힌 <see cref="RadComboBox"/> 위의 휠은 값을 바꾸지 않고 바깥 화면을 스크롤한다(<c>IsMouseWheelEnabled=False</c>).
///
/// 원인(최종 입력 시험 1단계): 목록창은 별도 창(<see cref="Popup"/>)인데 휠 이벤트는 논리 트리를 따라 콤보 → 바깥
/// <c>BodyScroll</c> 까지 올라간다. C-3 <see cref="NestedWheelScroll"/> 은 "안쪽 스크롤이 끝이면 바깥을 굴린다"를
/// 목록창 안의 휠에도 적용했고, 바깥이 움직여도 목록창은 제자리라 칸에서 떨어져 보였다.
/// 또 Telerik <c>RadComboBox.IsMouseWheelEnabled</c>(기본 True)는 포커스가 있는 닫힌 콤보 위의 휠로 선택값을 바꿨다.
///
/// 대상: <see cref="RadComboBox"/>(SingleChoiceBox·MultiChoiceBox·지점·빠른추가·서식수변 점수 포함) ·
/// <see cref="RadAutoCompleteBox"/> · <see cref="RadDatePicker"/> · WPF <see cref="ComboBox"/>·<see cref="DatePicker"/>.
/// </summary>
public static class DropDownWheelGuard
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnWindowPreviewWheel));
        // 목록창 안에서 처리되고 남은(또는 아무도 처리하지 않은) 휠이 목록창 밖으로 올라가지 않게 막는다
        EventManager.RegisterClassHandler(typeof(Popup), UIElement.MouseWheelEvent, new MouseWheelEventHandler(OnPopupWheel), true);
        EventManager.RegisterClassHandler(typeof(RadComboBox), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnComboLoaded));
    }

    private static void OnComboLoaded(object sender, RoutedEventArgs e) =>
        ((RadComboBox)sender).SetCurrentValue(RadComboBox.IsMouseWheelEnabledProperty, false);

    private static void OnPopupWheel(object sender, MouseWheelEventArgs e) => e.Handled = true;

    private static void OnWindowPreviewWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not Window w) return;
        var owner = FindOpen(w);
        if (owner is null)
        {
            ScrollPastClosedCombo(e);
            return;
        }

        // 목록창 안 — 그대로 흘려보낸다(목록이 스크롤하고, Popup 처리기가 바깥으로 못 나가게 막는다)
        if (e.OriginalSource is DependencyObject src && InOpenPopup(src, owner)) return;

        // 목록창 밖 — 닫는다
        e.Handled = true;
        Close(owner);
    }

    /// <summary>
    /// 닫힌 RadComboBox 위의 휠 — <c>IsMouseWheelEnabled=False</c> 여도 콤보가 휠을 처리됨으로 표시해 화면이 멈춘다.
    /// 콤보 바깥에서 그 방향으로 움직일 수 있는 가장 가까운 스크롤을 대신 굴린다(NestedWheelScroll 과 같은 순서 · 휠 한 칸 = 3줄).
    /// </summary>
    private static void ScrollPastClosedCombo(MouseWheelEventArgs e)
    {
        DependencyObject? d = e.OriginalSource as DependencyObject;
        RadComboBox? combo = null;
        for (; d is not null; d = Parent(d))
        {
            if (d is RadComboBox c) { combo = c; continue; }
            if (combo is null || d is not ScrollViewer sv || sv.ScrollableHeight <= 0) continue;
            e.Handled = true;   // 콤보가 처리됨으로 표시해 멈추는 것을 막는다(움직일 곳이 없으면 그대로 둔다)
            bool canGo = e.Delta > 0 ? sv.VerticalOffset > 0 : sv.VerticalOffset < sv.ScrollableHeight - 0.5;
            if (canGo)
            {
                for (int i = 0; i < SystemParameters.WheelScrollLines; i++)
                {
                    if (e.Delta > 0) sv.LineUp(); else sv.LineDown();
                }
                return;
            }
        }
    }

    private static DependencyObject? Parent(DependencyObject d) =>
        d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

    /// <summary>창 안에서 목록이 열린 드롭다운 하나(없으면 null).</summary>
    internal static FrameworkElement? FindOpen(DependencyObject root)
    {
        var q = new Queue<DependencyObject>();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            var d = q.Dequeue();
            if (d is FrameworkElement fe && IsOpen(fe)) return fe;
            if (d is UIElement { IsVisible: false }) continue;
            int n = d is Visual ? VisualTreeHelper.GetChildrenCount(d) : 0;
            for (int i = 0; i < n; i++) q.Enqueue(VisualTreeHelper.GetChild(d, i));
        }
        return null;
    }

    private static bool IsOpen(FrameworkElement fe) => fe switch
    {
        RadComboBox c => c.IsDropDownOpen,
        RadAutoCompleteBox a => a.IsDropDownOpen,
        RadDatePicker p => p.IsDropDownOpen,
        ComboBox c => c.IsDropDownOpen,
        DatePicker p => p.IsDropDownOpen,
        _ => false,
    };

    private static void Close(FrameworkElement fe)
    {
        switch (fe)
        {
            case RadComboBox c: c.IsDropDownOpen = false; break;
            case RadAutoCompleteBox a: a.IsDropDownOpen = false; break;
            case RadDatePicker p: p.IsDropDownOpen = false; break;
            case ComboBox c: c.IsDropDownOpen = false; break;
            case DatePicker p: p.IsDropDownOpen = false; break;
        }
    }

    /// <summary>휠이 난 곳이 그 드롭다운의 열린 목록창 안인가 — 목록창은 별도 시각 트리라 위로 올라가면 그 Popup 의 Child 를 만난다.</summary>
    private static bool InOpenPopup(DependencyObject src, FrameworkElement owner)
    {
        var roots = new List<DependencyObject>();
        CollectOpenPopupChildren(owner, roots);
        if (roots.Count == 0) return false;
        for (var d = src; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (roots.Contains(d)) return true;
        return false;
    }

    private static void CollectOpenPopupChildren(DependencyObject d, List<DependencyObject> roots)
    {
        if (d is Popup { IsOpen: true, Child: { } child }) roots.Add(child);
        int n = d is Visual ? VisualTreeHelper.GetChildrenCount(d) : 0;
        for (int i = 0; i < n; i++) CollectOpenPopupChildren(VisualTreeHelper.GetChild(d, i), roots);
    }
}
