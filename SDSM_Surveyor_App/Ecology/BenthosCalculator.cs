using SDSM_Models;

namespace SDSM_Surveyor_App.Ecology;

/// <summary>
/// 저서동물 생태지수 산출(우점도 DI·다양도 H'·풍부도 R1·균등도 J'·BMI).
/// 관리자 GB 로직을 그대로 이식하여 결과 동일. (CLAUDE.md §7.2)
/// </summary>
public static class BenthosCalculator
{
    private static readonly string[] UnavailableReasons = { "접근불가", "건천화", "준설", "공사중" };

    /// <summary>개체수 배열에서 특정 값의 순위점수(RankScorer 1~5). 0은 제외.</summary>
    public static int GetRankScorer(int[] range, int value)
    {
        if (value == 0) return 0;
        var valid = range.Where(x => x > 0).ToArray();
        if (valid.Length == 0) return 0;

        int rank = valid.Count(x => x > value) + 1;      // 엑셀 RANK 내림차순
        double percentile = (double)rank / valid.Length;

        if (percentile <= 0.2) return 5;
        if (percentile <= 0.4) return 4;
        if (percentile <= 0.6) return 3;
        if (percentile <= 0.8) return 2;
        return 1;
    }

    /// <summary>우점도(DI): (최대 + 2번째) / 합계. 오류 시 1.</summary>
    public static double GetDI(IEnumerable<ImportBenthosSpecies> data)
    {
        if (data == null) return 1;
        var counts = data.Select(x => x.IndividualCount ?? 0).Where(x => x > 0)
                         .OrderByDescending(x => x).ToList();
        if (counts.Count == 0) return 1;

        double largest = counts.ElementAtOrDefault(0);
        double second = counts.ElementAtOrDefault(1);
        double sum = counts.Sum();
        return sum == 0 ? 1 : (largest + second) / sum;
    }

    /// <summary>Shannon-Wiener 다양도(H').</summary>
    public static double GetH(IEnumerable<ImportBenthosSpecies> data)
    {
        if (data == null) return 0;
        var counts = data.Select(x => x.IndividualCount ?? 0).Where(x => x >= 0).ToArray();
        double sum = counts.Sum();
        if (sum == 0) return 0;

        double sumProduct = counts.Select(x => x * Math.Log(x == 0 ? 1 : x, 2)).Sum();
        double result = -sumProduct / sum + Math.Log(sum, 2);
        return double.IsNaN(result) || double.IsInfinity(result) ? 0 : result;
    }

    /// <summary>종풍부도(R1) = (출현종수-1) / ln(전체개체수).</summary>
    public static double GetR1(IEnumerable<ImportBenthosSpecies> data)
    {
        if (data == null) return 0;
        var counts = data.Select(x => x.IndividualCount ?? 0).Where(x => x > 0).ToList();
        int s = counts.Count;
        double n = counts.Sum();
        if (n <= 0) return 0;

        double result = (s - 1) / Math.Log(n);
        return double.IsNaN(result) || double.IsInfinity(result) ? 0 : result;
    }

    /// <summary>균등도(J') = H' / log2(출현종수).</summary>
    public static double GetJ(IEnumerable<ImportBenthosSpecies> data)
    {
        if (data == null) return 0;
        double h = GetH(data);
        int s = data.Select(x => x.IndividualCount ?? 0).Count(x => x > 0);
        if (s <= 1) return 0;

        double denom = Math.Log(s, 2);
        if (denom == 0) return 0;
        double result = h / denom;
        return double.IsNaN(result) || double.IsInfinity(result) ? 0 : result;
    }

    /// <summary>BMI 점수·등급. 각 종의 RankScorer가 미리 채워져 있어야 함.</summary>
    /// <param name="noSpeciesDeclared">조사 수행 + 출현종 0종 선언 여부. 선언되면 점수 0·등급 E.</param>
    public static (double? score, string? grade) GetBMI(
        IEnumerable<ImportBenthosSpecies> data, string? surveyUnavailableReason,
        bool noSpeciesDeclared = false)
    {
        var d = CalculateBmiDetail(data, surveyUnavailableReason, noSpeciesDeclared);
        return (d.Score, d.Grade);
    }

    /// <summary>
    /// BMI를 계산하면서 **중간 산출값까지 노출**한다(보고서 엑셀의 계산과정 수록용).
    /// 계산식은 <see cref="GetBMI"/>와 하나의 구현을 공유하므로 결과가 갈라지지 않는다.
    /// </summary>
    /// <param name="noSpeciesDeclared">조사 수행 + 출현종 0종 선언 여부. 선언되면 점수 0·등급 E.</param>
    public static BmiResult CalculateBmiDetail(
        IEnumerable<ImportBenthosSpecies> data, string? surveyUnavailableReason,
        bool noSpeciesDeclared = false)
    {
        var r = new BmiResult();
        if (data == null) return r;

        var list = data.ToList();

        // 표시용 집계 : 결측치(null)와 0 엄격 구분 — 0 초과 개체만 유효로 본다.
        var valid = list.Select(x => x.IndividualCount ?? 0).Where(x => x > 0).ToList();
        r.TotalSpecies = valid.Count;
        r.TotalIndiv = valid.Sum();
        r.DI = GetDI(list);
        r.H = GetH(list);
        r.R1 = GetR1(list);
        r.J = GetJ(list);

        // ① 조사불가 사유가 최우선 — 조사를 못 했으면 "0종"이 아니다
        if (!string.IsNullOrWhiteSpace(surveyUnavailableReason))
        {
            var reason = surveyUnavailableReason.Replace(" ", "");
            if (UnavailableReasons.Contains(reason))
            {
                r.Grade = "-";
                return r;
            }
        }

        // ② 조사 수행 + 0종 → 점수 0 · 등급 E (어류와 동일 규칙, 12_CALC_FIX §2-3)
        // ③ 선언이 없으면 "아직 미입력" — 점수·등급을 내지 않는다(화면에서 "-")
        if (r.TotalSpecies == 0)
        {
            if (noSpeciesDeclared)
            {
                r.NoSpeciesDeclared = true;
                r.Score = 0;
                r.Grade = "E";
            }
            return r;
        }

        var aArr = list.Select(x => double.TryParse(x.SaprobicValue, out var v) ? v : 0.0).ToArray();   // 오탁치 s
        var bArr = list.Select(x => int.TryParse(x.IndicatorWeight, out var v) ? v : 0).ToArray();       // 지표가중치 g
        var cArr = list.Select(x => x.RankScorer).ToArray();                                             // 순위점수 h

        int len = Math.Min(aArr.Length, Math.Min(bArr.Length, cArr.Length));
        if (len == 0) return r;

        double sp3 = 0, sp2 = 0;
        for (int i = 0; i < len; i++)
        {
            sp3 += aArr[i] * bArr[i] * cArr[i];
            sp2 += bArr[i] * cArr[i];
        }
        r.SumSGH = sp3;
        r.SumGH = sp2;
        if (sp2 == 0) return r;

        double value = (4 - (sp3 / sp2)) * 25;
        double rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
        if (double.IsNaN(rounded) || double.IsInfinity(rounded)) return r;

        r.Score = rounded;
        // 음수는 정상 범위(오탁치 0~4)를 벗어난 값이라 등급 대신 검토 표시(관리자와 동일, 12_CALC_FIX §3)
        r.Grade =
            rounded >= 80 ? "A" :
            rounded >= 65 ? "B" :
            rounded >= 50 ? "C" :
            rounded >= 35 ? "D" :
            rounded >= 0 ? "E" : "Check";
        return r;
    }
}

/// <summary>저서동물 BMI 상세 결과(계산과정 포함). 보고서 엑셀에 그대로 수록한다.</summary>
public sealed class BmiResult
{
    /// <summary>조사 수행 + 출현종 0종으로 선언된 결과(점수 0·등급 E).</summary>
    public bool NoSpeciesDeclared { get; set; }

    public int TotalSpecies { get; set; }    // 총 출현종수(개체수 > 0)
    public double TotalIndiv { get; set; }   // 총 개체수
    public double DI { get; set; }           // 우점도
    public double H { get; set; }            // 다양도 H'
    public double R1 { get; set; }           // 풍부도 R1
    public double J { get; set; }            // 균등도 J'
    public double SumSGH { get; set; }       // Σ(오탁치 s × 지표가중치 g × 순위점수 h)
    public double SumGH { get; set; }        // Σ(지표가중치 g × 순위점수 h)
    public double? Score { get; set; }       // BMI = (4 − ΣsghΣgh) × 25
    public string? Grade { get; set; }       // A≥80 · B≥65 · C≥50 · D≥35 · E
}
