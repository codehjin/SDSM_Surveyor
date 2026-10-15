namespace SDSM_Surveyor_App.Models;

/// <summary>조사자 앱 로컬 설정(%AppData%\SDSM_Surveyor\settings.json).</summary>
public class AppSettings
{
    /// <summary>교환소 폴더: 관리자가 species.json·reference.json을 올려두고, 조사자 내보내기가 쌓이는 공유/클라우드 폴더.</summary>
    public string? ExchangeFolder { get; set; }

    /// <summary>
    /// 담당 분류군(<see cref="TaxonKind"/> 이름). 첫 실행 화면에서 정해진다.
    /// 이 값이 정해지면 상단 분류군 탭 행을 표시하지 않고 그 분류군 야장만 연다.
    /// ⚠ **표시만 좁히는 값이다.** 세션은 그대로 7개 분류군을 함께 담으므로
    ///   분류군을 바꿔도 입력 자료는 남는다(README §상태).
    /// </summary>
    public string? SelectedTaxon { get; set; }

    /// <summary>켜져 있으면 다음 실행에서 분류군 선택 화면을 건너뛴다.</summary>
    public bool RememberTaxonOnStartup { get; set; }

    /// <summary>
    /// 조사개황 값 고정(압정) — 대분류·연도차수·조사일·날씨·조사자·조사기관 6개.
    /// 옛 settings.json 에 이 항목이 없어도 기본 인스턴스가 그대로 쓰이므로 하위호환은 자동이다.
    /// </summary>
    public PinnedMetaSettings PinnedMeta { get; set; } = new();
}
