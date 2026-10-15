using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SDSM_Surveyor_App.Helpers;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 지도 이미지 뷰어. 휠 확대/축소 · 끌어서 이동 · 마커 한 개 · 보정용 점 두 개.
///
/// 좌표계 규칙 — 이것만 지키면 나머지는 자동이다.
///  · <c>Stage</c>(Canvas) 는 **이미지 픽셀 크기**를 갖는다.
///  · 자식은 픽셀 좌표로 <c>Canvas.Left/Top</c> 에 놓는다.
///  · 확대·이동은 Canvas 전체에 걸린 Transform 이 처리하므로 자식은 저절로 따라간다.
///  · 화면좌표 = 픽셀 * zoom + pan  (ScaleTransform → TranslateTransform 순서)
/// </summary>
public partial class PanZoomImage : UserControl
{
    private const double MaxZoom = 8.0;
    private const double WheelStep = 1.15;
    private const double ClickSlop = 4.0;   // 이보다 적게 움직이면 끌기가 아니라 클릭

    private double _fitZoom = 1.0;
    private bool _fitted;        // 이번 이미지에 대해 한 번이라도 맞췄는지
    private bool _dragging;
    private Point _dragStart;
    private Point _panStart;

    public PanZoomImage() => InitializeComponent();

    /// <summary>직전 <see cref="SetImage"/> 가 실패한 이유(성공이면 null). 화면이 그대로 보여 준다.</summary>
    public string? LastError { get; private set; }

    /// <summary>이미지의 실제 픽셀 크기(불러오기 실패면 0).</summary>
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }

    /// <summary>보정용 기준점을 클릭으로 찍는 중인지.</summary>
    public bool PickMode { get; set; }

    /// <summary>보정 모드에서 지도를 클릭했다(이미지 픽셀 좌표).</summary>
    public event EventHandler<Point>? PointPicked;

    // ── 이미지 ────────────────────────────────────────────────────────────────

    /// <summary>지도 파일을 띄운다. 실패하면 비운다. 파일을 잠그지 않는다.</summary>
    public bool SetImage(string? path)
    {
        HideMarker();
        ClearPicks();

        _fitted = false;

        var bmp = ImageSize.Load(path ?? string.Empty, out var why);
        LastError = why;
        if (bmp is null)
        {
            MapImage.Source = null;
            PixelWidth = PixelHeight = 0;
            Stage.Width = Stage.Height = 0;
            return false;
        }

        PixelWidth = bmp.PixelWidth;
        PixelHeight = bmp.PixelHeight;

        // ⚠ DIP 가 아니라 픽셀로 고정한다. 시드 지도 중에는 DPI 가 82 인 것이 있어
        //    크기를 지정하지 않으면 17% 크게 그려지고 마커가 어긋난다.
        MapImage.Source = bmp;
        MapImage.Width = PixelWidth;
        MapImage.Height = PixelHeight;
        Stage.Width = PixelWidth;
        Stage.Height = PixelHeight;

        Fit();
        return true;
    }

    /// <summary>이미지 전체가 보이도록 맞춘다.</summary>
    public void Fit()
    {
        if (PixelWidth <= 0 || PixelHeight <= 0) return;
        var vw = Viewport.ActualWidth;
        var vh = Viewport.ActualHeight;
        if (vw <= 0 || vh <= 0) return;

        _fitZoom = Math.Min(vw / PixelWidth, vh / PixelHeight);
        if (_fitZoom <= 0 || double.IsInfinity(_fitZoom)) _fitZoom = 1.0;

        // 아주 작은 지도를 넣으면 fit 배율이 상한을 넘는다. 그대로 두면
        // 휠에서 Math.Clamp(min > max) 가 예외를 던져 앱이 죽는다.
        if (_fitZoom > MaxZoom) _fitZoom = MaxZoom;

        Zoom.ScaleX = Zoom.ScaleY = _fitZoom;
        Pan.X = (vw - PixelWidth * _fitZoom) / 2;
        Pan.Y = (vh - PixelHeight * _fitZoom) / 2;
        _fitted = true;

        RescaleOverlays();
    }

    // ── 마커 ──────────────────────────────────────────────────────────────────

    /// <summary>현재 입력 좌표 마커를 이미지 픽셀 위치에 찍는다.</summary>
    public void ShowMarker(double px, double py, string? label)
    {
        _markerPx = px;
        _markerPy = py;
        MarkerDot.Visibility = Visibility.Visible;
        Canvas.SetLeft(MarkerDot, px - MarkerDot.Width / 2);
        Canvas.SetTop(MarkerDot, py - MarkerDot.Height / 2);

        if (string.IsNullOrWhiteSpace(label))
        {
            MarkerLabel.Visibility = Visibility.Collapsed;
            return;
        }

        MarkerText.Text = label;
        MarkerLabel.Visibility = Visibility.Visible;

        // 마커가 이미지 위쪽에 붙어 있으면 라벨을 아래로 내린다(잘려 보이지 않게).
        var above = py > 40;
        Canvas.SetLeft(MarkerLabel, px + 12);
        Canvas.SetTop(MarkerLabel, above ? py - 26 : py + 12);
    }

    public void HideMarker()
    {
        _markerPx = _markerPy = null;
        MarkerDot.Visibility = Visibility.Collapsed;
        MarkerLabel.Visibility = Visibility.Collapsed;
    }

    private double? _markerPx, _markerPy;

    /// <summary>
    /// 마커·라벨·보정점은 지도와 함께 커지면 안 된다 — 8배로 확대하면 16px 점이
    /// 128px 덩어리가 되어 정작 봐야 할 지점을 덮는다. 배율의 역수를 걸어 크기를 고정한다.
    /// </summary>
    private void RescaleOverlays()
    {
        var z = Zoom.ScaleX;
        if (z <= 0) return;
        var inv = 1 / z;

        foreach (FrameworkElement el in new FrameworkElement[] { MarkerDot, MarkerLabel, PickA, PickB })
        {
            el.RenderTransformOrigin = ReferenceEquals(el, MarkerLabel) ? new Point(0, 0.5) : new Point(0.5, 0.5);
            el.RenderTransform = new ScaleTransform(inv, inv);
        }

        // 라벨은 크기뿐 아니라 **떨어진 거리**도 화면 기준으로 고정해야 한다.
        // Canvas.Left/Top 은 픽셀 좌표라 그대로 두면 배율만큼 멀어지거나 붙는다.
        if (_markerPx is double mx && _markerPy is double my)
        {
            Canvas.SetLeft(MarkerLabel, mx + 12 * inv);
            Canvas.SetTop(MarkerLabel, my > 40 ? my - 26 * inv : my + 12 * inv);
        }
    }

    // ── 보정 기준점 ───────────────────────────────────────────────────────────

    /// <summary>기준점 표시(index 0·1).</summary>
    public void SetPick(int index, double px, double py)
    {
        var dot = index == 0 ? PickA : PickB;
        dot.Visibility = Visibility.Visible;
        Canvas.SetLeft(dot, px - dot.Width / 2);
        Canvas.SetTop(dot, py - dot.Height / 2);
    }

    public void ClearPicks()
    {
        PickA.Visibility = Visibility.Collapsed;
        PickB.Visibility = Visibility.Collapsed;
    }

    // ── 입력 ──────────────────────────────────────────────────────────────────

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // ⚠ 배율값으로 '아직 안 맞춤'을 추정하면 안 된다.
        //    fit 배율이 우연히 1.0 이면 창 크기가 바뀔 때마다 조사자의 확대·이동이 초기화되고,
        //    1.0 이 아니면 창을 키워도 지도가 한쪽에 치우친 채 남는다.
        if (!_fitted) Fit();
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (PixelWidth <= 0) return;

        var s = e.GetPosition(Viewport);                       // 화면 좌표
        var z = Zoom.ScaleX;
        var next = Math.Clamp(z * (e.Delta > 0 ? WheelStep : 1 / WheelStep), _fitZoom * 0.5, MaxZoom);
        if (Math.Abs(next - z) < 1e-9) return;

        // 커서 아래 픽셀이 제자리에 있도록 pan 을 다시 잡는다.
        var px = (s.X - Pan.X) / z;
        var py = (s.Y - Pan.Y) / z;

        Zoom.ScaleX = Zoom.ScaleY = next;
        Pan.X = s.X - px * next;
        Pan.Y = s.Y - py * next;
        RescaleOverlays();

        e.Handled = true;
    }

    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (PixelWidth <= 0) return;
        _dragging = true;
        _dragStart = e.GetPosition(Viewport);
        _panStart = new Point(Pan.X, Pan.Y);
        Viewport.CaptureMouse();
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(Viewport);
        Pan.X = _panStart.X + (p.X - _dragStart.X);
        Pan.Y = _panStart.Y + (p.Y - _dragStart.Y);
    }

    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Viewport.ReleaseMouseCapture();

        var p = e.GetPosition(Viewport);
        var moved = Math.Abs(p.X - _dragStart.X) + Math.Abs(p.Y - _dragStart.Y);
        if (!PickMode || moved > ClickSlop) return;

        // 클릭이다 — Stage 기준 좌표가 곧 이미지 픽셀 좌표다(Canvas 가 픽셀 크기이므로).
        var pixel = e.GetPosition(Stage);
        if (pixel.X < 0 || pixel.Y < 0 || pixel.X > PixelWidth || pixel.Y > PixelHeight) return;
        PointPicked?.Invoke(this, pixel);
    }
}
