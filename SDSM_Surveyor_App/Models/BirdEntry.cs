using CommunityToolkit.Mvvm.ComponentModel;

namespace SDSM_Surveyor_App.Models;

/// <summary>조류 관찰 1행. 관리자 <c>Bird</c> 모델의 자동계산 외 전 필드(조류는 자동계산 없음).</summary>
public partial class BirdEntry : ObservableObject
{
    [ObservableProperty] private string? _speciesKo;      // 일반명(초성검색)
    [ObservableProperty] private string? _speciesEn;      // 학명
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
}
