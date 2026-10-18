using SDSM_Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>조사지점 마스터(sites.json) 제공자. 조사자는 등록된 지점만 고를 수 있다.</summary>
public interface ISiteListProvider
{
    /// <summary>지점 마스터 버전(없으면 null).</summary>
    string? Version { get; }

    /// <summary>전체 지점.</summary>
    IReadOnlyList<SurveySite> All { get; }

    /// <summary>대분류(방류하천/생태현황)로 거른 지점 목록. 대분류가 비면 빈 목록.
    /// <paramref name="year"/> 를 주면 그 조사연도에 유효한 지점만(ValidFrom~ValidTo · 진위천1 처럼 해마다 다른 장소 · D4).</summary>
    List<SurveySite> ByProject(string? project, int? year = null);

    /// <summary>지점명·조사장소 번호(ST1·St.1·st 1)·연도별 표기로 지점을 찾는다. 못 찾으면 null.
    /// <paramref name="year"/> 를 주면 그 연도에 유효한 지점 안에서만 찾는다.</summary>
    SurveySite? Resolve(string? input, string? project = null, int? year = null);

    /// <summary>
    /// St.번호 형태 입력의 후보 전부(C-7). 둘 이상이면 자동으로 고르지 않고 이 후보를 보여 준다.
    /// St.번호 형태가 아니면 빈 목록.
    /// </summary>
    IReadOnlyList<SurveySite> Candidates(string? input, string? project = null, int? year = null) =>
        SiteOrdering.StCandidates(ByProject(project, year), input);
}
