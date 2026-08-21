using SDSM_Surveyor_App.Models;
using Telerik.Windows.Documents.Spreadsheet.Model;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 보고서 엑셀 표 앞머리의 구분 컬럼 — 관리자 <c>SiteDivision</c> 4단계와 같은 순서다
/// (프로젝트명 · 사업장 · 과업대표하천 · 지점명). 04_FEATURE_SITE_SESSION §A-2-5.
/// 기존에는 지점명 한 칸만 있었으므로 표 본문 열은 <see cref="Extra"/> 만큼 오른쪽으로 밀린다.
/// </summary>
internal static class SiteColumns
{
    /// <summary>구분 컬럼 헤더(4개).</summary>
    public static readonly string[] Headers = { "프로젝트명", "사업장", "과업대표하천", "지점명" };

    /// <summary>지점명 한 칸 대비 늘어난 칸 수. 기존 표 본문 열 인덱스에 이만큼 더한다.</summary>
    public const int Extra = 3;

    /// <summary>구분 컬럼 + 나머지 헤더를 이어붙인다.</summary>
    public static string[] With(params string[] rest) => Headers.Concat(rest).ToArray();

    /// <summary>한 행의 구분 컬럼 값(0~3열)을 쓴다.</summary>
    public static void Write(Worksheet ws, int row, SurveyMeta m)
    {
        Str(ws, row, 0, m.Project);
        Str(ws, row, 1, m.Workplace);
        Str(ws, row, 2, m.River);
        Str(ws, row, 3, m.Site);
    }

    /// <summary>구분 컬럼 4개의 열 너비.</summary>
    public static void Widths(Worksheet ws)
    {
        Width(ws, 0, 90); Width(ws, 1, 70); Width(ws, 2, 100); Width(ws, 3, 110);
    }
}
