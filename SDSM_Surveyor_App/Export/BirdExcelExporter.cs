using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 조류 → 관리자 '조류' 시트 일괄입력 엑셀 내보내기.
/// 일반 레이아웃: 1행 = 1출현기록. 데이터는 **0-based 행4**(엑셀 5행)부터, 앵커는 **B열(0-based 1)=연도차수**.
/// 매핑 근거: 관리자 BulkBirdControlViewModel (FirstRowNumber=5, FirstColumnNumber=2).
/// 조류는 자동계산이 없어 전 필드를 그대로 내보낸다.
/// ※ 좌표는 도분초(9~14)를 비우고 십진 위/경도(15·16)만 채운다 —
///   관리자는 도·분·초가 모두 있을 때만 도분초로 환산하고, 없으면 십진값을 그대로 쓴다.
/// </summary>
public static class BirdExcelExporter
{
    private const int FirstRow = 4;   // 0-based (FirstRowNumber=5)

    public static string? Export(BirdEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "조류 일괄입력 엑셀 내보내기",
            ReportExporterBase.BulkFileName(vm.Meta, "조류"),
            path => Write(vm, path));

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(BirdEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = new Workbook();
        var ws = wb.Worksheets.Add();
        ws.Name = "조류";

        // 헤더(행1~3은 관리자가 읽지 않는다. 사람이 확인하기 쉽도록 이름만 적어둔다)
        var headers = new (int Col, string Name)[]
        {
            (1, "연도차수"), (2, "조사일"), (3, "날씨"), (4, "하천명"), (5, "지점명"),
            (6, "국명"), (7, "학명"), (8, "특징"),
            (15, "위도"), (16, "경도"), (18, "개체수"), (19, "도래유형"),
            (20, "대항목"), (21, "세부항목"), (22, "서식유형"), (23, "조사자"), (24, "비고")
        };
        foreach (var (col, name) in headers) ws.Cells[FirstRow - 1, col].SetValue(name);

        int r = FirstRow;
        foreach (var e in vm.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.SpeciesKo) || (e.IndividualCount ?? 0) <= 0) continue;  // 관측된 기록만

            void Set(int col, string? val) { if (!string.IsNullOrWhiteSpace(val)) ws.Cells[r, col].SetValue(val); }

            Set(1, m.YearChsu);                            // 앵커(비면 그 행은 무시된다)
            Set(2, m.SurveyDate?.ToString("yyyyMMdd"));
            Set(3, m.Weather);
            Set(4, m.River);
            Set(5, m.Site);
            ws.Cells[r, 6].SetValue(e.SpeciesKo);
            Set(7, e.SpeciesEn);
            Set(8, e.Feature);
            if (e.Lat is double lat) ws.Cells[r, 15].SetValue(lat);
            if (e.Lng is double lng) ws.Cells[r, 16].SetValue(lng);
            ws.Cells[r, 18].SetValue((double)e.IndividualCount!.Value);
            Set(19, e.MigratoryType);
            Set(20, e.Category);
            Set(21, e.CategoryDetail);
            Set(22, e.HabitatType);
            Set(23, m.Surveyor);
            Set(24, e.Note);
            r++;
        }

        ReportExporterBase.Save(wb, path);

    }
}
