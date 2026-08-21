using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 양서파충류 → 관리자 '양서파충류' 시트 일괄입력 엑셀 내보내기.
/// 일반 레이아웃: 1행 = 1출현기록. 데이터는 **0-based 행4**(엑셀 5행)부터, 앵커는 **B열=연도차수**.
/// 매핑 근거: 관리자 BulkAmphibianReptileControlViewModel (FirstRowNumber=5, FirstColumnNumber=2).
/// ⚠ 어류·포유류와 달리 **흔적 6종이 앞(9~14)**, 좌표가 뒤(15~22)에 온다.
/// </summary>
public static class AmphibianReptileExcelExporter
{
    private const int FirstRow = 4;      // 0-based
    private const int TraceFirstCol = 9;  // Trace1~6 = 9~14

    public static string? Export(AmphibianReptileEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "양서파충류 일괄입력 엑셀 내보내기",
            ReportExporterBase.BulkFileName(vm.Meta, "양서파충류"),
            path => Write(vm, path));

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(AmphibianReptileEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = new Workbook();
        var ws = wb.Worksheets.Add();
        ws.Name = "양서파충류";

        var headers = new (int Col, string Name)[]
        {
            (1, "연도차수"), (2, "조사일"), (3, "날씨"), (4, "하천명"), (5, "지점명"),
            (6, "국명"), (7, "학명"), (8, "특징"),
            (21, "위도"), (22, "경도"), (24, "대분류"), (25, "중분류"), (26, "조사자"), (27, "비고")
        };
        foreach (var (col, name) in headers) ws.Cells[FirstRow - 1, col].SetValue(name);
        for (int i = 0; i < AmphibianReptileReportExporter.TraceNames.Length; i++)
            ws.Cells[FirstRow - 1, TraceFirstCol + i].SetValue(AmphibianReptileReportExporter.TraceNames[i]);

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

            var traces = new[] { e.Trace1, e.Trace2, e.Trace3, e.Trace4, e.Trace5, e.Trace6 };
            for (int i = 0; i < traces.Length; i++) SetInt(TraceFirstCol + i, traces[i]);  // 미입력은 빈 칸

            if (e.Lat is double lat) ws.Cells[r, 21].SetValue(lat);
            if (e.Lng is double lng) ws.Cells[r, 22].SetValue(lng);
            Set(24, e.MajorCategory);
            Set(25, e.MiddleCategory);
            Set(26, m.Surveyor);
            Set(27, e.Note);
            r++;
        }

        ReportExporterBase.Save(wb, path);

    }
}
