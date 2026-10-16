using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 조류 → 관리자 '조류' 시트 일괄입력 엑셀 내보내기.
/// 일반 레이아웃: 1행 = 1출현기록. 데이터는 **0-based 행4**(엑셀 5행)부터, 앵커는 **B열(0-based 1)=연도차수**.
/// 매핑 근거: 관리자 BulkBirdControlViewModel (FirstRowNumber=5, FirstColumnNumber=2).
/// 조류는 자동계산이 없어 전 필드를 그대로 내보낸다.
/// ※ 좌표는 도분초(9~14)를 비우고 십진 위/경도(15·16)만 채운다 —
///   관리자는 도·분·초가 모두 있을 때만 도분초로 환산하고, 없으면 십진값을 그대로 쓴다.
///
/// B-7 : 빈 통합문서 대신 **내장 양식**(`templates\bulk\bird.xlsx`)을 열어 같은 좌표에 쓴다.
/// 양식의 1~3행 머리글·4행 예시·필독·보조 시트가 함께 나가므로 종전의 임시 머리글(4행)은 더 쓰지 않는다.
/// </summary>
public static class BirdExcelExporter
{
    private const string TemplateFile = "bird.xlsx";
    private const string SheetName = "조류";
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
        var wb = BulkTemplate.Open(TemplateFile);
        var ws = BulkTemplate.Sheet(wb, SheetName);

        int r = FirstRow;
        foreach (var e in vm.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.SpeciesKo) || (e.IndividualCount ?? 0) <= 0) continue;  // 관측된 기록만

            void Set(int col, string? val) => BulkTemplate.Put(ws, r, col, val);

            Set(1, m.YearChsu);                            // 앵커(비면 그 행은 무시된다)
            Set(2, m.SurveyDate?.ToString("yyyyMMdd"));
            Set(3, m.Weather);
            Set(4, m.River);
            Set(5, m.Site);
            Set(6, e.SpeciesKo);
            Set(7, e.SpeciesEn);
            Set(8, e.Feature);
            BulkTemplate.Put(ws, r, 15, e.Lat);
            BulkTemplate.Put(ws, r, 16, e.Lng);
            BulkTemplate.Put(ws, r, 18, (double?)e.IndividualCount);
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
