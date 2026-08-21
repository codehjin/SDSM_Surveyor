using Microsoft.Extensions.DependencyInjection;
using SDSM_Models;
using System.Text.Json;
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

    public string CurrentKey => SurveySession.MakeId(_meta.Project, _meta.YearChsu, _meta.Site);

    public Task<List<SessionIndexEntry>> ListAsync() => _store.ListAsync();

    /// <summary>현재 화면 전체를 세션으로 저장한다. 지점·차수가 바뀌었으면 파일 키도 옮긴다.</summary>
    public async Task<SessionIndexEntry> SaveCurrentAsync()
    {
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
            if (session.Taxa.TryGetValue(t.Key, out var json)) t.RestoreState(json);
        }

        CurrentSessionId = session.SessionId;
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
        return true;
    }

    public Task<int> MigrateLegacyDraftsAsync() => _store.MigrateLegacyDraftsAsync();

    /// <summary>확인이 필요한 항목 수 — 자료함 목록의 '경고' 열.</summary>
    private int CountWarnings()
    {
        int n = 0;
        if (string.IsNullOrWhiteSpace(_meta.Project)) n++;
        if (string.IsNullOrWhiteSpace(_meta.YearChsu)) n++;
        if (string.IsNullOrWhiteSpace(_meta.Site)) n++;
        if (!_meta.IsSiteRegistered) n++;               // 지점 마스터에 없는 지점
        if (_meta.SurveyDate is null) n++;
        if (string.IsNullOrWhiteSpace(_meta.Surveyor)) n++;
        return n;
    }
}
