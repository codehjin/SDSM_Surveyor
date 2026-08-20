using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 서식·수변환경 → 보고서·기록용 엑셀. 시트:
///  [조사개황] · [평가항목] 항목별 선택지·점수 · [건강성평가(HRI)] 지점명 포함 1행 관리 테이블.
/// 계산은 <see cref="Ecology.HabitatEvaluator"/>(ViewModel의 ComputeDetail)가 하고 여기서는 값만 옮긴다.
/// </summary>
public static class HabitatWaterEdgeReportExporter
{
    public static string? Export(HabitatWaterEdgeEntryViewModel vm)
    {
        var m = vm.Meta;
        var project = string.IsNullOrWhiteSpace(m.Project) ? "" : m.Project! + "_";
        var site = string.IsNullOrWhiteSpace(m.Site) ? "" : m.Site + "_";

        var dlg = new SaveFileDialog
        {
            Title = "서식·수변환경 조사결과(보고서용) 엑셀 내보내기",
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = $"{project}{site}서식수변_조사결과.xlsx"
        };
        if (dlg.ShowDialog() != true) return null;

        Write(vm, dlg.FileName);
        return dlg.FileName;
    }

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(HabitatWaterEdgeEntryViewModel vm, string path)
    {
        var wb = new Workbook();
        WriteOverview(wb, vm);
        WriteItems(wb, vm);
        WriteAssessment(wb, vm);

        using (var stream = new FileStream(path, FileMode.Create))
            new XlsxFormatProvider().Export(wb, stream, null);

    }

    // ── 시트1 : 조사개황 ──
    private static void WriteOverview(Workbook wb, HabitatWaterEdgeEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "조사개황";
        var m = vm.Meta;
        int row = 0;

        void Kv(string key, string? val)
        {
            KeyCell(ws.Cells[row, 0]);
            ws.Cells[row, 0].SetValue(key);
            Str(ws, row, 1, val);
            row++;
        }
        void Head(string t) { Section(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue(t); row += 2; }

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("서식·수변환경 조사결과 — 조사개황"); row += 2;
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
        Head("[ 특이사항 ]");
        Kv("조사불가시", vm.SurveyUnavailableReason);
        Kv("비고", vm.Note);

        Width(ws, 0, 200); Width(ws, 1, 240);
        FontAll(ws, row, 1);
    }

    // ── 시트2 : 평가항목 (항목 | 좌안 | 우안 | 적용점수) ──
    private static void WriteItems(Workbook wb, HabitatWaterEdgeEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "평가항목";
        int row = 0;

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("서식·수변환경 평가항목 [별표5]"); row += 2;

        int headerRow = row;
        WriteHeader(ws, row, "지점명", "번호", "평가항목", "좌안 선택", "좌안 점수", "우안 선택", "우안 점수", "적용점수");
        row++;

        // 좌/우안이 있는 항목은 두 선택을 모두 남기고, 적용점수(평균)를 함께 기록한다.
        var rows = new (string Name, HriOption? L, HriOption? R, HriOption? Single)[]
        {
            (HabitatWaterEdgeEntryViewModel.ItemNames[0], null,    null,    vm.S1),
            (HabitatWaterEdgeEntryViewModel.ItemNames[1], vm.B2L,  vm.B2R,  null),
            (HabitatWaterEdgeEntryViewModel.ItemNames[2], null,    null,    vm.S3),
            (HabitatWaterEdgeEntryViewModel.ItemNames[3], null,    null,    vm.S4),
            (HabitatWaterEdgeEntryViewModel.ItemNames[4], vm.B5L,  vm.B5R,  null),
            (HabitatWaterEdgeEntryViewModel.ItemNames[5], vm.B6L,  vm.B6R,  null),
            (HabitatWaterEdgeEntryViewModel.ItemNames[6], null,    null,    vm.S7),
            (HabitatWaterEdgeEntryViewModel.ItemNames[7], null,    null,    vm.S8),
            (HabitatWaterEdgeEntryViewModel.ItemNames[8], vm.B9L,  vm.B9R,  null),
            (HabitatWaterEdgeEntryViewModel.ItemNames[9], vm.B10L, vm.B10R, null),
        };

        var detail = vm.ComputeDetail();
        var site = vm.Meta.Site ?? "";
        for (int i = 0; i < rows.Length; i++)
        {
            var (name, l, r, single) = rows[i];
            Str(ws, row, 0, site);
            Num(ws, row, 1, i + 1);
            ws.Cells[row, 2].SetValue(name);
            if (single is not null)
            {
                ws.Cells[row, 3].SetValue(single.Desc);
                Num(ws, row, 4, single.Score);
            }
            else
            {
                if (l is not null) { ws.Cells[row, 3].SetValue(l.Desc); Num(ws, row, 4, l.Score); }
                if (r is not null) { ws.Cells[row, 5].SetValue(r.Desc); Num(ws, row, 6, r.Score); }
            }
            if (i < detail.Items.Length) Num(ws, row, 7, detail.Items[i]);
            row++;
        }

        // 합계
        KeyCell(ws.Cells[row, 2]);
        ws.Cells[row, 2].SetValue("합계");
        Num(ws, row, 7, detail.Total);
        ws.Cells[row, 7].SetIsBold(true);

        int lastRow = row;
        Width(ws, 0, 110); Width(ws, 1, 55); Width(ws, 2, 150);
        Width(ws, 3, 190); Width(ws, 4, 85); Width(ws, 5, 190); Width(ws, 6, 85); Width(ws, 7, 85);
        TryAutoFilter(ws, headerRow, lastRow - 1, 7);

        row += 2;
        ws.Cells[row, 0].SetValue("※ 좌/우안이 있는 항목의 적용점수는 좌·우안 산술평균이다(한쪽만 입력하면 그 값).");
        FontAll(ws, row, 7);
    }

    // ── 시트3 : 건강성평가(HRI) — 항목1~10 점수 → 합계 → 평가점수 → 등급 ──
    private static void WriteAssessment(Workbook wb, HabitatWaterEdgeEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "건강성평가(HRI)";
        var m = vm.Meta;
        var d = vm.ComputeDetail();

        int row = 0;
        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("서식·수변환경 평가(HRI)"); row += 2;

        var headers = new List<string> { "지점명", "연도차수", "하천명", "조사일" };
        for (int i = 0; i < HabitatWaterEdgeEntryViewModel.ItemNames.Length; i++)
            headers.Add($"{i + 1}. {HabitatWaterEdgeEntryViewModel.ItemNames[i]}");
        headers.Add("합계");
        headers.Add("평가점수(합÷2)");
        headers.Add("등급");

        int headerRow = row;
        WriteHeader(ws, row, headers.ToArray());
        row++;

        Str(ws, row, 0, m.Site);
        Str(ws, row, 1, m.YearChsu);
        Str(ws, row, 2, m.River);
        Str(ws, row, 3, m.SurveyDate?.ToString("yyyy-MM-dd"));

        int col = 4;
        for (int i = 0; i < HabitatWaterEdgeEntryViewModel.ItemNames.Length; i++, col++)
            if (i < d.Items.Length) Num(ws, row, col, d.Items[i]);

        Num(ws, row, col, d.Total); col++;
        Num(ws, row, col, d.Score); col++;
        var g = d.Grade ?? "-";
        ws.Cells[row, col].SetValue(g);
        GradeCell(ws.Cells[row, col], d.Grade);

        int lastCol = headers.Count - 1;
        int lastRow = row;
        Width(ws, 0, 110); Width(ws, 1, 100); Width(ws, 2, 100); Width(ws, 3, 95);
        for (int c = 4; c <= lastCol; c++) Width(ws, c, 120);
        TryAutoFilter(ws, headerRow, lastRow, lastCol);

        row += 2;
        ws.Cells[row, 0].SetValue("※ 평가점수 = 평가항목 10개 합계 ÷ 2. 등급 A≥80·B≥60·C≥40·D≥20·E.");
        row++;
        ws.Cells[row, 0].SetValue("※ 조사불가(접근불가) 시 등급 '-'.");
        FontAll(ws, row, lastCol);
    }
}
