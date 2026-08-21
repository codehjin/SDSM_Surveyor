using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 서식·수변환경 → 관리자 '서식 및 수변환경' 시트 일괄입력 엑셀 내보내기.
/// 전치 레이아웃: 필드=행, 조사레코드=열 F(0-based 5). 앵커=행1(연도차수).
/// 매핑 근거: 관리자 BulkHabitatWaterEdgeControlViewModel (FirstRowNumber=2, FirstColumnNumber=6).
/// 자동계산(평가점수·평가등급)은 관리자가 재계산하므로 내보내지 않고, **평가항목 1~10 점수만** 넣는다.
/// </summary>
public static class HabitatWaterEdgeExcelExporter
{
    private const int RecordCol = 5;    // F열(0-based)
    private const int LabelCol = 2;     // C열 — 사람이 읽기 위한 라벨(관리자는 행 번호로 읽는다)
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
        var wb = new Workbook();
        var ws = wb.Worksheets.Add();
        ws.Name = "서식 및 수변환경";

        void Set(int row, string label, string? val)
        {
            ws.Cells[row, LabelCol].SetValue(label);
            if (!string.IsNullOrWhiteSpace(val)) ws.Cells[row, RecordCol].SetValue(val);
        }

        Set(1, "연도차수", m.YearChsu);                          // 앵커(필수)
        Set(2, "지점명", m.Site);
        Set(3, "조사일", m.SurveyDate?.ToString("yyyyMMdd"));
        Set(4, "대권역명", m.MajorRegion);
        Set(5, "중권역명", m.MiddleRegion);
        Set(6, "하천명", m.River);
        Set(7, "하천유형", m.RiverType);
        Set(8, "위도", m.Lat);
        Set(9, "경도", m.Lng);
        Set(10, "날씨", m.Weather);
        Set(11, "조사기관", m.SurveyAgency);
        Set(12, "조사자", m.Surveyor);
        Set(13, "조사불가시", vm.SurveyUnavailableReason);
        Set(14, "비고", vm.Note);

        // 평가항목 1~10 (좌/우안 항목은 화면과 동일하게 평균값을 넣는다)
        var items = vm.ComputeDetail().Items;
        for (int i = 0; i < HabitatWaterEdgeEntryViewModel.ItemNames.Length; i++)
        {
            int row = ItemFirstRow + i;
            ws.Cells[row, LabelCol].SetValue($"{i + 1}. {HabitatWaterEdgeEntryViewModel.ItemNames[i]}");
            if (i < items.Length && items[i] is double v) ws.Cells[row, RecordCol].SetValue(v);
        }

        ReportExporterBase.Save(wb, path);

    }
}
