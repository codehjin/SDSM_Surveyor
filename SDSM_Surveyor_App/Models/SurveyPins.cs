using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 조사개황 값 고정(압정) 6개. 여러 지점을 이어서 입력할 때 매번 다시 치지 않게 한다
/// (발주처 요구 · 2026-09-09 확정).
///
/// 흐름
///  1. 조사자가 압정을 켠다 → 그 순간 값이 settings.json 에 저장된다
///  2. 앱을 다시 켜면 <see cref="SeedInto"/> 가 켜진 칸만 조사개황에 채운다
///  3. 조사자가 그 칸을 고치면 고정값도 새 값으로 갱신된다(압정은 켜진 채)
///
/// ⚠ <see cref="SurveyMeta"/> 를 생성자로 받지 않는다 — <c>SurveyMeta ↔ SurveyPins</c> 순환이 되어
///   앱이 아예 시작되지 않는다(CLAUDE.md §2). <see cref="Attach"/> 로 나중에 연결한다.
///
/// ⚠ 지점명·좌표·하천은 고정 대상이 아니다. 대분류를 고정해도 <c>OnProjectChanged</c> 가
///   지점을 비우므로 다른 대분류의 지점이 섞이지 않는다.
/// </summary>
public partial class SurveyPins : ObservableObject, ISingletonService
{
    private readonly ISettingsStore _store;
    private SurveyMeta? _meta;

    public SurveyPins(ISettingsStore store)
    {
        _store = store;
        _store.Settings.PinnedMeta ??= new PinnedMetaSettings();
        var s = _store.Settings.PinnedMeta;

        Project = new PinToggle(nameof(SurveyMeta.Project), "대분류", s.PinProject, OnToggled);
        YearChsu = new PinToggle(nameof(SurveyMeta.YearChsu), "연도차수", s.PinYearChsu, OnToggled);
        SurveyDate = new PinToggle(nameof(SurveyMeta.SurveyDate), "조사일", s.PinSurveyDate, OnToggled);
        Weather = new PinToggle(nameof(SurveyMeta.Weather), "날씨", s.PinWeather, OnToggled);
        Surveyor = new PinToggle(nameof(SurveyMeta.Surveyor), "조사자", s.PinSurveyor, OnToggled);
        SurveyAgency = new PinToggle(nameof(SurveyMeta.SurveyAgency), "조사기관", s.PinSurveyAgency, OnToggled);

        All = new[] { Project, YearChsu, SurveyDate, Weather, Surveyor, SurveyAgency };
        foreach (var p in All) p.ValueText = p.IsOn ? StoredText(p.Key) : null;
        RaiseNotice();
    }

    public PinToggle Project { get; }
    public PinToggle YearChsu { get; }
    public PinToggle SurveyDate { get; }
    public PinToggle Weather { get; }
    public PinToggle Surveyor { get; }
    public PinToggle SurveyAgency { get; }

    /// <summary>6개 전부(순서는 화면 왼쪽부터).</summary>
    public IReadOnlyList<PinToggle> All { get; }

    private PinnedMetaSettings S => _store.Settings.PinnedMeta!;

    // ── 화면 안내 ──────────────────────────────────────────────────────────────

    public int PinnedCount => All.Count(p => p.IsOn);
    public bool HasPins => PinnedCount > 0;

    /// <summary>`고정된 값 3개가 그대로 들어갑니다 — 조사일 2025-04-28 · 날씨 맑음 · 조사자 홍길동`.</summary>
    public string PinnedNoticeText
    {
        get
        {
            var on = All.Where(p => p.IsOn).ToList();
            if (on.Count == 0) return string.Empty;

            var shown = on.Where(p => p.HasValue).ToList();
            if (shown.Count == 0)
                return $"고정한 칸 {on.Count}개가 비어 있습니다 — 값을 넣으면 그때 고정됩니다.";

            // 지금 야장 값과 고정값이 다른 칸(세션을 불러왔을 때 생긴다). 띠가 거짓말하면 안 된다.
            // 야장이 아직 연결되지 않았으면(생성자 시점) 비교하지 않는다 — 전부 불일치로 보인다.
            var stale = _meta is null
                ? new List<string>()
                : shown.Where(p => !string.Equals(p.ValueText, LiveText(p.Key), StringComparison.Ordinal))
                       .Select(p => p.Label).ToList();

            var head = $"고정된 값 {shown.Count}개 — {string.Join(" · ", shown.Select(p => $"{p.Label} {p.ValueText}"))}";
            return stale.Count == 0
                ? $"{head} (새 야장에 그대로 들어갑니다)"
                : $"{head} · ⚠ 지금 야장의 {string.Join(" · ", stale)} 은(는) 고정값과 다릅니다";
        }
    }

    /// <summary>대량 적용(세션 복원)이 끝난 뒤 불일치 표시를 다시 계산한다.</summary>
    public void RefreshNotice() => RaiseNotice();

    private void RaiseNotice()
    {
        OnPropertyChanged(nameof(PinnedCount));
        OnPropertyChanged(nameof(HasPins));
        OnPropertyChanged(nameof(PinnedNoticeText));
    }

    // ── 조사개황과 연결 ────────────────────────────────────────────────────────

    /// <summary>
    /// <see cref="SurveyMeta"/> 생성자가 자기 자신을 넘겨 부른다.
    /// 이 시점이 "앱을 켜서 빈 야장이 처음 만들어지는" 그 순간이라 시드 지점으로 맞다.
    ///
    /// ⚠ 세션 복원(<c>SurveyMetaSnapshot.ApplyTo</c>)은 이보다 항상 나중이므로
    ///   저장된 세션을 여는 흐름에서는 복원값이 시드값을 정상적으로 덮어쓴다.
    /// </summary>
    public void Attach(SurveyMeta meta)
    {
        _meta = meta;
        SeedInto(meta);
    }

    /// <summary>켜져 있고 값이 있는 칸만 조사개황에 넣는다. 대분류를 가장 먼저 넣는다.</summary>
    public void SeedInto(SurveyMeta m)
    {
        // 대분류가 먼저다 — 세터가 지점 목록을 그 대분류로 다시 거른다.
        if (Project.IsOn && !string.IsNullOrWhiteSpace(S.Project)) m.Project = S.Project;
        if (YearChsu.IsOn && !string.IsNullOrWhiteSpace(S.YearChsu)) m.YearChsu = S.YearChsu!;
        if (SurveyDate.IsOn && S.SurveyDate is DateTime d) m.SurveyDate = d;
        if (Weather.IsOn && !string.IsNullOrWhiteSpace(S.Weather)) m.Weather = S.Weather;
        if (Surveyor.IsOn && !string.IsNullOrWhiteSpace(S.Surveyor)) m.Surveyor = S.Surveyor;
        if (SurveyAgency.IsOn && !string.IsNullOrWhiteSpace(S.SurveyAgency)) m.SurveyAgency = S.SurveyAgency;
    }

    /// <summary>
    /// 조사자가 그 칸을 직접 고쳤다 → 켜져 있으면 고정값도 새 값으로 갱신한다.
    /// <see cref="SurveyMeta"/> 가 대량 적용(세션 복원·시드) 중에는 부르지 않는다.
    /// </summary>
    public void Capture(string key, object? value)
    {
        var p = All.FirstOrDefault(x => x.Key == key);
        if (p is null) return;
        if (!p.IsOn) { RaiseNotice(); return; }   // 불일치 표시만 다시 계산한다

        Store(key, value);
        p.ValueText = StoredText(key);
        _store.Save();
        RaiseNotice();
    }

    // ── 토글 ──────────────────────────────────────────────────────────────────

    private void OnToggled(PinToggle p)
    {
        switch (p.Key)
        {
            case nameof(SurveyMeta.Project): S.PinProject = p.IsOn; break;
            case nameof(SurveyMeta.YearChsu): S.PinYearChsu = p.IsOn; break;
            case nameof(SurveyMeta.SurveyDate): S.PinSurveyDate = p.IsOn; break;
            case nameof(SurveyMeta.Weather): S.PinWeather = p.IsOn; break;
            case nameof(SurveyMeta.Surveyor): S.PinSurveyor = p.IsOn; break;
            case nameof(SurveyMeta.SurveyAgency): S.PinSurveyAgency = p.IsOn; break;
        }

        // 켜는 순간 화면에 있는 값을 갈무리한다.
        if (p.IsOn && _meta is not null) Store(p.Key, CurrentOf(p.Key));

        p.ValueText = p.IsOn ? StoredText(p.Key) : null;
        if (_suspendSave) return;

        _store.Save();
        RaiseNotice();
    }

    /// <summary>고정을 모두 해제한다(값은 지우지 않는다 — 다시 켜면 그대로 쓴다).</summary>
    [RelayCommand]
    private void ClearAll()
    {
        // 토글마다 저장하면 settings.json 을 여러 번 통째로 다시 쓴다.
        // 그 사이에 앱이 죽으면 파일이 깨지고 담당 분류군 기억까지 날아간다 — 한 번만 쓴다.
        _suspendSave = true;
        try { foreach (var p in All) p.IsOn = false; }
        finally { _suspendSave = false; }

        _store.Save();
        RaiseNotice();
    }

    private bool _suspendSave;

    // ── 값 읽기/쓰기 ──────────────────────────────────────────────────────────

    private object? CurrentOf(string key) => key switch
    {
        nameof(SurveyMeta.Project) => _meta?.Project,
        nameof(SurveyMeta.YearChsu) => _meta?.YearChsu,
        nameof(SurveyMeta.SurveyDate) => _meta?.SurveyDate,
        nameof(SurveyMeta.Weather) => _meta?.Weather,
        nameof(SurveyMeta.Surveyor) => _meta?.Surveyor,
        nameof(SurveyMeta.SurveyAgency) => _meta?.SurveyAgency,
        _ => null
    };

    private void Store(string key, object? value)
    {
        switch (key)
        {
            case nameof(SurveyMeta.Project): S.Project = value as string; break;
            case nameof(SurveyMeta.YearChsu): S.YearChsu = value as string; break;
            case nameof(SurveyMeta.SurveyDate): S.SurveyDate = value as DateTime?; break;
            case nameof(SurveyMeta.Weather): S.Weather = value as string; break;
            case nameof(SurveyMeta.Surveyor): S.Surveyor = value as string; break;
            case nameof(SurveyMeta.SurveyAgency): S.SurveyAgency = value as string; break;
        }
    }

    private string? StoredText(string key) => key switch
    {
        nameof(SurveyMeta.Project) => Blank(S.Project),
        nameof(SurveyMeta.YearChsu) => Blank(S.YearChsu),
        nameof(SurveyMeta.SurveyDate) => S.SurveyDate is DateTime d ? d.ToString("yyyy-MM-dd") : null,
        nameof(SurveyMeta.Weather) => Blank(S.Weather),
        nameof(SurveyMeta.Surveyor) => Blank(S.Surveyor),
        nameof(SurveyMeta.SurveyAgency) => Blank(S.SurveyAgency),
        _ => null
    };

    /// <summary>지금 야장에 들어 있는 값(고정값과 같은 형식). 안내 띠가 불일치를 알리는 데 쓴다.</summary>
    private string? LiveText(string key) => key switch
    {
        nameof(SurveyMeta.SurveyDate) => _meta?.SurveyDate is DateTime d ? d.ToString("yyyy-MM-dd") : null,
        _ => Blank(CurrentOf(key) as string)
    };

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
}
