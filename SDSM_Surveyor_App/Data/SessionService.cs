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
            taxa[t.Key] = JsonSerializer.SerializeToNode(Capture(t), SessionJson.Options);
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

    // ── 분류군 조사일(N3) — 분류군 자료 JSON 안의 칸 하나로 넣고 뺀다 ───────────────────
    // 분류군 DTO(FishDraft 등)는 바꾸지 않는다. 어류·저서 DTO 에는 예전부터 조사개황 사본 'SurveyDate' 가 있어 이름을 달리 한다.
    // 칸이 없는 예전 세션 = 분류군 조사일 없음(조사개황 조사일을 따름).

    internal const string TaxonDateProperty = "TaxonSurveyDate";

    /// <summary>분류군 상태 + 분류군 조사일(있으면).</summary>
    private static JsonElement Capture(ITaxonSession t)
    {
        var node = JsonSerializer.SerializeToNode(t.CaptureState(), SessionJson.Options);
        if (node is JsonObject o && t.TaxonSurveyDate is DateTime d)
            o[TaxonDateProperty] = d.Date;
        return JsonSerializer.SerializeToElement(node, SessionJson.Options);
    }

    private static void Restore(ITaxonSession t, JsonElement json)
    {
        t.RestoreState(json);
        t.TaxonSurveyDate = TaxonDateOf(json);
    }

    private static void Clear(ITaxonSession t)
    {
        t.ClearData();
        t.TaxonSurveyDate = null;
    }

    /// <summary>세션 파일의 분류군 자료에서 분류군 조사일을 읽는다(없으면 null).</summary>
    public static DateTime? TaxonDateOf(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty(TaxonDateProperty, out var p)
        && p.ValueKind == JsonValueKind.String && p.TryGetDateTime(out var d) ? d.Date : null;

    /// <summary>분류군 자료에 분류군 조사일을 적는다.</summary>
    private static JsonElement WithTaxonDate(JsonElement json, DateTime date)
    {
        var o = JsonNode.Parse(json.GetRawText()) as JsonObject ?? new JsonObject();
        o[TaxonDateProperty] = date.Date;
        return JsonSerializer.SerializeToElement(o, SessionJson.Options);
    }

    public Func<SessionSaveConflict, SessionConflictChoice>? ResolveSaveConflict { get; set; }

    public Func<SessionRename, SessionRenameChoice>? ResolveRename { get; set; }

    public string? LastSaveNotice { get; private set; }

    public string? LastSaveCancel { get; private set; }

    public const string CancelSameData = "저장하지 않았습니다(같은 자료가 있어 취소)";
    public const string CancelRename = "저장하지 않았습니다(세션 이름이 바뀐 저장을 취소)";

    /// <summary>
    /// 현재 화면 전체를 세션으로 저장한다. 불러온 세션의 이름(대분류·지점·차수)이 바뀌었으면 원래 세션을 어떻게 할지 묻는다(§32 · S4).
    /// ⚠ 같은 키(대분류·연도차수·지점)를 <b>다른</b> 세션이 이미 쓰고 있으면 통째로 덮어쓰지 않고 묻는다(2026-10-20 · 문제 1 —
    ///   포유류를 저장한 지점에 [새 지점]으로 서식수변을 저장하자 포유류 5행이 사라졌다).
    ///   [합치기] = 지금 화면에 자료가 있는 분류군만 새 값 · 나머지 분류군은 기존 자료 · 조사개황은 지금 값(빈칸이면 기존 값).
    ///   합친 뒤에는 그 세션을 화면에 다시 불러온다 — 그러지 않으면 다음 저장에서 기존 분류군이 또 지워진다.
    /// </summary>
    public async Task<SessionIndexEntry?> SaveCurrentAsync()
    {
        LastSaveNotice = null;
        LastSaveCancel = null;
        // 저장하는 바로 그 내용을 기준으로 삼는다(파일 쓰는 동안 화면이 바뀌어도 그 변경은 '저장 안 함' 으로 남는다).
        var snapshot = CaptureSnapshot();

        var session = CaptureCurrentSession();
        var current = session.Taxa.Keys.ToList();

        // §32 · S4 : 불러온 세션의 이름(대분류 · 연도차수 · 지점)이 바뀌었다 — 종전에는 묻지 않고 원래 세션을 지웠다.
        //   [새 세션으로 저장](기본 · 원래 세션 그대로) · [원래 세션을 바꾸기](원래 것을 지우고 새 이름으로) · [취소].
        //   새 이름에 다른 세션이 이미 있으면 아래 같은 자료 규칙(합치기 · 바꾸기 · 취소)을 그대로 탄다.
        string? renamedFrom = null;
        var replaceOld = false;
        if (!string.IsNullOrEmpty(CurrentSessionId) && !string.Equals(CurrentSessionId, session.SessionId, StringComparison.Ordinal)
            && await _store.LoadAsync(CurrentSessionId) is { } old)
        {
            var choice = ResolveRename?.Invoke(new SessionRename(
                             CurrentSessionId, KeyDisplay(old.Meta), session.SessionId, KeyDisplay(session.Meta),
                             OrderedTaxa(old.Taxa.Keys).Select(TaxonNames.Korean).ToList(), old.UpdatedAt))
                         ?? SessionRenameChoice.SaveAsNew;
            if (choice == SessionRenameChoice.Cancel) { LastSaveCancel = CancelRename; return null; }
            replaceOld = choice == SessionRenameChoice.Replace;
            renamedFrom = KeyDisplay(old.Meta);
        }

        // 기존 파일이 있으면 만든 시각을 유지한다.
        var prev = await _store.LoadAsync(session.SessionId);
        var merged = false;
        if (prev is not null && !string.Equals(CurrentSessionId, session.SessionId, StringComparison.Ordinal))
        {
            var existing = OrderedTaxa(prev.Taxa.Keys);
            var choice = ResolveSaveConflict?.Invoke(new SessionSaveConflict(
                             session.SessionId, KeyDisplay(session.Meta),
                             existing.Select(TaxonNames.Korean).ToList(), current.Select(TaxonNames.Korean).ToList(),
                             prev.UpdatedAt, MergePreview(session.Meta, prev, current)))
                         ?? SessionConflictChoice.Merge;
            if (choice == SessionConflictChoice.Cancel) { LastSaveCancel = CancelSameData; return null; }
            if (choice == SessionConflictChoice.Merge)
            {
                LastSaveNotice = Merge(session, prev, current);
                merged = true;
            }
            else
            {
                var dropped = existing.Where(k => !current.Contains(k)).Select(TaxonNames.Korean).ToList();
                LastSaveNotice = $"이미 있던 자료({KeyDisplay(session.Meta)})를 지금 화면으로 바꿔 저장했습니다"
                                 + (dropped.Count > 0 ? $" — 지운 분류군: {string.Join(" · ", dropped)}" : string.Empty);
            }
        }
        if (prev is not null) session.CreatedAt = prev.CreatedAt;

        var filled = OrderedTaxa(session.Taxa.Keys).Select(TaxonNames.Korean).ToList();
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

        if (replaceOld)
            await _store.RenameAsync(CurrentSessionId!, session, index);   // [원래 세션을 바꾸기] — 새 이름으로 저장 + 원래 세션 지움
        else
            await _store.SaveAsync(session, index);                       // 그 밖에는 원래 세션을 지우는 일이 없다
        if (renamedFrom is not null)
        {
            var moved = replaceOld
                ? $"원래 세션({renamedFrom})을 지우고 새 이름({KeyDisplay(session.Meta)})으로 옮겼습니다"
                : $"새 세션({KeyDisplay(session.Meta)})으로 저장했습니다 — 원래 세션({renamedFrom})은 그대로 있습니다";
            LastSaveNotice = LastSaveNotice is null ? moved : $"{moved}\n{LastSaveNotice}";
        }

        CurrentSessionId = session.SessionId;
        if (merged) await LoadAsync(session.SessionId);   // 합친 결과를 화면에(기존 분류군도 보이게 · 저장 안 한 변경 없음)
        else _baseline = snapshot;
        return index;
    }

    /// <summary>지금 화면 → 세션(조사개황 + 자료가 있는 분류군 · 분류군 조사일 포함). 저장하지 않는다.</summary>
    public SurveySession CaptureCurrentSession()
    {
        var session = new SurveySession
        {
            SessionId = CurrentKey,
            Meta = SurveyMetaSnapshot.From(_meta)
        };
        foreach (var t in Taxa)
        {
            if (!t.HasData) continue;
            session.Taxa[t.Key] = Capture(t);
        }
        return session;
    }

    /// <summary>분류군 키를 7개 분류군 순서로.</summary>
    private List<string> OrderedTaxa(IEnumerable<string> keys)
    {
        var set = keys.ToHashSet(StringComparer.Ordinal);
        var order = Taxa.Select(t => t.Key).ToList();
        return order.Where(set.Contains).Concat(set.Where(k => !order.Contains(k))).ToList();
    }

    private static string KeyDisplay(SurveyMetaSnapshot m) =>
        string.Join(" · ", new[] { m.Project, m.YearChsu, string.IsNullOrWhiteSpace(m.Site) ? m.River : m.Site }
                              .Where(v => !string.IsNullOrWhiteSpace(v)));

    /// <summary>조사개황 칸 이름(합치기 알림용).</summary>
    private static readonly (string Label, Func<SurveyMetaSnapshot, object?> Get, Action<SurveyMetaSnapshot, object?> Set)[] MetaFields =
    {
        ("연도", m => m.SurveyYear, (m, v) => m.SurveyYear = (string?)v),
        ("조사일자", m => m.SurveyDate, (m, v) => m.SurveyDate = (DateTime?)v),
        ("대권역명", m => m.MajorRegion, (m, v) => m.MajorRegion = (string?)v),
        ("중권역명", m => m.MiddleRegion, (m, v) => m.MiddleRegion = (string?)v),
        ("하천명", m => m.River, (m, v) => m.River = (string?)v),
        ("하천유형(어류·저서)", m => m.RiverType, (m, v) => m.RiverType = (string?)v),
        ("하천유형(수질·서식수변)", m => m.RiverTypeWater, (m, v) => m.RiverTypeWater = (string?)v),
        ("사업장", m => m.Workplace, (m, v) => m.Workplace = (string?)v),
        ("위도", m => m.Lat, (m, v) => m.Lat = (string?)v),
        ("경도", m => m.Lng, (m, v) => m.Lng = (string?)v),
        ("날씨", m => m.Weather, (m, v) => m.Weather = (string?)v),
        ("조사기관", m => m.SurveyAgency, (m, v) => m.SurveyAgency = (string?)v),
        ("조사자", m => m.Surveyor, (m, v) => m.Surveyor = (string?)v),
    };

    private static bool IsBlank(object? v) => v is null || (v is string s && string.IsNullOrWhiteSpace(s));

    private static string MetaText(object? v) => v is DateTime d ? d.ToString("yyyy-MM-dd") : (v?.ToString() ?? string.Empty).Trim();

    /// <summary>
    /// 합치면 그대로 둘 기존 분류군 중 실제 조사일이 새 조사개황 조사일과 다른 것 — 그 분류군 조사일 칸에 옮겨 적는다(N3).
    /// 새 조사개황 조사일은 지금 값(빈칸이면 기존 값 — <see cref="Merge"/> 와 같은 규칙).
    /// </summary>
    private static List<(string Key, DateTime Date, bool AlreadySet)> KeptTaxonDates(SurveyMetaSnapshot now, SurveySession prev, IReadOnlyCollection<string> current)
    {
        var newDate = (now.SurveyDate ?? prev.Meta.SurveyDate)?.Date;
        var list = new List<(string, DateTime, bool)>();
        foreach (var (key, json) in prev.Taxa)
        {
            if (current.Contains(key)) continue;
            var own = TaxonDateOf(json);
            if ((own ?? prev.Meta.SurveyDate?.Date) is DateTime d && d != newDate) list.Add((key, d, own is not null));
        }
        return list;
    }

    /// <summary>[합치기]를 고르면 생기는 일 — 확인 창에 보인다(N3). 분류군 조사일 유지 · 값이 바뀌는 조사개황 칸(전 → 후).</summary>
    private List<string> MergePreview(SurveyMetaSnapshot now, SurveySession prev, IReadOnlyCollection<string> current)
    {
        var notes = KeptTaxonDates(now, prev, current)
            .OrderBy(x => Array.FindIndex(Taxa, t => t.Key == x.Key))
            .Select(x => $"{TaxonNames.Korean(x.Key)} 조사일 {x.Date:yyyy-MM-dd} 유지")
            .ToList();
        foreach (var (label, get, _) in MetaFields)
        {
            var (a, b) = (get(prev.Meta), get(now));
            if (IsBlank(a) || IsBlank(b) || MetaText(a) == MetaText(b)) continue;
            notes.Add($"{label} · {MetaText(a)} → {MetaText(b)}");
        }
        return notes;
    }

    /// <summary>
    /// [합치기] — 지금 화면에 자료가 있는 분류군은 새 값, 없는 분류군은 기존 자료를 그대로 옮긴다.
    /// 조사개황은 지금 값으로 하되 빈칸이면 기존 값을 남긴다(하천유형 두 칸 포함). 키를 이루는 칸(대분류·연도차수·지점)은 같다.
    /// </summary>
    private string Merge(SurveySession session, SurveySession prev, IReadOnlyCollection<string> current)
    {
        var preview = MergePreview(session.Meta, prev, current);
        var keptDates = KeptTaxonDates(session.Meta, prev, current);
        var kept = new List<string>();
        foreach (var (key, json) in prev.Taxa)
        {
            if (current.Contains(key)) continue;
            // N3 : 기존 분류군의 실제 조사일이 새 조사개황 조사일과 다르면 그 분류군 조사일 칸에 옮겨 적는다
            var keep = keptDates.FirstOrDefault(x => x.Key == key);
            session.Taxa[key] = keep.Key is not null && !keep.AlreadySet ? WithTaxonDate(json, keep.Date) : json;
            kept.Add(key);
        }
        var filledMeta = new List<string>();
        foreach (var (label, get, set) in MetaFields)
        {
            if (!IsBlank(get(session.Meta)) || IsBlank(get(prev.Meta))) continue;
            set(session.Meta, get(prev.Meta));
            filledMeta.Add(label);
        }
        var parts = new List<string>
        {
            $"새 값: {(current.Count > 0 ? string.Join(" · ", OrderedTaxa(current).Select(TaxonNames.Korean)) : "없음")}",
            $"그대로 둔 분류군: {(kept.Count > 0 ? string.Join(" · ", OrderedTaxa(kept).Select(TaxonNames.Korean)) : "없음")}",
        };
        if (filledMeta.Count > 0) parts.Add($"조사개황 빈칸은 기존 값: {string.Join(" · ", filledMeta)}");
        if (preview.Count > 0) parts.Add(string.Join(" · ", preview));
        return $"같은 자료({KeyDisplay(session.Meta)})에 합쳐 저장했습니다 — {string.Join(" / ", parts)}";
    }

    /// <summary>세션을 화면 전체에 되돌린다.</summary>
    public async Task<bool> LoadAsync(string sessionId)
    {
        var session = await _store.LoadAsync(sessionId);
        if (session is null) return false;

        ApplyToScreen(session);
        CurrentSessionId = session.SessionId;
        MarkClean();   // 불러온 직후 닫으면 묻지 않는다(B-5)
        // 단, 불러오며 예전 값을 옮긴 분류군이 있으면(§20 저서 암반·콘크리트 → 특이사항) 화면이 파일과 다르다 —
        // '저장 안 한 변경'으로 두어 닫거나 다른 세션으로 넘어갈 때 저장 확인을 묻는다(G2). 기준을 어떤 화면과도 같지 않은 값으로.
        if (Taxa.Any(t => t.RestoreAdjusted)) _baseline = AdjustedOnLoad;
        return true;
    }

    /// <summary>불러오며 값을 옮겨 화면이 파일과 다를 때의 저장 기준(어떤 스냅샷과도 같지 않다 · <see cref="HasUnsavedChanges"/> = true).</summary>
    private const string AdjustedOnLoad = "(불러오며 옮긴 값 — 아직 저장 안 함)";

    /// <summary>세션 내용을 조사개황 + 7개 탭에 올린다(열린 세션 키·저장 기준은 부르는 쪽이 정한다).</summary>
    private void ApplyToScreen(SurveySession session)
    {
        session.Meta.ApplyTo(_meta);
        foreach (var t in Taxa)
        {
            Clear(t);                                         // 이전 세션 잔상 제거
            if (!session.Taxa.TryGetValue(t.Key, out var json)) continue;

            // ⚠ 값 타입이 어긋난 세션 파일 하나가 앱을 통째로 죽이지 않게 한다
            //    (손으로 고친 json · 옛 판 파일). 그 분류군만 비워 두고 나머지는 살린다.
            try { Restore(t, json); }
            catch { Clear(t); }
        }
    }

    public void ApplyRecovered(SurveySession session)
    {
        ApplyToScreen(session);
        CurrentSessionId = null;   // 새 세션 — 저장할 때 같은 키가 있으면 묻는다
        _baseline = string.Empty;  // 아직 저장하지 않았다 — 닫을 때 저장을 묻는다(B-5)
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
        foreach (var t in Taxa) Clear(t);

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
        foreach (var t in Taxa) Clear(t);

        _meta.ClearSite();
        _meta.RiverType = null;        // 하천유형은 지점마다 다르다 — 두 칸 모두 다음 지점으로 따라가지 않게(D7 · E1)
        _meta.RiverTypeWater = null;
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
        var taxa = Taxa.Where(t => t.HasData).ToDictionary(t => t.Key, Capture);
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
                Clear(t);
                if (!taxa.TryGetValue(t.Key, out var json)) continue;
                try { Restore(t, json); }
                catch { Clear(t); }
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

        foreach (var t in Taxa) t.TaxonSurveyDate = null;   // N3 : 지난 차수의 분류군 조사일은 새 차수로 따라가지 않는다
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
