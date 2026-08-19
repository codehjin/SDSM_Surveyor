using SDSM_Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>종 목록(초성 자동완성/검증 기준) 제공자.</summary>
public interface ISpeciesListProvider
{
    /// <summary>어류 공식 종목록.</summary>
    List<FishSpeciesList> GetFishSpecies();

    /// <summary>저서동물 공식 종목록.</summary>
    List<BenthosSpeciesList> GetBenthosSpecies();

    // 관찰형(조류·포유류·양서파충류)은 종명 문자열 목록만 사용
    string[] GetBirdSpecies();
    string[] GetMammalSpecies();
    string[] GetAmphibianSpecies();
}
