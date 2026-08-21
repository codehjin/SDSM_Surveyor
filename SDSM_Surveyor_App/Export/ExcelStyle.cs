using Telerik.Windows.Documents.Spreadsheet.Formatting;   // ColumnWidth, RadHorizontalAlignment
using Telerik.Windows.Documents.Spreadsheet.Model;        // Worksheet, CellSelection, CellRange, PatternFill
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using ThemableColor = Telerik.Documents.Common.Model.ThemableColor;
using ThemableFontFamily = Telerik.Documents.Common.Model.ThemableFontFamily;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 내보내기 엑셀 공통 서식. 7개 분류군 exporter가 이 헬퍼만 사용해 서식을 통일한다.
/// 색·폰트를 바꾸려면 이 파일 한 곳만 고친다(design.md 토큰과 동일 계열).
/// </summary>
public static class ExcelStyle
{
    public static readonly Color Accent = Color.FromRgb(0x03, 0x81, 0xFE);   // 강조
    public static readonly Color Header = Color.FromRgb(0x35, 0x3E, 0x52);   // 헤더 배경
    public static readonly Color Key    = Color.FromRgb(0xEE, 0xF1, 0xF5);   // 라벨 배경
    public const string Font = "맑은 고딕";

    /// <summary>파일명 토큰. 빈 값이면 빈 문자열, 값이 있으면 `값_` 로 만들어 이어붙인다.
    /// 파일명에 못 쓰는 문자는 '_' 로 바꾼다(지점명에 '/' 가 들어간 경우 등).</summary>
    public static string FileToken(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "";
        var t = v.Trim();
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) t = t.Replace(c, '_');
        return t + "_";
    }

    /// <summary>시트 제목(큰 강조).</summary>
    public static void Title(CellSelection c)
    {
        c.SetIsBold(true);
        c.SetFontSize(13);
        c.SetForeColor(new ThemableColor(Accent));
    }

    /// <summary>구획 소제목.</summary>
    public static void Section(CellSelection c)
    {
        c.SetIsBold(true);
        c.SetFontSize(12);
        c.SetForeColor(new ThemableColor(Accent));
    }

    /// <summary>표 헤더 셀(진한 배경 + 흰 글씨 + 가운데).</summary>
    public static void HeaderCell(CellSelection c)
    {
        c.SetIsBold(true);
        c.SetForeColor(new ThemableColor(Colors.White));
        c.SetFill(new PatternFill(PatternType.Solid, Header, Colors.White));
        c.SetHorizontalAlignment(RadHorizontalAlignment.Center);
    }

    /// <summary>항목|값 표의 왼쪽 라벨 셀.</summary>
    public static void KeyCell(CellSelection c)
    {
        c.SetIsBold(true);
        c.SetFill(new PatternFill(PatternType.Solid, Key, Colors.White));
    }

    public static void Width(Worksheet ws, int col, double px)
        => ws.Columns[col].SetWidth(new ColumnWidth(px, true));

    public static void FontAll(Worksheet ws, int lastRow, int lastCol)
    {
        if (lastRow < 0 || lastCol < 0) return;
        ws.Cells[0, 0, lastRow, lastCol].SetFontFamily(new ThemableFontFamily(Font));
    }

    /// <summary>등급 색 : A 파랑 · B 초록 · C 주황 · D 빨강 · E 진빨강 · 그 외 회색.</summary>
    public static Color GradeColor(string? grade) => grade switch
    {
        "A" => Color.FromRgb(0x1E, 0x88, 0xE5),
        "B" => Color.FromRgb(0x43, 0xA0, 0x47),
        "C" => Color.FromRgb(0xFB, 0x8C, 0x00),
        "D" => Color.FromRgb(0xE5, 0x39, 0x35),
        "E" => Color.FromRgb(0xB7, 0x1C, 0x1C),
        _ => Color.FromRgb(0x75, 0x75, 0x75)
    };

    /// <summary>등급 셀(굵게·가운데·등급색).</summary>
    public static void GradeCell(CellSelection c, string? grade)
    {
        c.SetIsBold(true);
        c.SetHorizontalAlignment(RadHorizontalAlignment.Center);
        c.SetForeColor(new ThemableColor(GradeColor(grade)));
    }

    /// <summary>헤더 행에 자동 필터를 건다.
    /// 이 Telerik 버전에서는 <c>Filter.FilterRange</c> 속성 대입이 동작한다(90_TECH_NOTES §3).
    /// 버전에 따라 접근이 막힐 수 있으므로 실패하면 조용히 건너뛴다 — 표 구조만 있으면
    /// 사용자가 엑셀에서 [데이터 > 필터]를 한 번 누르면 동일하다.</summary>
    public static void TryAutoFilter(Worksheet ws, int headerRow, int lastRow, int lastCol)
    {
        if (lastRow < headerRow) return;
        try
        {
            ws.Filter.FilterRange = new CellRange(headerRow, 0, lastRow, lastCol);
        }
        catch
        {
            // 자동 필터는 편의 기능이므로 실패해도 내보내기를 막지 않는다.
        }
    }

    /// <summary>표 헤더 한 줄을 쓰고 서식을 적용한다.</summary>
    public static void WriteHeader(Worksheet ws, int row, params string[] headers)
    {
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cells[row, c].SetValue(headers[c]);
            HeaderCell(ws.Cells[row, c]);
        }
    }

    /// <summary>문자열 셀(빈 값이면 쓰지 않는다).</summary>
    public static void Str(Worksheet ws, int row, int col, string? v)
    {
        if (!string.IsNullOrWhiteSpace(v)) ws.Cells[row, col].SetValue(v);
    }

    /// <summary>숫자 셀(오른쪽 정렬).</summary>
    public static void Num(Worksheet ws, int row, int col, double v)
    {
        ws.Cells[row, col].SetValue(v);
        ws.Cells[row, col].SetHorizontalAlignment(RadHorizontalAlignment.Right);
    }

    /// <summary>숫자 셀(null이면 비움).</summary>
    public static void Num(Worksheet ws, int row, int col, double? v)
    {
        if (v is double d) Num(ws, row, col, d);
    }

    /// <summary>문자열을 실수로 파싱해 숫자 셀로. 파싱 실패 시 원문을 문자열로 남긴다
    /// (조사자가 "&lt;0.01" 같이 적은 값을 잃지 않도록).</summary>
    public static void NumOrText(Worksheet ws, int row, int col, string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return;
        if (double.TryParse(v, out var d)) Num(ws, row, col, d);
        else ws.Cells[row, col].SetValue(v);
    }
}
