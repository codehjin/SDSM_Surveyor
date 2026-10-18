using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 서식·수변환경 → 관리자 '서식 및 수변환경' 시트 일괄입력 엑셀 내보내기.
/// 전치 레이아웃: 필드=행, 조사레코드=열 F(0-based 5). 앵커=행1(연도차수).
/// 매핑 근거: 관리자 BulkHabitatWaterEdgeControlViewModel (FirstRowNumber=2, FirstColumnNumber=6).
/// 자동계산(평가점수·평가등급)은 관리자가 재계산하므로 내보내지 않고, **평가항목 1~10 점수만** 넣는다.
///
/// B-7 : 빈 통합문서 대신 **내장 양식**(`templates\bulk\habitat.xlsx`)을 열어 같은 좌표에 쓴다.
/// 양식의 라벨(B~D)·예시(E)·평가 수식(27~29행)·필독이 함께 나가므로 종전의 임시 라벨(C열)은 더 쓰지 않는다.
/// </summary>
public static class HabitatWaterEdgeExcelExporter
{
    private const string TemplateFile = "habitat.xlsx";
    private const string SheetName = "서식 및 수변환경";
    private const int RecordCol = 5;     // F열(0-based)
    private const int ItemFirstRow = 16; // 평가항목1 = 행16

    public static string? Export(HabitatWaterEdgeEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "서식·수변환경 일괄입력 엑셀 내보내기",
            ReportExporterBase.BulkFileName(vm.Meta, "서식수변"),
            path => Write(vm, path));

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(HabitatWaterEdgeEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = BulkTemplate.Open(TemplateFile);
        var ws = BulkTemplate.Sheet(wb, SheetName);

        void Set(int row, string? val) => BulkTemplate.Put(ws, row, RecordCol, val);

        Set(1, m.YearChsu);                          // 앵커(필수)
        Set(2, m.Site);
        Set(3, m.SurveyDate?.ToString("yyyyMMdd"));
        Set(4, m.MajorRegion);
        Set(5, m.MiddleRegion);
        Set(6, m.River);
        Set(7, m.RiverTypeWater);   // 하천유형(수질·서식수변 칸 · E1)
        Set(8, m.Lat);
        Set(9, m.Lng);
        Set(10, m.Weather);
        Set(11, m.SurveyAgency);
        Set(12, m.Surveyor);
        Set(13, vm.SurveyUnavailableReason);
        Set(14, vm.Note);

        // 평가항목 1~10 (좌/우안 항목은 화면과 동일하게 평균값을 넣는다)
        var items = vm.ComputeDetail().Items;
        for (int i = 0; i < HabitatWaterEdgeEntryViewModel.ItemNames.Length; i++)
            if (i < items.Length) BulkTemplate.Put(ws, ItemFirstRow + i, RecordCol, items[i]);

        ReportExporterBase.Save(wb, path);
    }
}
