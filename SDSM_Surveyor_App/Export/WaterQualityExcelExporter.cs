using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 수질 → 관리자 '수질' 시트 일괄입력 엑셀 내보내기.
/// 전치 레이아웃: 필드=행, 조사레코드=열 F(0-based 5). 앵커=행1(연도차수).
/// 매핑 근거: 관리자 BulkWaterQualityControlViewModel (FirstRowNumber=2, FirstColumnNumber=6).
/// 자동계산(등급 8종)은 관리자가 재계산하므로 내보내지 않는다.
///
/// ⚠ 확장항목 14종은 고정 행이 아니다. 관리자가 **행22부터 C열(+D열)의 라벨 텍스트를 검색**해 찾으므로
///   반드시 C열에 라벨을 기입한다. 라벨에 단위를 붙이면 다른 항목 검색어와 충돌할 수 있어
///   (예: "초당유량(m3/sec)"에 'ec'가 들어가 EC로 오인) 단위는 검색 대상이 아닌 E열에 둔다.
/// </summary>
public static class WaterQualityExcelExporter
{
    private const int RecordCol = 5;    // F열(0-based)
    private const int LabelCol = 2;     // C열 — 관리자가 확장항목을 찾는 라벨 열
    private const int UnitCol = 4;      // E열 — 검색 대상이 아니므로 단위는 여기에
    private const int ExtraFirstRow = 22;

    public static string? Export(WaterQualityEntryViewModel vm)
    {
        var m = vm.Meta;
        var project = string.IsNullOrWhiteSpace(m.Project) ? "방류하천" : m.Project!;
        var site = string.IsNullOrWhiteSpace(m.Site) ? "" : $"_{m.Site}";

        var dlg = new SaveFileDialog
        {
            Title = "수질 일괄입력 엑셀 내보내기",
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = $"{project}{site}_수질_DB_v1.xlsx"
        };
        if (dlg.ShowDialog() != true) return null;

        Write(vm, dlg.FileName);
        return dlg.FileName;
    }

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(WaterQualityEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = new Workbook();
        var ws = wb.Worksheets.Add();
        ws.Name = "수질";

        // 고정 행 : 라벨(C, 사람이 보기 위함 · 관리자는 행 번호로 읽는다) + 값(F)
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
        Set(13, "pH", vm.PH);
        Set(14, "BOD", vm.Bod);
        Set(15, "COD", vm.Cod);
        Set(16, "TOC", vm.Toc);
        Set(17, "SS", vm.Ss);
        Set(18, "DO", vm.Dox);
        Set(19, "T-P", vm.Tp);
        Set(20, "생태독성", vm.Ecotoxicity);
        Set(21, "대장균군", vm.EColi);

        // 확장항목 : 라벨(C) + 단위(E) + 값(F). 라벨 문구는 관리자 검색어와 일치해야 한다.
        var extras = new (string Label, string Unit, string? Value)[]
        {
            ("T-N",       "mg/L",    vm.TN),
            ("전기전도도", "µS/cm",   vm.EC),
            ("염소이온",   "mg/L",    vm.Cl),
            ("황이온",     "mg/L",    vm.SO42),
            ("구리",       "mg/L",    vm.Cu),
            ("아연",       "mg/L",    vm.Zn),
            ("크롬",       "mg/L",    vm.Cr),
            ("탁도",       "NTU",     vm.Turbidity),
            ("클로로필a",  "mg/m3",   vm.Chla),
            ("수온",       "℃",      vm.WaterTemperature),
            ("수심",       "cm",      vm.WaterDepth),
            ("유속",       "cm/s",    vm.FlowVelocity),
            ("초당유량",   "m3/sec",  vm.FlowSec),
            ("일당유량",   "m3/day",  vm.FlowDay),
        };

        int r = ExtraFirstRow;
        foreach (var (label, unit, value) in extras)
        {
            ws.Cells[r, LabelCol].SetValue(label);
            ws.Cells[r, UnitCol].SetValue(unit);
            if (!string.IsNullOrWhiteSpace(value)) ws.Cells[r, RecordCol].SetValue(value);
            r++;
        }

        using (var stream = new FileStream(path, FileMode.Create))
            new XlsxFormatProvider().Export(wb, stream, null);

    }
}
