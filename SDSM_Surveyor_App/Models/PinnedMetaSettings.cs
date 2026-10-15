namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 조사개황 값 고정(압정) 설정. <see cref="AppSettings"/> 에 실려 settings.json 으로 저장된다.
///
/// 고정 대상은 <b>6개뿐</b>이다(발주처 요구 · 2026-09-09 확정) —
/// 대분류 · 연도차수 · 조사일 · 날씨 · 조사자 · 조사기관.
/// 지점명·좌표·하천은 지점마다 달라지므로 <b>절대 고정하지 않는다</b>
/// (대분류 격리 규칙 · CLAUDE.md §7).
///
/// 모든 <c>Pin*</c> 기본값이 <c>false</c> 다 — 그래서 <b>조사일은 기본 꺼짐</b>이다.
/// 지난 조사일이 다른 날 자료에 그대로 붙는 사고가 가장 흔하기 때문이다.
///
/// 옛 settings.json 에 이 항목이 없어도 System.Text.Json 이 기본값을 그대로 두므로
/// 하위호환은 자동이다. 다만 파일에 <c>"PinnedMeta": null</c> 이 명시된 경우를 대비해
/// 읽는 쪽에서 <c>??= new()</c> 로 한 번 더 막는다.
/// </summary>
public class PinnedMetaSettings
{
    public bool PinProject { get; set; }
    public bool PinYearChsu { get; set; }
    public bool PinSurveyDate { get; set; }
    public bool PinWeather { get; set; }
    public bool PinSurveyor { get; set; }
    public bool PinSurveyAgency { get; set; }

    public string? Project { get; set; }
    public string? YearChsu { get; set; }
    public DateTime? SurveyDate { get; set; }
    public string? Weather { get; set; }
    public string? Surveyor { get; set; }
    public string? SurveyAgency { get; set; }
}
