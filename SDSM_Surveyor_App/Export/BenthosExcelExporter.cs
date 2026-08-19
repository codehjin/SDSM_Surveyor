using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 저서동물 → 관리자 '입력' 시트 일괄입력 엑셀 내보내기.
/// 전치 레이아웃: 필드=행, 조사레코드=열 M(0-based 12). 종=행47(0-based 46)부터.
/// ※ 어류와 달리 종 좌측열은 학명=J(9)/국명=K(10) 순, 개체수는 실수(double).
/// 매핑 근거: 관리자 BulkBenthosControlViewModel. 자동계산(DI/H'/R1/J'/BMI)은 내보내지 않음.
/// </summary>
public static class BenthosExcelExporter
{
    private const int RecordCol = 12;       // M열(0-based)
    private const int SpeciesFirstRow = 46; // 행47(0-based)

    public static string? Export(BenthosEntryViewModel vm)
    {
        var m = vm.Meta;
        var project = string.IsNullOrWhiteSpace(m.Project) ? "방류하천" : m.Project!;
        var site = string.IsNullOrWhiteSpace(m.Site) ? "" : $"_{m.Site}";

        var dlg = new SaveFileDialog
        {
            Title = "저서동물 일괄입력 엑셀 내보내기",
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = $"{project}{site}_저서동물_DB_v1.xlsx"
        };
        if (dlg.ShowDialog() != true) return null;

        var wb = new Workbook();
        var ws = wb.Worksheets.Add();
        ws.Name = "입력";

        void Set(int row, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val)) ws.Cells[row, RecordCol].SetValue(val);
        }

        Set(1, m.YearChsu);
        Set(2, m.Site);
        Set(3, m.SurveyDate?.ToString("yyyyMMdd"));
        Set(4, m.MajorRegion);
        Set(5, m.MiddleRegion);
        Set(6, m.River);
        Set(7, m.RiverType);
        Set(8, m.Lat);
        Set(9, m.Lng);
        Set(10, m.Weather);
        Set(11, m.SurveyAgency);
        Set(12, m.Surveyor);
        Set(13, vm.Surbernet30); Set(14, vm.Surbernet50); Set(15, vm.Dredge); Set(16, vm.Ekman);
        Set(17, vm.Watershed); Set(18, vm.PollutionSource); Set(19, vm.CanopyCover);
        Set(20, vm.Floodplain); Set(21, vm.LeveeLeft); Set(22, vm.LeveeRight);
        Set(23, vm.Bedrock); Set(24, vm.Concrete); Set(25, vm.Mud); Set(26, vm.Sand);
        Set(27, vm.FineGravel); Set(28, vm.Gravel); Set(29, vm.SmallStone); Set(30, vm.BigStone);
        Set(32, vm.HabitatRiverType); Set(33, vm.RiverWidth); Set(34, vm.WaterWidth);
        Set(35, vm.AverageDepth); Set(36, vm.AverageVelocity);
        Set(37, vm.AirTemperature); Set(38, vm.WaterTemperature);
        Set(39, vm.FlowState); Set(40, vm.Transparency); Set(41, vm.Smell);
        Set(42, vm.Note); Set(43, vm.SurveyUnavailableReason);

        // 종 목록 : 행47+ (관측된 종 개체수>0만). 좌측 A~K, 개체수는 레코드 열(M, double).
        int r = SpeciesFirstRow;
        foreach (var e in vm.SpeciesEntries)
        {
            var ko = e.SelectedSpecies?.SpeciesKo ?? e.SpeciesKo;
            if (string.IsNullOrWhiteSpace(ko) || (e.IndividualCount ?? 0) <= 0) continue;

            var sp = e.SelectedSpecies;
            void SetSp(int col, string? val) { if (!string.IsNullOrWhiteSpace(val)) ws.Cells[r, col].SetValue(val); }

            if (sp?.SaprobicValue is double sv) ws.Cells[r, 0].SetValue(sv);
            if (sp?.IndicatorWeight is int iw) ws.Cells[r, 1].SetValue(iw);
            SetSp(2, sp?.Endangered1);
            SetSp(3, sp?.Endangered2);
            SetSp(4, sp?.Endemic);
            SetSp(5, sp?.PhylumKo);
            SetSp(6, sp?.ClassKo);
            SetSp(7, sp?.OrderKo);
            SetSp(8, sp?.FamilyKo);
            SetSp(9, sp?.SpeciesEn);   // 학명 J
            ws.Cells[r, 10].SetValue(ko);   // 국명 K
            ws.Cells[r, RecordCol].SetValue(e.IndividualCount!.Value);  // 개체수 M (double)
            r++;
        }

        using (var stream = new FileStream(dlg.FileName, FileMode.Create))
            new XlsxFormatProvider().Export(wb, stream, null);

        return dlg.FileName;
    }
}
