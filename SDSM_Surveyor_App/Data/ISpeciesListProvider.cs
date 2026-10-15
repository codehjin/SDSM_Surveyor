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

    /// <summary>어류 공식 종목록.</summary>
    List<FishSpeciesList> GetFishSpecies();

    /// <summary>저서동물 공식 종목록.</summary>
    List<BenthosSpeciesList> GetBenthosSpecies();

    // 관찰형(조류·포유류·양서파충류) 공식 종목록(국가생물종목록) — 국명·학명·목·과·보호종
    List<ObservedSpecies> GetBirdSpecies();
    List<ObservedSpecies> GetMammalSpecies();
    List<ObservedSpecies> GetAmphibianSpecies();
}
