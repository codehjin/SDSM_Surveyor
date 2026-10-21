using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
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

    int ITaxonSession.EntryCount =>
        SpeciesEntries.Count(r => !string.IsNullOrWhiteSpace(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo));

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
        IsEnvironmentExpanded = IsEnvironmentExpanded, IsHealthExpanded = IsHealthExpanded,
        Rows = SpeciesEntries.Select(r => new BenthosDraftRow(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo,
                                                              r.IndividualCount, r.Note)).ToList()
    };

    object ITaxonSession.CaptureState() => ToDraft();

    /// <summary>마지막 불러오기에서 예전 암반·콘크리트를 특이사항으로 옮겼는가(§20 · G2).</summary>
    private bool _restoreAdjusted;
    bool ITaxonSession.RestoreAdjusted => _restoreAdjusted;

    void ITaxonSession.RestoreState(JsonElement json)
    {
        _restoreAdjusted = false;
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
        IsEnvironmentExpanded = d.IsEnvironmentExpanded; IsHealthExpanded = d.IsHealthExpanded;

        // §20(2026-10-21) : 저서 하상은 지침 6칸 — 예전 세션의 암반·콘크리트 값(0 보다 큼)은 특이사항으로 옮기고 칸은 비운다.
        // 세션 파일의 칸(Bedrock·Concrete)은 지우지 않는다(읽기 호환). 저장은 조사자가 한다 —
        // 옮겼으면 불러온 직후를 '저장 안 한 변경'으로 두어 닫기·다른 세션으로 넘어갈 때 묻게 한다(G2 · SessionService.LoadAsync).
        _restoreAdjusted = MoveRockConcreteToNote();
        if (_restoreAdjusted)
            WeakReferenceMessenger.Default.Send(new Messengers.NotifyMessage((
                "저서 하상 암반·콘크리트 값을 특이사항으로 옮겼습니다(지침 저서 하상 6칸 밖) — [임시 저장]을 누르면 반영됩니다", false)));

        SpeciesEntries.Clear();
        foreach (var r in d.Rows)
        {
            if (string.IsNullOrWhiteSpace(r.SpeciesKo)) continue;
            var e = new BenthosSpeciesEntry { Note = r.Note };
            var match = SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == r.SpeciesKo);
            if (match is not null) e.SelectedSpecies = match;   // 오탁치·지표가중치 자동 연결
            else e.SpeciesKo = r.SpeciesKo;
            e.IndividualCount = r.IndividualCount;
            SpeciesEntries.Add(e);
        }
    }

    void ITaxonSession.ClearData()
    {
        _restoreAdjusted = false;
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
