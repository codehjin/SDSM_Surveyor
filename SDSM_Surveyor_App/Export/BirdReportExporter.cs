using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;
using ThemableColor = Telerik.Documents.Common.Model.ThemableColor;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 조류 → 보고서·기록용 엑셀. 시트:
///  [조사개황] · [출현종] 지점명 포함 필터 테이블 · [출현종요약] 지점명 포함 1행 관리 테이블.
/// 조류는 건강성평가 지수가 없으므로 3번째 시트가 요약표다(03_FEATURE_EXPORT §2).
/// </summary>
public static class BirdReportExporter
{
    public static string? Export(BirdEntryViewModel vm)
    {
        var m = vm.Meta;
        var project = string.IsNullOrWhiteSpace(m.Project) ? "" : m.Project! + "_";
        var site = string.IsNullOrWhiteSpace(m.Site) ? "" : m.Site + "_";

        var dlg = new SaveFileDialog
        {
            Title = "조류 조사결과(보고서용) 엑셀 내보내기",
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = $"{project}{site}조류_조사결과.xlsx"
        };
        if (dlg.ShowDialog() != true) return null;

        Write(vm, dlg.FileName);
        return dlg.FileName;
    }

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(BirdEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = new Workbook();
        ObservationReportCommon.WriteOverview(wb, m, "조류 조사결과 — 조사개황");
        WriteSpecies(wb, vm);
        WriteSummary(wb, vm);

        using (var stream = new FileStream(path, FileMode.Create))
            new XlsxFormatProvider().Export(wb, stream, null);

    }

    // ── 시트2 : 출현종 (1행 = 1출현기록) ──
    private static void WriteSpecies(Workbook wb, BirdEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "출현종";
        int row = 0;

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("조류 출현종 목록"); row += 2;

        int headerRow = row;
        WriteHeader(ws, row, "지점명", "목", "과", "국명", "학명", "개체수",
                    "도래유형", "대항목", "세부항목", "서식유형", "위도", "경도", "특징", "특이사항", "구분");
        row++;

        var site = vm.Meta.Site ?? "";
        foreach (var e in vm.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.SpeciesKo) || (e.IndividualCount ?? 0) <= 0) continue;  // 관측된 종만

            Str(ws, row, 0, site);
            Str(ws, row, 1, e.OrderKo);
            Str(ws, row, 2, e.FamilyKo);
            ws.Cells[row, 3].SetValue(e.SpeciesKo);
            Str(ws, row, 4, e.SpeciesEn);
            Num(ws, row, 5, e.IndividualCount!.Value);
            Str(ws, row, 6, e.MigratoryType);
            Str(ws, row, 7, e.Category);
            Str(ws, row, 8, e.CategoryDetail);
            Str(ws, row, 9, e.HabitatType);
            Num(ws, row, 10, e.Lat);
            Num(ws, row, 11, e.Lng);
            Str(ws, row, 12, e.Feature);
            Str(ws, row, 13, e.Note);
            var tag = e.ProtectionText;
            if (!string.IsNullOrEmpty(tag))
            {
                ws.Cells[row, 14].SetValue(tag);
                ws.Cells[row, 14].SetIsBold(true);
                ws.Cells[row, 14].SetForeColor(new ThemableColor(e.IsInvasive ? GradeColor("D") : Accent));
            }
            row++;
        }

        int lastRow = row - 1;
        Width(ws, 0, 110); Width(ws, 1, 95); Width(ws, 2, 100); Width(ws, 3, 140); Width(ws, 4, 200);
        Width(ws, 5, 75); Width(ws, 6, 90); Width(ws, 7, 100); Width(ws, 8, 100); Width(ws, 9, 100);
        Width(ws, 10, 95); Width(ws, 11, 95); Width(ws, 12, 130); Width(ws, 13, 130); Width(ws, 14, 150);
        TryAutoFilter(ws, headerRow, lastRow, 14);
        FontAll(ws, lastRow, 14);
    }

    // ── 시트3 : 출현종요약 (지수 없음 → 총계·보호종·도래유형별 합계) ──
    private static void WriteSummary(Workbook wb, BirdEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "출현종요약";
        var m = vm.Meta;

        var observed = vm.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.SpeciesKo) && (e.IndividualCount ?? 0) > 0)
            .ToList();

        int row = 0;
        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("조류 출현종 요약"); row += 2;

        // 도래유형별 합계(조류에는 흔적 구분이 없어 도래유형이 이에 대응한다)
        var types = vm.MigratoryTypes;
        var headers = new List<string>
        {
            "지점명", "연도차수", "하천명", "조사일",
            "총종수", "총개체수", "법정보호종 수", "법정보호종 목록", "생태계교란생물"
        };
        headers.AddRange(types.Select(t => $"{t} 개체수"));
        headers.Add("기타 도래유형 개체수");

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
        Num(ws, row, 5, observed.Sum(e => e.IndividualCount ?? 0));
        Num(ws, row, 6, protectedList.Count);
        Str(ws, row, 7, string.Join(", ", protectedList));
        Str(ws, row, 8, string.Join(", ", invasiveList));

        int col = 9;
        foreach (var t in types)
        {
            Num(ws, row, col, observed.Where(e => e.MigratoryType == t).Sum(e => e.IndividualCount ?? 0));
            col++;
        }
        Num(ws, row, col, observed.Where(e => string.IsNullOrWhiteSpace(e.MigratoryType) || !types.Contains(e.MigratoryType))
                                  .Sum(e => e.IndividualCount ?? 0));

        int lastCol = headers.Count - 1;
        int lastRow = row;
        Width(ws, 0, 110); Width(ws, 1, 100); Width(ws, 2, 100); Width(ws, 3, 95);
        Width(ws, 4, 75); Width(ws, 5, 85); Width(ws, 6, 100); Width(ws, 7, 320); Width(ws, 8, 220);
        for (int c = 9; c <= lastCol; c++) Width(ws, c, 110);
        TryAutoFilter(ws, headerRow, lastRow, lastCol);

        row += 2;
        ws.Cells[row, 0].SetValue("※ 조류는 건강성평가 지수가 없어 총계·보호종·도래유형별 합계로 대신한다.");
        row++;
        ws.Cells[row, 0].SetValue("※ 보호종·교란종 표기는 공식 종목록(국가생물종목록) 기준 자동 산출이다.");
        FontAll(ws, row, lastCol);
    }
}
