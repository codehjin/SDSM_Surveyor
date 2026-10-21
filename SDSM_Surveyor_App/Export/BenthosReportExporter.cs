using System.IO;
using Microsoft.Win32;
using SDSM_Core.Ecology;
using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Documents.Spreadsheet.Model;
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
    public static string? Export(BenthosEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "저서동물 조사결과(보고서용) 엑셀 내보내기",
            ReportExporterBase.ReportFileName(vm.Meta, "저서동물"),
            path => Write(vm, path));

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(BenthosEntryViewModel vm, string path) => ReportExporterBase.Save(Build(vm), path);

    /// <summary>통합문서만 만든다(저장하지 않음) — C-8 보고서 일괄이 여러 지점을 파일 하나로 합칠 때 쓴다.</summary>
    internal static Workbook Build(BenthosEntryViewModel vm)
    {
        var wb = new Workbook();
        WriteOverview(wb, vm);
        WriteSpecies(wb, vm);
        WriteAssessment(wb, vm);

        return wb;
    }

    // ── 시트1 : 조사개황 ──
    private static void WriteOverview(Workbook wb, BenthosEntryViewModel vm)
    {
        var m = vm.Meta;
        var s = ReportExporterBase.BeginOverview(wb, "저서동물 조사결과 — 조사개황");
        s.Head("[ 조사개황 ]");
        s.WriteSurveyMeta(m, vm.EffectiveSurveyDate);
        s.Blank();
        s.Head("[ 채집방법 ]");
        s.Kv("Surber 30×30 (회)", vm.Surbernet30);
        s.Kv("Surber 50×50 (회)", vm.Surbernet50);
        s.Kv("드렛지/D-Net (회)", vm.Dredge);
        s.Kv("에크만 (회)", vm.Ekman);
        // H1 : 채집 면적과 계산 근거(드렛지/D-Net 이 섞이면 '환산 불가', 횟수가 없으면 계산하지 않음)
        var sampling = vm.DensitySampling;
        s.Kv("채집 면적(㎡)", sampling.Area is double area ? BenthosDensity.Num(area) : null);
        s.Kv("개체밀도 계산", sampling.Text);
        s.Blank();
        s.Head("[ 서식처(하천 이용·식생) ]");
        s.Kv("유역이용", vm.Watershed);
        s.Kv("확인 가능 오염원", vm.PollutionSource);
        s.Kv("식생 수피도", vm.CanopyCover);
        s.Kv("범람원의 이용", vm.Floodplain);
        s.Kv("제방(좌안)", vm.LeveeLeft);
        s.Kv("제방(우안)", vm.LeveeRight);
        s.Blank();
        // §20(2026-10-21) : 지침(제2026-30호) [서식 2] · <표 1> 6칸 — 암반·콘크리트 없음
        s.Head("[ 서식지 하상구성(%) ]");
        s.Kv("진흙 이하(<0.063mm)", vm.Mud);
        s.Kv("모래(0.063~2mm)", vm.Sand);
        s.Kv("왕모래(2~16mm)", vm.FineGravel);
        s.Kv("자갈(16~64mm)", vm.Gravel);
        s.Kv("작은돌(64~256mm)", vm.SmallStone);
        s.Kv("큰돌 이상(>256mm)", vm.BigStone);
        s.Blank();
        s.Head("[ 서식처(수리·환경) ]");
        // 양식 33행. 조사개황의 '하천유형'(8행)과 이름이 같아 화면처럼 (서식처)를 붙인다
        s.Kv("하천유형(서식처)", vm.HabitatRiverType);
        s.Kv("하폭(m)", vm.RiverWidth);
        s.Kv("수폭(m)", vm.WaterWidth);
        s.Kv("평균수심(cm)", vm.AverageDepth);
        s.Kv("평균유속(cm/s)", vm.AverageVelocity);
        s.Kv("기온(℃)", vm.AirTemperature);
        s.Kv("수온(℃)", vm.WaterTemperature);
        s.Kv("흐름상태", vm.FlowState);
        s.Kv("투명도", vm.Transparency);
        s.Kv("냄새", vm.Smell);
        s.Blank();
        s.Head("[ 특이사항 ]");
        s.Kv("채집불가시", vm.SurveyUnavailableReason);
        s.Kv("비고", vm.Note);

        s.Finish();
    }

    // ── 시트2 : 출현종 (지점명 포함 · 필터 테이블) ──
    private static void WriteSpecies(Workbook wb, BenthosEntryViewModel vm)
    {
        var ws = wb.Worksheets.Add();
        ws.Name = "출현종";
        int row = 0;

        Title(ws.Cells[row, 0]); ws.Cells[row, 0].SetValue("저서동물 출현종 목록"); row += 2;

        int headerRow = row;
        // H1 : 개수 옆에 개체밀도(표시용 · 환산할 수 없으면 빈칸 — 까닭은 조사개황 '개체밀도 계산'·각주)
        WriteHeader(ws, row, SiteColumns.With("문", "강", "목", "과", "국명", "학명", "개체수", "개체밀도(inds./㎡)", "오탁치", "지표가중치", "보호종"));
        row++;

        var sampling = vm.DensitySampling;
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
            Num(ws, row, 11, sampling.Density(e.IndividualCount));
            Num(ws, row, 12, sp?.SaprobicValue);
            if (sp?.IndicatorWeight is int iw) Num(ws, row, 13, iw);
            if (e.IsProtected)
            {
                ws.Cells[row, 14].SetValue("보호종");
                ws.Cells[row, 14].SetForeColor(new ThemableColor(Accent));
                ws.Cells[row, 14].SetIsBold(true);
            }
            row++;
        }

        int lastRow = row - 1;
        SiteColumns.Widths(ws);
        Width(ws, 3, 110); Width(ws, 4, 95); Width(ws, 5, 95); Width(ws, 6, 110); Width(ws, 7, 120);
        Width(ws, 8, 150); Width(ws, 9, 220); Width(ws, 10, 75); Width(ws, 11, 120); Width(ws, 12, 70); Width(ws, 13, 85); Width(ws, 14, 70);
        TryAutoFilter(ws, headerRow, lastRow, 14);

        // 각주 — 자료 아래 빈 줄 다음(여러 지점 일괄 보고서는 첫 지점 것을 한 번만 붙인다)
        row++;
        foreach (var f in BenthosDensity.Footnotes) ws.Cells[row++, 0].SetValue(f);
        FontAll(ws, row - 1, 14);
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
            "총출현종수", "총개체수", "채집 면적(㎡)", "총 개체밀도(inds./㎡)", "우점종",
            "우점도 DI", "다양도 H'", "풍부도 R1", "균등도 J'",
            "Σ(s·g·h)", "Σ(g·h)", "BMI점수", "등급"));
        row++;

        SiteColumns.Write(ws, row, m);
        Str(ws, row, 4, m.YearChsu);
        Str(ws, row, 5, m.River);
        Str(ws, row, 6, vm.EffectiveSurveyDate?.ToString("yyyy-MM-dd"));
        Num(ws, row, 7, b.TotalSpecies);
        Num(ws, row, 8, b.TotalIndiv);
        // H1 : 총 개체밀도 = 총 개체수 ÷ 면적(반올림한 종별 값을 더하지 않는다) · 환산할 수 없으면 그 까닭을 적는다
        var sampling = vm.DensitySampling;
        Num(ws, row, 9, sampling.Area);
        // 종이 하나도 없으면 '미입력'과 구분할 수 없다 — 화면과 같이 출현종 없음(0종) 선언일 때만 0
        if (sampling.CanConvert) Num(ws, row, 10, b.TotalSpecies == 0 && !vm.NoSpeciesDeclared ? null : sampling.Density(b.TotalIndiv));
        else ws.Cells[row, 10].SetValue(sampling.Text);
        Str(ws, row, 11, vm.DominantSpecies);
        // N6(2026-10-20 연계 시험 2차) : 출현종(개체수 > 0)이 없으면(조사불가 · 0종) 화면처럼 DI·H′·R1·J′ 빈칸 — 관리자 DB 도 NULL
        double? Idx(double v) => b.TotalSpecies == 0 ? null : Math.Round(v, 3);
        Num(ws, row, 12, Idx(b.DI));
        Num(ws, row, 13, Idx(b.H));
        Num(ws, row, 14, Idx(b.R1));
        Num(ws, row, 15, Idx(b.J));
        Num(ws, row, 16, Math.Round(b.SumSGH, 3));
        Num(ws, row, 17, Math.Round(b.SumGH, 3));
        Num(ws, row, 18, b.Score);
        var g = b.Grade ?? "-";
        ws.Cells[row, 19].SetValue(g);
        GradeCell(ws.Cells[row, 19], b.Grade);

        int lastRow = row;
        SiteColumns.Widths(ws);
        Width(ws, 3, 110); Width(ws, 4, 100); Width(ws, 5, 100); Width(ws, 6, 95);
        Width(ws, 7, 90); Width(ws, 8, 80); Width(ws, 9, 95); Width(ws, 10, 150); Width(ws, 11, 130);
        Width(ws, 12, 80); Width(ws, 13, 80); Width(ws, 14, 80); Width(ws, 15, 80);
        Width(ws, 16, 95); Width(ws, 17, 90); Width(ws, 18, 80); Width(ws, 19, 60);
        TryAutoFilter(ws, headerRow, lastRow, 19);

        row += 2;
        ws.Cells[row, 0].SetValue("※ BMI = (4 − Σ(s·g·h)/Σ(g·h)) × 25.  s=오탁치, g=지표가중치, h=개체수 순위점수.");
        row++;
        ws.Cells[row, 0].SetValue("※ 등급 A≥80·B≥65·C≥50·D≥35·E<35. 채집불가(접근불가/건천화/준설/공사중) 시 등급 '-'.");
        foreach (var f in BenthosDensity.Footnotes) ws.Cells[++row, 0].SetValue(f);
        FontAll(ws, row, 19);
    }
}
