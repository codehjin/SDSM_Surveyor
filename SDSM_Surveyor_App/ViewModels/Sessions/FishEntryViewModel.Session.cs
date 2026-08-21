using System.Text.Json;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>어류 탭의 세션 저장·복원. 기존 임시저장 DTO(FishDraft)를 그대로 쓴다.</summary>
public partial class FishEntryViewModel : ITaxonSession
{
    string ITaxonSession.Key => TaxonKey;

    bool ITaxonSession.HasData =>
        SpeciesEntries.Any(r => !string.IsNullOrWhiteSpace(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo))
        || NoSpeciesDeclared
        || !string.IsNullOrWhiteSpace(SurveyUnavailableReason);

    /// <summary>화면 상태 → DTO. 임시저장과 같은 값을 쓰므로 두 곳이 어긋나지 않는다.</summary>
    public FishDraft ToDraft() => new()
    {
        SurveyYear = Meta.SurveyYear, YearChsu = Meta.YearChsu, SurveyDate = Meta.SurveyDate,
        MajorRegion = Meta.MajorRegion, MiddleRegion = Meta.MiddleRegion, River = Meta.River, RiverType = Meta.RiverType,
        Site = Meta.Site, Lat = Meta.Lat, Lng = Meta.Lng, Weather = Meta.Weather,
        SurveyAgency = Meta.SurveyAgency, Surveyor = Meta.Surveyor,
        CollectionTime = CollectionTime, CollectionTool = CollectionTool,
        CollectionFlowState = CollectionFlowState, RiverChasu = RiverChasu,
        Bedrock = Bedrock, Concrete = Concrete, Mud = Mud, Sand = Sand,
        FineGravel = FineGravel, Gravel = Gravel, SmallStone = SmallStone, BigStone = BigStone,
        HabitatRiverType = HabitatRiverType, HabitatFlowState = HabitatFlowState,
        SurveyUnavailableReason = SurveyUnavailableReason, Note = Note,
        NoSpeciesDeclared = NoSpeciesDeclared,
        DeCount = DeCount, EfCount = EfCount, LeCount = LeCount, TuCount = TuCount,
        Rows = SpeciesEntries.Select(r => new FishDraftRow(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo,
                                                           r.IndividualCount)).ToList()
    };

    object ITaxonSession.CaptureState() => ToDraft();

    void ITaxonSession.RestoreState(JsonElement json)
    {
        var d = json.Deserialize<FishDraft>(SessionJson.Options);
        if (d is null) return;

        CollectionTime = d.CollectionTime; CollectionTool = d.CollectionTool;
        CollectionFlowState = d.CollectionFlowState; RiverChasu = d.RiverChasu;
        Bedrock = d.Bedrock; Concrete = d.Concrete; Mud = d.Mud; Sand = d.Sand;
        FineGravel = d.FineGravel; Gravel = d.Gravel; SmallStone = d.SmallStone; BigStone = d.BigStone;
        HabitatRiverType = d.HabitatRiverType; HabitatFlowState = d.HabitatFlowState;
        SurveyUnavailableReason = d.SurveyUnavailableReason; Note = d.Note;
        NoSpeciesDeclared = d.NoSpeciesDeclared;
        DeCount = d.DeCount; EfCount = d.EfCount; LeCount = d.LeCount; TuCount = d.TuCount;

        SpeciesEntries.Clear();
        foreach (var r in d.Rows)
        {
            if (string.IsNullOrWhiteSpace(r.SpeciesKo)) continue;
            var e = new FishSpeciesEntry();
            // 공식 종목록에서 다시 찾아 길드·오탁치를 잇는다(지수 계산이 저장본에 의존하지 않게).
            var match = SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == r.SpeciesKo);
            if (match is not null) e.SelectedSpecies = match;
            else e.SpeciesKo = r.SpeciesKo;
            e.IndividualCount = r.IndividualCount;
            SpeciesEntries.Add(e);
        }
    }

    void ITaxonSession.ClearData()
    {
        SpeciesEntries.Clear();
        CollectionTime = CollectionTool = CollectionFlowState = null;
        RiverChasu = null;
        Bedrock = Concrete = Mud = Sand = FineGravel = Gravel = SmallStone = BigStone = null;
        HabitatRiverType = HabitatFlowState = null;
        SurveyUnavailableReason = Note = null;
        NoSpeciesDeclared = false;
        DeCount = EfCount = LeCount = TuCount = null;
    }
}
