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
        MajorRegion = m.MajorRegion, MiddleRegion = m.MiddleRegion, River = m.River, RiverType = m.RiverType,
        Workplace = m.Workplace, Site = m.Site, Lat = m.Lat, Lng = m.Lng, Weather = m.Weather,
        SurveyAgency = m.SurveyAgency, Surveyor = m.Surveyor
    };

    /// <summary>스냅샷을 화면 조사개황에 되돌린다.</summary>
    public void ApplyTo(SurveyMeta m)
    {
        m.Project = Project;                 // 대분류가 먼저여야 지점 목록이 그 대분류로 걸러진다
        m.SurveyYear = SurveyYear;
        m.YearChsu = YearChsu ?? string.Empty;   // 화면 값은 non-null 문자열
        m.SurveyDate = SurveyDate;
        m.MajorRegion = MajorRegion;
        m.MiddleRegion = MiddleRegion;
        m.RiverType = RiverType;
        m.Weather = Weather;
        m.SurveyAgency = SurveyAgency;
        m.Surveyor = Surveyor;

        // 지점을 마스터에서 되찾으면 하천·사업장·좌표가 따라 채워진다.
        // 못 찾으면(마스터에서 빠진 옛 지점) 저장된 값을 그대로 되살린다.
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
