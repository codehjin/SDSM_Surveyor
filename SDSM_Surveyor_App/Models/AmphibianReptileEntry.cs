using CommunityToolkit.Mvvm.ComponentModel;

namespace SDSM_Surveyor_App.Models;

/// <summary>양서파충류 관찰 1행 (흔적 6종 체크).</summary>
public partial class AmphibianReptileEntry : ObservableObject
{
    [ObservableProperty] private string? _speciesKo;   // 일반명(초성검색)

    public string? MajorCategory { get; set; }         // 대분류(양서류/파충류)
    public string? MiddleCategory { get; set; }        // 중분류

    // 흔적 6종
    public bool Adult { get; set; }     // 성체
    public bool Larva { get; set; }     // 유생
    public bool Egg { get; set; }       // 알
    public bool Sound { get; set; }     // 울음소리
    public bool RoadKill { get; set; }  // 로드킬
    public bool Etc { get; set; }       // 기타

    public string? Feature { get; set; }
    public string? Note { get; set; }
}
