using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;
using ThemableColor = Telerik.Documents.Common.Model.ThemableColor;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 양서파충류 → 보고서·기록용 엑셀. 시트:
///  [조사개황] · [출현종] 흔적 6종 포함 필터 테이블 · [출현종요약] 흔적유형별 합계.
/// 양서파충류는 건강성평가 지수가 없으므로 3번째 시트가 요약표다(03_FEATURE_EXPORT §2).
/// </summary>
public static class AmphibianReptileReportExporter
{
    /// <summary>흔적 6종 이름(관리자 AmphibianReptile.Trace1~6 순서).</summary>
    internal static readonly string[] TraceNames = { "성체", "유생", "알", "울음소리", "로드킬", "기타" };

    private static int?[] Traces(Models.AmphibianReptileEntry e) => new[]
    {
        e.Trace1, e.Trace2, e.Trace3, e.Trace4, e.Trace5, e.Trace6
    };

    public static string? Export(AmphibianReptileEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "양서파충류 조사결과(보고서용) 엑셀 내보내기",
            ReportExporterBase.ReportFileName(vm.Meta, "양서파충류"),
            path => Write(vm, path));

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(AmphibianReptileEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = new Workbook();
        ObservationReportCommon.WriteOverview(wb, m, "양서파충류 조사결과 — 조사개황");
        WriteSpecies(wb, vm);
        WriteSummary(wb, vm);

        ReportExporterBase.Save(wb, path);

    }

    // ── 시트2 : 출현종 (1행 = 1출현기록, 흔적 6종 개체수 포함) ──
    private static void WriteSpecies(Workbook wb, AmphibianReptileEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "출현종";
        int row = 0;

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("양서파충류 출현종 목록"); row += 2;

        var headers = new List<string>(SiteColumns.Headers) { "목", "과", "국명", "학명", "대분류", "중분류" };
        headers.AddRange(TraceNames);
        headers.AddRange(new[] { "합계", "위도", "경도", "특징", "특이사항", "구분" });

        int headerRow = row;
        WriteHeader(ws, row, headers.ToArray());
        row++;

        foreach (var e in vm.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.SpeciesKo) || e.TraceSum <= 0) continue;   // 관측된 기록만

            SiteColumns.Write(ws, row, vm.Meta);
            Str(ws, row, 4, e.OrderKo);
            Str(ws, row, 5, e.FamilyKo);
            ws.Cells[row, 6].SetValue(e.SpeciesKo);
            Str(ws, row, 7, e.SpeciesEn);
            Str(ws, row, 8, e.MajorCategory);
            Str(ws, row, 9, e.MiddleCategory);

            int col = 10;
            foreach (var t in Traces(e)) { Num(ws, row, col, t); col++; }   // 미입력(null)은 빈 칸

            Num(ws, row, col, e.TraceSum); col++;
            Num(ws, row, col, e.Lat); col++;
            Num(ws, row, col, e.Lng); col++;
            Str(ws, row, col, e.Feature); col++;
            Str(ws, row, col, e.Note); col++;
            var tag = e.ProtectionText;
            if (!string.IsNullOrEmpty(tag))
            {
                ws.Cells[row, col].SetValue(tag);
                ws.Cells[row, col].SetIsBold(true);
                ws.Cells[row, col].SetForeColor(new ThemableColor(e.IsInvasive ? GradeColor("D") : Accent));
            }
            row++;
        }

        int lastCol = headers.Count - 1;
        int lastRow = row - 1;
        SiteColumns.Widths(ws);
        Width(ws, 3, 110); Width(ws, 4, 95); Width(ws, 5, 110); Width(ws, 6, 140); Width(ws, 7, 200);
        Width(ws, 8, 90); Width(ws, 9, 110);
        for (int c = 10; c < 10 + TraceNames.Length; c++) Width(ws, c, 75);
        Width(ws, lastCol - 4, 70);  Width(ws, lastCol - 3, 95); Width(ws, lastCol - 2, 95);
        Width(ws, lastCol - 1, 130); Width(ws, lastCol, 150);
        TryAutoFilter(ws, headerRow, lastRow, lastCol);
        FontAll(ws, lastRow, lastCol);
    }

    // ── 시트3 : 출현종요약 (총계·보호종·흔적유형별 합계) ──
    private static void WriteSummary(Workbook wb, AmphibianReptileEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "출현종요약";
        var m = vm.Meta;

        var observed = vm.Entries.Where(e => !string.IsNullOrWhiteSpace(e.SpeciesKo) && e.TraceSum > 0).ToList();

        int row = 0;
        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("양서파충류 출현종 요약"); row += 2;

        var headers = new List<string>(SiteColumns.Headers)
        {
            "연도차수", "하천명", "조사일",
            "총종수", "총개체수", "관찰건수", "법정보호종 수", "법정보호종 목록", "생태계교란생물",
            "양서류 종수", "파충류 종수"
        };
        headers.AddRange(TraceNames.Select(t => $"{t} 합계"));

        int headerRow = row;
        WriteHeader(ws, row, headers.ToArray());
        row++;

        var protectedList = observed.Where(e => e.IsProtected).Select(e => e.SpeciesKo!).Distinct().ToList();
        var invasiveList = observed.Where(e => e.IsInvasive).Select(e => e.SpeciesKo!).Distinct().ToList();

        SiteColumns.Write(ws, row, m);
        Str(ws, row, 4, m.YearChsu);
        Str(ws, row, 5, m.River);
        Str(ws, row, 6, m.SurveyDate?.ToString("yyyy-MM-dd"));
        Num(ws, row, 7, observed.Select(e => e.SpeciesKo).Distinct().Count());
        Num(ws, row, 8, observed.Sum(e => e.TraceSum));
        Num(ws, row, 9, observed.Count);
        Num(ws, row, 10, protectedList.Count);
        Str(ws, row, 11, string.Join(", ", protectedList));
        Str(ws, row, 12, string.Join(", ", invasiveList));
        Num(ws, row, 13, observed.Where(e => e.MajorCategory == "양서류").Select(e => e.SpeciesKo).Distinct().Count());
        Num(ws, row, 14, observed.Where(e => e.MajorCategory == "파충류").Select(e => e.SpeciesKo).Distinct().Count());

        int col = 15;
        for (int i = 0; i < TraceNames.Length; i++, col++)
            Num(ws, row, col, observed.Sum(e => Traces(e)[i] ?? 0));

        int lastCol = headers.Count - 1;
        int lastRow = row;
        SiteColumns.Widths(ws);
        Width(ws, 3, 110); Width(ws, 4, 100); Width(ws, 5, 100); Width(ws, 6, 95);
        Width(ws, 7, 75); Width(ws, 8, 85); Width(ws, 9, 85); Width(ws, 10, 100);
        Width(ws, 11, 320); Width(ws, 12, 220); Width(ws, 13, 95); Width(ws, 14, 95);
        for (int c = 15; c <= lastCol; c++) Width(ws, c, 90);
        TryAutoFilter(ws, headerRow, lastRow, lastCol);

        row += 2;
        ws.Cells[row, 0].SetValue("※ 양서파충류는 건강성평가 지수가 없어 총계·보호종·흔적유형별 합계로 대신한다.");
        row++;
        ws.Cells[row, 0].SetValue("※ 총개체수 = 흔적 6종 합계. 미입력(빈 칸)은 0으로 보지 않고 집계에서만 0으로 캐스팅한다.");
        FontAll(ws, row, lastCol);
    }
}
