using System.Globalization;
using System.Text.RegularExpressions;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 좌표 변환·표시. 제출 엑셀은 도분초(DMS), 관리자 DB는 십진수라 양쪽을 오간다.
/// (04_FEATURE_SITE_SESSION §A-2-4)
/// </summary>
public static class CoordinateHelper
{
    private static readonly Regex Dms = new(
        @"^\s*(\d+)\s*°\s*(\d+)\s*'\s*([\d.]+)\s*""?\s*([NSEWnsew])?\s*$", RegexOptions.Compiled);

    /// <summary>DMS(`36°45'47.09"N`) 또는 십진 문자열을 십진수로. 실패하면 null.</summary>
    public static double? ToDecimal(string? text)
    {
        var t = text?.Trim();
        if (string.IsNullOrEmpty(t)) return null;

        var m = Dms.Match(t);
        if (m.Success)
        {
            double deg = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            double min = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            double sec = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            double val = deg + min / 60 + sec / 3600;

            var hemi = m.Groups[4].Value.ToUpperInvariant();
            if (hemi is "S" or "W") val = -val;
            return Math.Round(val, 6);
        }

        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? Math.Round(d, 6)
            : null;
    }

    /// <summary>십진수를 DMS 문자열로. <paramref name="isLat"/> 로 N/S · E/W 를 정한다.</summary>
    public static string? ToDms(double? value, bool isLat)
    {
        if (value is not double v) return null;

        var hemi = isLat ? (v < 0 ? "S" : "N") : (v < 0 ? "W" : "E");
        v = Math.Abs(v);

        int deg = (int)v;
        double restMin = (v - deg) * 60;
        int min = (int)restMin;
        double sec = Math.Round((restMin - min) * 60, 2);

        // 반올림으로 60초/60분이 되는 경우 올림 처리
        if (sec >= 60) { sec -= 60; min += 1; }
        if (min >= 60) { min -= 60; deg += 1; }

        return $"{deg}°{min}'{sec.ToString("0.##", CultureInfo.InvariantCulture)}\"{hemi}";
    }

    /// <summary>지도 열기용 URL. 좌표가 없으면 null.</summary>
    public static string? MapUrl(double? lat, double? lng)
    {
        if (lat is not double la || lng is not double ln) return null;
        var q = $"{la.ToString(CultureInfo.InvariantCulture)},{ln.ToString(CultureInfo.InvariantCulture)}";
        return $"https://www.google.com/maps/search/?api=1&query={q}";
    }
}
