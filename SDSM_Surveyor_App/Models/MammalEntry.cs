using CommunityToolkit.Mvvm.ComponentModel;

namespace SDSM_Surveyor_App.Models;

/// <summary>포유류 관찰 1행 (흔적 12종 체크).</summary>
public partial class MammalEntry : ObservableObject
{
    [ObservableProperty] private string? _speciesKo;   // 일반명(초성검색)

    public string? ObservationSite { get; set; }       // 관찰지 유형

    // 흔적 12종
    public bool Capture { get; set; }       // 포획
    public bool Observe { get; set; }       // 관찰
    public bool Sound { get; set; }         // 울음
    public bool Carcass { get; set; }       // 사체
    public bool Footprint { get; set; }     // 족적
    public bool Fur { get; set; }           // 털
    public bool FeedingMark { get; set; }   // 식흔
    public bool Burrow { get; set; }        // 굴
    public bool Breeding { get; set; }      // 번식지
    public bool Feces { get; set; }         // 배설물
    public bool Camera { get; set; }        // 카메라
    public bool Etc { get; set; }           // 기타(탐문 등)

    public string? Feature { get; set; }    // 특징
    public string? Note { get; set; }       // 특이사항
}
