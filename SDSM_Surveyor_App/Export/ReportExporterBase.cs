using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using SDSM_Surveyor_App.Models;
using Telerik.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;   // XlsxFormatProvider
using Telerik.Documents.Spreadsheet.Model;                          // Workbook, Worksheet
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
    public static string? SaveWithDialog(string title, string fileName, Action<string> write) =>
        SaveWithDialog(title, fileName, path => { write(path); return null; });

    /// <summary>
    /// 위와 같고, <paramref name="write"/> 가 돌려준 한 줄을 저장 완료 안내에 덧붙인다
    /// (예: `양식 종목록에 없어 끝에 추가한 종: …` · B-7).
    /// </summary>
    public static string? SaveWithDialog(string title, string fileName, Func<string, string?> write)
    {
        var dlg = new SaveFileDialog
        {
            Title = title,
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = fileName
        };
        if (dlg.ShowDialog() != true) return null;

        // 일괄입력용은 양식(저서 내장본 약 1MB)을 열고 다시 저장하느라 몇 초 걸린다(저서 4~5초 · B-7 실측).
        // 그동안 창이 멈춘 것처럼 보이지 않게 대기 커서를 띄운다.
        string? note;
        var previousCursor = System.Windows.Input.Mouse.OverrideCursor;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            note = write(dlg.FileName);
        }
        catch (BulkTemplateException ex)
        {
            // 양식이 없거나(설치 손상) 양식과 좌표가 어긋났다 — 빈 통합문서로 대신하지 않고 멈춘다.
            System.Windows.Input.Mouse.OverrideCursor = previousCursor;
            System.Windows.MessageBox.Show(
                "일괄입력용 양식을 쓸 수 없어 저장하지 않았습니다. 프로그램을 다시 설치하거나 담당자에게 알려 주세요."
                + $"\n\n{ex.Message}",
                "저장 중단", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return null;
        }
        catch (IOException ex)
        {
            // 같은 이름 파일이 엑셀에 열려 있으면 FileStream 이 IOException 을 던진다.
            // 잡지 않으면 입력 중이던 야장째로 앱이 종료된다.
            System.Windows.Input.Mouse.OverrideCursor = previousCursor;
            // 파일 잠김뿐 아니라 경로 없음(USB 분리 등)도 IOException 파생이다 — 원인을 함께 보여 준다.
            System.Windows.MessageBox.Show(
                "저장하지 못했습니다. 같은 이름 파일이 엑셀에 열려 있는지, 저장 위치가 살아 있는지 확인해 주세요."
                + $"\n\n{ex.Message}",
                "저장 실패", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return null;
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = previousCursor;
        }

        OfferToOpen(dlg.FileName, note);
        return dlg.FileName;
    }

    /// <summary>
    /// 셸 실행(파일·탐색기 열기). 자동 검증이 실제 엑셀·탐색기를 띄우지 않게 바꿔 끼울 수 있다 — 평소에는 그대로 실행한다.
    /// </summary>
    internal static Action<ProcessStartInfo> Shell { get; set; } = psi => Process.Start(psi);

    private static System.Windows.Window? ActiveWindow =>
        System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
        ?? System.Windows.Application.Current?.MainWindow;

    /// <summary>
    /// 저장한 뒤 무엇을 열지 묻는다(C-1). 기본 <b>[파일 열기]</b>, 보조 [폴더 열기], [닫기].
    /// 버튼 글자가 곧 동작이다 — MessageBox 의 예/아니요에 다른 뜻을 얹지 않는다(<see cref="Views.Windows.ChoiceDialog"/>).
    /// 파일 열기에 실패하면(엑셀 미설치 등) 폴더 열기를 권한다.
    /// </summary>
    internal static void OfferToOpen(string path, string? note = null)
    {
        // 제목이 이미 '저장 완료'다. 본문에서 또 "저장했습니다" 라고 하면
        // 화면 아래 알림 띠(NotifyMessage)까지 세 번 같은 말을 하게 된다.
        var pick = Views.Windows.ChoiceDialog.Ask(ActiveWindow, "저장 완료",
            note is null ? path : $"{path}\n\n{note}",
            new Views.Windows.ChoiceDialog.Option("닫기", IsCancel: true),
            new Views.Windows.ChoiceDialog.Option("폴더 열기", ToolTip: "탐색기에서 저장한 파일을 선택한 채로 엽니다"),
            new Views.Windows.ChoiceDialog.Option("파일 열기", IsPrimary: true, ToolTip: "저장한 엑셀 파일을 엽니다"));

        if (pick == 1) OpenFolder(path);
        else if (pick == 2) OpenFile(path);
    }

    /// <summary>파일을 기본 프로그램(엑셀)으로 연다. 못 열면 폴더 열기를 권한다.</summary>
    private static void OpenFile(string path)
    {
        try
        {
            Shell(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            var pick = Views.Windows.ChoiceDialog.Ask(ActiveWindow, "파일을 열지 못했습니다",
                $"{path}\n\n이 파일을 여는 프로그램을 찾지 못했습니다(엑셀이 설치되어 있는지 확인하세요).\n{ex.Message}\n\n저장은 끝났습니다. 저장한 폴더를 열어 파일을 확인할 수 있습니다.",
                new Views.Windows.ChoiceDialog.Option("닫기", IsCancel: true),
                new Views.Windows.ChoiceDialog.Option("폴더 열기", IsPrimary: true));
            if (pick == 1) OpenFolder(path);
        }
    }

    /// <summary>탐색기를 <c>/select</c> 로 열면 저장한 파일이 선택된 채로 뜬다.</summary>
    internal static void OpenFolder(string path)
    {
        try
        {
            // 따옴표 안에 경로를 넣어야 공백·한글 폴더에서도 열린다.
            Shell(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            // 탐색기를 못 열어도 저장은 이미 끝났다 — 조사를 막지 않는다.
        }
    }

    /// <summary>
    /// 보고서용 파일명 — `대분류_연도차수_지점_분류군_조사결과.xlsx`.
    /// 세션 정보(대분류·연도차수·지점)를 파일명에 그대로 반영한다.
    /// </summary>
    public static string ReportFileName(SurveyMeta m, string taxon) =>
        $"{FileToken(m.Project)}{FileToken(m.YearChsu)}{FileToken(m.SiteKey)}{taxon}_조사결과.xlsx";

    /// <summary>
    /// 일괄입력용 파일명 — `대분류_연도차수_지점_분류군_DB_v1.xlsx`(예: `방류하천_2025분기1차_곡교천1_어류_DB_v1.xlsx`).
    /// 2026-10-20 결정(문제 14) : 연도차수를 넣는다 — 없을 때는 같은 지점의 다른 차수 파일 이름이 같아 관리자가 '저장된 기록이 있는 파일명' 거짓 경고를 냈다.
    /// ⚠ 관리자 Import 가 **파일명 키워드로 대분류(방류하천·생태현황)·분류군을 판별**한다 — 대분류로 시작하고 분류군 이름을 담는 형식은 지킨다.
    /// </summary>
    public static string BulkFileName(SurveyMeta m, string taxon) => BulkFileName(m.Project, m.YearChsu, m.SiteKey, taxon);   // 지점 자리 = SiteKey(D4 ②)

    /// <summary>위와 같은 이름 — 세션 목록 항목으로 미리 계산할 때(C-8 일괄 출력).</summary>
    public static string BulkFileName(string? project, string? yearChsu, string? site, string taxon)
    {
        var p = string.IsNullOrWhiteSpace(project) ? "방류하천" : FileToken(project).TrimEnd('_');
        return $"{p}_{FileToken(yearChsu)}{FileToken(site)}{taxon}_DB_v1.xlsx";
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
    /// 모든 분류군이 똑같이 쓰는 조사개황 15행(관찰형 3종은 하천유형을 뺀 14행 · G2).
    /// ⚠ 순서·문구를 바꾸면 7개 보고서 엑셀이 한꺼번에 달라진다. 기준 파일 대조로 확인할 것.
    /// </summary>
    public void WriteSurveyMeta(SurveyMeta m, DateTime? surveyDate) => WriteSurveyMeta(m, surveyDate, m.RiverType);

    /// <summary>하천유형 칸에 쓸 값을 정해서(E1 — 수질·서식수변 보고서는 수질·서식수변 칸 값).
    /// <paramref name="withRiverType"/> 이 false 면 하천유형 행을 쓰지 않는다(G2 — 관찰형 3종 양식에는 하천유형이 없다).
    /// <paramref name="surveyDate"/> = 그 분류군 조사일(없으면 조사개황 조사일 · N3).</summary>
    public void WriteSurveyMeta(SurveyMeta m, DateTime? surveyDate, string? riverType, bool withRiverType = true)
    {
        Kv("대분류", m.Project);
        Kv("연도", m.SurveyYear);
        Kv("연도차수", m.YearChsu);
        Kv("조사일자", surveyDate?.ToString("yyyy-MM-dd"));
        Kv("대권역명", m.MajorRegion);
        Kv("중권역명", m.MiddleRegion);
        Kv("하천명", m.River);
        if (withRiverType) Kv("하천유형", riverType);
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
