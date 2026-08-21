using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;   // XlsxFormatProvider
using Telerik.Windows.Documents.Spreadsheet.Model;                          // Workbook, Worksheet
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 내보내기 공통 골격 (05_REFACTORING §1-2).
/// 14개 exporter 가 똑같이 하던 것 — 저장 대화상자, 통합문서 저장, 조사개황 시트 —
/// 을 한 곳으로 모았다. 각 exporter 에는 **분류군 고유 시트 내용**만 남는다.
///
/// ⚠ exporter 들이 `static class` 라 상속을 쓸 수 없다. 그래서 "기반 클래스"가 아니라
///   **공통 호출 지점**으로 만들었다. 동작은 바뀌지 않는다.
/// </summary>
internal static class ReportExporterBase
{
    /// <summary>
    /// 저장 대화상자를 띄우고, 사용자가 고르면 <paramref name="write"/> 로 파일을 만든다.
    /// 취소하면 null.
    /// </summary>
    public static string? SaveWithDialog(string title, string fileName, Action<string> write)
    {
        var dlg = new SaveFileDialog
        {
            Title = title,
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = fileName
        };
        if (dlg.ShowDialog() != true) return null;

        write(dlg.FileName);
        return dlg.FileName;
    }

    /// <summary>
    /// 보고서용 파일명 — `대분류_연도차수_지점_분류군_조사결과.xlsx`.
    /// 세션 정보(대분류·연도차수·지점)를 파일명에 그대로 반영한다.
    /// </summary>
    public static string ReportFileName(SurveyMeta m, string taxon) =>
        $"{FileToken(m.Project)}{FileToken(m.YearChsu)}{FileToken(m.Site)}{taxon}_조사결과.xlsx";

    /// <summary>
    /// 일괄입력용 파일명 — `대분류_지점_분류군_DB_v1.xlsx`.
    /// ⚠ 관리자 Import 가 **파일명 키워드로 대분류를 판별**한다. 형식을 바꾸지 말 것.
    /// </summary>
    public static string BulkFileName(SurveyMeta m, string taxon)
    {
        var project = string.IsNullOrWhiteSpace(m.Project) ? "방류하천" : m.Project!;
        var site = string.IsNullOrWhiteSpace(m.Site) ? "" : $"_{m.Site}";
        return $"{project}{site}_{taxon}_DB_v1.xlsx";
    }

    /// <summary>통합문서를 xlsx 로 저장한다. 3번째 인자 null(타임아웃 무제한)은 Telerik 필수 인자다.</summary>
    public static void Save(Workbook wb, string path)
    {
        using var stream = new FileStream(path, FileMode.Create);
        new XlsxFormatProvider().Export(wb, stream, null);
    }

    /// <summary>조사개황 시트를 만들고 제목 줄까지 찍는다.</summary>
    public static OverviewSheet BeginOverview(Workbook wb, string title) => new(wb, title);
}

/// <summary>
/// 조사개황 시트 작성기. `항목 | 값` 2열 표를 순서대로 쌓는다.
/// 4개 분류군 exporter 가 저마다 같은 지역 함수(<c>Kv</c>·<c>Head</c>)를 두고 있던 것을 대체한다.
/// </summary>
internal sealed class OverviewSheet
{
    private int _row;

    public OverviewSheet(Workbook wb, string title)
    {
        Ws = wb.Worksheets.Add();
        Ws.Name = "조사개황";

        Title(Ws.Cells[_row, 0]);
        Ws.Cells[_row, 0].SetValue(title);
        _row += 2;
    }

    public Worksheet Ws { get; }

    /// <summary>구획 소제목(`[ 채집방법 ]` 등). 아래로 한 줄 띄운다.</summary>
    public void Head(string text)
    {
        Section(Ws.Cells[_row, 0]);
        Ws.Cells[_row, 0].SetValue(text);
        _row += 2;
    }

    /// <summary>항목|값 한 줄. 값이 비면 값 칸을 비워 둔다(0과 미입력을 구분).</summary>
    public void Kv(string key, string? value)
    {
        KeyCell(Ws.Cells[_row, 0]);
        Ws.Cells[_row, 0].SetValue(key);
        if (!string.IsNullOrWhiteSpace(value)) Ws.Cells[_row, 1].SetValue(value);
        _row++;
    }

    /// <summary>구획 사이 빈 줄.</summary>
    public void Blank() => _row++;

    /// <summary>본문 아래에 남기는 안내 문구.</summary>
    public void Note(string text)
    {
        Ws.Cells[_row, 0].SetValue(text);
        _row++;
    }

    /// <summary>
    /// 모든 분류군이 똑같이 쓰는 조사개황 15행.
    /// ⚠ 순서·문구를 바꾸면 7개 보고서 엑셀이 한꺼번에 달라진다. 기준 파일 대조로 확인할 것.
    /// </summary>
    public void WriteSurveyMeta(SurveyMeta m)
    {
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
    }

    /// <summary>열 너비·글꼴을 적용하고 마무리한다.</summary>
    public void Finish()
    {
        Width(Ws, 0, 200);
        Width(Ws, 1, 240);
        FontAll(Ws, _row, 1);
    }
}
