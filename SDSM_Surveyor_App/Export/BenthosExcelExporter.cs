using SDSM_Surveyor_App.ViewModels;
using Telerik.Documents.Spreadsheet.Model;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 저서동물 → 관리자 '입력' 시트 일괄입력 엑셀 내보내기.
/// 전치 레이아웃: 필드=행, 조사레코드=열 M(0-based 12). 종=행47(0-based 46)부터.
/// ※ 어류와 달리 종 좌측열은 학명=J(9)/국명=K(10) 순, 개체수는 실수(double).
/// 매핑 근거: 관리자 BulkBenthosControlViewModel. 자동계산(DI/H'/R1/J'/BMI)은 내보내지 않음.
///
/// B-7 : 빈 통합문서 대신 **내장 양식**(`templates\bulk\benthos.xlsx` · 2026-03 최종본에서 실자료만 지운 것)을 열어
/// 같은 좌표에 쓴다. 라벨 열(I·J·K)·예시 열(L)·필독 시트·하상 합계 수식(32행)·종목록이 함께 나간다.
/// 종은 양식 종목록에서 국명(K)이 같은 행을 찾아 개체수만 쓰고, 양식에 없는 종은 마지막 종 행 다음에 덧붙인다.
/// </summary>
public static class BenthosExcelExporter
{
    private const string TemplateFile = "benthos.xlsx";
    private const string SheetName = "입력";
    private const int RecordCol = 12;       // M열(0-based)
    private const int SpeciesFirstRow = 46; // 행47(0-based)
    private const int NameCol = 10;         // 국명 K

    public static string? Export(BenthosEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "저서동물 일괄입력 엑셀 내보내기",
            ReportExporterBase.BulkFileName(vm.Meta, "저서동물"),
            path =>
            {
                var appended = Write(vm, path);
                return appended.Count == 0 ? null : $"양식 종목록에 없어 끝에 추가한 종: {string.Join(", ", appended)}";
            });

    /// <summary>
    /// 대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).
    /// 반환값은 양식 종목록에 없어 끝에 덧붙인 종(국명).
    /// </summary>
    public static IReadOnlyList<string> Write(BenthosEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = BulkTemplate.Open(TemplateFile);
        var ws = BulkTemplate.Sheet(wb, SheetName);

        void Set(int row, string? val) => BulkTemplate.Put(ws, row, RecordCol, val);

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

        // 양식 종목록 : 국명(K) → 첫 행. 같은 국명이 두 번 있으면 첫 행에 쓴다(작업로그 B-7).
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        int lastSpeciesRow = SpeciesFirstRow - 1;
        var used = ws.UsedCellRange;
        for (int r = SpeciesFirstRow; r <= used.ToIndex.RowIndex; r++)
        {
            var name = ws.Cells[r, NameCol].GetValue().Value.RawValue?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            rows.TryAdd(name, r);
            lastSpeciesRow = r;
        }

        // 종 : 관측된 종(개체수>0)만. 개체수는 레코드 열(M, double).
        var appended = new List<string>();
        var written = new Dictionary<int, double>();
        int next = lastSpeciesRow + 1;
        foreach (var e in vm.SpeciesEntries)
        {
            var ko = (e.SelectedSpecies?.SpeciesKo ?? e.SpeciesKo)?.Trim();
            if (string.IsNullOrWhiteSpace(ko) || (e.IndividualCount ?? 0) <= 0) continue;

            if (rows.TryGetValue(ko, out var row))
            {
                // 불러온 세션에 같은 종이 두 행 남아 있으면(B-1 경고 대상) 한 행에 더한다 — 덮어써 잃지 않는다.
                written[row] = written.GetValueOrDefault(row) + e.IndividualCount!.Value;
                BulkTemplate.Put(ws, row, RecordCol, written[row]);
                continue;
            }

            // 양식에 없는 종 — 마지막 종 행 다음에 좌측 A~K(분류·형질·국명)와 개체수를 덧붙인다.
            int r = next++;
            var sp = e.SelectedSpecies;
            void SetSp(int col, string? val) { if (!string.IsNullOrWhiteSpace(val)) ws.Cells[r, col].SetValueAsText(val); }

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
            ws.Cells[r, NameCol].SetValueAsText(ko);   // 국명 K
            written[r] = e.IndividualCount!.Value;
            BulkTemplate.Put(ws, r, RecordCol, written[r]);  // 개체수 M (double)
            rows[ko] = r;   // 같은 종이 다시 나오면 이 행에 더한다
            appended.Add(ko);
        }

        ReportExporterBase.Save(wb, path);
        return appended;
    }
}
