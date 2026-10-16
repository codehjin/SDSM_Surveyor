using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 포유류 → 관리자 '포유류' 시트 일괄입력 엑셀 내보내기.
/// 일반 레이아웃: 1행 = 1출현기록. 데이터는 **0-based 행4**(엑셀 5행)부터, 앵커는 **B열=연도차수**.
/// 매핑 근거: 관리자 BulkMammalControlViewModel (FirstRowNumber=5, FirstColumnNumber=2).
/// 흔적 12종은 열 19~30(Trace1~12)이며 **정수 개체수**로 넣는다.
///
/// B-7 : 빈 통합문서 대신 **내장 양식**(`templates\bulk\mammal.xlsx`)을 열어 쓴다. 양식 머리글·예시·필독·보조 시트가 함께 나간다.
/// ⚠ 관찰지유형은 양식 머리글대로 **S열(0-based 18)** 에 쓴다(10-16 (2) 결정). 종전·관리자 Import 는 R열(17)이었다 —
///   관리자 Import 수정은 관리자 저장소에서 따로 한다(90_TECH_NOTES §4).
/// </summary>
public static class MammalExcelExporter
{
    private const string TemplateFile = "mammal.xlsx";
    private const string SheetName = "포유류";
    private const int FirstRow = 4;            // 0-based
    private const int ObservationSiteCol = 18; // S열 — 양식 머리글 '관찰지 유형'
    private const int TraceFirstCol = 19;      // Trace1

    public static string? Export(MammalEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "포유류 일괄입력 엑셀 내보내기",
            ReportExporterBase.BulkFileName(vm.Meta, "포유류"),
            path => Write(vm, path));

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(MammalEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = BulkTemplate.Open(TemplateFile);
        var ws = BulkTemplate.Sheet(wb, SheetName);

        int r = FirstRow;
        foreach (var e in vm.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.SpeciesKo) || e.TraceSum <= 0) continue;   // 관측된 기록만

            void Set(int col, string? val) => BulkTemplate.Put(ws, r, col, val);

            Set(1, m.YearChsu);                            // 앵커
            Set(2, m.SurveyDate?.ToString("yyyyMMdd"));
            Set(3, m.Weather);
            Set(4, m.River);
            Set(5, m.Site);
            Set(6, e.SpeciesKo);
            Set(7, e.SpeciesEn);
            Set(8, e.Feature);
            BulkTemplate.Put(ws, r, 15, e.Lat);
            BulkTemplate.Put(ws, r, 16, e.Lng);
            Set(ObservationSiteCol, e.ObservationSite);

            var traces = new[]
            {
                e.Trace1, e.Trace2, e.Trace3, e.Trace4, e.Trace5, e.Trace6,
                e.Trace7, e.Trace8, e.Trace9, e.Trace10, e.Trace11, e.Trace12
            };
            for (int i = 0; i < traces.Length; i++) BulkTemplate.Put(ws, r, TraceFirstCol + i, (double?)traces[i]);  // 미입력은 빈 칸(null 유지)

            Set(31, m.Surveyor);
            Set(32, e.Note);
            r++;
        }

        ReportExporterBase.Save(wb, path);
    }
}
