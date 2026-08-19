namespace SDSM_Surveyor_App.Models;

/// <summary>조사자 앱 로컬 설정(%AppData%\SDSM_Surveyor\settings.json).</summary>
public class AppSettings
{
    /// <summary>교환소 폴더: 관리자가 species.json·reference.json을 올려두고, 조사자 내보내기가 쌓이는 공유/클라우드 폴더.</summary>
    public string? ExchangeFolder { get; set; }
}
