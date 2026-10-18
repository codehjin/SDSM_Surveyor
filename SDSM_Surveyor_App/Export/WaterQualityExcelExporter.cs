using Telerik.Documents.Spreadsheet.Model;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 수질 → 관리자 '수질' 시트 일괄입력 엑셀 내보내기.
/// 전치 레이아웃: 필드=행, 조사레코드=열 F(0-based 5). 앵커=행1(연도차수).
/// 매핑 근거: 관리자 BulkWaterQualityControlViewModel (FirstRowNumber=2, FirstColumnNumber=6).
/// 자동계산(등급 8종)은 관리자가 재계산하므로 내보내지 않는다.
///
/// B-7 : 빈 통합문서 대신 **내장 양식**(`templates\bulk\water.xlsx`)을 열어 F열에만 쓴다. 양식 라벨(B~D)·예시(E)·등급 수식·필독이 함께 나간다.
/// ⚠ 확장항목 14종은 고정 행이 아니다. 관리자가 **행22(0-based)부터 C열+D열 글자를 검색**(공백 제거·소문자·포함·첫 일치)해 찾는다.
///   그래서 여기서도 **같은 규칙으로 양식 행을 찾아** F열에만 쓴다(C·E 열은 건드리지 않는다 — E 는 양식의 예시 열).
///   양식에 없는 항목(최종본 기준 `수심` 하나)은 양식의 `지속 삽입 가능` 행 아래 빈 행에 C열 라벨(관리자 검색어 그대로)과 F열 값을 덧붙인다
///   (10-16 (2) 결정). 값이 없으면 덧붙이지 않는다.
/// </summary>
public static class WaterQualityExcelExporter
{
    private const string TemplateFile = "water.xlsx";
    private const string SheetName = "수질";
    private const int RecordCol = 5;       // F열(0-based)
    private const int LabelCol = 2;        // C열 — 관리자가 확장항목을 찾는 라벨 열
    private const int DescCol = 3;         // D열 — 검색에 함께 쓰인다
    private const int ExtraSearchFrom = 22; // 관리자 검색 시작 행(0-based)

    public static string? Export(WaterQualityEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "수질 일괄입력 엑셀 내보내기",
            ReportExporterBase.BulkFileName(vm.Meta, "수질"),
            path =>
            {
                var appended = Write(vm, path);
                return appended.Count == 0 ? null : $"양식에 없어 덧붙인 항목: {string.Join(", ", appended)}";
            });

    /// <summary>
    /// 대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).
    /// 반환값은 양식에 없어 덧붙인 확장항목(라벨).
    /// </summary>
    public static IReadOnlyList<string> Write(WaterQualityEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = BulkTemplate.Open(TemplateFile);
        var ws = BulkTemplate.Sheet(wb, SheetName);

        // 고정 행 : 관리자는 행 번호로 읽는다(라벨은 양식의 것).
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
        Set(13, vm.PH);
        Set(14, vm.Bod);
        Set(15, vm.Cod);
        Set(16, vm.Toc);
        Set(17, vm.Ss);
        Set(18, vm.Dox);
        Set(19, vm.Tp);
        Set(20, vm.Ecotoxicity);
        Set(21, vm.EColi);

        // 확장항목 : (덧붙일 때 쓰는 라벨 = 관리자 검색어, 관리자 검색어 목록, 값).
        // 검색어 목록은 관리자 BulkWaterQualityControlViewModel 의 nameMap 과 같다.
        var extras = new (string Label, string[] Keys, string? Value)[]
        {
            ("T-N",       new[] { "TN", "T-N" },                                              vm.TN),
            ("전기전도도", new[] { "EC", "전기전도도", "ElectricalConductivity", "Electrical" }, vm.EC),
            ("염소이온",   new[] { "Cl", "Cl-", "염소이온" },                                  vm.Cl),
            ("황이온",     new[] { "SO42", "SO42-", "황이온" },                                vm.SO42),
            ("구리",       new[] { "구리", "Cu" },                                             vm.Cu),
            ("아연",       new[] { "아연", "Zn" },                                             vm.Zn),
            ("크롬",       new[] { "크롬", "Cr" },                                             vm.Cr),
            ("탁도",       new[] { "탁도" },                                                   vm.Turbidity),
            ("클로로필a",  new[] { "클로로필a", "클로로필", "Chla", "Chl-a" },                 vm.Chla),
            ("수온",       new[] { "수온", "온도" },                                           vm.WaterTemperature),
            ("수심",       new[] { "수심" },                                                   vm.WaterDepth),
            ("유속",       new[] { "유속" },                                                   vm.FlowVelocity),
            ("초당유량",   new[] { "초당유량", "m3/sec", "㎥/sec" },                           vm.FlowSec),
            ("일당유량",   new[] { "일당유량", "m3/day", "㎥/day" },                           vm.FlowDay),
        };

        var appended = new List<string>();
        foreach (var (label, keys, value) in extras)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;

            var row = FindRow(ws, keys);
            if (row is null)
            {
                // 양식에 없는 항목 — '지속 삽입 가능' 행 아래 첫 빈 행에 라벨(C)과 값(F)을 덧붙인다.
                row = AppendRow(ws);
                BulkTemplate.PutText(ws, row.Value, LabelCol, label);
                appended.Add(label);
            }
            BulkTemplate.Put(ws, row.Value, RecordCol, value);
        }

        ReportExporterBase.Save(wb, path);
        return appended;
    }

    /// <summary>관리자와 같은 규칙 — 행22부터 C+D 글자(공백 제거·소문자)에 검색어가 들어 있는 첫 행. C 가 비면 건너뛴다.</summary>
    private static int? FindRow(Worksheet ws, string[] keys)
    {
        var low = keys.Select(k => k.ToLowerInvariant()).ToArray();
        var used = ws.UsedCellRange;
        for (int r = ExtraSearchFrom; r <= used.ToIndex.RowIndex; r++)
        {
            var c2 = Text(ws, r, LabelCol);
            if (string.IsNullOrWhiteSpace(c2)) continue;
            var both = c2 + Text(ws, r, DescCol);
            if (low.Any(both.Contains)) return r;
        }
        return null;

        static string Text(Worksheet w, int r, int c) =>
            (w.Cells[r, c].GetValue().Value.RawValue ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
    }

    /// <summary>양식의 '지속 삽입 가능' 행 아래에서 B~F 가 모두 빈 첫 행.</summary>
    private static int AppendRow(Worksheet ws)
    {
        var used = ws.UsedCellRange;
        int start = -1;
        for (int r = ExtraSearchFrom; r <= used.ToIndex.RowIndex && start < 0; r++)
            for (int c = 1; c <= LabelCol; c++)
                if ((ws.Cells[r, c].GetValue().Value.RawValue ?? string.Empty).Replace(" ", string.Empty).Contains("지속삽입가능"))
                    start = r + 1;
        if (start < 0)
            throw new BulkTemplateException("수질 양식에서 '지속 삽입 가능' 행을 찾지 못했습니다(양식이 바뀌었는지 확인).");

        for (int r = start; ; r++)
        {
            bool empty = true;
            for (int c = 1; c <= RecordCol && empty; c++)
                empty = ws.Cells[r, c].GetValue().Value.ValueType == CellValueType.Empty;
            if (empty) return r;
        }
    }
}
