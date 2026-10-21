using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 양서파충류 → 관리자 '양서파충류' 시트 일괄입력 엑셀 내보내기.
/// 일반 레이아웃: 1행 = 1출현기록. 데이터는 **0-based 행4**(엑셀 5행)부터, 앵커는 **B열=연도차수**.
/// 매핑 근거: 관리자 BulkAmphibianReptileControlViewModel (FirstRowNumber=5, FirstColumnNumber=2).
/// ⚠ 어류·포유류와 달리 **흔적 6종이 앞(9~14)**, 좌표가 뒤(15~22)에 온다.
///
/// B-7 : 빈 통합문서 대신 **내장 양식**(`templates\bulk\amphrep.xlsx`)을 열어 쓴다. 양식 머리글·예시·필독·목록 시트가 함께 나간다.
/// 최종본 자료 행의 학명(H)·특징(I) VLOOKUP 수식은 TemplateGen 이 지웠다(10-16 (2) 결정) — 앱이 그 칸에 값을 쓴다.
/// </summary>
public static class AmphibianReptileExcelExporter
{
    private const string TemplateFile = "amphrep.xlsx";
    private const string SheetName = "양서파충류";
    private const int FirstRow = 4;      // 0-based
    private const int TraceFirstCol = 9;  // Trace1~6 = 9~14

    public static string? Export(AmphibianReptileEntryViewModel vm) =>
        ReportExporterBase.SaveWithDialog(
            "양서파충류 일괄입력 엑셀 내보내기",
            ReportExporterBase.BulkFileName(vm.Meta, "양서파충류"),
            path => Write(vm, path));

    /// <summary>대화상자 없이 지정 경로로 저장한다(자동 검증·일괄 생성용).</summary>
    public static void Write(AmphibianReptileEntryViewModel vm, string path)
    {
        var m = vm.Meta;
        var wb = BulkTemplate.Open(TemplateFile);
        var ws = BulkTemplate.Sheet(wb, SheetName);

        int r = FirstRow;
        foreach (var e in vm.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.SpeciesKo)) continue;   // 국명이 있는 행은 모두(D3 · 빈칸은 빈칸 · 0 은 0)

            void Set(int col, string? val) => BulkTemplate.Put(ws, r, col, val);

            Set(1, m.YearChsu);                            // 앵커
            // ⚠ 양식의 조사일(C) 칸은 날짜 서식(mm-dd-yy)이다. 숫자 20250923 을 넣으면 날짜로 읽혀 깨진다 →
            //   종전과 같은 글자 `yyyyMMdd` 로 쓴다(관리자 Import 는 8자리 숫자 글자를 날짜로 바꾼다).
            if (vm.EffectiveSurveyDate is DateTime d) BulkTemplate.PutText(ws, r, 2, d.ToString("yyyyMMdd"));
            Set(3, m.Weather);
            Set(4, m.River);
            Set(5, m.Site);
            Set(6, e.SpeciesKo);
            Set(7, e.SpeciesEn);
            Set(8, e.Feature);

            var traces = new[] { e.Trace1, e.Trace2, e.Trace3, e.Trace4, e.Trace5, e.Trace6 };
            for (int i = 0; i < traces.Length; i++) BulkTemplate.Put(ws, r, TraceFirstCol + i, (double?)traces[i]);  // 미입력은 빈 칸

            var (lat, lng) = ObservationReportCommon.RowOrSite(e.Lat, e.Lng, m);   // 행 좌표가 비면 지점 좌표(문제 6)
            BulkTemplate.Put(ws, r, 21, lat);
            BulkTemplate.Put(ws, r, 22, lng);
            Set(24, e.MajorCategory);
            Set(25, e.MiddleCategory);
            Set(26, m.Surveyor);
            Set(27, e.Note);
            r++;
        }

        ReportExporterBase.Save(wb, path);
    }
}
