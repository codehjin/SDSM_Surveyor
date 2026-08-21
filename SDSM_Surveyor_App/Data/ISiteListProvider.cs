using SDSM_Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>조사지점 마스터(sites.json) 제공자. 조사자는 등록된 지점만 고를 수 있다.</summary>
public interface ISiteListProvider
{
    /// <summary>지점 마스터 버전(없으면 null).</summary>
    string? Version { get; }

    /// <summary>전체 지점.</summary>
    IReadOnlyList<SurveySite> All { get; }

    /// <summary>대분류(방류하천/생태현황)로 거른 지점 목록. 대분류가 비면 전체.</summary>
    List<SurveySite> ByProject(string? project);

    /// <summary>지점명·조사장소 번호(ST1·St.1·st 1)·연도별 표기로 지점을 찾는다. 못 찾으면 null.</summary>
    SurveySite? Resolve(string? input, string? project = null);
}
