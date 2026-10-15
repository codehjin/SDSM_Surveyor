namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 좌표 ↔ 지도 픽셀 변환. UI·파일 입출력이 없는 순수 계산이라 그대로 시험할 수 있다.
///
/// 변환식은 1차 다항식이다 — <c>px = a*lon + b*lat + c</c>.
/// 시드 지도의 계수는 지점 12~13곳을 최소제곱으로 맞춘 것이라 교차항(b)이 0 이 아니다.
/// 2점 보정으로 만든 계수는 교차항이 0 인 근사다(북쪽이 위인 지도에서만 정확하다).
/// </summary>
public static class MapProjection
{
    /// <summary>좌표를 지도 픽셀로. 보정이 없으면 null.</summary>
    public static (double X, double Y)? ToPixel(SiteMap map, double lat, double lon)
    {
        if (!map.HasCalibration) return null;
        var x = map.Calibration!.X!;
        var y = map.Calibration!.Y!;
        return (x[0] * lon + x[1] * lat + x[2],
                y[0] * lon + y[1] * lat + y[2]);
    }

    /// <summary>지도 범위 안인지. 범위가 없으면 픽셀이 이미지 안에 떨어지는지로 판단한다.</summary>
    public static bool InBounds(SiteMap map, double lat, double lon)
    {
        if (map.Bounds is MapBounds b)
            return lon >= b.LonMin && lon <= b.LonMax && lat >= b.LatMin && lat <= b.LatMax;

        var p = ToPixel(map, lat, lon);
        if (p is null) return false;
        return p.Value.X >= 0 && p.Value.X <= map.Width && p.Value.Y >= 0 && p.Value.Y <= map.Height;
    }

    /// <summary>
    /// 2점 보정. 두 점의 픽셀 좌표와 경위도로 축척·원점을 구한다.
    /// 두 점의 경도(또는 위도)가 같으면 그 축을 구할 수 없어 null 을 돌려준다.
    /// </summary>
    public static MapCalibration? FitTwoPoint(
        double px1, double py1, double lat1, double lon1,
        double px2, double py2, double lat2, double lon2)
    {
        var dLon = lon2 - lon1;
        var dLat = lat2 - lat1;
        if (Math.Abs(dLon) < 1e-9 || Math.Abs(dLat) < 1e-9) return null;

        var ax = (px2 - px1) / dLon;
        var cx = px1 - ax * lon1;
        var ay = (py2 - py1) / dLat;
        var cy = py1 - ay * lat1;

        if (double.IsNaN(ax) || double.IsInfinity(ax) || double.IsNaN(ay) || double.IsInfinity(ay))
            return null;

        return new MapCalibration { X = new[] { ax, 0d, cx }, Y = new[] { 0d, ay, cy } };
    }

    /// <summary>보정에서 지도 범위를 역산한다(네 모서리를 경위도로 되돌려 min/max).</summary>
    public static MapBounds? BoundsFrom(MapCalibration cal, int width, int height)
    {
        if (cal.X is not { Length: 3 } x || cal.Y is not { Length: 3 } y) return null;
        if (Math.Abs(x[0]) < 1e-12 || Math.Abs(y[1]) < 1e-12) return null;

        // 2점 보정 결과는 교차항이 0 이라 축별로 바로 역산된다.
        double LonAt(double px) => (px - x[2]) / x[0];
        double LatAt(double py) => (py - y[2]) / y[1];

        var lons = new[] { LonAt(0), LonAt(width) };
        var lats = new[] { LatAt(0), LatAt(height) };

        return new MapBounds
        {
            LonMin = lons.Min(),
            LonMax = lons.Max(),
            LatMin = lats.Min(),
            LatMax = lats.Max()
        };
    }

    /// <summary>두 보정점이 너무 가까운지(이미지 대각선의 30% 미만). 가까우면 오차가 크게 벌어진다.</summary>
    public static bool PointsTooClose(double px1, double py1, double px2, double py2, int width, int height)
    {
        var d = Math.Sqrt((px2 - px1) * (px2 - px1) + (py2 - py1) * (py2 - py1));
        var diag = Math.Sqrt((double)width * width + (double)height * height);
        return diag > 0 && d < diag * 0.3;
    }
}
