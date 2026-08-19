using CommunityToolkit.Mvvm.ComponentModel;

namespace SDSM_Surveyor_App.Models;

/// <summary>조류 관찰 1행.</summary>
public partial class BirdEntry : ObservableObject
{
    [ObservableProperty] private string? _speciesKo;      // 일반명(초성검색)
    [ObservableProperty] private int? _individualCount;   // 개체수 (null vs 0 구분)

    public string? MigratoryType { get; set; }   // 도래유형
    public string? Category { get; set; }         // 대항목
    public string? CategoryDetail { get; set; }   // 세부항목
    public string? HabitatType { get; set; }      // 서식유형
    public string? Feature { get; set; }          // 특징
    public string? Note { get; set; }             // 특이사항
}
