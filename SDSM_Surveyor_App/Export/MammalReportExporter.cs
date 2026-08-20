using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;
using ThemableColor = Telerik.Documents.Common.Model.ThemableColor;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 포유류 → 보고서·기록용 엑셀. 시트:
///  [조사개황] · [출현종] 흔적 12종 포함 필터 테이블 · [출현종요약] 흔적유형별 합계.
/// 포유류는 건강성평가 지수가 없으므로 3번째 시트가 요약표다(03_FEATURE_EXPORT §2).
/// </summary>
public static class MammalReportExporter
{
    /// <summary>흔적 12종 이름(관리자 Mammal.Trace1~12 순서).</summary>
    internal static readonly string[] TraceNames =
    {
        "포획", "관찰", "울음", "사체", "족적", "털", "식흔", "굴", "번식지", "배설물", "카메라", "기타"
    };

    private static int?[] Traces(Models.MammalEntry e) => new[]
    {
        e.Trace1, e.Trace2, e.Trace3, e.Trace4, e.Trace5, e.Trace6,
        e.Trace7, e.Trace8, e.Trace9, e.Trace10, e.Trace11, e.Trace12
    };

    public static string? Export(MammalEntryViewModel vm)
    {
        var m = vm.Meta;
        var project = string.IsNullOrWhiteSpace(m.Project) ? "" : m.Project! + "_";
        var site = string.IsNullOrWhiteSpace(m.Site) ? "" : m.Site + "_";

        var dlg = new SaveFileDialog
        {
            Title = "포유류 조사결과(보고서용) 엑셀 내보내기",
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = $"{project}{site}포유류_조사결과.xlsx"
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
        ObservationReportCommon.WriteOverview(wb, m, "포유류 조사결과 — 조사개황");
        WriteSpecies(wb, vm);
        WriteSummary(wb, vm);

        using (var stream = new FileStream(path, FileMode.Create))
            new XlsxFormatProvider().Export(wb, stream, null);

    }

    // ── 시트2 : 출현종 (1행 = 1출현기록, 흔적 12종 개체수 포함) ──
    private static void WriteSpecies(Workbook wb, MammalEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "출현종";
        int row = 0;

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("포유류 출현종 목록"); row += 2;

        var headers = new List<string> { "지점명", "목", "과", "국명", "학명", "관찰지유형" };
        headers.AddRange(TraceNames);
        headers.AddRange(new[] { "합계", "위도", "경도", "특징", "특이사항", "구분" });

        int headerRow = row;
        WriteHeader(ws, row, headers.ToArray());
        row++;

        var site = vm.Meta.Site ?? "";
        foreach (var e in vm.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.SpeciesKo) || e.TraceSum <= 0) continue;   // 관측된 기록만

            Str(ws, row, 0, site);
            Str(ws, row, 1, e.OrderKo);
            Str(ws, row, 2, e.FamilyKo);
            ws.Cells[row, 3].SetValue(e.SpeciesKo);
            Str(ws, row, 4, e.SpeciesEn);
            Str(ws, row, 5, e.ObservationSite);

            int col = 6;
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
        Width(ws, 0, 110); Width(ws, 1, 95); Width(ws, 2, 110); Width(ws, 3, 140); Width(ws, 4, 200); Width(ws, 5, 110);
        for (int c = 6; c < 6 + TraceNames.Length; c++) Width(ws, c, 70);
        Width(ws, lastCol - 4, 70);  Width(ws, lastCol - 3, 95); Width(ws, lastCol - 2, 95);
        Width(ws, lastCol - 1, 130); Width(ws, lastCol, 150);
        TryAutoFilter(ws, headerRow, lastRow, lastCol);
        FontAll(ws, lastRow, lastCol);
    }

    // ── 시트3 : 출현종요약 (총계·보호종·흔적유형별 합계) ──
    private static void WriteSummary(Workbook wb, MammalEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "출현종요약";
        var m = vm.Meta;

        var observed = vm.Entries.Where(e => !string.IsNullOrWhiteSpace(e.SpeciesKo) && e.TraceSum > 0).ToList();

        int row = 0;
        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("포유류 출현종 요약"); row += 2;

        var headers = new List<string>
        {
            "지점명", "연도차수", "하천명", "조사일",
            "총종수", "총개체수", "관찰건수", "법정보호종 수", "법정보호종 목록", "생태계교란생물"
        };
        headers.AddRange(TraceNames.Select(t => $"{t} 합계"));

        int headerRow = row;
        WriteHeader(ws, row, headers.ToArray());
        row++;

        var protectedList = observed.Where(e => e.IsProtected).Select(e => e.SpeciesKo!).Distinct().ToList();
        var invasiveList = observed.Where(e => e.IsInvasive).Select(e => e.SpeciesKo!).Distinct().ToList();

        Str(ws, row, 0, m.Site);
        Str(ws, row, 1, m.YearChsu);
        Str(ws, row, 2, m.River);
        Str(ws, row, 3, m.SurveyDate?.ToString("yyyy-MM-dd"));
        Num(ws, row, 4, observed.Select(e => e.SpeciesKo).Distinct().Count());
        Num(ws, row, 5, observed.Sum(e => e.TraceSum));
        Num(ws, row, 6, observed.Count);
        Num(ws, row, 7, protectedList.Count);
        Str(ws, row, 8, string.Join(", ", protectedList));
        Str(ws, row, 9, string.Join(", ", invasiveList));

        int col = 10;
        for (int i = 0; i < TraceNames.Length; i++, col++)
            Num(ws, row, col, observed.Sum(e => Traces(e)[i] ?? 0));

        int lastCol = headers.Count - 1;
        int lastRow = row;
        Width(ws, 0, 110); Width(ws, 1, 100); Width(ws, 2, 100); Width(ws, 3, 95);
        Width(ws, 4, 75); Width(ws, 5, 85); Width(ws, 6, 85); Width(ws, 7, 100);
        Width(ws, 8, 320); Width(ws, 9, 220);
        for (int c = 10; c <= lastCol; c++) Width(ws, c, 85);
        TryAutoFilter(ws, headerRow, lastRow, lastCol);

        row += 2;
        ws.Cells[row, 0].SetValue("※ 포유류는 건강성평가 지수가 없어 총계·보호종·흔적유형별 합계로 대신한다.");
        row++;
        ws.Cells[row, 0].SetValue("※ 총개체수 = 흔적 12종 합계. 미입력(빈 칸)은 0으로 보지 않고 집계에서만 0으로 캐스팅한다.");
        FontAll(ws, row, lastCol);
    }
}
