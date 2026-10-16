using System.Globalization;
using System.IO;
using System.Text.Json;
using Telerik.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Documents.Spreadsheet.Model;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 일괄입력용 엑셀의 **내장 양식**(B-7 · `templates\bulk\`). 2026-03 최종본에서 실자료만 지운 파일이다(`tools\TemplateGen`).
///
/// exporter 는 빈 통합문서 대신 이 양식을 열어 **지금과 같은 셀 좌표**(관리자 Import 와 대조 검증된 좌표 · 90_TECH_NOTES §4)에만 값을 쓴다.
/// 그래서 사람이 열었을 때 라벨·예시·필독·합계 수식이 함께 보인다.
///
/// ⚠ 양식을 못 찾으면 빈 통합문서로 몰래 대신하지 않는다 — <see cref="BulkTemplateException"/> 으로 저장을 멈춘다.
/// ⚠ 쓰려는 칸이 양식에서 **수식**이면 쓰지 않고 멈춘다(양식의 계산을 덮어쓰지 않는다).
/// </summary>
internal static class BulkTemplate
{
    /// <summary>실행 폴더의 `templates\bulk\`.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "templates", "bulk");

    /// <summary>내장본을 연다. 없으면 사유를 담아 멈춘다.</summary>
    public static Workbook Open(string fileName)
    {
        var path = Path.Combine(Folder, fileName);
        if (!File.Exists(path))
            throw new BulkTemplateException($"일괄입력용 양식 파일이 없습니다: {path}");
        try
        {
            using var fs = File.OpenRead(path);
            return new XlsxFormatProvider().Import(fs, null);
        }
        catch (Exception ex) when (ex is not BulkTemplateException)
        {
            throw new BulkTemplateException($"일괄입력용 양식 파일을 열지 못했습니다: {path} — {ex.Message}", ex);
        }
    }

    /// <summary>양식의 입력 시트(관리자 Import 가 이름으로 찾는 시트).</summary>
    public static Worksheet Sheet(Workbook wb, string name) =>
        wb.Worksheets.FirstOrDefault(w => w.Name == name)
        ?? throw new BulkTemplateException($"일괄입력용 양식에 '{name}' 시트가 없습니다.");

    public static bool IsFormula(Worksheet ws, int row, int col) =>
        ws.Cells[row, col].GetValue().Value.ValueType == CellValueType.Formula;

    private static void Guard(Worksheet ws, int row, int col)
    {
        if (IsFormula(ws, row, col))
            throw new BulkTemplateException(
                $"양식 '{ws.Name}' 시트 {Address(row, col)} 칸은 수식이라 값을 쓰지 않았습니다(양식과 내보내기 좌표가 어긋남).");
    }

    /// <summary>
    /// 값을 쓴다. 숫자로 읽히는 값은 **숫자**로, 나머지는 **글자 그대로** 쓴다(B-7 — 문자열 "3" 으로 쓰지 않는다).
    /// `흐름,소`·`1,3` 처럼 쉼표가 든 다중값은 숫자로 바꾸지 않는다(관리자가 `,` 로 나눈다 · §3-5).
    /// 비어 있으면 쓰지 않는다.
    /// </summary>
    public static void Put(Worksheet ws, int row, int col, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        Guard(ws, row, col);
        var v = value.Trim();
        if (!v.Contains(',') && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            ws.Cells[row, col].SetValue(d);
        else
            ws.Cells[row, col].SetValueAsText(value);
    }

    /// <summary>글자 그대로 쓴다(숫자로 바꾸지 않는다 · 라벨용).</summary>
    public static void PutText(Worksheet ws, int row, int col, string text)
    {
        Guard(ws, row, col);
        ws.Cells[row, col].SetValueAsText(text);
    }

    /// <summary>숫자를 쓴다(null 이면 쓰지 않는다).</summary>
    public static void Put(Worksheet ws, int row, int col, double? value)
    {
        if (value is not double d) return;
        Guard(ws, row, col);
        ws.Cells[row, col].SetValue(d);
    }

    /// <summary>엑셀 주소(0-based → `M12`).</summary>
    public static string Address(int row, int col)
    {
        var name = string.Empty;
        for (int c = col + 1; c > 0; c = (c - 1) / 26) name = (char)('A' + (c - 1) % 26) + name;
        return $"{name}{row + 1}";
    }

    // ── 어류 : 양식을 내장하지 않고 글자만 덧붙인다(B-7 10-16 결정) ──────────────────────

    /// <summary>
    /// 어류 일괄입력용에 최종본의 라벨 열(M·N·O)·예시 열(P) 글자와 `필독` 시트 글자를 적는다.
    /// 글자는 `fish_labels.json`(TemplateGen 이 최종본에서 한 번 뽑은 것) 그대로다. 수식은 넣지 않는다.
    /// <paramref name="input"/> 은 이미 만든 `어류_입력` 시트 — 레코드 열(Q)·종 행(79행~)과 겹치지 않는 칸에만 쓴다.
    /// </summary>
    public static void ApplyFishLabels(Workbook wb, Worksheet input)
    {
        var path = Path.Combine(Folder, "fish_labels.json");
        if (!File.Exists(path))
            throw new BulkTemplateException($"어류 일괄입력용 라벨 파일이 없습니다: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var sheets = doc.RootElement.GetProperty("sheets");

        var notice = wb.Worksheets.Insert(0);   // 최종본과 같이 맨 앞
        notice.Name = "필독";
        Write(notice, sheets.GetProperty("필독"));
        Write(input, sheets.GetProperty("어류_입력"));

        static void Write(Worksheet ws, JsonElement cells)
        {
            foreach (var cell in cells.EnumerateArray())
            {
                int r = cell[0].GetInt32(), c = cell[1].GetInt32();
                var v = cell[2];
                if (v.ValueKind == JsonValueKind.Number) ws.Cells[r, c].SetValue(v.GetDouble());
                else ws.Cells[r, c].SetValueAsText(v.GetString() ?? string.Empty);
            }
        }
    }
}

/// <summary>일괄입력용 양식을 쓸 수 없어 저장을 멈춘다(설치 손상·양식 좌표 불일치).</summary>
internal sealed class BulkTemplateException : Exception
{
    public BulkTemplateException(string message, Exception? inner = null) : base(message, inner) { }
}
