using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 어류 → 관리자 '어류_입력' 일괄입력 엑셀 내보내기.
/// 관리자와 동일한 Telerik 문서처리 라이브러리·동일 셀 좌표(전치: 필드=행, 조사레코드=열 Q).
/// 매핑 근거: 관리자 BulkFishControlViewModel(0-based Cells[row,col]) + 실제 제출 엑셀 대조.
/// 자동계산(FAI 등)은 관리자가 재계산하므로 내보내지 않는다.
/// </summary>
public static class FishExcelExporter
{
    private const int RecordCol = 16;       // Q열(0-based) : 조사자 1개 레코드를 이 열에 기록
    private const int SpeciesFirstRow = 78; // 행79(0-based 78)부터 종 목록

    /// <summary>저장 대화상자 → 내보내기. 저장 경로 반환(취소 시 null).</summary>
    public static string? Export(FishEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "어류 일괄입력 엑셀 내보내기",
            ReportExporterBase.BulkFileName(vm.Meta, "어류"),
            path => Write(vm, path));

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(FishEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = new Workbook();
        var ws = wb.Worksheets.Add();
        ws.Name = "어류_입력";

        // 레코드 열(Q)에 필드 기록 : 값이 있을 때만
        void Set(int row, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val)) ws.Cells[row, RecordCol].SetValue(val);
        }

        Set(1, m.YearChsu);                        // 연도차수(앵커·필수)
        Set(2, m.Site);                            // 지점명
        Set(3, m.SurveyDate?.ToString("yyyyMMdd")); // 조사일
        Set(4, m.MajorRegion);
        Set(5, m.MiddleRegion);
        Set(6, m.River);
        Set(7, m.RiverType);
        Set(8, m.Lat);
        Set(9, m.Lng);
        Set(10, m.Weather);
        Set(11, m.SurveyAgency);
        Set(12, m.Surveyor);
        Set(13, vm.CollectionTime);
        Set(14, vm.CollectionTool);
        Set(15, vm.CollectionFlowState);
        Set(16, vm.RiverChasu?.ToString());         // 하천차수(FAI 재계산 입력)
        Set(17, vm.Bedrock); Set(18, vm.Concrete); Set(19, vm.Mud); Set(20, vm.Sand);
        Set(21, vm.FineGravel); Set(22, vm.Gravel); Set(23, vm.SmallStone); Set(24, vm.BigStone);
        Set(26, vm.HabitatRiverType); Set(27, vm.HabitatFlowState);
        Set(28, vm.SurveyUnavailableReason); Set(29, vm.Note);
        Set(43, vm.DeCount); Set(44, vm.EfCount); Set(45, vm.LeCount); Set(46, vm.TuCount);

        // 종 목록 : 행79+ (개체수 입력된 종만). 좌측 A~O에 분류/형질, 개체수는 레코드 열(Q).
        int r = SpeciesFirstRow;
        foreach (var e in vm.SpeciesEntries)
        {
            var ko = e.SelectedSpecies?.SpeciesKo ?? e.SpeciesKo;
            if (string.IsNullOrWhiteSpace(ko) || (e.IndividualCount ?? 0) <= 0) continue;  // 관측된 종(개체수>0)만

            var sp = e.SelectedSpecies;
            void SetSp(int col, string? val) { if (!string.IsNullOrWhiteSpace(val)) ws.Cells[r, col].SetValue(val); }

            SetSp(0, sp?.FishTrait);
            SetSp(1, sp?.ToleranceGuild);
            SetSp(2, sp?.FeedingGuild);
            SetSp(3, sp?.HabitatGuild);
            SetSp(4, sp?.Exotic);
            SetSp(5, sp?.Endemic);
            SetSp(6, sp?.Endangered1);
            SetSp(7, sp?.Endangered2);
            SetSp(8, sp?.NaturalMonument);
            if (sp?.LineageOrder is int lo) ws.Cells[r, 9].SetValue(lo);
            SetSp(10, sp?.ClassKo);
            SetSp(11, sp?.OrderKo);
            SetSp(12, sp?.FamilyKo);
            ws.Cells[r, 13].SetValue(ko);                              // 국명 N
            SetSp(14, sp?.SpeciesEn);                                  // 학명 O
            ws.Cells[r, RecordCol].SetValue((double)e.IndividualCount.Value); // 개체수 Q
            r++;
        }

        ReportExporterBase.Save(wb, path);

    }
}
