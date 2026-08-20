using CommunityToolkit.Mvvm.ComponentModel;
using SDSM_Models;

namespace SDSM_Surveyor_App.Models;

/// <summary>양서파충류 관찰 1행. 관리자 <c>AmphibianReptile</c> 모델과 1:1(자동계산 없음).
/// 흔적 6종은 관리자와 동일하게 <b>체크(bool)가 아니라 개체수/횟수(int?)</b> 이다.</summary>
public partial class AmphibianReptileEntry : ObservableObject
{
    [ObservableProperty] private string? _speciesKo;        // 일반명(초성검색)
    [ObservableProperty] private string? _speciesEn;        // 학명(종 선택 시 자동 · 수정 가능)
    [ObservableProperty] private string? _orderKo;          // 목(자동 · 수정 가능)
    [ObservableProperty] private string? _familyKo;         // 과(자동 · 수정 가능)

    [ObservableProperty] private string? _majorCategory;    // 대분류(양서류/파충류)
    [ObservableProperty] private string? _middleCategory;   // 중분류

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

    /// <summary>공식 종목록(국가생물종목록)에서 찾은 종. 보호종·교란종 표기의 근거.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSpecies))]
    [NotifyPropertyChangedFor(nameof(IsProtected))]
    [NotifyPropertyChangedFor(nameof(IsInvasive))]
    [NotifyPropertyChangedFor(nameof(ProtectionText))]
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

    /// <summary>흔적 6종 개체수 합계(행 단위 총 개체수). 미입력(null)은 집계에서 0으로 캐스팅한다.</summary>
    public int TraceSum =>
        (Trace1 ?? 0) + (Trace2 ?? 0) + (Trace3 ?? 0) + (Trace4 ?? 0) + (Trace5 ?? 0) + (Trace6 ?? 0);
}
