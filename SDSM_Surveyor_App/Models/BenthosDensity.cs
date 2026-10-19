using System.Globalization;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 저서동물 개체밀도(inds./㎡) — 2026-10-19 결정 H1. <b>채집 면적은 이 파일 한 곳에서만 정한다.</b>
///
/// - 채집 면적 = Σ(정량 채집기 면적 × 조사 횟수). Surber 30×30 = 0.09㎡ · Surber 50×50 = 0.25㎡ · 에크만 = 0.0225㎡(15×15cm · 기종 확인 전 기본값).
/// - 드렛지/D-Net 은 면적 기준이 없다. 종별 개체수가 채집기 구분 없이 합쳐져 있으므로 드렛지 횟수가 하나라도 있으면 환산하지 않는다.
/// - 정량 채집기 횟수가 모두 비었거나 0 이면 계산하지 않는다.
/// - 개체밀도 = 실측 개체수 ÷ 채집 면적, 소수 1자리(AwayFromZero). 총 개체밀도는 총 개체수 ÷ 면적 — 반올림한 종별 값을 더하지 않는다.
/// - <b>표시만 한다.</b> 세션·일괄입력용(2026-03 양식)은 실측 개체수 그대로이고, 지수(BMI·DI·H'·R1·J')도 실측 개체수로 계산한다(관리자와 같게).
/// </summary>
public static class BenthosDensity
{
    // ── 면적표(㎡) ─────────────────────────────────────────────────────────
    public const double Surber30Area = 0.09;     // 30×30cm
    public const double Surber50Area = 0.25;     // 50×50cm
    public const double EkmanArea = 0.0225;      // 15×15cm

    public const string Surber30Label = "Surber 30×30";
    public const string Surber50Label = "Surber 50×50";
    public const string EkmanLabel = "에크만";
    public const string DredgeLabel = "드렛지/D-Net";

    /// <summary>드렛지/D-Net 이 섞여 환산하지 않을 때의 문구.</summary>
    public const string DredgeText = "환산 불가(드렛지/D-Net: 면적 기준 없음)";
    /// <summary>정량 채집기 횟수가 없을 때의 문구.</summary>
    public const string NoCountText = "채집 횟수를 넣으면 계산";

    /// <summary>보고서 각주(출현종·건강성평가 시트 공통).</summary>
    public static readonly string[] Footnotes =
    {
        "※ 개체밀도(inds./㎡) = 실측 개체수 ÷ 채집 면적. 채집 면적 = Σ(채집기 면적 × 조사 횟수) — Surber 30×30 0.09㎡ · Surber 50×50 0.25㎡ · 에크만 0.0225㎡(15×15cm 기준).",
        "※ 드렛지/D-Net 은 면적 기준이 없어 환산하지 않는다 — 드렛지/D-Net 이 섞인 조사는 '환산 불가'.",
        "※ 지수(BMI·DI·H'·R1·J')는 실측 개체수로 계산한다(개체밀도와 관계없음).",
    };

    public enum State { Ok, NoCount, Dredge }

    /// <summary>채집 면적 판정. <see cref="Area"/> 는 <see cref="State.Ok"/> 일 때만 값이 있다.</summary>
    public sealed record Sampling(State State, double? Area, string Text)
    {
        public bool CanConvert => State == State.Ok && Area is > 0;

        /// <summary>개체수 → 개체밀도(소수 1자리). 환산할 수 없거나 개체수가 비면 null(0 으로 채우지 않는다).</summary>
        public double? Density(double? count) =>
            CanConvert && count is double c ? Math.Round(c / Area!.Value, 1, MidpointRounding.AwayFromZero) : null;
    }

    /// <summary>조사환경의 채집기 횟수 4칸(글자)으로 채집 면적을 정한다.</summary>
    public static Sampling Of(string? surber30, string? surber50, string? dredge, string? ekman)
    {
        // 드렛지/D-Net — 횟수가 하나라도 있으면(숫자가 아닌 글자가 적혀 있어도) 환산하지 않는다
        if (!string.IsNullOrWhiteSpace(dredge) && (Count(dredge) is not double d || d > 0))
            return new Sampling(State.Dredge, null, DredgeText);

        var parts = new List<(string Label, double Times, double Area)>();
        void Add(string label, string? times, double area)
        {
            if (Count(times) is double t && t > 0) parts.Add((label, t, area));
        }
        Add(Surber30Label, surber30, Surber30Area);
        Add(Surber50Label, surber50, Surber50Area);
        Add(EkmanLabel, ekman, EkmanArea);
        if (parts.Count == 0) return new Sampling(State.NoCount, null, NoCountText);

        var area = Math.Round(parts.Sum(p => p.Times * p.Area), 4, MidpointRounding.AwayFromZero);
        var basis = string.Join(" + ", parts.Select(p => $"{p.Label} × {Num(p.Times)}회")) + $" = {Num(area)}㎡";
        return new Sampling(State.Ok, area, basis);
    }

    private static double? Count(string? v) =>
        double.TryParse(v?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && !double.IsNaN(x) && !double.IsInfinity(x) ? x : null;

    /// <summary>면적·횟수 표시 — 뒤의 0 을 지운다(0.27 · 0.0225 · 3).</summary>
    public static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}
