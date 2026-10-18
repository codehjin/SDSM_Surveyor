using Microsoft.Extensions.DependencyInjection;
using SDSM_Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 화면(공통 조사개황 + 7개 분류군 탭) ↔ 세션 파일을 잇는다.
/// 세션 저장·불러오기·복제는 모두 이 서비스를 거친다(04_FEATURE_SITE_SESSION §B-3).
/// </summary>
public sealed class SessionService : ISessionService, ISingletonService
{
    private readonly ISessionStore _store;
    private readonly SurveyMeta _meta;
    private readonly IServiceProvider _provider;
    private ITaxonSession[]? _taxaCache;

    /// <summary>
    /// 7개 분류군 ViewModel은 <see cref="IServiceProvider"/>로 늦게 가져온다.
    /// 각 ViewModel이 이 서비스를 주입받으므로(임시저장 → 세션 저장), 생성자에서 받으면 순환 의존이 된다.
    /// </summary>
    public SessionService(ISessionStore store, SurveyMeta meta, IServiceProvider provider)
    {
        _store = store;
        _meta = meta;
        _provider = provider;
    }

    private ITaxonSession[] Taxa => _taxaCache ??= new ITaxonSession[]
    {
        _provider.GetRequiredService<FishEntryViewModel>(),
        _provider.GetRequiredService<BenthosEntryViewModel>(),
        _provider.GetRequiredService<BirdEntryViewModel>(),
        _provider.GetRequiredService<MammalEntryViewModel>(),
        _provider.GetRequiredService<AmphibianReptileEntryViewModel>(),
        _provider.GetRequiredService<HabitatWaterEdgeEntryViewModel>(),
        _provider.GetRequiredService<WaterQualityEntryViewModel>(),
    };

    /// <summary>현재 열려 있는 세션의 키. 저장하면 채워지고, 지점·차수를 바꾸면 re-key 된다.</summary>
    public string? CurrentSessionId { get; private set; }

    public string CurrentKey => SurveySession.MakeId(_meta.Project, _meta.YearChsu, _meta.SiteKey);   // 생태현황 지점명 없음 → 하천명(D4 ②)

    public Task<List<SessionIndexEntry>> ListAsync() => _store.ListAsync();

    // ── 저장 안 한 변경 (B-5) ────────────────────────────────────────────────
    // "마지막으로 저장하거나 불러온 시점" 의 화면을 세션 JSON 과 같은 방식으로 직렬화해 두고,
    // 닫을 때 지금 화면과 비교한다. 속성마다 dirty 플래그를 다는 방식은 화면 상태
    // (접힘·검색어·칩)와 섞여 오판하기 쉽다 — 스냅샷은 저장되는 내용만 본다.

    private string? _baseline;

    /// <summary>
    /// 지금 화면을 '저장된 상태' 로 기록한다. 앱 시작(압정 주입·예시 시드 이후)에 창이 부르고,
    /// 저장·불러오기·지점 복사·차수 복제가 끝날 때 이 서비스가 직접 부른다.
    /// </summary>
    public void MarkClean() => _baseline = CaptureSnapshot();

    /// <summary>마지막 저장·불러오기 이후 저장될 내용이 바뀌었는가. 기준을 아직 잡지 않았으면 false.</summary>
    public bool HasUnsavedChanges() => _baseline is not null && CaptureSnapshot() != _baseline;

    /// <summary>
    /// 저장 키(파일 이름)를 만드는 칸 중 비어 있는 것. 비어도 <see cref="SaveCurrentAsync"/> 는
    /// '무제' 로 저장해 버린다 — 닫기 확인에서는 저장하기 전에 무엇이 필요한지 먼저 알린다.
    /// </summary>
    public IReadOnlyList<string> MissingKeyFields()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_meta.Project)) missing.Add("대분류");
        if (string.IsNullOrWhiteSpace(_meta.YearChsu)) missing.Add("연도차수");
        // D4 ② : 생태현황은 지점명이 없어도 하천명(대상지명)이 있으면 저장한다(원본 양서파충류는 지점명 칸이 비어 있다)
        if (_meta.SiteKey is null) missing.Add(SurveyMeta.IsEcoStatus(_meta.Project) ? "지점(또는 하천명)" : "지점");
        return missing;
    }

    /// <summary>
    /// 저장될 내용의 직렬화(조사개황 + 자료가 있는 분류군 — <see cref="SaveCurrentAsync"/> 와 같은 기준).
    /// 저장 시각은 세션 겉봉에만 있어 여기 들어오지 않는다. 화면 상태인
    /// 접힘(`…Expanded`)과 방금 추가 강조(`IsJustAdded`)는 세션에 실려도 비교에서 뺀다.
    /// </summary>
    private string CaptureSnapshot()
    {
        var taxa = new JsonObject();
        foreach (var t in Taxa)
        {
            if (!t.HasData) continue;
            taxa[t.Key] = JsonSerializer.SerializeToNode(t.CaptureState(), SessionJson.Options);
        }
        var root = new JsonObject
        {
            ["Meta"] = JsonSerializer.SerializeToNode(SurveyMetaSnapshot.From(_meta), SessionJson.Options),
            ["Taxa"] = taxa,
        };
        StripViewState(root);
        return root.ToJsonString();
    }

    private static void StripViewState(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var key in o.Select(p => p.Key)
                                     .Where(k => k == "IsJustAdded" || k.EndsWith("Expanded", StringComparison.Ordinal))
                                     .ToList())
                    o.Remove(key);
                foreach (var p in o) StripViewState(p.Value);
                break;
            case JsonArray a:
                foreach (var item in a) StripViewState(item);
                break;
        }
    }

    /// <summary>현재 화면 전체를 세션으로 저장한다. 지점·차수가 바뀌었으면 파일 키도 옮긴다.</summary>
    public async Task<SessionIndexEntry> SaveCurrentAsync()
    {
        // 저장하는 바로 그 내용을 기준으로 삼는다(파일 쓰는 동안 화면이 바뀌어도 그 변경은 '저장 안 함' 으로 남는다).
        var snapshot = CaptureSnapshot();

        var session = new SurveySession
        {
            SessionId = CurrentKey,
            Meta = SurveyMetaSnapshot.From(_meta)
        };

        var filled = new List<string>();
        foreach (var t in Taxa)
        {
            if (!t.HasData) continue;
            var json = JsonSerializer.SerializeToElement(t.CaptureState(), SessionJson.Options);
            session.Taxa[t.Key] = json;
            filled.Add(TaxonNames.Korean(t.Key));
        }

        // 기존 파일이 있으면 만든 시각을 유지한다.
        var prev = await _store.LoadAsync(session.SessionId);
        if (prev is not null) session.CreatedAt = prev.CreatedAt;

        var index = new SessionIndexEntry
        {
            SessionId = session.SessionId,
            Project = _meta.Project,
            YearChsu = _meta.YearChsu,
            Site = _meta.Site,
            River = _meta.River,
            Workplace = _meta.Workplace,
            Taxa = filled,
            Warnings = CountWarnings()
        };

        if (!string.IsNullOrEmpty(CurrentSessionId) && CurrentSessionId != session.SessionId)
            await _store.RenameAsync(CurrentSessionId, session, index);   // 지점·차수 변경 → re-key
        else
            await _store.SaveAsync(session, index);

        CurrentSessionId = session.SessionId;
        _baseline = snapshot;
        return index;
    }

    /// <summary>세션을 화면 전체에 되돌린다.</summary>
    public async Task<bool> LoadAsync(string sessionId)
    {
        var session = await _store.LoadAsync(sessionId);
        if (session is null) return false;

        session.Meta.ApplyTo(_meta);
        foreach (var t in Taxa)
        {
            t.ClearData();                                    // 이전 세션 잔상 제거
            if (!session.Taxa.TryGetValue(t.Key, out var json)) continue;

            // ⚠ 값 타입이 어긋난 세션 파일 하나가 앱을 통째로 죽이지 않게 한다
            //    (손으로 고친 json · 옛 판 파일). 그 분류군만 비워 두고 나머지는 살린다.
            try { t.RestoreState(json); }
            catch { t.ClearData(); }
        }

        CurrentSessionId = session.SessionId;
        MarkClean();   // 불러온 직후 닫으면 묻지 않는다(B-5)
        return true;
    }

    public async Task DeleteAsync(string sessionId)
    {
        await _store.DeleteAsync(sessionId);
        if (CurrentSessionId == sessionId) CurrentSessionId = null;
    }

    /// <summary>
    /// 지점 간 값 복사 — 조사개황(연도차수·조사자·날씨·조사기관 등)은 두고
    /// 지점과 그에 딸린 하천·사업장·좌표만 새 지점으로 바꾸며, 분류군 자료는 비운다.
    /// </summary>
    public void CopyToSite(SurveySite site)
    {
        foreach (var t in Taxa) t.ClearData();

        _meta.SelectedSite = null;
        _meta.SelectedSite = site;     // 지점명·하천·사업장·좌표가 함께 채워진다
        CurrentSessionId = null;       // 아직 저장되지 않은 새 세션
        MarkClean();                   // 아직 아무것도 치지 않았다 — 이 상태가 출발점이다(B-5)
    }

    /// <summary>
    /// 새 지점으로 시작(C-2) — <see cref="CopyToSite"/> 와 같은 흐름에서 지점만 비운 형태(site 가 null)이거나,
    /// 지점을 직접 바꾸면서 [새 지점으로 시작]을 고른 경우(site = 새 지점)다.
    /// 압정 6칸을 포함한 나머지 조사개황은 그대로 둔다 — 하천유형만 비운다(D7).
    /// </summary>
    public void StartNewSite(SurveySite? site)
    {
        foreach (var t in Taxa) t.ClearData();

        _meta.ClearSite();
        _meta.RiverType = null;   // 하천유형은 지점마다 다르다 — 다음 지점으로 따라가지 않게(2026-10-18 결정 D7)
        if (site is not null) _meta.SelectedSite = site;   // 지점명·하천·사업장·좌표·권역이 함께 채워진다
        CurrentSessionId = null;       // 아직 저장되지 않은 새 세션
        MarkClean();                   // 새 지점의 출발점(B-5)
    }

    public bool HasInputData() => Taxa.Any(t => t.HasData);

    /// <summary>
    /// 지금 화면을 그대로 보존한 채 <paramref name="work"/> 를 한다(C-8 일괄 출력).
    /// 일괄 출력은 세션을 차례로 화면 VM 에 불러와 파일을 쓰므로(VM 이 싱글턴) 끝나면 보던 화면을 되돌려야 한다.
    /// 보존하는 것: 조사개황 · 자료가 있는 분류군 상태 · 열린 세션 키 · '저장 안 한 변경' 기준점(B-5).
    /// 저장 확인(B-5)은 부르는 쪽이 먼저 한다 — 여기서는 저장하지 않고 메모리에만 들고 있다.
    /// </summary>
    public async Task<T> PreserveScreenAsync<T>(Func<Task<T>> work)
    {
        var meta = SurveyMetaSnapshot.From(_meta);
        var taxa = Taxa.Where(t => t.HasData)
                       .ToDictionary(t => t.Key, t => JsonSerializer.SerializeToElement(t.CaptureState(), SessionJson.Options));
        var id = CurrentSessionId;
        var baseline = _baseline;
        try
        {
            return await work();
        }
        finally
        {
            meta.ApplyTo(_meta);
            foreach (var t in Taxa)
            {
                t.ClearData();
                if (!taxa.TryGetValue(t.Key, out var json)) continue;
                try { t.RestoreState(json); }
                catch { t.ClearData(); }
            }
            CurrentSessionId = id;
            _baseline = baseline;
        }
    }

    /// <summary>
    /// 이전 차수 복사 — 같은 지점의 지난 회차를 종 목록까지 통째로 불러온 뒤
    /// 연도차수만 새 값으로 바꾼다(개체수만 갱신하면 되도록).
    /// </summary>
    public async Task<bool> CopyFromPreviousAsync(string sessionId, string newYearChsu, DateTime? newDate)
    {
        if (!await LoadAsync(sessionId)) return false;

        _meta.YearChsu = newYearChsu;
        if (newDate is not null) _meta.SurveyDate = newDate;
        CurrentSessionId = null;       // 새 회차이므로 별도 세션으로 저장된다
        MarkClean();                   // 복제 직후가 출발점이다(B-5)
        return true;
    }

    public Task<int> MigrateLegacyDraftsAsync() => _store.MigrateLegacyDraftsAsync();

    public Task<int> SeedTestDataIfEmptyAsync() => _store.SeedTestDataIfEmptyAsync();

    /// <summary>확인이 필요한 항목 수 — 자료함 목록의 '경고' 열.</summary>
    private int CountWarnings()
    {
        int n = 0;
        if (string.IsNullOrWhiteSpace(_meta.Project)) n++;
        if (string.IsNullOrWhiteSpace(_meta.YearChsu)) n++;
        if (_meta.SiteKey is null) n++;
        if (!_meta.IsSiteRegistered) n++;               // 지점 마스터에 없는 지점
        if (_meta.SurveyDate is null) n++;
        if (string.IsNullOrWhiteSpace(_meta.Surveyor)) n++;
        return n;
    }
}
