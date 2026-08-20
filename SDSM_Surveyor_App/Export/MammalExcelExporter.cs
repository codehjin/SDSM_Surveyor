using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 포유류 → 관리자 '포유류' 시트 일괄입력 엑셀 내보내기.
/// 일반 레이아웃: 1행 = 1출현기록. 데이터는 **0-based 행4**(엑셀 5행)부터, 앵커는 **B열=연도차수**.
/// 매핑 근거: 관리자 BulkMammalControlViewModel (FirstRowNumber=5, FirstColumnNumber=2).
/// 흔적 12종은 열 19~30(Trace1~12)이며 **정수 개체수**로 넣는다.
/// </summary>
public static class MammalExcelExporter
{
    private const int FirstRow = 4;      // 0-based
    private const int TraceFirstCol = 19; // Trace1

    public static string? Export(MammalEntryViewModel vm)
    {
        var m = vm.Meta;
        var project = string.IsNullOrWhiteSpace(m.Project) ? "방류하천" : m.Project!;
        var site = string.IsNullOrWhiteSpace(m.Site) ? "" : $"_{m.Site}";

        var dlg = new SaveFileDialog
        {
            Title = "포유류 일괄입력 엑셀 내보내기",
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = $"{project}{site}_포유류_DB_v1.xlsx"
        };
        if (dlg.ShowDialog() != true) return null;

        Write(vm, dlg.FileName);
        return dlg.FileName;
    }

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(MammalEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = new Workbook();
        var ws = wb.Worksheets.Add();
        ws.Name = "포유류";

        var headers = new (int Col, string Name)[]
        {
            (1, "연도차수"), (2, "조사일"), (3, "날씨"), (4, "하천명"), (5, "지점명"),
            (6, "국명"), (7, "학명"), (8, "특징"),
            (15, "위도"), (16, "경도"), (17, "관찰지유형"), (31, "조사자"), (32, "비고")
        };
        foreach (var (col, name) in headers) ws.Cells[FirstRow - 1, col].SetValue(name);
        for (int i = 0; i < MammalReportExporter.TraceNames.Length; i++)
            ws.Cells[FirstRow - 1, TraceFirstCol + i].SetValue(MammalReportExporter.TraceNames[i]);

        int r = FirstRow;
        foreach (var e in vm.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.SpeciesKo) || e.TraceSum <= 0) continue;   // 관측된 기록만

            void Set(int col, string? val) { if (!string.IsNullOrWhiteSpace(val)) ws.Cells[r, col].SetValue(val); }
            void SetInt(int col, int? val) { if (val is int v) ws.Cells[r, col].SetValue((double)v); }

            Set(1, m.YearChsu);                            // 앵커
            Set(2, m.SurveyDate?.ToString("yyyyMMdd"));
            Set(3, m.Weather);
            Set(4, m.River);
            Set(5, m.Site);
            ws.Cells[r, 6].SetValue(e.SpeciesKo);
            Set(7, e.SpeciesEn);
            Set(8, e.Feature);
            if (e.Lat is double lat) ws.Cells[r, 15].SetValue(lat);
            if (e.Lng is double lng) ws.Cells[r, 16].SetValue(lng);
            Set(17, e.ObservationSite);

            var traces = new[]
            {
                e.Trace1, e.Trace2, e.Trace3, e.Trace4, e.Trace5, e.Trace6,
                e.Trace7, e.Trace8, e.Trace9, e.Trace10, e.Trace11, e.Trace12
            };
            for (int i = 0; i < traces.Length; i++) SetInt(TraceFirstCol + i, traces[i]);  // 미입력은 빈 칸(null 유지)

            Set(31, m.Surveyor);
            Set(32, e.Note);
            r++;
        }

        using (var stream = new FileStream(path, FileMode.Create))
            new XlsxFormatProvider().Export(wb, stream, null);

    }
}
