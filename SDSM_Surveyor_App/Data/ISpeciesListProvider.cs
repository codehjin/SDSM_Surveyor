using SDSM_Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>종 목록(초성 자동완성/검증 기준) 제공자.</summary>
public interface ISpeciesListProvider
{
    /// <summary>
    /// 종목록 카탈로그 버전(species.json 의 <c>Version</c>). 화면에 계산 근거로 표기한다 —
    /// 지수가 관리자와 일치하려면 종목록 버전이 같아야 하기 때문이다(CLAUDE.md §4).
    /// </summary>
    string? Version { get; }

    /// <summary>
    /// 부분(어류 · 저서 · 조류 · 포유류 · 양서파충류)별 판 · 종 수 · 출처(교체본 · 번들) — 정보 창에 보인다(v0.5.0).
    /// 교체본에 어류 · 저서만 있으면 관찰형은 번들에서 온다.
    /// </summary>
    IReadOnlyList<SpeciesListPart> Parts { get; }

    /// <summary>그 부분의 판(Fish · Benthos · Bird · Mammal · Amphibian). 없으면 null.</summary>
    string? PartVersion(string taxonKey);

    /// <summary>어류 공식 종목록.</summary>
    List<FishSpeciesList> GetFishSpecies();

    /// <summary>저서동물 공식 종목록.</summary>
    List<BenthosSpeciesList> GetBenthosSpecies();

    // 관찰형(조류·포유류·양서파충류) 공식 종목록(국가생물종목록) — 국명·학명·목·과·보호종
    List<ObservedSpecies> GetBirdSpecies();
    List<ObservedSpecies> GetMammalSpecies();
    List<ObservedSpecies> GetAmphibianSpecies();
}
