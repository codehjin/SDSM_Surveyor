using System.IO;
using System.IO.Compression;
using System.Text.Json;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 자료함 [내보내기]·[가져오기](C-9 · 지시서 §3-1 "개인 폴더 유지 + 주고받기").
///
/// zip 구성 — 세션 파일은 자료함에 저장되는 것과 <b>같은 형식</b>이다(세션 JSON 형식을 바꾸지 않는다):
/// <code>
/// manifest.json            형식 표식 · 내보낸 시각 · 건수
/// index.json               내보낸 세션의 목록 항목(자료함 index.json 과 같은 모양)
/// sessions\{세션키}.json   세션 한 건
/// </code>
/// 가져오기: zip 을 검사한 뒤 세션을 더한다. 같은 세션 키(대분류·연도차수·지점)가 있으면 묻는다(기본 건너뜀).
/// 깨진 zip·JSON 은 <b>그 건만</b> 건너뛰고 결과를 요약한다. 기본 라이브러리 <c>System.IO.Compression</c> 만 쓴다.
/// </summary>
public sealed class SessionArchive : ISingletonService
{
    public const string Format = "SDSM_Surveyor.sessions";
    private const long MaxEntryBytes = 20L * 1024 * 1024;   // 세션 한 건은 수십 KB — 이보다 크면 세션 파일이 아니다

    private static readonly JsonSerializerOptions Write = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };

    private readonly ISessionStore _store;

    public SessionArchive(ISessionStore store) => _store = store;

    /// <summary>같은 세션 키가 이미 있을 때의 답.</summary>
    public enum Conflict { Skip, Overwrite, SkipAll, OverwriteAll }

    /// <summary>가져오기 결과 요약.</summary>
    public sealed class ImportResult
    {
        public List<string> Added { get; } = new();
        public List<string> Overwritten { get; } = new();
        public List<string> Skipped { get; } = new();
        /// <summary>읽지 못한 항목 — `이름: 이유`.</summary>
        public List<string> Failed { get; } = new();

        public string Summary
        {
            get
            {
                var lines = new List<string>
                {
                    $"추가 {Added.Count}건 · 덮어씀 {Overwritten.Count}건 · 건너뜀 {Skipped.Count}건 · 읽지 못함 {Failed.Count}건",
                };
                if (Added.Count > 0) lines.Add("추가: " + string.Join(", ", Added));
                if (Overwritten.Count > 0) lines.Add("덮어씀: " + string.Join(", ", Overwritten));
                if (Skipped.Count > 0) lines.Add("건너뜀(같은 세션이 있음): " + string.Join(", ", Skipped));
                if (Failed.Count > 0) lines.Add("읽지 못함: " + string.Join(" / ", Failed));
                return string.Join("\n", lines);
            }
        }
    }

    /// <summary>고른 세션을 zip 하나로 내보낸다. 내보낸 건수(세션 파일을 못 찾은 것은 빠진다).</summary>
    public async Task<int> ExportAsync(IEnumerable<string> sessionIds, string zipPath)
    {
        var index = await _store.ListAsync();
        var picked = new List<(SurveySession Session, SessionIndexEntry Entry)>();
        foreach (var id in sessionIds.Distinct())
        {
            var s = await _store.LoadAsync(id);
            if (s is null) continue;
            picked.Add((s, index.FirstOrDefault(e => e.SessionId == id) ?? EntryOf(s)));
        }

        // 다 만든 뒤 한 번에 쓴다 — 중간에 실패하면 반쯤 쓴 zip 이 남지 않게 임시 파일에 쓰고 바꾼다.
        var tmp = zipPath + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "manifest.json", JsonSerializer.Serialize(new Manifest
            {
                Format = Format, Version = 1, ExportedAt = DateTime.Now, Count = picked.Count,
            }, Write));
            WriteEntry(zip, "index.json", JsonSerializer.Serialize(picked.Select(p => p.Entry).ToList(), Write));
            foreach (var (s, _) in picked)
                WriteEntry(zip, $"sessions/{s.SessionId}.json", JsonSerializer.Serialize(s, Write));
        }
        File.Move(tmp, zipPath, overwrite: true);
        return picked.Count;
    }

    /// <summary>
    /// zip 의 세션을 자료함에 더한다. 같은 세션 키가 있으면 <paramref name="ask"/> 로 묻는다(null 이면 건너뜀).
    /// </summary>
    public async Task<ImportResult> ImportAsync(string zipPath, Func<SurveySession, Conflict>? ask)
    {
        var result = new ImportResult();
        ZipArchive zip;
        try { zip = ZipFile.OpenRead(zipPath); }
        catch (Exception ex)
        {
            result.Failed.Add($"{Path.GetFileName(zipPath)}: zip 파일을 열 수 없습니다({ex.Message})");
            return result;
        }

        using (zip)
        {
            // 목록 항목(경고 수 등)은 zip 의 index.json 에서 가져온다 — 없거나 깨져도 세션 파일로 다시 만든다.
            var zipIndex = new List<SessionIndexEntry>();
            var idx = zip.Entries.FirstOrDefault(e => e.FullName.Equals("index.json", StringComparison.OrdinalIgnoreCase));
            if (idx is not null)
            {
                try { zipIndex = JsonSerializer.Deserialize<List<SessionIndexEntry>>(ReadEntry(idx), Read) ?? new(); }
                catch { /* 목록은 보조 정보 — 세션 파일이 원본이다 */ }
            }

            var existing = (await _store.ListAsync()).Select(e => e.SessionId).ToHashSet(StringComparer.Ordinal);
            Conflict? all = null;

            foreach (var entry in zip.Entries.Where(IsSessionEntry).OrderBy(e => e.FullName, StringComparer.Ordinal))
            {
                var label = entry.FullName;
                SurveySession? s;
                try
                {
                    if (entry.Length > MaxEntryBytes) throw new InvalidDataException("세션 파일로 보기에는 너무 큽니다");
                    s = JsonSerializer.Deserialize<SurveySession>(ReadEntry(entry), Read);
                    if (s is null || s.Meta is null) throw new InvalidDataException("세션 내용이 비어 있습니다");
                }
                catch (Exception ex)
                {
                    result.Failed.Add($"{label}: {Reason(ex)}");
                    continue;
                }

                // ⚠ 세션 키는 파일 이름이 된다 — zip 안의 값을 그대로 믿지 않는다(경로 문자·빈 값이면 조사개황으로 다시 만든다).
                var key = SafeKey(s.SessionId) ?? SurveySession.MakeId(s.Meta.Project, s.Meta.YearChsu, SiteKeys.Of(s.Meta.Project, s.Meta.Site, s.Meta.River));
                s.SessionId = key;
                var name = $"{s.Meta.Site ?? "(지점 없음)"} · {s.Meta.YearChsu ?? "(차수 없음)"}";

                bool overwrite = false;
                if (existing.Contains(key))
                {
                    var answer = all ?? (ask?.Invoke(s) ?? Conflict.Skip);
                    if (answer is Conflict.SkipAll or Conflict.OverwriteAll) all = answer;
                    overwrite = answer is Conflict.Overwrite or Conflict.OverwriteAll;
                    if (!overwrite) { result.Skipped.Add(name); continue; }
                }

                try
                {
                    var e = zipIndex.FirstOrDefault(x => x.SessionId == key) ?? EntryOf(s);
                    e.SessionId = key;
                    await _store.SaveAsync(s, e);
                    (overwrite ? result.Overwritten : result.Added).Add(name);
                    existing.Add(key);
                }
                catch (Exception ex)
                {
                    result.Failed.Add($"{label}: 저장하지 못했습니다({ex.Message})");
                }
            }

            if (result.Added.Count + result.Overwritten.Count + result.Skipped.Count + result.Failed.Count == 0)
                result.Failed.Add($"{Path.GetFileName(zipPath)}: 세션 파일이 없습니다(자료함에서 내보낸 zip 인지 확인하세요)");
        }
        return result;
    }

    // ── 도움 ─────────────────────────────────────────────────────────────

    private sealed class Manifest
    {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public DateTime ExportedAt { get; set; }
        public int Count { get; set; }
    }

    private static bool IsSessionEntry(ZipArchiveEntry e) =>
        e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        && !e.FullName.Equals("index.json", StringComparison.OrdinalIgnoreCase)
        && !e.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase);

    private static void WriteEntry(ZipArchive zip, string name, string text)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var w = new StreamWriter(e.Open(), new System.Text.UTF8Encoding(false));
        w.Write(text);
    }

    private static string ReadEntry(ZipArchiveEntry e)
    {
        using var r = new StreamReader(e.Open());
        return r.ReadToEnd();
    }

    /// <summary>파일 이름으로 써도 안전한 세션 키면 그대로, 아니면 null.</summary>
    private static string? SafeKey(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (id.Contains("..") || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        return id.Length > 150 ? null : id;
    }

    private static string Reason(Exception ex) => ex switch
    {
        JsonException => $"JSON 형식이 깨졌습니다({ex.Message.Split('.')[0]})",
        InvalidDataException => ex.Message,
        _ => ex.Message,
    };

    private static SessionIndexEntry EntryOf(SurveySession s) => new()
    {
        SessionId = s.SessionId,
        Project = s.Meta.Project,
        YearChsu = s.Meta.YearChsu,
        Site = s.Meta.Site,
        River = s.Meta.River,
        Workplace = s.Meta.Workplace,
        UpdatedAt = s.UpdatedAt,
        Taxa = s.Taxa.Keys.Select(TaxonNames.Korean).ToList(),
    };
}
