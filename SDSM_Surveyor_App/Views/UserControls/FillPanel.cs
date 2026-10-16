using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 세로로 쌓되 <b>한 칸(<see cref="FillProperty"/>)만 남은 높이를 받는</b> 패널(C-3 · 화면 전체 스크롤).
///
/// 조사개황·조사환경·건강성을 펼치면 종 그리드가 밀려 몇 행밖에 안 보였다. 그래서 본문을 바깥
/// <see cref="ScrollViewer"/> 로 감싸고, 그리드 칸에 최소 높이(<c>MinHeight</c> · 8행)를 준다.
/// <list type="bullet">
/// <item>다 들어가면 지금과 같다 — 채움 칸이 남은 높이를 모두 받고 바깥 스크롤은 생기지 않는다.</item>
/// <item>모자라면 채움 칸은 최소 높이를 지키고, 그만큼 패널이 길어져 바깥이 스크롤된다.</item>
/// </list>
/// ⚠ <c>RadGridView</c> 를 높이 제한 없는 곳에 두면 행 가상화가 꺼져 수백 행에서 느려진다.
///   그래서 채움 칸은 언제나 <b>유한한 높이</b>(남은 높이와 최소 높이 중 큰 값)를 받는다
///   (스크롤 안의 맨 바깥 패널은 바깥 스크롤의 보이는 높이를 기준으로 삼는다).
/// ⚠ WPF 는 요소가 받은 높이보다 크게 원해도 <c>DesiredSize</c> 를 받은 높이로 잘라 알린다 — 안쪽 최소 높이가 바깥으로 올라오지 않는다.
///   그래서 채움 칸을 재기 전에 그 안의 <b>최소 필요 높이</b>를 직접 센다(<see cref="MinHeightOf"/>):
///   채움 칸 안에 다시 <see cref="FillPanel"/> 이 있으면(분류군 화면) 그 패널의 고정 칸 합 + 그 채움 칸 최소 높이.
/// ⚠ 자식은 <b>나온 순서대로</b> 쌓인다(<c>Grid.Row</c> 는 보지 않는다).
/// </summary>
public class FillPanel : Panel
{
    /// <summary>남은 높이를 받는 칸. 패널마다 하나(둘 이상이면 첫 번째만).</summary>
    public static readonly DependencyProperty FillProperty = DependencyProperty.RegisterAttached(
        "Fill", typeof(bool), typeof(FillPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    public static bool GetFill(DependencyObject d) => (bool)d.GetValue(FillProperty);
    public static void SetFill(DependencyObject d, bool value) => d.SetValue(FillProperty, value);

    private ScrollViewer? _viewport;

    public FillPanel()
    {
        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();
    }

    private UIElement? FillChild =>
        InternalChildren.Cast<UIElement>().FirstOrDefault(c => c.Visibility != Visibility.Collapsed && GetFill(c));

    protected override Size MeasureOverride(Size available)
    {
        double height = available.Height;
        // 스크롤 안(높이 무한)이면 보이는 높이를 쓴다. 이 패널의 Margin 은 FrameworkElement 가 따로 더하므로 뺀다.
        if (double.IsInfinity(height) && ViewportHeight() is double vh)
            height = Math.Max(0, vh - Margin.Top - Margin.Bottom);

        var fill = FillChild;
        double fixedSum = MeasureFixed(available.Width, fill, out double width);

        double minRequired = fixedSum;
        if (fill is not null)
        {
            double fillMin = MinHeightOf(fill, available.Width);
            minRequired += fillMin;
            double fillHeight = double.IsInfinity(height) ? double.PositiveInfinity : Math.Max(height - fixedSum, fillMin);
            fill.Measure(new Size(available.Width, fillHeight));
            fixedSum += double.IsInfinity(fillHeight) ? fill.DesiredSize.Height : fillHeight;
            width = Math.Max(width, fill.DesiredSize.Width);
        }
        NotifyIfMinChanged(minRequired);
        return new Size(double.IsInfinity(available.Width) ? width : available.Width, fixedSum);
    }

    private double _lastMinRequired = double.NaN;

    /// <summary>
    /// 이 패널은 받은 높이를 늘 다 채우므로, 블록을 접어 필요 높이가 줄어도 <c>DesiredSize</c> 가 그대로라
    /// 바깥 패널이 다시 재지 않는다(바깥 스크롤이 남는다). 최소 필요 높이가 바뀌면 바깥 FillPanel 에 직접 알린다.
    /// </summary>
    private void NotifyIfMinChanged(double minRequired)
    {
        if (!double.IsNaN(_lastMinRequired) && Math.Abs(minRequired - _lastMinRequired) < 0.5) return;
        bool first = double.IsNaN(_lastMinRequired);
        _lastMinRequired = minRequired;
        if (first) return;
        for (DependencyObject? d = VisualTreeHelper.GetParent(this); d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is FillPanel outer) outer.InvalidateMeasure();
    }

    protected override Size ArrangeOverride(Size final)
    {
        var fill = FillChild;
        double fixedSum = InternalChildren.Cast<UIElement>().Where(c => c != fill).Sum(c => c.DesiredSize.Height);
        double fillHeight = fill is null ? 0 : Math.Max(fill.DesiredSize.Height, final.Height - fixedSum);

        double y = 0;
        foreach (UIElement c in InternalChildren)
        {
            double h = c == fill ? fillHeight : c.DesiredSize.Height;
            c.Arrange(new Rect(0, y, final.Width, h));
            y += h;
        }
        return new Size(final.Width, Math.Max(final.Height, y));
    }

    /// <summary>채움 칸이 아닌 자식을 높이 제한 없이 재고 그 높이 합을 돌려준다.</summary>
    private double MeasureFixed(double width, UIElement? fill, out double maxWidth)
    {
        double sum = 0; maxWidth = 0;
        foreach (UIElement c in InternalChildren)
        {
            if (c == fill) continue;
            c.Measure(new Size(width, double.PositiveInfinity));
            sum += c.DesiredSize.Height;
            maxWidth = Math.Max(maxWidth, c.DesiredSize.Width);
        }
        return sum;
    }

    /// <summary>이 패널이 내용을 자르지 않으려면 필요한 최소 높이(Margin 제외).</summary>
    private double MinRequired(double width)
    {
        var fill = FillChild;
        double sum = MeasureFixed(width, fill, out _);
        return fill is null ? sum : sum + MinHeightOf(fill, width);
    }

    /// <summary>
    /// 요소의 최소 필요 높이(Margin 포함). 안에 <see cref="FillPanel"/> 이 있으면 그 패널 기준으로 센다.
    /// 감싸는 요소(패널·장식·UserControl·ContentPresenter)만 따라 내려가고, 그 밖의 컨트롤(그리드 등)은 자기 MinHeight 만 본다.
    /// </summary>
    private static double MinHeightOf(UIElement e, double width, int depth = 0)
    {
        if (e.Visibility == Visibility.Collapsed) return 0;
        var fe = e as FrameworkElement;
        double mv = fe is null ? 0 : fe.Margin.Top + fe.Margin.Bottom;
        double mh = fe is null ? 0 : fe.Margin.Left + fe.Margin.Right;
        double own = fe?.MinHeight ?? 0;

        if (e is FillPanel fp) return Math.Max(own, fp.MinRequired(Math.Max(0, width - mh))) + mv;

        bool container = e is Panel or Decorator or ContentPresenter or UserControl;
        if (!container || depth > 12) return own + mv;

        double inner = 0;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(e); i++)
            if (VisualTreeHelper.GetChild(e, i) is UIElement c)
                inner = Math.Max(inner, MinHeightOf(c, Math.Max(0, width - mh), depth + 1));
        return Math.Max(own, inner) + mv;
    }

    /// <summary>바깥 스크롤의 보이는 높이. 아직 배치 전이면 스크롤 자신의 높이.</summary>
    private double? ViewportHeight()
    {
        _viewport ??= FindScrollViewer();
        if (_viewport is null) return null;
        var h = _viewport.ViewportHeight > 0 ? _viewport.ViewportHeight : _viewport.ActualHeight;
        return h > 0 ? h : null;
    }

    private ScrollViewer? FindScrollViewer()
    {
        for (DependencyObject? d = VisualTreeHelper.GetParent(this); d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ScrollViewer sv) return sv;
            if (d is FillPanel) return null;   // 바깥 FillPanel 이 이미 높이를 준다
        }
        return null;
    }

    private void Hook()
    {
        Unhook();
        _viewport = FindScrollViewer();
        if (_viewport is not null) _viewport.ScrollChanged += OnScrollChanged;
        InvalidateMeasure();
    }

    private void Unhook()
    {
        if (_viewport is not null) _viewport.ScrollChanged -= OnScrollChanged;
        _viewport = null;
    }

    // 창 크기가 바뀌면 보이는 높이가 바뀐다 — 다시 잰다(첫 배치 직후 한 번 더 도는 것도 여기서).
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.Source == _viewport && e.ViewportHeightChange != 0) InvalidateMeasure();
    }
}
