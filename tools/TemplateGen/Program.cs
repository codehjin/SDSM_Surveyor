using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Telerik.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Documents.Spreadsheet.Model;

// ─────────────────────────────────────────────────────────────────────────────
// 일괄입력용 양식 내장본 생성기 (B-7 · docs\CLAUDE_CODE_TASK_261015.md)
//
// 2026-03 일괄입력 최종본(③)을 **읽기만** 해서 앱에 넣을 내장본을 만든다.
//   · 6개 분류군(저서·조류·포유류·양서파충류·수질·서식수변) : 양식 그대로 + 실자료만 지운 내장본 xlsx
//   · 어류 : 양식을 내장하지 않는다(10-16 결정 — Telerik 이 어류 양식 수식을 다시 저장하면 깨진다).
//            라벨 열(M·N·O)·예시 열(P)·필독 시트의 **글자만** 뽑아 fish_labels.json 으로 둔다.
//
// 처리 규칙(지시서 1단계)
//   1. `조사지점` 시트는 뺀다(하천마다 지도가 다르다).
//   2. 레코드 영역의 **상수만** 지운다. 수식·서식은 남긴다.
//   3. 라벨 열·예시 열·종 행의 분류·형질·국명은 그대로.
//   4. 필독·보조 시트는 남긴다.
//
// ⚠ 원본 폴더에는 쓰지 않는다. 출력은 저장소의 SDSM_Surveyor_App\templates\bulk\ 에만 쓴다.
// ⚠ 이 도구는 Telerik.Licensing 을 참조한다 — 없으면 출력 끝에 'License' 시트가 붙는다.
// ─────────────────────────────────────────────────────────────────────────────

Console.OutputEncoding = Encoding.UTF8;

var repoRoot = FindRepoRoot();
var src = @"F:\17_한국환경지리연구소\2_삼성DS\2026년\5_보낸파일\20260305\일괄입력자료";
var outDir = Path.Combine(repoRoot, "SDSM_Surveyor_App", "templates", "bulk");
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--src") src = Path.GetFullPath(args[i + 1]);
    if (args[i] == "--out") outDir = Path.GetFullPath(args[i + 1]);
}
Directory.CreateDirectory(outDir);
Console.WriteLine($"원본 : {src}  (읽기만 한다)");
Console.WriteLine($"출력 : {outDir}\n");

// 레코드 영역 = (rowFrom, colFrom) 부터 사용 범위 끝까지. 0-based.
// 저서 : 레코드 열 M(12)부터 모든 행(머리글·라벨은 A~L 에 있다).
// 관찰형 : 엑셀 5행(0-based 4)부터 모든 열(1~3행 머리글 · 4행 예시).
// 수질·서식수변 : 레코드 열 F(5)부터, 엑셀 2행(0-based 1)부터.
var specs = new[]
{
    new Spec("benthos", "2602_방류하천_곡교천_저서동물_DB_v1.xlsx", "입력", RowFrom: 0, ColFrom: 12),
    new Spec("bird", "2602_방류하천_곡교천_조류_DB_v1.xlsx", "조류", RowFrom: 4, ColFrom: 0),
    new Spec("mammal", "2602_방류하천_곡교천_포유류_DB_v1.xlsx", "포유류", RowFrom: 4, ColFrom: 0),
    new Spec("amphrep", "2603_생태현황_양서파충류_DB_v1.xlsx", "양서파충류", RowFrom: 4, ColFrom: 0),
    new Spec("water", "2602_방류하천_곡교천_수질_DB_v1.xlsx", "수질", RowFrom: 1, ColFrom: 5),
    new Spec("habitat", "2602_방류하천_곡교천_서식수변_DB_v1.xlsx", "서식 및 수변환경", RowFrom: 1, ColFrom: 5),
};

var readme = new StringBuilder();
readme.AppendLine("# 일괄입력용 양식 내장본 (B-7)");
readme.AppendLine();
readme.AppendLine($"생성 {DateTime.Now:yyyy-MM-dd HH:mm} · `tools\\TemplateGen` · 원본 `{src}`(③ 2026-03 최종본 · 읽기만 함)");
readme.AppendLine();
readme.AppendLine("앱의 일괄입력용 exporter 가 이 파일을 열어 **첫 레코드 열(또는 자료 행)에만** 값을 쓴다. 손으로 고치지 말고 TemplateGen 을 다시 돌린다.");
readme.AppendLine();
readme.AppendLine("처리 : ① `조사지점` 시트 제거 ② 레코드 영역의 **상수만** 지움(수식·서식·데이터 유효성 유지) ③ 라벨·예시 열과 종 행의 분류·형질·국명 유지 ④ 필독·보조 시트 유지");
readme.AppendLine();
readme.AppendLine("| 내장본 | 원본 | 원본 크기 | 원본 수정일 | 입력 시트 | 레코드 영역(0-based 행·열부터) | 지운 상수 | 남은 상수 | 남은 수식 | 시트 | 내장본 크기 |");
readme.AppendLine("|---|---|---:|---|---|---|---:|---:|---:|---|---:|");

int fail = 0;
foreach (var s in specs)
{
    var path = Path.Combine(src, s.Source);
    var fi = new FileInfo(path);
    var sw = Stopwatch.StartNew();
    Workbook wb;
    using (var fs = File.OpenRead(path)) wb = new XlsxFormatProvider().Import(fs, null);

    bool removedSite = wb.Worksheets.Remove("조사지점");
    var ws = wb.Worksheets.First(w => w.Name == s.Sheet);
    var used = ws.UsedCellRange;

    int cleared = 0, formulas = 0;
    for (int r = Math.Max(s.RowFrom, used.FromIndex.RowIndex); r <= used.ToIndex.RowIndex; r++)
    for (int c = Math.Max(s.ColFrom, used.FromIndex.ColumnIndex); c <= used.ToIndex.ColumnIndex; c++)
    {
        var v = ws.Cells[r, c].GetValue().Value;
        if (v.ValueType == CellValueType.Empty) continue;
        if (v.ValueType == CellValueType.Formula) { formulas++; continue; }
        ws.Cells[r, c].ClearValue();   // 값만 지운다 — 서식·유효성은 남는다
        cleared++;
    }

    // 잔존 검사(같은 규칙으로 다시 센다) — 저장 전에 한 번, 저장본을 다시 열어 한 번
    int Remaining(Worksheet w)
    {
        int n = 0;
        var u = w.UsedCellRange;
        for (int r = Math.Max(s.RowFrom, u.FromIndex.RowIndex); r <= u.ToIndex.RowIndex; r++)
        for (int c = Math.Max(s.ColFrom, u.FromIndex.ColumnIndex); c <= u.ToIndex.ColumnIndex; c++)
        {
            var t = w.Cells[r, c].GetValue().Value.ValueType;
            if (t != CellValueType.Empty && t != CellValueType.Formula) n++;
        }
        return n;
    }

    var outPath = Path.Combine(outDir, s.Name + ".xlsx");
    using (var o = File.Create(outPath)) new XlsxFormatProvider().Export(wb, o, null);
    // Telerik 은 지운 `조사지점` 시트의 지도 그림(약 4.5MB)을 아무도 가리키지 않는 채로 패키지에 남긴다 → 걷어낸다.
    var (prunedParts, prunedBytes) = PruneOrphanMedia(outPath);
    if (prunedParts > 0) Console.WriteLine($"         참조 없는 그림 {prunedParts}개 · {prunedBytes / 1024:N0} KB 제거");

    Workbook back;
    using (var fs = File.OpenRead(outPath)) back = new XlsxFormatProvider().Import(fs, null);
    int remaining = Remaining(back.Worksheets.First(w => w.Name == s.Sheet));
    var sheets = string.Join(" · ", back.Worksheets.Select(w => w.Name));
    if (remaining != 0 || back.Worksheets.Any(w => w.Name is "License" or "조사지점")) fail++;

    var size = new FileInfo(outPath).Length;
    Console.WriteLine($"{s.Name,-8} 지운 상수 {cleared,6} · 남은 상수 {remaining} · 수식 {formulas,5} · 조사지점 제거 {removedSite} · {sw.Elapsed.TotalSeconds:F1}s · {size / 1024:N0} KB · 시트 {sheets}");
    readme.AppendLine($"| `{s.Name}.xlsx` | `{s.Source}` | {fi.Length / 1024:N0} KB | {fi.LastWriteTime:yyyy-MM-dd HH:mm} | `{s.Sheet}` | 행 {s.RowFrom}·열 {s.ColFrom} | {cleared:N0} | **{remaining}** | {formulas:N0} | {sheets} | {size / 1024:N0} KB |");
}

// ── 어류 : 라벨·예시·필독 글자만 뽑는다 ───────────────────────────────────────
const string FishSource = "2602_방류하천_곡교천_어류_DB_v1.xlsx";
{
    var path = Path.Combine(src, FishSource);
    var fi = new FileInfo(path);
    Workbook wb;
    using (var fs = File.OpenRead(path)) wb = new XlsxFormatProvider().Import(fs, null);

    var input = Extract(wb.Worksheets.First(w => w.Name == "어류_입력"), rowTo: 77, colFrom: 12, colTo: 15, out int skippedFormula);
    var notice = Extract(wb.Worksheets.First(w => w.Name == "필독"), rowTo: int.MaxValue, colFrom: 0, colTo: int.MaxValue, out _);

    var doc = new
    {
        source = FishSource,
        sourceBytes = fi.Length,
        sourceModified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
        generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        note = "어류_입력 1~78행 M·N·O(라벨)·P(예시)의 상수 글자와 필독 시트 글자. 수식 칸은 넣지 않았다(B-7 10-16 결정).",
        sheets = new Dictionary<string, List<object[]>> { ["필독"] = notice, ["어류_입력"] = input },
    };
    var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // 한글을 그대로 둔다
    });
    var outPath = Path.Combine(outDir, "fish_labels.json");
    File.WriteAllText(outPath, json, new UTF8Encoding(false));
    Console.WriteLine($"fish     라벨 {input.Count}칸(수식 {skippedFormula}칸 제외) · 필독 {notice.Count}칸 → fish_labels.json");

    readme.AppendLine();
    readme.AppendLine("## 어류 — 양식을 내장하지 않는다");
    readme.AppendLine();
    readme.AppendLine("Telerik(2026.3.1006)으로 어류 양식을 다시 저장하면 ① 빈 하천차수 열의 `CHOOSE` 에서 저장 예외 ② `SUMPRODUCT` 에 `@` 가 끼어 Excel 에서 `#VALUE!` 가 된다(작업로그_261015 10-16 절).");
    readme.AppendLine($"그래서 `{FishSource}`({fi.Length / 1024:N0} KB · {fi.LastWriteTime:yyyy-MM-dd HH:mm})에서 **글자만** 뽑아 `fish_labels.json` 에 둔다.");
    readme.AppendLine($"`어류_입력` 1~78행 M·N·O·P 상수 {input.Count}칸(P열 수식 {skippedFormula}칸은 넣지 않음) + `필독` {notice.Count}칸. 어류 exporter 는 이 글자를 같은 칸에 적고, 레코드(Q열)·종 행은 지금처럼 쓴다.");
}

File.WriteAllText(Path.Combine(outDir, "README.md"), readme.ToString(), new UTF8Encoding(false));
Console.WriteLine(fail == 0 ? "\n완료 — 남은 상수 0 · License·조사지점 시트 없음" : $"\n⚠ 확인 필요 {fail}건");
return fail == 0 ? 0 : 1;

// [행, 열, 값] — 값은 글자면 string, 숫자면 double. 수식 칸은 건너뛴다.
static List<object[]> Extract(Worksheet ws, int rowTo, int colFrom, int colTo, out int skippedFormula)
{
    var list = new List<object[]>();
    skippedFormula = 0;
    var u = ws.UsedCellRange;
    for (int r = u.FromIndex.RowIndex; r <= Math.Min(rowTo, u.ToIndex.RowIndex); r++)
    for (int c = Math.Max(colFrom, u.FromIndex.ColumnIndex); c <= Math.Min(colTo, u.ToIndex.ColumnIndex); c++)
    {
        var v = ws.Cells[r, c].GetValue().Value;
        switch (v)
        {
            case FormulaCellValue: skippedFormula++; break;
            case NumberCellValue n: list.Add(new object[] { r, c, n.Value }); break;
            case TextCellValue t when !string.IsNullOrEmpty(t.Value): list.Add(new object[] { r, c, t.Value }); break;
            case EmptyCellValue: break;
            default:
                var raw = v.RawValue;
                if (!string.IsNullOrEmpty(raw)) list.Add(new object[] { r, c, raw });
                break;
        }
    }
    return list;
}

// 어떤 관계(.rels)도 가리키지 않는 xl/media/* 부품을 지운다. [Content_Types].xml 의 Override 도 함께 정리한다.
static (int Parts, long Bytes) PruneOrphanMedia(string xlsx)
{
    using var zip = System.IO.Compression.ZipFile.Open(xlsx, System.IO.Compression.ZipArchiveMode.Update);
    var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)).ToList())
    {
        using var r = new StreamReader(e.Open());
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(r.ReadToEnd(), "Target=\"([^\"]+)\""))
            referenced.Add(Path.GetFileName(m.Groups[1].Value));
    }
    var orphans = zip.Entries.Where(e => e.FullName.StartsWith("xl/media/", StringComparison.OrdinalIgnoreCase)
                                         && !referenced.Contains(Path.GetFileName(e.FullName))).ToList();
    long bytes = orphans.Sum(e => e.Length);
    var names = orphans.Select(e => "/" + e.FullName).ToList();
    foreach (var e in orphans) e.Delete();

    var ct = zip.GetEntry("[Content_Types].xml");
    if (ct is not null && names.Count > 0)
    {
        string xml;
        using (var r = new StreamReader(ct.Open())) xml = r.ReadToEnd();
        foreach (var n in names)
            xml = System.Text.RegularExpressions.Regex.Replace(xml, "<Override[^>]*PartName=\"" + System.Text.RegularExpressions.Regex.Escape(n) + "\"[^>]*/>", "");
        ct.Delete();
        var fresh = zip.CreateEntry("[Content_Types].xml");
        using var w = new StreamWriter(fresh.Open(), new UTF8Encoding(false));
        w.Write(xml);
    }
    return (orphans.Count, bytes);
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "SDSM_Surveyor_App")) && Directory.Exists(Path.Combine(dir.FullName, "docs")))
            return dir.FullName;
        dir = dir.Parent;
    }
    throw new InvalidOperationException("저장소 루트를 찾지 못했습니다.");
}

record Spec(string Name, string Source, string Sheet, int RowFrom, int ColFrom);
