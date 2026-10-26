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

    int ITaxonSession.EntryCount =>
        SpeciesEntries.Count(r => !string.IsNullOrWhiteSpace(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo));

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
        IsEnvironmentExpanded = IsEnvironmentExpanded, IsHealthExpanded = IsHealthExpanded,
        DeCount = DeCount, EfCount = EfCount, LeCount = LeCount, TuCount = TuCount,
        Rows = SpeciesEntries.Select(r => new FishDraftRow(r.SelectedSpecies?.SpeciesKo ?? r.SpeciesKo,
                                                           r.IndividualCount, r.Note, r.SelectedSpecies?.SpeciesEn)).ToList()
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
        IsEnvironmentExpanded = d.IsEnvironmentExpanded; IsHealthExpanded = d.IsHealthExpanded;
        DeCount = d.DeCount; EfCount = d.EfCount; LeCount = d.LeCount; TuCount = d.TuCount;
        // 문제 7·10 : 저장된 하천차수가 없으면(예전 세션) 지점 값으로 채운다 — 저장된 값이 있으면 그대로
        if (RiverChasu is null) PrefillRiverChasu(Meta.SelectedSite);

        SpeciesEntries.Clear();
        foreach (var r in d.Rows)
        {
            if (string.IsNullOrWhiteSpace(r.SpeciesKo)) continue;
            var e = new FishSpeciesEntry();
            // 공식 종목록에서 다시 찾아 길드·오탁치를 잇는다(지수 계산이 저장본에 의존하지 않게).
            // v0.5.0 : 학명(옛 표기는 지침 표기로 읽음)이 있으면 국명 + 학명으로, 없으면 국명으로(Helpers.SpeciesLookup).
            var match = Helpers.SpeciesLookup.Fish(SpeciesListSource, r.SpeciesKo, r.SpeciesEn);
            if (match is not null) e.SelectedSpecies = match;
            else e.SpeciesKo = r.SpeciesKo;
            e.IndividualCount = r.IndividualCount;
            e.Note = r.Note;
            SpeciesEntries.Add(e);
        }
    }

    void ITaxonSession.ClearData()
    {
        SpeciesEntries.Clear();
        CollectionTime = CollectionTool = CollectionFlowState = null;
        RiverChasu = null;
        PrefillRiverChasu(Meta.SelectedSite);   // 하천차수는 지점 고정값 — 빈 화면에도 지점 값을 미리 채운다(문제 7·10)
        Bedrock = Concrete = Mud = Sand = FineGravel = Gravel = SmallStone = BigStone = null;
        HabitatRiverType = HabitatFlowState = null;
        SurveyUnavailableReason = Note = null;
        NoSpeciesDeclared = false;
        DeCount = EfCount = LeCount = TuCount = null;
        // 접힘 상태는 화면 설정이지 입력 자료가 아니다 — 지점을 옮겨도 그대로 둔다.
    }
}
