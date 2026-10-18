using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 조사 세션 = 대분류 + 연도차수 + 지점 하나. 공통 조사개황과 7개 분류군 자료를 함께 보관한다.
/// (04_FEATURE_SITE_SESSION §B-2) 파일 한 개 = 세션 한 개(`sessions\{SessionId}.json`).
/// </summary>
public sealed class SurveySession
{
    /// <summary>파일명으로 쓰는 키. <see cref="MakeId"/> 로 만든다.</summary>
    public string SessionId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>공통 조사개황.</summary>
    public SurveyMetaSnapshot Meta { get; set; } = new();

    /// <summary>분류군키(Fish·Benthos·Bird…) → 그 분류군의 화면 상태.</summary>
    public Dictionary<string, JsonElement> Taxa { get; set; } = new();

    /// <summary>대분류·연도차수·지점을 파일명 안전 문자로 이어붙인 키.</summary>
    public static string MakeId(string? project, string? yearChsu, string? site)
    {
        static string Part(string? s)
        {
            var v = (s ?? "").Trim();
            if (v.Length == 0) return "무제";
            foreach (var c in Path.GetInvalidFileNameChars()) v = v.Replace(c, '_');
            return v.Replace(' ', '_');
        }
        return $"{Part(project)}_{Part(yearChsu)}_{Part(site)}";
    }
}

/// <summary>조사개황 스냅샷(ViewModel과 분리된 순수 값).</summary>
public sealed class SurveyMetaSnapshot
{
    public string? Project { get; set; }
    public string? SurveyYear { get; set; }
    public string? YearChsu { get; set; }
    public DateTime? SurveyDate { get; set; }
    public string? MajorRegion { get; set; }
    public string? MiddleRegion { get; set; }
    public string? River { get; set; }
    public string? RiverType { get; set; }
    /// <summary>하천유형(수질·서식수변) — 2026-10-18 결정 E1 로 더한 칸(세션 형식 원칙의 유일한 예외 · 추가만).
    /// 이 칸이 없는 예전 세션은 <see cref="RiverType"/> 이 본류/지류/기타면 여기로 옮겨 읽는다.</summary>
    public string? RiverTypeWater { get; set; }
    public string? Workplace { get; set; }
    public string? Site { get; set; }
    public string? Lat { get; set; }
    public string? Lng { get; set; }
    public string? Weather { get; set; }
    public string? SurveyAgency { get; set; }
    public string? Surveyor { get; set; }

    public static SurveyMetaSnapshot From(SurveyMeta m) => new()
    {
        Project = m.Project, SurveyYear = m.SurveyYear, YearChsu = m.YearChsu, SurveyDate = m.SurveyDate,
        MajorRegion = m.MajorRegion, MiddleRegion = m.MiddleRegion, River = m.River, RiverType = m.RiverType, RiverTypeWater = m.RiverTypeWater,
        Workplace = m.Workplace, Site = m.Site, Lat = m.Lat, Lng = m.Lng, Weather = m.Weather,
        SurveyAgency = m.SurveyAgency, Surveyor = m.Surveyor
    };

    /// <summary>스냅샷을 화면 조사개황에 되돌린다.</summary>
    public void ApplyTo(SurveyMeta m)
    {
        // ⚠ 대량 적용으로 감싼다. 감싸지 않으면 여기서 세팅되는 6개 값이
        //    '사람이 고쳤다' 로 오인되어 저장된 옛 세션 값이 고정값을 덮어쓴다.
        m.BeginBulkApply();
        try
        {
        m.Project = Project;                 // 대분류가 먼저여야 지점 목록이 그 대분류로 걸러진다
        m.SurveyYear = SurveyYear;
        m.YearChsu = YearChsu ?? string.Empty;   // 화면 값은 non-null 문자열
        m.SurveyDate = SurveyDate;
        m.MajorRegion = MajorRegion;
        m.MiddleRegion = MiddleRegion;
        // E1 : 예전 세션(하천유형 한 칸) — 값이 본류/지류/기타면 수질·서식수변 칸으로 옮기고, 그 밖은 원래 칸에 둔다(지우는 값 없음)
        var fishType = RiverType; var waterType = RiverTypeWater;
        if (waterType is null && fishType is not null && FormChoices.RiverTypeWater.Any(c => c.Value == fishType.Trim()))
        {
            waterType = fishType.Trim();
            fishType = null;
        }
        m.RiverType = fishType;
        m.RiverTypeWater = waterType;
        m.Weather = Weather;
        m.SurveyAgency = SurveyAgency;
        m.Surveyor = Surveyor;

        // 지점을 마스터에서 되찾으면 하천·사업장·좌표가 따라 채워진다.
        // 못 찾으면(마스터에서 빠진 옛 지점 · 생태현황 목록 밖 지점) 저장된 값을 그대로 되살린다.
        m.SelectedSite = null;
        m.ResolveSiteText(Site);
        if (m.SelectedSite is null)
        {
            m.Site = Site;
            m.River = River;
            m.Workplace = Workplace;
            m.Lat = Lat;
            m.Lng = Lng;
        }
        else
        {
            // D4 : 마스터에 비어 있는 칸(생태현황 조류·포유류 지점의 좌표 · 사업장)은 조사자가 적어 저장한 값을 되살린다.
            var site = m.SelectedSite;
            if (string.IsNullOrWhiteSpace(site.River)) m.River = River;
            if (string.IsNullOrWhiteSpace(site.Workplace)) m.Workplace = Workplace;
            if (site.Lat is null) m.Lat = Lat;
            if (site.Lng is null) m.Lng = Lng;
        }
        }
        finally { m.EndBulkApply(); }
    }
}

/// <summary>자료함 목록 한 줄(`index.json`). 세션 본문을 열지 않고도 목록을 그린다.</summary>
public sealed class SessionIndexEntry
{
    public string SessionId { get; set; } = string.Empty;
    public string? Project { get; set; }
    public string? YearChsu { get; set; }
    public string? Site { get; set; }
    public string? River { get; set; }
    public string? Workplace { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>자료가 들어 있는 분류군 이름(어류·조류…).</summary>
    public List<string> Taxa { get; set; } = new();

    /// <summary>확인이 필요한 항목 수(미등록 지점·연도차수 누락 등).</summary>
    public int Warnings { get; set; }

    [JsonIgnore] public string TaxaText => Taxa.Count == 0 ? "-" : string.Join(", ", Taxa);
    [JsonIgnore] public string UpdatedText => UpdatedAt.ToString("yyyy-MM-dd HH:mm");
}
