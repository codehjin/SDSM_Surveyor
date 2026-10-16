using Telerik.Documents.Spreadsheet.Model;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 보고서 일괄(C-8 · 사용자 선택 C안) — <b>모든 분류군을 통합문서 하나에</b> 담는다.
/// <list type="bullet">
/// <item>시트 = 분류군 × 시트 종류(`어류_조사개황` · `어류_출현종` · `어류_건강성평가(FAI)` …). 시트 종류·순서는 지점별 보고서와 같다.</item>
/// <item>표 시트(출현종·건강성평가·요약·측정결과·평가항목)는 고른 지점들의 행을 <b>위아래로 이어 붙인다</b> — 행마다 앞 4열(프로젝트·사업장·과업대표하천·지점명)이 있어 지점을 가르고 거를 수 있다.</item>
/// <item>조사개황 시트는 <b>항목 × 지점</b> 가로 표(열마다 `지점 · 연도차수`).</item>
/// </list>
/// 지점 하나의 내용은 지점별 보고서 exporter 의 <c>Build(vm)</c> 가 만든 것을 그대로 옮긴다(서식 포함 · 계산을 다시 하지 않는다).
/// </summary>
internal static class CombinedReport
{
    /// <summary>지점(세션) 하나의 보고서 통합문서와 열 머리 글자.</summary>
    public sealed record Part(string Label, Workbook Book);

    private const string OverviewSheet = "조사개황";
    private static readonly string SiteHeader = SiteColumns.Headers[0];

    /// <summary>분류군 순서대로 합친다. <paramref name="taxa"/> = (분류군 이름, 지점별 통합문서들).</summary>
    public static Workbook Merge(IReadOnlyList<(string Taxon, IReadOnlyList<Part> Parts)> taxa)
    {
        var wb = new Workbook();
        foreach (var (taxon, parts) in taxa)
        {
            if (parts.Count == 0) continue;
            var first = parts[0].Book;
            for (int i = 0; i < first.Worksheets.Count; i++)
            {
                var name = first.Worksheets[i].Name;
                var target = wb.Worksheets.Add();
                target.Name = SheetName(taxon, name);
                var sources = parts.Select(p => (p.Label, Ws: p.Book.Worksheets.FirstOrDefault(w => w.Name == name))).Where(x => x.Ws is not null).ToList();
                if (name == OverviewSheet) MergeOverview(target, sources!);
                else MergeTable(target, sources!);
            }
        }
        if (wb.Worksheets.Count > 0) wb.ActiveWorksheet = wb.Worksheets[0];
        return wb;
    }

    /// <summary>시트 이름 — `분류군_시트` (엑셀 31자 · 금지 문자 제외).</summary>
    private static string SheetName(string taxon, string sheet)
    {
        var n = $"{taxon}_{sheet}";
        foreach (var c in new[] { '[', ']', ':', '*', '?', '/', '\\' }) n = n.Replace(c, '_');
        return n.Length <= 31 ? n : n[..31];
    }

    private static string Text(Worksheet ws, int r, int c) => ws.Cells[r, c].GetValue().Value.RawValue ?? string.Empty;

    private static int LastRow(Worksheet ws) => ws.UsedCellRange.ToIndex.RowIndex;
    private static int LastCol(Worksheet ws) => ws.UsedCellRange.ToIndex.ColumnIndex;

    /// <summary>원본 범위를 서식째 옮긴다.</summary>
    private static void CopyRange(Worksheet from, int r0, int c0, int r1, int c1, Worksheet to, int tr, int tc)
    {
        if (r1 < r0 || c1 < c0) return;
        using var frag = from.Cells[r0, c0, r1, c1].Copy();
        to.Cells[tr, tc].Paste(frag, PasteOptions.All);
    }

    private static void CopyWidths(Worksheet from, Worksheet to, int lastCol)
    {
        for (int c = 0; c <= lastCol; c++)
            to.Columns[c].SetWidth(from.Columns[c].GetWidth().Value);
    }

    // ── 표 시트 : 제목·머리줄은 첫 지점 것, 자료 행은 지점 순서대로 이어 붙임, 각주는 끝에 한 번 ──
    private static void MergeTable(Worksheet target, IReadOnlyList<(string Label, Worksheet Ws)> sources)
    {
        var first = sources[0].Ws;
        int header = HeaderRow(first);
        int lastCol = sources.Max(s => LastCol(s.Ws));

        if (header < 0)
        {
            // 표 머리줄(구분 열)을 못 찾은 시트 — 지점별로 통째로 위아래에 쌓는다(빈 줄 하나 사이).
            int at = 0;
            foreach (var (label, ws) in sources)
            {
                target.Cells[at, 0].SetValue(label); Section(target.Cells[at, 0]); at++;
                CopyRange(ws, 0, 0, LastRow(ws), LastCol(ws), target, at, 0);
                at += LastRow(ws) + 2;
            }
            CopyWidths(first, target, lastCol);
            return;
        }

        CopyRange(first, 0, 0, header, lastCol, target, 0, 0);
        int row = header + 1;
        foreach (var (_, ws) in sources)
        {
            int end = DataEnd(ws, header);
            if (end > header)
            {
                CopyRange(ws, header + 1, 0, end, LastCol(ws), target, row, 0);
                row += end - header;
            }
        }
        int lastData = row - 1;

        // 각주(자료 아래 빈 줄 다음의 ※ 문구) — 첫 지점 것을 한 번만
        int fEnd = DataEnd(first, header);
        if (LastRow(first) > fEnd + 1)
            CopyRange(first, fEnd + 1, 0, LastRow(first), LastCol(first), target, lastData + 1, 0);

        CopyWidths(first, target, lastCol);
        TryAutoFilter(target, header, lastData, lastCol);
    }

    /// <summary>표 머리줄 — 0열이 구분 열 첫 머리(`프로젝트명`)인 행.</summary>
    private static int HeaderRow(Worksheet ws)
    {
        for (int r = 0; r <= Math.Min(LastRow(ws), 20); r++)
            if (Text(ws, r, 0) == SiteHeader) return r;
        return -1;
    }

    /// <summary>머리줄 아래 자료 행의 끝 — 구분 열(0~3열)이 모두 빈 첫 행 바로 위.</summary>
    private static int DataEnd(Worksheet ws, int header)
    {
        int last = LastRow(ws);
        for (int r = header + 1; r <= last; r++)
            if (Enumerable.Range(0, 4).All(c => Text(ws, r, c).Length == 0)) return r - 1;
        return last;
    }

    // ── 조사개황 : 항목(0열)은 첫 지점 것, 값은 지점마다 한 열 ──
    private static void MergeOverview(Worksheet target, IReadOnlyList<(string Label, Worksheet Ws)> sources)
    {
        var first = sources[0].Ws;
        int last = LastRow(first);
        CopyRange(first, 0, 0, last, 0, target, 0, 0);

        // 항목 → 행(같은 이름이 둘이면 앞에서부터 차례로 맞춘다)
        var rowsOf = new Dictionary<string, Queue<int>>();
        for (int r = 0; r <= last; r++)
        {
            var k = Text(first, r, 0);
            if (k.Length == 0) continue;
            if (!rowsOf.TryGetValue(k, out var q)) rowsOf[k] = q = new Queue<int>();
            q.Enqueue(r);
        }
        int extra = last + 1;

        for (int j = 0; j < sources.Count; j++)
        {
            var (label, ws) = sources[j];
            int col = 1 + j;
            target.Cells[1, col].SetValue(label);    // 1행은 지점별 보고서에서 비어 있는 줄이다
            HeaderCell(target.Cells[1, col]);
            target.Columns[col].SetWidth(new Telerik.Documents.Spreadsheet.Model.ColumnWidth(170, true));

            var used = rowsOf.ToDictionary(p => p.Key, p => new Queue<int>(p.Value));
            for (int r = 0; r <= LastRow(ws); r++)
            {
                var k = Text(ws, r, 0);
                var v = Text(ws, r, 1);
                if (k.Length == 0 || v.Length == 0) continue;
                int tr;
                if (used.TryGetValue(k, out var q) && q.Count > 0) tr = q.Dequeue();
                else
                {
                    // 첫 지점에 없던 항목 — 맨 아래에 항목째 붙인다
                    tr = extra++;
                    CopyRange(ws, r, 0, r, 0, target, tr, 0);
                }
                CopyRange(ws, r, 1, r, 1, target, tr, col);
            }
        }

        target.Columns[0].SetWidth(first.Columns[0].GetWidth().Value);
        FontAll(target, Math.Max(extra - 1, last), sources.Count);
    }
}
