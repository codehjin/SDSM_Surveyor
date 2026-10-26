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
//   5. (v0.5.0 · 2026-10-26) 저서 종 행(47행~ A~K)을 앱 종목록(species.json · 관리자 1.1.0 목록)에 맞춘다 —
//      오탁치 · 가중치 · 보호종 · 분류 · 학명을 종목록 값으로, 양식에 없는 종목록 종은 같은 과 아래에 끼워 넣는다.
//      양식 행 짝 = 국명 + 학명(옛 학명은 SpeciesNameMap 으로 지침 표기). `--only benthos` 로 저서만 다시 만든다.
//
// ⚠ 원본 폴더에는 쓰지 않는다. 출력은 저장소의 SDSM_Surveyor_App\templates\bulk\ 에만 쓴다.
// ⚠ 이 도구는 Telerik.Licensing 을 참조한다 — 없으면 출력 끝에 'License' 시트가 붙는다.
// ─────────────────────────────────────────────────────────────────────────────

Console.OutputEncoding = Encoding.UTF8;

var repoRoot = FindRepoRoot();
var src = @"F:\17_한국환경지리연구소\2_삼성DS\2026년\5_보낸파일\20260305\일괄입력자료";
var outDir = Path.Combine(repoRoot, "SDSM_Surveyor_App", "templates", "bulk");
var speciesPath = Path.Combine(repoRoot, "SDSM_Surveyor_App", "species.json");
string? only = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--src") src = Path.GetFullPath(args[i + 1]);
    if (args[i] == "--out") outDir = Path.GetFullPath(args[i + 1]);
    if (args[i] == "--species") speciesPath = Path.GetFullPath(args[i + 1]);
    if (args[i] == "--only") only = args[i + 1];   // 예: --only benthos — 그 내장본만 다시 만들고 README 의 그 줄 · 종 행 절만 바꾼다
}
Directory.CreateDirectory(outDir);
Console.WriteLine($"원본 : {src}  (읽기만 한다)");
Console.WriteLine($"출력 : {outDir}\n");

// --criteria : 서식수변 평가내용(10-17 화면 검토)만 다시 뽑는다 — 내장본 xlsx 는 다시 만들지 않는다.
if (args.Contains("--criteria"))
{
    var section = WriteHabitatCriteria(src, outDir);
    var readmePath = Path.Combine(outDir, "README.md");
    var text = File.Exists(readmePath) ? File.ReadAllText(readmePath) : string.Empty;
    const string Mark = "## 서식수변 — 평가내용";
    int at = text.IndexOf(Mark, StringComparison.Ordinal);
    text = (at >= 0 ? text[..at] : text.TrimEnd() + "\n\n") + section;
    File.WriteAllText(readmePath, text, new UTF8Encoding(false));
    return 0;
}

// 레코드 영역 = (rowFrom, colFrom) 부터 사용 범위 끝까지. 0-based.
// 저서 : 레코드 열 M(12)부터 모든 행(머리글·라벨은 A~L 에 있다).
// 관찰형 : 엑셀 5행(0-based 4)부터 모든 열(1~3행 머리글 · 4행 예시).
// 수질·서식수변 : 레코드 열 F(5)부터, 엑셀 2행(0-based 1)부터.
var specs = new[]
{
    new Spec("benthos", "2602_방류하천_곡교천_저서동물_DB_v1.xlsx", "입력", RowFrom: 0, ColFrom: 12),
    new Spec("bird", "2602_방류하천_곡교천_조류_DB_v1.xlsx", "조류", RowFrom: 4, ColFrom: 0),
    new Spec("mammal", "2602_방류하천_곡교천_포유류_DB_v1.xlsx", "포유류", RowFrom: 4, ColFrom: 0),
    // 양서파충류 : 자료 행(5~824행)의 학명(H)·특징(I) 칸이 VLOOKUP 수식이다. 앱이 그 칸에 값을 쓰므로
    // 자료 행의 이 두 열은 수식까지 지운다(10-16 (2) 결정). 4행 예시의 수식은 레코드 영역 밖이라 남는다.
    new Spec("amphrep", "2603_생태현황_양서파충류_DB_v1.xlsx", "양서파충류", RowFrom: 4, ColFrom: 0, ClearFormulaCols: new[] { 7, 8 }),
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
readme.AppendLine("⑤ 양서파충류만 : 자료 행(5행~)의 학명(H)·특징(I) VLOOKUP 수식도 지움 — 앱이 그 칸에 값을 쓴다(10-16 (2) 결정).");
readme.AppendLine();
readme.AppendLine("| 내장본 | 원본 | 원본 크기 | 원본 수정일 | 입력 시트 | 레코드 영역(0-based 행·열부터) | 지운 상수 | 지운 수식 | 남은 상수 | 남은 수식 | 시트 | 내장본 크기 |");
readme.AppendLine("|---|---|---:|---|---|---|---:|---:|---:|---:|---|---:|");

int fail = 0;
string speciesSection = string.Empty;
foreach (var s in specs.Where(x => only is null || x.Name == only))
{
    var path = Path.Combine(src, s.Source);
    var fi = new FileInfo(path);
    var sw = Stopwatch.StartNew();
    Workbook wb;
    using (var fs = File.OpenRead(path)) wb = new XlsxFormatProvider().Import(fs, null);

    bool removedSite = wb.Worksheets.Remove("조사지점");
    var ws = wb.Worksheets.First(w => w.Name == s.Sheet);
    var used = ws.UsedCellRange;

    int cleared = 0, formulas = 0, clearedFormulas = 0;
    for (int r = Math.Max(s.RowFrom, used.FromIndex.RowIndex); r <= used.ToIndex.RowIndex; r++)
    for (int c = Math.Max(s.ColFrom, used.FromIndex.ColumnIndex); c <= used.ToIndex.ColumnIndex; c++)
    {
        var v = ws.Cells[r, c].GetValue().Value;
        if (v.ValueType == CellValueType.Empty) continue;
        if (v.ValueType == CellValueType.Formula)
        {
            if (s.ClearFormulaCols?.Contains(c) == true) { ws.Cells[r, c].ClearValue(); clearedFormulas++; continue; }
            formulas++;
            continue;
        }
        ws.Cells[r, c].ClearValue();   // 값만 지운다 — 서식·유효성은 남는다
        cleared++;
    }

    // ⑤ 저서 종 행을 앱 종목록에 맞춘다(v0.5.0)
    if (s.Name == "benthos") speciesSection = SyncBenthosSpecies(ws, speciesPath);

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
    if (clearedFormulas > 0) Console.WriteLine($"         자료 행 수식 {clearedFormulas}칸 지움(열 {string.Join(",", s.ClearFormulaCols!.Select(c => (char)('A' + c)))})");
    Console.WriteLine($"{s.Name,-8} 지운 상수 {cleared,6} · 남은 상수 {remaining} · 수식 {formulas,5} · 조사지점 제거 {removedSite} · {sw.Elapsed.TotalSeconds:F1}s · {size / 1024:N0} KB · 시트 {sheets}");
    readme.AppendLine($"| `{s.Name}.xlsx` | `{s.Source}` | {fi.Length / 1024:N0} KB | {fi.LastWriteTime:yyyy-MM-dd HH:mm} | `{s.Sheet}` | 행 {s.RowFrom}·열 {s.ColFrom} | {cleared:N0} | {clearedFormulas:N0} | **{remaining}** | {formulas:N0} | {sheets} | {size / 1024:N0} KB |");
}

// --only : 그 내장본 줄 · 저서 종 행 절만 README 에서 바꾸고 끝낸다(다른 내장본 · 어류 라벨 · 서식수변 평가내용은 그대로)
if (only is not null)
{
    var readmePath = Path.Combine(outDir, "README.md");
    var lines = File.ReadAllLines(readmePath).ToList();
    var fresh = readme.ToString().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith($"| `{only}.xlsx`")).ToList();
    for (int i = 0; i < lines.Count && fresh.Count == 1; i++)
        if (lines[i].StartsWith($"| `{only}.xlsx`")) lines[i] = fresh[0];
    var text = string.Join("\n", lines).TrimEnd() + "\n";
    if (speciesSection.Length > 0)
    {
        const string Mark = "## 저서 — 종 행";
        int at = text.IndexOf(Mark, StringComparison.Ordinal);
        if (at >= 0)
        {
            int end = text.IndexOf("\n## ", at + Mark.Length, StringComparison.Ordinal);
            text = text[..at] + speciesSection.TrimEnd() + "\n" + (end >= 0 ? "\n" + text[(end + 1)..] : "");
        }
        else text = text.TrimEnd() + "\n\n" + speciesSection;
    }
    File.WriteAllText(readmePath, text.Replace("\r\n", "\n"), new UTF8Encoding(false));
    Console.WriteLine(fail == 0 ? "\n완료 — 남은 상수 0 · License·조사지점 시트 없음" : $"\n⚠ 확인 필요 {fail}건");
    return fail == 0 ? 0 : 1;
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

readme.AppendLine();
readme.Append(WriteHabitatCriteria(src, outDir));
if (speciesSection.Length > 0) readme.AppendLine().Append(speciesSection);

File.WriteAllText(Path.Combine(outDir, "README.md"), readme.ToString(), new UTF8Encoding(false));
Console.WriteLine(fail == 0 ? "\n완료 — 남은 상수 0 · License·조사지점 시트 없음" : $"\n⚠ 확인 필요 {fail}건");
return fail == 0 ? 0 : 1;

// ── 서식수변 평가내용 : 화면의 '평가 내용' 열·툴팁에 쓰는 글자(10-17 화면 검토) ─────────────
// 최종본 서식수변 양식 `서식 및 수변환경` 시트 17~26행(평가항목 1~10)의 A(번호)·B(항목)·C(평가내용)·D(판정점수) 글자를
// 그대로 뽑는다(손으로 다시 치지 않는다). 줄바꿈도 원본 그대로 둔다 — 화면이 한 줄로 이어 보인다.
static string WriteHabitatCriteria(string src, string outDir)
{
    const string Source = "2602_방류하천_곡교천_서식수변_DB_v1.xlsx";
    var path = Path.Combine(src, Source);
    var fi = new FileInfo(path);
    Workbook wb;
    using (var fs = File.OpenRead(path)) wb = new XlsxFormatProvider().Import(fs, null);
    var ws = wb.Worksheets.First(w => w.Name == "서식 및 수변환경");

    string T(int r, int c) => ws.Cells[r, c].GetValue().Value.RawValue ?? string.Empty;
    var items = new List<object>();
    for (int r = 16; r <= 25; r++)   // 0-based 16~25 = 엑셀 17~26행
        items.Add(new { no = T(r, 0), name = T(r, 1), content = T(r, 2), scoring = T(r, 3) });

    var doc = new
    {
        source = Source,
        sourceBytes = fi.Length,
        sourceModified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
        generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        note = "서식 및 수변환경 시트 17~26행 A(번호)·B(항목)·C(평가내용)·D(판정점수) 글자. 화면 표시용 — 일괄입력용·보고서 출력에는 쓰지 않는다.",
        items,
    };
    var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    File.WriteAllText(Path.Combine(outDir, "habitat_criteria.json"), json, new UTF8Encoding(false));
    Console.WriteLine($"habitat  평가내용 {items.Count}항목 → habitat_criteria.json");

    return "## 서식수변 — 평가내용 (화면 표시용 · 10-17)\n\n" +
           $"`{Source}`({fi.Length / 1024:N0} KB · {fi.LastWriteTime:yyyy-MM-dd HH:mm})의 `서식 및 수변환경` 시트 17~26행 A·B·C·D 글자를 `habitat_criteria.json` 에 둔다" +
           $"(생성 {DateTime.Now:yyyy-MM-dd HH:mm}). 서식수변 화면의 '평가 내용' 열(넓은 창)·항목 툴팁(C열)·선택지 툴팁(D열)이 이 글자를 그대로 쓴다. 출력 엑셀에는 쓰지 않는다.\n";
}

// ── 저서 종 행 ↔ 앱 종목록(v0.5.0) ─────────────────────────────────────────────
// 양식 `입력` 시트 47행(0-based 46)~ : A 오탁치 · B 지표가중치 · C 멸종위기 I · D II · E 고유 · F 문 · G 강 · H 목 · I 과 · J 학명 · K 국명.
// 짝 = 국명 + 학명(양식의 옛 학명은 SpeciesNameMap.Benthos 로 지침 표기). 짝이 맞으면 A~K 를 종목록 값으로 쓰고,
// 종목록에 있는데 양식에 없는 종은 같은 과(없으면 같은 목)의 마지막 행 아래에 끼워 넣는다(위 행 서식 복사 · 아래 행은 내려감). 양식에만 있는 행은 지우지 않고 알린다.
static string SyncBenthosSpecies(Worksheet ws, string speciesPath)
{
    const int First = 46, NameCol = 10, EnCol = 9;
    var cat = JsonSerializer.Deserialize<SDSM_Models.SpeciesCatalog>(File.ReadAllText(speciesPath),
                  new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    var list = cat.Benthos;
    string Key(string? ko, string? en) => $"{ko?.Trim()}\u001F{en?.Trim()}";
    var byKey = list.GroupBy(b => Key(b.SpeciesKo, b.SpeciesEn)).ToDictionary(g => g.Key, g => g.First());

    string T(int r, int c) => ws.Cells[r, c].GetValue().Value.RawValue ?? string.Empty;
    var used = ws.UsedCellRange;
    int last = First - 1;
    var matched = new HashSet<SDSM_Models.BenthosSpeciesList>(ReferenceEqualityComparer.Instance);
    var templateOnly = new List<string>();
    int valueRows = 0, enRows = 0, cells = 0;
    var changedNames = new List<string>();
    for (int r = First; r <= used.ToIndex.RowIndex; r++)
    {
        var ko = T(r, NameCol).Trim();
        if (ko.Length == 0) continue;
        last = r;
        var en = SDSM_Surveyor_App.Helpers.SpeciesNameMap.Benthos(T(r, EnCol).Trim());
        if (!byKey.TryGetValue(Key(ko, en), out var sp) || matched.Contains(sp)) { templateOnly.Add($"{r + 1}행 {ko} {T(r, EnCol)}"); continue; }
        matched.Add(sp);
        var (n, vals, enChanged) = WriteSpeciesRow(ws, r, sp);
        cells += n;
        if (vals) { valueRows++; changedNames.Add($"{ko} {sp.SpeciesEn}"); }
        if (enChanged) enRows++;
    }
    // 양식에 없는 종목록 종 — 같은 과(없으면 같은 목)의 마지막 행 바로 아래에 행을 끼워 넣는다.
    // 종 행 아래의 빈 줄과 '> 새로운 종 추가' 안내 줄(A열)은 그대로 한 줄씩 내려간다(덮어쓰지 않는다).
    var added = new List<string>();
    foreach (var sp in list.Where(b => !matched.Contains(b)))
    {
        int after = last;
        for (int r = First; r <= last; r++)
            if (T(r, NameCol).Trim().Length > 0 && T(r, 8) == (sp.FamilyKo ?? "") && T(r, 5) == (sp.PhylumKo ?? "")) after = r;
        if (after == last)
            for (int r = First; r <= last; r++)
                if (T(r, NameCol).Trim().Length > 0 && T(r, 7) == (sp.OrderKo ?? "") && T(r, 5) == (sp.PhylumKo ?? "")) after = r;
        int at = after + 1;
        ws.Rows.Insert(at, 1);
        CopyRowFormat(ws, after, at, NameCol);   // 위 종 행 서식(A~K) — Copy/Paste 는 이 양식의 도형 때문에 예외가 나서 칸마다 옮긴다
        WriteSpeciesRow(ws, at, sp);
        last++;
        added.Add($"{at + 1}행 {sp.SpeciesKo} {sp.SpeciesEn}(위 {after + 1}행 {T(after, NameCol)} · {T(after, 8)})");
    }
    int rowsNow = Enumerable.Range(First, last - First + 1).Count(r => T(r, NameCol).Trim().Length > 0);
    Console.WriteLine($"benthos  종 행 ↔ {cat.Version}: 양식 {matched.Count + templateOnly.Count}행 · 짝 {matched.Count} · 값 고친 행 {valueRows} · 학명 고친 행 {enRows} · 칸 {cells} · 더한 행 {added.Count} · 양식에만 {templateOnly.Count} → 종 행 {rowsNow}");
    foreach (var a in added) Console.WriteLine($"         더함 {a}");
    foreach (var t in templateOnly) Console.WriteLine($"         ⚠ 종목록에 없음 {t}");

    var sb = new StringBuilder();
    sb.AppendLine("## 저서 — 종 행(앱 종목록에 맞춤 · v0.5.0)");
    sb.AppendLine();
    sb.AppendLine($"`benthos.xlsx` `입력` 시트 47행~ 종 행 A~K(오탁치 · 지표가중치 · 보호종 · 분류 · 학명 · 국명)를 앱 종목록 `species.json`(**{cat.Version}** · 저서 {list.Count}종)에 맞췄다(생성 {DateTime.Now:yyyy-MM-dd HH:mm}).");
    sb.AppendLine("짝은 국명 + 학명(양식의 옛 학명은 `Helpers\\SpeciesNameMap` 으로 지침 표기). 종목록에 있는데 양식에 없는 종은 같은 과(없으면 같은 목)의 마지막 행 아래에 끼워 넣었다(위 행 서식 · 그 아래 행과 '> 새로운 종 추가' 안내 줄은 한 줄씩 내려감).");
    sb.AppendLine();
    sb.AppendLine("| 양식 종 행 | 짝 | 값(A~I) 고친 행 | 학명(J) 고친 행 | 바뀐 칸 | 더한 행 | 양식에만 있는 행 | 결과 종 행 |");
    sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|");
    sb.AppendLine($"| {matched.Count + templateOnly.Count} | {matched.Count} | {valueRows} | {enRows} | {cells} | {added.Count} | {templateOnly.Count} | **{rowsNow}** |");
    sb.AppendLine();
    if (changedNames.Count > 0) sb.AppendLine($"값을 고친 종: {string.Join(" · ", changedNames)}").AppendLine();
    if (added.Count > 0) sb.AppendLine($"더한 종: {string.Join(" · ", added)}").AppendLine();
    if (templateOnly.Count > 0) sb.AppendLine($"⚠ 종목록에 없는 양식 행: {string.Join(" · ", templateOnly)}").AppendLine();
    return sb.ToString().Replace("\r\n", "\n");
}

// 행 서식 옮기기(A~to) — 스타일 이름 · 테두리 · 채우기 · 글꼴 · 정렬 · 표시 형식 · 줄바꿈.
static void CopyRowFormat(Worksheet ws, int from, int to, int toCol)
{
    for (int c = 0; c <= toCol; c++)
    {
        var src = ws.Cells[from, c];
        var dst = ws.Cells[to, c];
        dst.SetStyleName(src.GetStyleName().Value);
        dst.SetBorders(src.GetBorders());
        dst.SetFill(src.GetFill().Value);
        dst.SetFontFamily(src.GetFontFamily().Value);
        dst.SetFontSize(src.GetFontSize().Value);
        dst.SetIsBold(src.GetIsBold().Value);
        dst.SetIsItalic(src.GetIsItalic().Value);
        dst.SetForeColor(src.GetForeColor().Value);
        dst.SetHorizontalAlignment(src.GetHorizontalAlignment().Value);
        dst.SetVerticalAlignment(src.GetVerticalAlignment().Value);
        dst.SetFormat(src.GetFormat().Value);
        dst.SetIsWrapped(src.GetIsWrapped().Value);
    }
}

// 종목록 1종 → 그 행 A~K. (바뀐 칸 수, A~I 값이 바뀌었는지, 학명이 바뀌었는지)
static (int Cells, bool Values, bool En) WriteSpeciesRow(Worksheet ws, int r, SDSM_Models.BenthosSpeciesList sp)
{
    int n = 0; bool vals = false, enChanged = false;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    void Put(int c, object? v)
    {
        var cur = ws.Cells[r, c].GetValue().Value;
        string now = cur.ValueType == CellValueType.Empty ? string.Empty : cur.RawValue ?? string.Empty;
        string want = v switch { null => string.Empty, double d => d.ToString(inv), int i => i.ToString(inv), _ => v.ToString() ?? string.Empty };
        bool same = now == want
                    || (double.TryParse(now, System.Globalization.NumberStyles.Float, inv, out var a)
                        && double.TryParse(want, System.Globalization.NumberStyles.Float, inv, out var b) && a == b);
        if (same) return;
        if (string.IsNullOrEmpty(want)) ws.Cells[r, c].ClearValue();
        else if (v is double d2) ws.Cells[r, c].SetValue(d2);
        else if (v is int i2) ws.Cells[r, c].SetValue(i2);
        else ws.Cells[r, c].SetValueAsText(want);
        n++;
        if (c <= 8) vals = true;
        if (c == 9) enChanged = true;
    }
    Put(0, sp.SaprobicValue); Put(1, sp.IndicatorWeight);
    Put(2, Blank(sp.Endangered1)); Put(3, Blank(sp.Endangered2)); Put(4, Blank(sp.Endemic));
    Put(5, Blank(sp.PhylumKo)); Put(6, Blank(sp.ClassKo)); Put(7, Blank(sp.OrderKo)); Put(8, Blank(sp.FamilyKo));
    Put(9, Blank(sp.SpeciesEn)); Put(10, Blank(sp.SpeciesKo));
    return (n, vals, enChanged);

    static string? Blank(string? t) => string.IsNullOrWhiteSpace(t) ? null : t;
}

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

record Spec(string Name, string Source, string Sheet, int RowFrom, int ColFrom, int[]? ClearFormulaCols = null);
