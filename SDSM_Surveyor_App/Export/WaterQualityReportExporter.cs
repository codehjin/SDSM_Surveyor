using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 수질 → 보고서·기록용 엑셀. 시트:
///  [조사개황] · [측정결과] 항목별 (측정값, 등급) 세로 표 · [건강성평가(수질등급)] 지점명 포함 1행 관리 테이블.
/// 등급은 화면과 동일하게 <see cref="Ecology.WaterQualityCalculator"/> 결과(ViewModel 노출값)를 그대로 쓴다.
/// </summary>
public static class WaterQualityReportExporter
{
    /// <summary>등급 산정 대상 8종. (항목명, 측정값, 등급).</summary>
    private static (string Name, string? Value, string Grade)[] Graded(WaterQualityEntryViewModel vm) => new[]
    {
        ("pH",                   vm.PH,    vm.PhGradeText),
        ("BOD (mg/L)",           vm.Bod,   vm.BodGradeText),
        ("COD (mg/L)",           vm.Cod,   vm.CodGradeText),
        ("TOC (mg/L)",           vm.Toc,   vm.TocGradeText),
        ("SS (mg/L)",            vm.Ss,    vm.SsGradeText),
        ("DO (mg/L)",            vm.Dox,   vm.DoGradeText),
        ("T-P (mg/L)",           vm.Tp,    vm.TpGradeText),
        ("대장균군 (군수/100mL)", vm.EColi, vm.EColiGradeText),
    };

    /// <summary>등급이 없는 추가 항목(생태독성 + 확장 14종).</summary>
    private static (string Name, string? Value)[] Extra(WaterQualityEntryViewModel vm) => new[]
    {
        ("생태독성 (TU)",        vm.Ecotoxicity),
        ("T-N (mg/L)",           vm.TN),
        ("전기전도도 (µS/cm)",   vm.EC),
        ("염소이온 (mg/L)",      vm.Cl),
        ("황이온 (mg/L)",        vm.SO42),
        ("구리 Cu (mg/L)",       vm.Cu),
        ("아연 Zn (mg/L)",       vm.Zn),
        ("크롬 Cr (mg/L)",       vm.Cr),
        ("탁도 (NTU)",           vm.Turbidity),
        ("클로로필a (mg/m³)",    vm.Chla),
        ("수온 (℃)",            vm.WaterTemperature),
        ("수심 (cm)",            vm.WaterDepth),
        ("유속 (cm/s)",          vm.FlowVelocity),
        ("초당유량 (m³/sec)",    vm.FlowSec),
        ("일당유량 (m³/day)",    vm.FlowDay),
    };

    public static string? Export(WaterQualityEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "수질 조사결과(보고서용) 엑셀 내보내기",
            ReportExporterBase.ReportFileName(vm.Meta, "수질"),
            path => Write(vm, path));

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(WaterQualityEntryViewModel vm, string path)
    {
        var wb = new Workbook();
        WriteOverview(wb, vm);
        WriteMeasurements(wb, vm);
        WriteAssessment(wb, vm);

        ReportExporterBase.Save(wb, path);

    }

    // ── 시트1 : 조사개황 ──
    private static void WriteOverview(Workbook wb, WaterQualityEntryViewModel vm)
    {
        var m = vm.Meta;
        var s = ReportExporterBase.BeginOverview(wb, "수질 조사결과 — 조사개황");
        s.Head("[ 조사개황 ]");
        s.WriteSurveyMeta(m);

        s.Finish();
    }

    // ── 시트2 : 측정결과 (항목 | 측정값 | 등급) ──
    private static void WriteMeasurements(Workbook wb, WaterQualityEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "측정결과";
        int row = 0;

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("수질 측정결과"); row += 2;

        int headerRow = row;
        WriteHeader(ws, row, SiteColumns.With("구분", "항목", "측정값", "등급"));
        row++;

        foreach (var (name, value, grade) in Graded(vm))
        {
            SiteColumns.Write(ws, row, vm.Meta);
            ws.Cells[row, 4].SetValue("측정항목");
            ws.Cells[row, 5].SetValue(name);
            NumOrText(ws, row, 6, value);
            ws.Cells[row, 7].SetValue(grade);
            ws.Cells[row, 7].SetIsBold(true);
            row++;
        }
        foreach (var (name, value) in Extra(vm))
        {
            SiteColumns.Write(ws, row, vm.Meta);
            ws.Cells[row, 4].SetValue("추가항목");
            ws.Cells[row, 5].SetValue(name);
            NumOrText(ws, row, 6, value);
            ws.Cells[row, 7].SetValue("-");     // 등급 산정 대상 아님
            row++;
        }

        int lastRow = row - 1;
        SiteColumns.Widths(ws);
        Width(ws, 3, 110); Width(ws, 4, 90); Width(ws, 5, 190); Width(ws, 6, 100); Width(ws, 7, 70);
        TryAutoFilter(ws, headerRow, lastRow, 7);

        row++;
        ws.Cells[row, 0].SetValue("※ 등급은 조사자가 입력하지 않고 측정값으로 자동 산정된다(관리자와 동일 기준).");
        FontAll(ws, row, 7);
    }

    // ── 시트3 : 건강성평가(수질등급) — 1조사 = 1행 ──
    private static void WriteAssessment(Workbook wb, WaterQualityEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "건강성평가(수질등급)";
        var m = vm.Meta;

        int row = 0;
        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("수질 항목별 측정값·등급"); row += 2;

        var graded = Graded(vm);
        var extra = Extra(vm);

        var headers = new List<string>(SiteColumns.Headers) { "연도차수", "하천명", "조사일" };
        foreach (var (name, _, _) in graded) { headers.Add(name); headers.Add($"{name} 등급"); }
        foreach (var (name, _) in extra) headers.Add(name);

        int headerRow = row;
        WriteHeader(ws, row, headers.ToArray());
        row++;

        SiteColumns.Write(ws, row, m);
        Str(ws, row, 4, m.YearChsu);
        Str(ws, row, 5, m.River);
        Str(ws, row, 6, m.SurveyDate?.ToString("yyyy-MM-dd"));

        int col = 7;
        foreach (var (_, value, grade) in graded)
        {
            NumOrText(ws, row, col, value);
            ws.Cells[row, col + 1].SetValue(grade);
            ws.Cells[row, col + 1].SetIsBold(true);
            col += 2;
        }
        foreach (var (_, value) in extra)
        {
            NumOrText(ws, row, col, value);
            col++;
        }

        int lastCol = headers.Count - 1;
        int lastRow = row;
        SiteColumns.Widths(ws);
        Width(ws, 3, 110); Width(ws, 4, 100); Width(ws, 5, 100); Width(ws, 6, 95);
        for (int c = 7; c <= lastCol; c++) Width(ws, c, 110);
        TryAutoFilter(ws, headerRow, lastRow, lastCol);

        row += 2;
        ws.Cells[row, 0].SetValue("※ 등급 8종(pH·BOD·COD·TOC·SS·DO·T-P·대장균군)은 자동 산정 결과다.");
        row++;
        ws.Cells[row, 0].SetValue("※ 추가항목(생태독성·T-N·전기전도도 등 15종)은 등급 산정 대상이 아니며 측정값만 기록한다.");
        FontAll(ws, row, lastCol);
    }
}
