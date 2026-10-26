using System.Text.Json;
using SDSM_Surveyor_App.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>수질 탭의 세션 저장·복원. 측정값은 원문 문자열 그대로 오간다.</summary>
public partial class WaterQualityEntryViewModel : ITaxonSession
{
    string ITaxonSession.Key => TaxonKey;

    /// <summary>측정 항목 23종. HasData 와 EntryCount 가 같은 기준을 보도록 한 곳에 둔다.</summary>
    private string?[] MeasuredValues => new[]
    {
        PH, Bod, Cod, Toc, Ss, Dox, Tp, EColi, Ecotoxicity,
        TN, EC, Cl, SO42, Cu, Zn, Cr, Turbidity, Chla,
        WaterTemperature, WaterDepth, FlowVelocity, FlowSec, FlowDay
    };

    bool ITaxonSession.HasData => MeasuredValues.Any(v => !string.IsNullOrWhiteSpace(v));

    int ITaxonSession.EntryCount => MeasuredValues.Count(v => !string.IsNullOrWhiteSpace(v));

    object ITaxonSession.CaptureState() => new WaterQualityDraft
    {
        PH = PH, Bod = Bod, Cod = Cod, Toc = Toc, Ss = Ss, Dox = Dox, Tp = Tp,
        EColi = EColi, Ecotoxicity = Ecotoxicity,
        TN = TN, EC = EC, Cl = Cl, SO42 = SO42, Cu = Cu, Zn = Zn, Cr = Cr,
        Turbidity = Turbidity, Chla = Chla,
        WaterTemperature = WaterTemperature, WaterDepth = WaterDepth,
        FlowVelocity = FlowVelocity, FlowSec = FlowSec, FlowDay = FlowDay,
        Note = Note,
        IsGradeExpanded = IsGradeExpanded, IsExtraExpanded = IsExtraExpanded
    };

    void ITaxonSession.RestoreState(JsonElement json)
    {
        var d = json.Deserialize<WaterQualityDraft>(SessionJson.Options);
        if (d is null) return;

        PH = d.PH; Bod = d.Bod; Cod = d.Cod; Toc = d.Toc; Ss = d.Ss; Dox = d.Dox; Tp = d.Tp;
        EColi = d.EColi; Ecotoxicity = d.Ecotoxicity;
        TN = d.TN; EC = d.EC; Cl = d.Cl; SO42 = d.SO42; Cu = d.Cu; Zn = d.Zn; Cr = d.Cr;
        Turbidity = d.Turbidity; Chla = d.Chla;
        WaterTemperature = d.WaterTemperature; WaterDepth = d.WaterDepth;
        FlowVelocity = d.FlowVelocity; FlowSec = d.FlowSec; FlowDay = d.FlowDay;
        Note = d.Note;   // §24-4 칸 — 예전 세션은 없음(null)
        IsGradeExpanded = d.IsGradeExpanded; IsExtraExpanded = d.IsExtraExpanded;
    }

    void ITaxonSession.ClearData()
    {
        PH = Bod = Cod = Toc = Ss = Dox = Tp = EColi = Ecotoxicity = null;
        TN = EC = Cl = SO42 = Cu = Zn = Cr = Turbidity = Chla = null;
        WaterTemperature = WaterDepth = FlowVelocity = FlowSec = FlowDay = null;
        Note = null;
    }
}

/// <summary>수질 세션 자료(등급 8종 + 추가항목 15종). 등급은 자동계산이라 저장하지 않는다.</summary>
public sealed class WaterQualityDraft
{
    public string? PH { get; init; }
    public string? Bod { get; init; }
    public string? Cod { get; init; }
    public string? Toc { get; init; }
    public string? Ss { get; init; }
    public string? Dox { get; init; }
    public string? Tp { get; init; }
    public string? EColi { get; init; }
    public string? Ecotoxicity { get; init; }
    public string? TN { get; init; }
    public string? EC { get; init; }
    public string? Cl { get; init; }
    public string? SO42 { get; init; }
    public string? Cu { get; init; }
    public string? Zn { get; init; }
    public string? Cr { get; init; }
    public string? Turbidity { get; init; }
    public string? Chla { get; init; }
    public string? WaterTemperature { get; init; }
    public string? WaterDepth { get; init; }
    public string? FlowVelocity { get; init; }
    public string? FlowSec { get; init; }
    /// <summary>비고(특이사항) — 검출한계 미만 'ND' · '&lt;값' 등(§24-4 · 2026-10-27 추가만 · 예전 세션은 없음).</summary>
    public string? Note { get; init; }
    public string? FlowDay { get; init; }

    // 접힘 상태 — 다음 실행에도 유지된다(README §상태). 자료가 아니므로 HasData 에는 넣지 않는다.
    public bool IsGradeExpanded { get; init; }
    public bool IsExtraExpanded { get; init; }
}
