using System.IO;
using Microsoft.Win32;
using SDSM_Core.Ecology;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;
using ThemableColor = Telerik.Documents.Common.Model.ThemableColor;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 저서동물 → 보고서·기록용 엑셀. 시트:
///  [조사개황] 항목|값 · [출현종] 지점명 포함 필터 테이블 · [건강성평가(BMI)] 지점명 포함 1행 관리 테이블.
/// 계산은 <see cref="BenthosCalculator"/>가 하고 여기서는 값만 옮긴다(계산식 재구현 금지).
/// </summary>
public static class BenthosReportExporter
{
    public static string? Export(BenthosEntryViewModel vm)
    {
        var m = vm.Meta;
        var project = FileToken(m.Project);
        var chasu = FileToken(m.YearChsu);      // 세션 정보(대분류·연도차수·지점)를 파일명에 반영
        var site = FileToken(m.Site);

        var dlg = new SaveFileDialog
        {
            Title = "저서동물 조사결과(보고서용) 엑셀 내보내기",
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = $"{project}{chasu}{site}저서동물_조사결과.xlsx"
        };
        if (dlg.ShowDialog() != true) return null;

        Write(vm, dlg.FileName);
        return dlg.FileName;
    }

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(BenthosEntryViewModel vm, string path)
    {
        var wb = new Workbook();
        WriteOverview(wb, vm);
        WriteSpecies(wb, vm);
        WriteAssessment(wb, vm);

        using (var stream = new FileStream(path, FileMode.Create))
            new XlsxFormatProvider().Export(wb, stream, null);

    }

    // ── 시트1 : 조사개황 ──
    private static void WriteOverview(Workbook wb, BenthosEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "조사개황";
        var m = vm.Meta;
        int row = 0;

        void Kv(string key, string? val)
        {
            KeyCell(ws.Cells[row, 0]);
            ws.Cells[row, 0].SetValue(key);
            Str(ws, row, 1, val);
            row++;
        }
        void Head(string t) { Section(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue(t); row += 2; }

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("저서동물 조사결과 — 조사개황"); row += 2;
        Head("[ 조사개황 ]");
        Kv("대분류", m.Project);
        Kv("연도", m.SurveyYear);
        Kv("연도차수", m.YearChsu);
        Kv("조사일자", m.SurveyDate?.ToString("yyyy-MM-dd"));
        Kv("대권역명", m.MajorRegion);
        Kv("중권역명", m.MiddleRegion);
        Kv("하천명", m.River);
        Kv("하천유형", m.RiverType);
        Kv("사업장", m.Workplace);
        Kv("지점명", m.Site);
        Kv("위도", m.Lat);
        Kv("경도", m.Lng);
        Kv("날씨", m.Weather);
        Kv("조사기관", m.SurveyAgency);
        Kv("조사자", m.Surveyor);
        row++;
        Head("[ 채집방법 ]");
        Kv("Surber net 30×30", vm.Surbernet30);
        Kv("Surber net 50×50", vm.Surbernet50);
        Kv("드렛지", vm.Dredge);
        Kv("에크만", vm.Ekman);
        row++;
        Head("[ 서식처(하천 이용·식생) ]");
        Kv("유역이용", vm.Watershed);
        Kv("확인가능 오염원", vm.PollutionSource);
        Kv("식생 수피도", vm.CanopyCover);
        Kv("범람원의 이용", vm.Floodplain);
        Kv("제방(좌안)", vm.LeveeLeft);
        Kv("제방(우안)", vm.LeveeRight);
        row++;
        Head("[ 서식지 하상구성(%) ]");
        Kv("암반", vm.Bedrock);
        Kv("콘크리트", vm.Concrete);
        Kv("진흙이하(<0.063mm)", vm.Mud);
        Kv("모래(0.063-2mm)", vm.Sand);
        Kv("잔자갈(2-16mm)", vm.FineGravel);
        Kv("자갈(16-64mm)", vm.Gravel);
        Kv("작은돌(64-256mm)", vm.SmallStone);
        Kv("큰돌(>256mm)", vm.BigStone);
        row++;
        Head("[ 서식처(수리·환경) ]");
        Kv("하천형태", vm.HabitatRiverType);
        Kv("하폭(m)", vm.RiverWidth);
        Kv("수폭(m)", vm.WaterWidth);
        Kv("평균수심(cm)", vm.AverageDepth);
        Kv("평균유속(cm/s)", vm.AverageVelocity);
        Kv("기온(℃)", vm.AirTemperature);
        Kv("수온(℃)", vm.WaterTemperature);
        Kv("흐름상태", vm.FlowState);
        Kv("투명도", vm.Transparency);
        Kv("냄새", vm.Smell);
        row++;
        Head("[ 특이사항 ]");
        Kv("채집불가시", vm.SurveyUnavailableReason);
        Kv("비고", vm.Note);

        Width(ws, 0, 200); Width(ws, 1, 240);
        FontAll(ws, row, 1);
    }

    // ── 시트2 : 출현종 (지점명 포함 · 필터 테이블) ──
    private static void WriteSpecies(Workbook wb, BenthosEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "출현종";
        int row = 0;

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("저서동물 출현종 목록"); row += 2;

        int headerRow = row;
        WriteHeader(ws, row, SiteColumns.With("문", "강", "목", "과", "국명", "학명", "개체수", "오탁치", "지표가중치", "보호종"));
        row++;

        foreach (var e in vm.SpeciesEntries)
        {
            var ko = e.SelectedSpecies?.SpeciesKo ?? e.SpeciesKo;
            if (string.IsNullOrWhiteSpace(ko) || (e.IndividualCount ?? 0) <= 0) continue;   // 관측된 종만

            var sp = e.SelectedSpecies;
            SiteColumns.Write(ws, row, vm.Meta);
            Str(ws, row, 4, sp?.PhylumKo);
            Str(ws, row, 5, sp?.ClassKo);
            Str(ws, row, 6, sp?.OrderKo);
            Str(ws, row, 7, sp?.FamilyKo);
            ws.Cells[row, 8].SetValue(ko);
            Str(ws, row, 9, sp?.SpeciesEn);
            Num(ws, row, 10, e.IndividualCount!.Value);
            Num(ws, row, 11, sp?.SaprobicValue);
            if (sp?.IndicatorWeight is int iw) Num(ws, row, 12, iw);
            if (e.IsProtected)
            {
                ws.Cells[row, 13].SetValue("보호종");
                ws.Cells[row, 13].SetForeColor(new ThemableColor(Accent));
                ws.Cells[row, 13].SetIsBold(true);
            }
            row++;
        }

        int lastRow = row - 1;
        SiteColumns.Widths(ws);
        Width(ws, 3, 110); Width(ws, 4, 95); Width(ws, 5, 95); Width(ws, 6, 110); Width(ws, 7, 120);
        Width(ws, 8, 150); Width(ws, 9, 220); Width(ws, 10, 75); Width(ws, 11, 70); Width(ws, 12, 85); Width(ws, 13, 70);
        TryAutoFilter(ws, headerRow, lastRow, 13);
        FontAll(ws, lastRow, 13);
    }

    // ── 시트3 : 건강성평가(BMI) — 계산과정 → 결과 → 등급 ──
    private static void WriteAssessment(Workbook wb, BenthosEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "건강성평가(BMI)";
        var m = vm.Meta;

        // 화면과 동일한 순서로 RankScorer를 채운 뒤 계산(화면 값과 일치시키기 위함)
        var entries = vm.SpeciesEntries.ToList();
        var counts = entries.Select(e => (int)(e.IndividualCount ?? 0)).ToArray();
        foreach (var e in entries)
            e.RankScorer = BenthosCalculator.GetRankScorer(counts, (int)(e.IndividualCount ?? 0));

        var imports = entries.Select(e => e.ToImport()).ToList();
        var b = BenthosCalculator.CalculateBmiDetail(imports, vm.SurveyUnavailableReason, vm.NoSpeciesDeclared);

        int row = 0;
        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("저서동물지수(BMI) 건강성평가"); row += 2;

        int headerRow = row;
        WriteHeader(ws, row, SiteColumns.With(
            "연도차수", "하천명", "조사일",
            "총출현종수", "총개체수", "우점종",
            "우점도 DI", "다양도 H'", "풍부도 R1", "균등도 J'",
            "Σ(s·g·h)", "Σ(g·h)", "BMI점수", "등급"));
        row++;

        SiteColumns.Write(ws, row, m);
        Str(ws, row, 4, m.YearChsu);
        Str(ws, row, 5, m.River);
        Str(ws, row, 6, m.SurveyDate?.ToString("yyyy-MM-dd"));
        Num(ws, row, 7, b.TotalSpecies);
        Num(ws, row, 8, b.TotalIndiv);
        Str(ws, row, 9, vm.DominantSpecies);
        Num(ws, row, 10, Math.Round(b.DI, 3));
        Num(ws, row, 11, Math.Round(b.H, 3));
        Num(ws, row, 12, Math.Round(b.R1, 3));
        Num(ws, row, 13, Math.Round(b.J, 3));
        Num(ws, row, 14, Math.Round(b.SumSGH, 3));
        Num(ws, row, 15, Math.Round(b.SumGH, 3));
        Num(ws, row, 16, b.Score);
        var g = b.Grade ?? "-";
        ws.Cells[row, 17].SetValue(g);
        GradeCell(ws.Cells[row, 17], b.Grade);

        int lastRow = row;
        SiteColumns.Widths(ws);
        Width(ws, 3, 110); Width(ws, 4, 100); Width(ws, 5, 100); Width(ws, 6, 95);
        Width(ws, 7, 90); Width(ws, 8, 80); Width(ws, 9, 130);
        Width(ws, 10, 80); Width(ws, 11, 80); Width(ws, 12, 80); Width(ws, 13, 80);
        Width(ws, 14, 95); Width(ws, 15, 90); Width(ws, 16, 80); Width(ws, 17, 60);
        TryAutoFilter(ws, headerRow, lastRow, 17);

        row += 2;
        ws.Cells[row, 0].SetValue("※ BMI = (4 − Σ(s·g·h)/Σ(g·h)) × 25.  s=오탁치, g=지표가중치, h=개체수 순위점수.");
        row++;
        ws.Cells[row, 0].SetValue("※ 등급 A≥80·B≥65·C≥50·D≥35·E<35. 채집불가(접근불가/건천화/준설/공사중) 시 등급 '-'.");
        FontAll(ws, row, 17);
    }
}
