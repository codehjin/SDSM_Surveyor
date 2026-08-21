using System.IO;
using System.Text.Json;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// %AppData%\SDSM_Surveyor\sessions\{sessionId}.json (다건) + index.json(목록).
/// 실행폴더가 아닌 AppData 를 쓰는 이유는 임시저장과 같다(업데이트·재설치에도 보존).
/// </summary>
public sealed class SessionStore : ISessionStore, ISingletonService
{
    private static readonly JsonSerializerOptions Write = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };

    /// <summary>구버전 임시저장 파일명 → 분류군키.</summary>
    private static readonly string[] LegacyTaxa =
        { "Fish", "Benthos", "Bird", "Mammal", "AmphibianReptile", "HabitatWaterEdge", "WaterQuality" };

    private static string Root
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SDSM_Surveyor", "sessions");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string IndexPath => Path.Combine(Root, "index.json");
    private static string FilePath(string id) => Path.Combine(Root, $"{id}.json");

    public async Task<List<SessionIndexEntry>> ListAsync()
    {
        var list = await ReadIndexAsync();
        return list.OrderByDescending(e => e.UpdatedAt).ToList();
    }

    public async Task<SurveySession?> LoadAsync(string sessionId)
    {
        var path = FilePath(sessionId);
        if (!File.Exists(path)) return null;
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SurveySession>(fs, Read);
    }

    public async Task<string> SaveAsync(SurveySession session, SessionIndexEntry index)
    {
        session.UpdatedAt = DateTime.Now;
        index.UpdatedAt = session.UpdatedAt;

        var path = FilePath(session.SessionId);
        await using (var fs = File.Create(path))
            await JsonSerializer.SerializeAsync(fs, session, Write);

        var list = await ReadIndexAsync();
        list.RemoveAll(e => e.SessionId == session.SessionId);
        list.Add(index);
        await WriteIndexAsync(list);
        return path;
    }

    public async Task DeleteAsync(string sessionId)
    {
        var path = FilePath(sessionId);
        if (File.Exists(path)) File.Delete(path);

        var list = await ReadIndexAsync();
        list.RemoveAll(e => e.SessionId == sessionId);
        await WriteIndexAsync(list);
    }

    public async Task<string> RenameAsync(string oldId, SurveySession session, SessionIndexEntry index)
    {
        var saved = await SaveAsync(session, index);
        if (!string.IsNullOrEmpty(oldId) && oldId != session.SessionId)
            await DeleteAsync(oldId);
        return saved;
    }

    public async Task<int> MigrateLegacyDraftsAsync()
    {
        var drafts = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SDSM_Surveyor", "drafts");
        if (!Directory.Exists(drafts)) return 0;

        // 이미 옮긴 뒤라면 표식 파일이 있다 — 두 번 편입하지 않는다.
        var marker = Path.Combine(drafts, "_migrated_to_sessions.txt");
        if (File.Exists(marker)) return 0;

        var session = new SurveySession();
        SurveyMetaSnapshot? meta = null;
        int moved = 0;

        foreach (var taxon in LegacyTaxa)
        {
            var path = Path.Combine(drafts, $"{taxon}.json");
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                session.Taxa[taxon] = doc.RootElement.Clone();
                // 구버전은 조사개황을 분류군마다 복사해 넣었다 — 첫 파일 것을 세션 공통값으로 쓴다.
                meta ??= JsonSerializer.Deserialize<SurveyMetaSnapshot>(doc.RootElement.GetRawText(), Read);
                moved++;
            }
            catch
            {
                // 손상된 파일 하나 때문에 나머지 편입을 막지 않는다.
            }
        }

        if (moved == 0)
        {
            await File.WriteAllTextAsync(marker, "no drafts");
            return 0;
        }

        session.Meta = meta ?? new SurveyMetaSnapshot();
        session.SessionId = SurveySession.MakeId(session.Meta.Project, session.Meta.YearChsu, session.Meta.Site);

        // 구버전 임시저장은 조사개황이 비어 있는 경우가 흔하다(단일 슬롯이라 지점을 안 적어도 덮어써졌다).
        // 그때 '무제_무제_무제' 라는 이름이 나오므로, 알아볼 수 있는 이름으로 바꾼다.
        if (session.SessionId == SurveySession.MakeId(null, null, null))
            session.SessionId = $"이전임시저장_{DateTime.Now:yyyyMMdd}";

        await SaveAsync(session, new SessionIndexEntry
        {
            SessionId = session.SessionId,
            Project = session.Meta.Project,
            YearChsu = session.Meta.YearChsu,
            Site = session.Meta.Site,
            River = session.Meta.River,
            Workplace = session.Meta.Workplace,
            Taxa = session.Taxa.Keys.Select(TaxonNames.Korean).ToList()
        });

        await File.WriteAllTextAsync(marker, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} → {session.SessionId}");
        return moved;
    }

    private static async Task<List<SessionIndexEntry>> ReadIndexAsync()
    {
        if (!File.Exists(IndexPath)) return RebuildIndex();
        try
        {
            await using var fs = File.OpenRead(IndexPath);
            return await JsonSerializer.DeserializeAsync<List<SessionIndexEntry>>(fs, Read) ?? new();
        }
        catch
        {
            return RebuildIndex();   // 목록이 깨져도 세션 파일이 원본이므로 다시 만든다
        }
    }

    /// <summary>index.json 이 없거나 깨졌을 때 세션 파일에서 목록을 복원한다.</summary>
    private static List<SessionIndexEntry> RebuildIndex()
    {
        var list = new List<SessionIndexEntry>();
        foreach (var f in Directory.GetFiles(Root, "*.json"))
        {
            if (Path.GetFileName(f).Equals("index.json", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var s = JsonSerializer.Deserialize<SurveySession>(File.ReadAllText(f), Read);
                if (s is null) continue;
                list.Add(new SessionIndexEntry
                {
                    SessionId = s.SessionId,
                    Project = s.Meta.Project,
                    YearChsu = s.Meta.YearChsu,
                    Site = s.Meta.Site,
                    River = s.Meta.River,
                    Workplace = s.Meta.Workplace,
                    UpdatedAt = s.UpdatedAt,
                    Taxa = s.Taxa.Keys.Select(TaxonNames.Korean).ToList()
                });
            }
            catch { /* 읽히지 않는 파일은 건너뛴다 */ }
        }
        return list;
    }

    private static async Task WriteIndexAsync(List<SessionIndexEntry> list)
    {
        await using var fs = File.Create(IndexPath);
        await JsonSerializer.SerializeAsync(fs, list, Write);
    }
}

/// <summary>분류군키 ↔ 한글 이름. 자료함 목록과 상태 문구에 쓴다.</summary>
public static class TaxonNames
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["Fish"] = "어류",
        ["Benthos"] = "저서동물",
        ["Bird"] = "조류",
        ["Mammal"] = "포유류",
        ["AmphibianReptile"] = "양서파충류",
        ["HabitatWaterEdge"] = "서식수변",
        ["WaterQuality"] = "수질",
    };

    public static string Korean(string key) => Map.TryGetValue(key, out var v) ? v : key;
}
