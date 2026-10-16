using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SDSM_Surveyor_App.Behaviors;

/// <summary>
/// 바깥 스크롤 안에 있는 그리드·스크롤 칸 위에서 휠을 굴릴 때 —
/// <b>안쪽이 더 갈 수 있으면 안쪽이, 끝에 닿았으면 바깥이</b> 스크롤되게 한다(C-3).
///
/// WPF <see cref="ScrollViewer"/> 는 끝에 닿아도 휠 이벤트를 처리한 것으로 표시해 바깥으로 넘기지 않는다
/// (<c>RadGridView</c> 의 <c>GridViewScrollViewer</c> 도 <see cref="ScrollViewer"/> 를 상속한다 — 메타데이터 확인).
/// 그래서 바깥 스크롤에서 미리보기 단계(<c>PreviewMouseWheel</c>)로 먼저 보고, 끝에 닿은 안쪽만 대신 처리한다.
/// 안쪽 스크롤이 없는 곳(입력칸·숫자 상자 등)은 손대지 않는다 — 원래 동작 그대로다.
///
/// <code>&lt;ScrollViewer core:NestedWheelScroll.IsEnabled="True" …&gt;</code>
/// </summary>
public static class NestedWheelScroll
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(NestedWheelScroll), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv) return;
        sv.PreviewMouseWheel -= OnPreviewMouseWheel;
        if ((bool)e.NewValue) sv.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not ScrollViewer outer || e.Delta == 0) return;

        bool sawInner = false;
        for (var d = e.OriginalSource as DependencyObject; d is not null && d != outer; d = Parent(d))
        {
            if (d is not ScrollViewer inner || inner.ScrollableHeight <= 0) continue;
            sawInner = true;
            bool canGo = e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight - 0.5;
            if (canGo) return;              // 안쪽이 아직 갈 수 있다 — 안쪽에 맡긴다
        }
        if (!sawInner) return;              // 안쪽 스크롤이 없다 — 원래대로 바깥이 받는다

        // 안쪽이 끝에 닿았다 — 바깥을 대신 굴린다(줄 단위 3칸 = 기본 휠 한 칸과 같은 양).
        e.Handled = true;
        for (int i = 0; i < SystemParameters.WheelScrollLines; i++)
        {
            if (e.Delta > 0) outer.LineUp(); else outer.LineDown();
        }
    }

    private static DependencyObject? Parent(DependencyObject d) =>
        d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
}
