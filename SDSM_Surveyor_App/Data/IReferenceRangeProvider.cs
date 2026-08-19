using SDSM_Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>기준자료(종별 과거 개체수 범위) 제공.</summary>
public interface IReferenceRangeProvider
{
    string? Version { get; }
    SpeciesRange? GetFishRange(string? speciesKo);
    SpeciesRange? GetBenthosRange(string? speciesKo);
}
