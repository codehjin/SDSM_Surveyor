using SDSM_Surveyor_App.Models;
using Telerik.Windows.Documents.Spreadsheet.Model;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 관찰형 3종(조류·포유류·양서파충류) 보고서 엑셀의 공통 조각.
/// 이 분류군들은 고유 조사환경 입력이 없어 조사개황 시트가 완전히 동일하다.
/// </summary>
internal static class ObservationReportCommon
{
    /// <summary>시트1 : 조사개황(항목 | 값). 관리자 테이블에 없는 열은 안내 문구로 남긴다.</summary>
    public static void WriteOverview(Workbook wb, SurveyMeta m, string title)
    {
        var s = ReportExporterBase.BeginOverview(wb, title);
        s.Head("[ 조사개황 ]");

        s.WriteSurveyMeta(m);

        s.Blank();
        s.Note("※ 관리자 테이블에 대권역·중권역·하천유형·조사기관 컬럼이 없어 일괄입력 시에는 해당 열이 비워진다.");

        s.Finish();
    }

    /// <summary>보호종·교란종 표기(그리드 '구분' 열과 동일 문구).</summary>
    public static string Tag(SDSM_Models.ObservedSpecies? sp) => SpeciesTagBuilder.Build(sp);
}
