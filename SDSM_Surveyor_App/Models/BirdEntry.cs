using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using SDSM_Models;

namespace SDSM_Surveyor_App.Models;

/// <summary>조류 관찰 1행. 관리자 <c>Bird</c> 모델의 자동계산 외 전 필드(조류는 자동계산 없음).</summary>
public partial class BirdEntry : ObservableObject, IObservationRow
{
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsUnlisted))]
    private string? _speciesKo;      // 일반명(초성검색)

    /// <summary>방금 추가된 행(1.2초). 연속 입력 중 "지금 들어간 게 이거다"를 알려 준다.</summary>
    [ObservableProperty] private bool _isJustAdded;
    [ObservableProperty] private string? _speciesEn;      // 학명(종 선택 시 자동 · 수정 가능)
    [ObservableProperty] private string? _orderKo;        // 목(자동 · 수정 가능)
    [ObservableProperty] private string? _familyKo;       // 과(자동 · 수정 가능)
    [ObservableProperty] private int? _individualCount;   // 개체수 (null=미조사, 0=실측 부재)

    [ObservableProperty] private string? _migratoryType;  // 도래유형
    [ObservableProperty] private string? _category;       // 대항목
    [ObservableProperty] private string? _categoryDetail; // 세부항목
    [ObservableProperty] private string? _habitatType;    // 서식유형

    // 기록별 좌표 : 조사개황(지점) 좌표와 별개로 개체를 관찰한 지점을 남긴다.
    [ObservableProperty] private double? _lat;            // 위도
    [ObservableProperty] private double? _lng;            // 경도

    [ObservableProperty] private string? _feature;        // 특징
    [ObservableProperty] private string? _note;           // 특이사항

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

    /// <summary>생태계교란생물. 보호종과 의미가 반대이므로 표기를 섞지 않는다.</summary>
    public bool IsInvasive => MatchedSpecies?.IsInvasive ?? false;

    /// <summary>보호종·교란종 표기(그리드 '구분' 열).</summary>
    public string ProtectionText => SpeciesTagBuilder.Build(MatchedSpecies);
}
