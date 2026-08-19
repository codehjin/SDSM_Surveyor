using CommunityToolkit.Mvvm.ComponentModel;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 모든 분류군이 공유하는 "조사개황"(공통 식별·맥락 정보).
/// 각 분류군 ViewModel은 이 객체 하나를 <c>Meta</c> 로 가진다.
/// 형식·드롭다운 등 규칙 변경은 <c>SurveyOverviewControl</c> 한 곳에서 하면 전 분류군에 반영된다.
/// ※ 계산에 영향을 주는 값(하천차수·조사불가 사유 등)은 분류군 고유이므로 여기 두지 않는다.
/// </summary>
public partial class SurveyMeta : ObservableObject
{
    [ObservableProperty] private string? _project;                        // 대분류(방류하천/생태현황) — 파일명·구분(SiteDivision)
    [ObservableProperty] private string? _surveyYear;                     // 연도
    [ObservableProperty] private string _yearChsu = string.Empty;         // 연도차수
    [ObservableProperty] private DateTime? _surveyDate = DateTime.Today;  // 조사일자
    [ObservableProperty] private string? _majorRegion;                    // 대권역명
    [ObservableProperty] private string? _middleRegion;                   // 중권역명
    [ObservableProperty] private string? _river;                          // 하천명
    [ObservableProperty] private string? _riverType;                      // 하천유형
    [ObservableProperty] private string? _site;                           // 지점명
    [ObservableProperty] private string? _lat;                            // 위도
    [ObservableProperty] private string? _lng;                            // 경도
    [ObservableProperty] private string? _weather;                        // 날씨
    [ObservableProperty] private string? _surveyAgency;                   // 조사기관
    [ObservableProperty] private string? _surveyor;                       // 조사자

    // 목록형 옵션(현재는 날씨만 목록). 대권역·하천유형 등을 드롭다운으로 바꿀 때
    // 여기(또는 별도 옵션 제공자)에 목록을 채우고 컨트롤을 콤보로 교체하면 전 분류군 일괄 반영.
    public string[] Weathers { get; } = { "맑음", "흐림", "비(눈)" };
    public string[] Projects { get; } = { "방류하천", "생태현황" };
}
