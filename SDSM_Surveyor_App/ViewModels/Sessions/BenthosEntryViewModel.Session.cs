using System.Text.Json;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>저서동물 탭의 세션 저장·복원. 기존 임시저장 DTO(BenthosDraft)를 그대로 쓴다.</summary>
public partial class BenthosEntryViewModel : ITaxonSession
{
    string ITaxonSession.Key => TaxonKey;

    bool ITaxonSession.HasData =>
        SpeciesEntries.Any(r => !string.IsNullOrWhiteSpace(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo))
        || NoSpeciesDeclared
        || !string.IsNullOrWhiteSpace(SurveyUnavailableReason);

    /// <summary>화면 상태 → DTO.</summary>
    public BenthosDraft ToDraft() => new()
    {
        SurveyYear = Meta.SurveyYear, YearChsu = Meta.YearChsu, SurveyDate = Meta.SurveyDate,
        MajorRegion = Meta.MajorRegion, MiddleRegion = Meta.MiddleRegion, River = Meta.River, RiverType = Meta.RiverType,
        Site = Meta.Site, Lat = Meta.Lat, Lng = Meta.Lng, Weather = Meta.Weather,
        SurveyAgency = Meta.SurveyAgency, Surveyor = Meta.Surveyor,
        Surbernet30 = Surbernet30, Surbernet50 = Surbernet50, Dredge = Dredge, Ekman = Ekman,
        Watershed = Watershed, PollutionSource = PollutionSource, CanopyCover = CanopyCover,
        Floodplain = Floodplain, LeveeLeft = LeveeLeft, LeveeRight = LeveeRight,
        Bedrock = Bedrock, Concrete = Concrete, Mud = Mud, Sand = Sand,
        FineGravel = FineGravel, Gravel = Gravel, SmallStone = SmallStone, BigStone = BigStone,
        HabitatRiverType = HabitatRiverType, RiverWidth = RiverWidth, WaterWidth = WaterWidth,
        AverageDepth = AverageDepth, AverageVelocity = AverageVelocity,
        AirTemperature = AirTemperature, WaterTemperature = WaterTemperature,
        FlowState = FlowState, Transparency = Transparency, Smell = Smell,
        SurveyUnavailableReason = SurveyUnavailableReason, Note = Note,
        NoSpeciesDeclared = NoSpeciesDeclared,
        Rows = SpeciesEntries.Select(r => new BenthosDraftRow(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo,
                                                              r.IndividualCount)).ToList()
    };

    object ITaxonSession.CaptureState() => ToDraft();

    void ITaxonSession.RestoreState(JsonElement json)
    {
        var d = json.Deserialize<BenthosDraft>(SessionJson.Options);
        if (d is null) return;

        Surbernet30 = d.Surbernet30; Surbernet50 = d.Surbernet50; Dredge = d.Dredge; Ekman = d.Ekman;
        Watershed = d.Watershed; PollutionSource = d.PollutionSource; CanopyCover = d.CanopyCover;
        Floodplain = d.Floodplain; LeveeLeft = d.LeveeLeft; LeveeRight = d.LeveeRight;
        Bedrock = d.Bedrock; Concrete = d.Concrete; Mud = d.Mud; Sand = d.Sand;
        FineGravel = d.FineGravel; Gravel = d.Gravel; SmallStone = d.SmallStone; BigStone = d.BigStone;
        HabitatRiverType = d.HabitatRiverType; RiverWidth = d.RiverWidth; WaterWidth = d.WaterWidth;
        AverageDepth = d.AverageDepth; AverageVelocity = d.AverageVelocity;
        AirTemperature = d.AirTemperature; WaterTemperature = d.WaterTemperature;
        FlowState = d.FlowState; Transparency = d.Transparency; Smell = d.Smell;
        SurveyUnavailableReason = d.SurveyUnavailableReason; Note = d.Note;
        NoSpeciesDeclared = d.NoSpeciesDeclared;

        SpeciesEntries.Clear();
        foreach (var r in d.Rows)
        {
            if (string.IsNullOrWhiteSpace(r.SpeciesKo)) continue;
            var e = new BenthosSpeciesEntry();
            var match = SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == r.SpeciesKo);
            if (match is not null) e.SelectedSpecies = match;   // 오탁치·지표가중치 자동 연결
            else e.SpeciesKo = r.SpeciesKo;
            e.IndividualCount = r.IndividualCount;
            SpeciesEntries.Add(e);
        }
    }

    void ITaxonSession.ClearData()
    {
        SpeciesEntries.Clear();
        Surbernet30 = Surbernet50 = Dredge = Ekman = null;
        Watershed = PollutionSource = CanopyCover = Floodplain = LeveeLeft = LeveeRight = null;
        Bedrock = Concrete = Mud = Sand = FineGravel = Gravel = SmallStone = BigStone = null;
        HabitatRiverType = RiverWidth = WaterWidth = AverageDepth = AverageVelocity = null;
        AirTemperature = WaterTemperature = FlowState = Transparency = Smell = null;
        SurveyUnavailableReason = Note = null;
        NoSpeciesDeclared = false;
    }
}
