using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using SDSM_Models;

namespace SDSM_Surveyor_App.Models;

/// <summary>양서파충류 관찰 1행. 관리자 <c>AmphibianReptile</c> 모델과 1:1(자동계산 없음).
/// 흔적 6종은 관리자와 동일하게 <b>체크(bool)가 아니라 개체수/횟수(int?)</b> 이다.</summary>
public partial class AmphibianReptileEntry : ObservableObject, IObservationRow
{
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsUnlisted))]
    private string? _speciesKo;        // 일반명(초성검색)

    /// <summary>방금 추가된 행(1.2초). 연속 입력 중 "지금 들어간 게 이거다"를 알려 준다.</summary>
    [ObservableProperty] private bool _isJustAdded;
    [ObservableProperty] private string? _speciesEn;        // 학명(종 선택 시 자동 · 수정 가능)
    [ObservableProperty] private string? _orderKo;          // 목(자동 · 수정 가능)
    [ObservableProperty] private string? _familyKo;         // 과(자동 · 수정 가능)

    // 서식지 유형(양식 Y·Z열 · 2026-10-18 결정 D6) — 대분류 농림지역/산림지역/습지대/기타 지역 · 중분류는 대분류별 항목 여러 개(`, `)
    [ObservableProperty][NotifyPropertyChangedFor(nameof(MiddleOptions))][NotifyPropertyChangedFor(nameof(HabitatOutOfForm))]
    private string? _majorCategory;    // 대분류
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HabitatOutOfForm))]
    private string? _middleCategory;   // 중분류

    // 흔적 6종 (관리자 AmphibianReptile.Trace1~6). 미입력은 null 유지 — 0(실측 부재)과 구분한다.
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace1;   // 성체
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace2;   // 유생
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace3;   // 알
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace4;   // 울음소리
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace5;   // 로드킬
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace6;   // 기타

    // 기록별 좌표 : 조사개황(지점) 좌표와 별개로 개체를 관찰한 지점을 남긴다.
    [ObservableProperty] private double? _lat;              // 위도
    [ObservableProperty] private double? _lng;              // 경도

    [ObservableProperty] private string? _feature;          // 특징
    [ObservableProperty] private string? _note;             // 특이사항

    /// <summary>
    /// 이 행과 국명이 같은 행 수(이 행 포함). 2 이상이면 국명 칸에 '같은 종 n행' 을 띄운다(B-1).
    /// 화면 표시용이라 세션 JSON 에 저장하지 않는다 — 열 때마다 다시 센다.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SameSpeciesText))]
    [property: JsonIgnore]
    private int _sameSpeciesCount;

    /// <summary>
    /// 국명이 있는데 공식 종목록(국가생물종목록)에서 못 찾은 행(B-4). 국명 칸에 '목록 밖' 을 띄운다.
    /// 국명은 지우지 않고 그대로 저장한다 — 오타인지 신규 종인지는 조사자가 판단한다. 화면 표시용(저장 안 함).
    /// </summary>
    [JsonIgnore]
    public bool IsUnlisted => !string.IsNullOrWhiteSpace(SpeciesKo) && MatchedSpecies is null;

    /// <summary>'같은 종 n행' 표시 문구. 같은 종이 없으면 빈 문자열(표시 안 함).</summary>
    [JsonIgnore]
    public string SameSpeciesText => SameSpeciesCount > 1 ? $"같은 종 {SameSpeciesCount}행" : string.Empty;

    /// <summary>공식 종목록(국가생물종목록)에서 찾은 종. 보호종·교란종 표기의 근거.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSpecies))]
    [NotifyPropertyChangedFor(nameof(IsProtected))]
    [NotifyPropertyChangedFor(nameof(IsInvasive))]
    [NotifyPropertyChangedFor(nameof(ProtectionText))]
    [NotifyPropertyChangedFor(nameof(IsUnlisted))]
    [NotifyPropertyChangedFor(nameof(IsScientificNameLocked))]
    private ObservedSpecies? _matchedSpecies;

    /// <summary>그리드 국명 편집기(자동완성)에서 고른 종. 고르면 국명·학명·목·과가 함께 채워진다.</summary>
    public ObservedSpecies? SelectedSpecies
    {
        get => MatchedSpecies;
        set
        {
            if (value is null) return;   // 편집 취소로 null 이 들어와도 기존 입력을 지우지 않는다
            SpeciesKo = value.SpeciesKo;
            ApplySpecies(value);
        }
    }

    /// <summary>공식 종목록 조회 결과를 반영한다.
    /// 미매칭(null)일 때는 <b>이전에 자동으로 채운 값만</b> 비운다 — 조사자가 직접 쓴 값은 지우지 않는다.</summary>
    public void ApplySpecies(ObservedSpecies? sp)
    {
        bool wasAuto = MatchedSpecies is not null;
        MatchedSpecies = sp;
        if (sp is not null)
        {
            SpeciesEn = sp.SpeciesEn;
            OrderKo = sp.OrderKo;
            FamilyKo = sp.FamilyKo;
        }
        else if (wasAuto)
        {
            SpeciesEn = null; OrderKo = null; FamilyKo = null;
        }
    }

    /// <summary>법정보호종(멸종위기Ⅰ·Ⅱ급/천연기념물).</summary>
    public bool IsProtected => MatchedSpecies?.IsProtected ?? false;

    /// <summary>학명 칸 잠금 — 종목록에 있는 국명은 학명을 종목록대로(2026-10-18 결정 D2). 화면용(저장 안 함).</summary>
    [JsonIgnore] public bool IsScientificNameLocked => ScientificNameRule.IsLocked(this);

    /// <summary>고른 대분류의 중분류 선택지(D6). 대분류가 비면 빈 목록.</summary>
    [JsonIgnore] public IReadOnlyList<string> MiddleOptions => FormChoices.AmphHabitatMiddle(MajorCategory);

    /// <summary>서식지 유형 중 양식에 없는 값(예전 세션 `양서류` 등 · 지우지 않는다). 없으면 빈 글자.</summary>
    [JsonIgnore] public string HabitatOutOfForm
    {
        get
        {
            var bad = new List<string>();
            if (FormChoices.OutOfForm(MajorCategory, FormChoices.AmphHabitatMajor) is string m) bad.Add(m);
            bad.AddRange(FormChoices.OutOfForm(MiddleCategory, MiddleOptions));
            return bad.Count == 0 ? string.Empty : $"양식 외 {string.Join(",", bad)}";
        }
    }

    /// <summary>개체수·흔적이 비었거나 0 — 일괄입력용에는 그대로 내고 저장 때 알린다(D3). 화면용(저장 안 함).</summary>
    [JsonIgnore] public bool HasNoCount => TraceSum == 0;

    /// <summary>생태계교란생물. 보호종과 의미가 반대이므로 표기를 섞지 않는다.</summary>
    public bool IsInvasive => MatchedSpecies?.IsInvasive ?? false;

    /// <summary>보호종·교란종 표기(그리드 '구분' 열).</summary>
    public string ProtectionText => SpeciesTagBuilder.Build(MatchedSpecies);

    /// <summary>흔적 6종 개체수 합계(행 단위 총 개체수). 미입력(null)은 집계에서 0으로 캐스팅한다.</summary>
    public int TraceSum =>
        (Trace1 ?? 0) + (Trace2 ?? 0) + (Trace3 ?? 0) + (Trace4 ?? 0) + (Trace5 ?? 0) + (Trace6 ?? 0);
}
