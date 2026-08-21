using System.Text.Json;
using SDSM_Surveyor_App.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>
/// 서식·수변환경 탭의 세션 저장·복원.
/// 선택지는 점수만 저장하고, 복원할 때 항목별 선택지 목록에서 같은 점수를 찾아 되돌린다
/// (선택지 문구가 바뀌어도 점수 기준으로 복원되게).
/// </summary>
public partial class HabitatWaterEdgeEntryViewModel : ITaxonSession
{
    string ITaxonSession.Key => TaxonKey;

    bool ITaxonSession.HasData =>
        S1 is not null || B2L is not null || B2R is not null || S3 is not null || S4 is not null ||
        B5L is not null || B5R is not null || B6L is not null || B6R is not null ||
        S7 is not null || S8 is not null || B9L is not null || B9R is not null ||
        B10L is not null || B10R is not null ||
        !string.IsNullOrWhiteSpace(SurveyUnavailableReason);

    object ITaxonSession.CaptureState() => new HabitatWaterEdgeDraft
    {
        SurveyUnavailableReason = SurveyUnavailableReason, Note = Note,
        S1 = S1?.Score, B2L = B2L?.Score, B2R = B2R?.Score, S3 = S3?.Score, S4 = S4?.Score,
        B5L = B5L?.Score, B5R = B5R?.Score, B6L = B6L?.Score, B6R = B6R?.Score,
        S7 = S7?.Score, S8 = S8?.Score, B9L = B9L?.Score, B9R = B9R?.Score,
        B10L = B10L?.Score, B10R = B10R?.Score
    };

    void ITaxonSession.RestoreState(JsonElement json)
    {
        var d = json.Deserialize<HabitatWaterEdgeDraft>(SessionJson.Options);
        if (d is null) return;

        SurveyUnavailableReason = d.SurveyUnavailableReason;
        Note = d.Note;

        S1   = Pick(Opt1,  d.S1);
        B2L  = Pick(Opt2,  d.B2L);   B2R  = Pick(Opt2,  d.B2R);
        S3   = Pick(Opt3,  d.S3);
        S4   = Pick(Opt4,  d.S4);
        B5L  = Pick(Opt5,  d.B5L);   B5R  = Pick(Opt5,  d.B5R);
        B6L  = Pick(Opt6,  d.B6L);   B6R  = Pick(Opt6,  d.B6R);
        S7   = Pick(Opt7,  d.S7);
        S8   = Pick(Opt8,  d.S8);
        B9L  = Pick(Opt9,  d.B9L);   B9R  = Pick(Opt9,  d.B9R);
        B10L = Pick(Opt10, d.B10L);  B10R = Pick(Opt10, d.B10R);
    }

    void ITaxonSession.ClearData()
    {
        SurveyUnavailableReason = Note = null;
        S1 = B2L = B2R = S3 = S4 = B5L = B5R = B6L = B6R = S7 = S8 = B9L = B9R = B10L = B10R = null;
    }

    private static HriOption? Pick(HriOption[] options, double? score)
        => score is double s ? options.FirstOrDefault(o => o.Score == s) : null;
}

/// <summary>서식·수변환경 세션 자료(항목별 선택 점수). 평가점수·등급은 자동계산이라 저장하지 않는다.</summary>
public sealed class HabitatWaterEdgeDraft
{
    public string? SurveyUnavailableReason { get; init; }
    public string? Note { get; init; }
    public double? S1 { get; init; }
    public double? B2L { get; init; }
    public double? B2R { get; init; }
    public double? S3 { get; init; }
    public double? S4 { get; init; }
    public double? B5L { get; init; }
    public double? B5R { get; init; }
    public double? B6L { get; init; }
    public double? B6R { get; init; }
    public double? S7 { get; init; }
    public double? S8 { get; init; }
    public double? B9L { get; init; }
    public double? B9R { get; init; }
    public double? B10L { get; init; }
    public double? B10R { get; init; }
}
