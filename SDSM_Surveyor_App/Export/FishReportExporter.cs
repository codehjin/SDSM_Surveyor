using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.Ecology;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;  // XlsxFormatProvider
using Telerik.Windows.Documents.Spreadsheet.Formatting;                    // ColumnWidth, RadHorizontalAlignment
using Telerik.Windows.Documents.Spreadsheet.Model;                         // Workbook, Worksheet, CellSelection, CellRange
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using ThemableColor = Telerik.Documents.Common.Model.ThemableColor;
using ThemableFontFamily = Telerik.Documents.Common.Model.ThemableFontFamily;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 어류 → 보고서·기록용 엑셀. 시트:
///  [조사개황] 항목|값 · [출현종] 지점명 포함 필터 테이블 · [건강성평가(FAI)] 지점명 포함 1행 관리 테이블.
/// 지점명을 맨 왼쪽 컬럼으로 두어 여러 조사(연 8지점)를 한 표로 필터·관리할 수 있게 배치.
/// </summary>
public static class FishReportExporter
{
    private static readonly Color CAccent = Color.FromRgb(0x03, 0x81, 0xFE);
    private static readonly Color CHeader = Color.FromRgb(0x35, 0x3E, 0x52);
    private static readonly Color CKey    = Color.FromRgb(0xEE, 0xF1, 0xF5);
    private const string Font = "맑은 고딕";

    public static string? Export(FishEntryViewModel vm)
    {
        var m = vm.Meta;
        var project = string.IsNullOrWhiteSpace(m.Project) ? "" : m.Project! + "_";
        var site = string.IsNullOrWhiteSpace(m.Site) ? "" : m.Site + "_";

        var dlg = new SaveFileDialog
        {
            Title = "어류 조사결과(보고서용) 엑셀 내보내기",
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = $"{project}{site}어류_조사결과.xlsx"
        };
        if (dlg.ShowDialog() != true) return null;

        var wb = new Workbook();
        WriteOverview(wb, vm);
        WriteSpecies(wb, vm);
        WriteAssessment(wb, vm);

        using (var stream = new FileStream(dlg.FileName, FileMode.Create))
            new XlsxFormatProvider().Export(wb, stream, null);

        return dlg.FileName;
    }

    // ── 스타일 헬퍼 ──
    private static void Title(CellSelection c) { c.SetIsBold(true); c.SetFontSize(13); c.SetForeColor(new ThemableColor(CAccent)); }
    private static void Section(CellSelection c) { c.SetIsBold(true); c.SetFontSize(12); c.SetForeColor(new ThemableColor(CAccent)); }
    private static void HeaderCell(CellSelection c)
    {
        c.SetIsBold(true);
        c.SetForeColor(new ThemableColor(Colors.White));
        c.SetFill(new PatternFill(PatternType.Solid, CHeader, Colors.White));
        c.SetHorizontalAlignment(RadHorizontalAlignment.Center);
    }
    private static void KeyCell(CellSelection c) { c.SetIsBold(true); c.SetFill(new PatternFill(PatternType.Solid, CKey, Colors.White)); }
    private static void Width(Worksheet ws, int col, double px) => ws.Columns[col].SetWidth(new ColumnWidth(px, true));
    private static void FontAll(Worksheet ws, int lastRow, int lastCol)
        => ws.Cells[0, 0, lastRow, lastCol].SetFontFamily(new ThemableFontFamily(Font));
    private static void AutoFilter(Worksheet ws, int headerRow, int lastRow, int lastCol)
    {
        if (lastRow >= headerRow)
            ws.Filter.FilterRange = new CellRange(headerRow, 0, lastRow, lastCol);
    }

    // ── 시트1 : 조사개황 (항목 | 값) ──
    private static void WriteOverview(Workbook wb, FishEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "조사개황";
        var m = vm.Meta;
        int row = 0;

        void Kv(string key, string? val)
        {
            KeyCell(ws.Cells[row, 0]);
            ws.Cells[row, 0].SetValue(key);
            if (!string.IsNullOrWhiteSpace(val)) ws.Cells[row, 1].SetValue(val);
            row++;
        }
        void Head(string t) { Section(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue(t); row += 2; }

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("어류 조사결과 — 조사개황"); row += 2;
        Head("[ 조사개황 ]");
        Kv("대분류", m.Project);
        Kv("연도", m.SurveyYear);
        Kv("연도차수", m.YearChsu);
        Kv("조사일자", m.SurveyDate?.ToString("yyyy-MM-dd"));
        Kv("대권역명", m.MajorRegion);
        Kv("중권역명", m.MiddleRegion);
        Kv("하천명", m.River);
        Kv("하천유형", m.RiverType);
        Kv("지점명", m.Site);
        Kv("위도", m.Lat);
        Kv("경도", m.Lng);
        Kv("날씨", m.Weather);
        Kv("조사기관", m.SurveyAgency);
        Kv("조사자", m.Surveyor);
        row++;
        Head("[ 채집방법 ]");
        Kv("채집소요시간(분)", vm.CollectionTime);
        Kv("채집도구", vm.CollectionTool);
        Kv("흐름상태", vm.CollectionFlowState);
        Kv("하천차수", vm.RiverChasu?.ToString());
        row++;
        Head("[ 서식지 하상구성(%) ]");
        Kv("암반", vm.Bedrock);
        Kv("콘크리트", vm.Concrete);
        Kv("진흙이하(<0.063mm)", vm.Mud);
        Kv("모래(0.063-2mm)", vm.Sand);
        Kv("잔자갈(2-16mm)", vm.FineGravel);
        Kv("자갈(16-64mm)", vm.Gravel);
        Kv("작은돌(64-256mm)", vm.SmallStone);
        Kv("큰돌(>256mm)", vm.BigStone);
        row++;
        Head("[ 서식처·특이사항 ]");
        Kv("하천형태", vm.HabitatRiverType);
        Kv("흐름상태(서식처)", vm.HabitatFlowState);
        Kv("특이사항(조사불가 등)", vm.SurveyUnavailableReason);
        Kv("비고", vm.Note);
        row++;
        Head("[ 비정상종 개체수 ]");
        Kv("기형(DE)", vm.DeCount);
        Kv("지느러미손상(EF)", vm.EfCount);
        Kv("피부손상(LE)", vm.LeCount);
        Kv("종양(TU)", vm.TuCount);

        Width(ws, 0, 200); Width(ws, 1, 240);
        FontAll(ws, row, 1);
    }

    // ── 시트2 : 출현종 (지점명 포함 · 필터 테이블) ──
    private static void WriteSpecies(Workbook wb, FishEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "출현종";
        int row = 0;

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("어류 출현종 목록"); row += 2;

        int headerRow = row;
        string[] headers = { "지점명", "목", "과", "국명", "학명", "개체수", "보호종" };
        for (int c = 0; c < headers.Length; c++) { ws.Cells[row, c].SetValue(headers[c]); HeaderCell(ws.Cells[row, c]); }
        row++;

        var site = vm.Meta.Site ?? "";
        foreach (var e in vm.SpeciesEntries)
        {
            var ko = e.SelectedSpecies?.SpeciesKo ?? e.SpeciesKo;
            if (string.IsNullOrWhiteSpace(ko) || (e.IndividualCount ?? 0) <= 0) continue;

            var sp = e.SelectedSpecies;
            ws.Cells[row, 0].SetValue(site);
            if (!string.IsNullOrWhiteSpace(sp?.OrderKo)) ws.Cells[row, 1].SetValue(sp!.OrderKo);
            if (!string.IsNullOrWhiteSpace(sp?.FamilyKo)) ws.Cells[row, 2].SetValue(sp!.FamilyKo);
            ws.Cells[row, 3].SetValue(ko);
            if (!string.IsNullOrWhiteSpace(sp?.SpeciesEn)) ws.Cells[row, 4].SetValue(sp!.SpeciesEn);
            ws.Cells[row, 5].SetValue((double)e.IndividualCount!.Value);
            ws.Cells[row, 5].SetHorizontalAlignment(RadHorizontalAlignment.Right);
            if (e.IsProtected)
            {
                ws.Cells[row, 6].SetValue("보호종");
                ws.Cells[row, 6].SetForeColor(new ThemableColor(CAccent));
                ws.Cells[row, 6].SetIsBold(true);
            }
            row++;
        }

        int lastRow = row - 1;
        Width(ws, 0, 110); Width(ws, 1, 95); Width(ws, 2, 115); Width(ws, 3, 160);
        Width(ws, 4, 230); Width(ws, 5, 75); Width(ws, 6, 70);
        AutoFilter(ws, headerRow, lastRow, 6);
        FontAll(ws, lastRow, 6);
    }

    // ── 시트3 : 건강성평가 (지점명 포함 · 1행 관리 테이블) ──
    private static void WriteAssessment(Workbook wb, FishEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "건강성평가(FAI)";
        var m = vm.Meta;

        int abnormal = Pi(vm.DeCount) + Pi(vm.EfCount) + Pi(vm.LeCount) + Pi(vm.TuCount);
        int chasu = (int)(vm.RiverChasu ?? 0);
        var imports = vm.SpeciesEntries.Select(r => r.ToImport()).ToList();
        var f = EcologyCalculator.CalculateFaiDetail(imports, vm.SurveyUnavailableReason, abnormal, chasu);

        int row = 0;
        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("어류평가지수(FAI) 건강성평가"); row += 2;

        int headerRow = row;
        string[] headers =
        {
            "지점명", "연도차수", "하천명", "조사일", "하천차수",
            "총출현종수", "총개체수", "우점종",
            "M1 국내종수", "M1 점", "M2 여울성종수", "M2 점", "M3 민감종수", "M3 점", "M4 내성종비율%", "M4 점",
            "M5 잡식종비율%", "M5 점", "M6 충식종비율%", "M6 점", "M7 국내개체수", "M7 점", "M8 비정상비율%", "M8 점",
            "FAI총점", "등급"
        };
        for (int c = 0; c < headers.Length; c++) { ws.Cells[row, c].SetValue(headers[c]); HeaderCell(ws.Cells[row, c]); }
        row++;

        void Str(int col, string? v) { if (!string.IsNullOrWhiteSpace(v)) ws.Cells[row, col].SetValue(v); }
        void Num(int col, double v) { ws.Cells[row, col].SetValue(v); ws.Cells[row, col].SetHorizontalAlignment(RadHorizontalAlignment.Right); }

        Str(0, m.Site);
        Str(1, m.YearChsu);
        Str(2, m.River);
        Str(3, m.SurveyDate?.ToString("yyyy-MM-dd"));
        Num(4, chasu);
        Num(5, f.TotalSpecies);
        Num(6, f.TotalIndiv);
        Str(7, vm.DominantSpecies);
        Num(8,  f.DomesticSpecies);                       Num(9,  f.M1);
        Num(10, f.RiffleBenthic);                         Num(11, f.M2);
        Num(12, f.Sensitive);                             Num(13, f.M3);
        Num(14, System.Math.Round(f.TolerantRatio, 1));   Num(15, f.M4);
        Num(16, System.Math.Round(f.OmnivoreRatio, 1));   Num(17, f.M5);
        Num(18, System.Math.Round(f.InsectRatio, 1));     Num(19, f.M6);
        Num(20, f.DomesticIndiv);                         Num(21, f.M7);
        Num(22, System.Math.Round(f.AbnormalRatio, 1));   Num(23, f.M8);
        if (f.Score is double sc) Num(24, sc);
        var g = f.Grade ?? "-";
        ws.Cells[row, 25].SetValue(g);
        ws.Cells[row, 25].SetIsBold(true);
        ws.Cells[row, 25].SetHorizontalAlignment(RadHorizontalAlignment.Center);
        ws.Cells[row, 25].SetForeColor(new ThemableColor(GradeColor(f.Grade)));

        int lastRow = row;
        Width(ws, 0, 110); Width(ws, 1, 100); Width(ws, 2, 100); Width(ws, 3, 95); Width(ws, 4, 75);
        Width(ws, 5, 90); Width(ws, 6, 80); Width(ws, 7, 110);
        AutoFilter(ws, headerRow, lastRow, 25);

        row += 2;
        ws.Cells[row, 0].SetValue("※ 각 M = 산출값→배점(0/6.25/12.5), 합산=FAI총점. 등급 A≥80·B≥60·C≥40·D≥20·E<20.");
        FontAll(ws, row, 25);
    }

    private static Color GradeColor(string? g) => g switch
    {
        "A" => Color.FromRgb(0x1E, 0x88, 0xE5),
        "B" => Color.FromRgb(0x43, 0xA0, 0x47),
        "C" => Color.FromRgb(0xFB, 0x8C, 0x00),
        "D" => Color.FromRgb(0xE5, 0x39, 0x35),
        "E" => Color.FromRgb(0xB7, 0x1C, 0x1C),
        _   => Color.FromRgb(0x75, 0x75, 0x75)
    };

    private static int Pi(string? s) => int.TryParse(s, out var n) ? n : 0;
}
