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
    public static (double? score, string? grade) GetBMI(
        IEnumerable<ImportBenthosSpecies> data, string? surveyUnavailableReason)
    {
        if (!string.IsNullOrWhiteSpace(surveyUnavailableReason))
        {
            var reason = surveyUnavailableReason.Replace(" ", "");
            if (UnavailableReasons.Contains(reason)) return (null, "-");
        }
        if (data == null) return (null, null);

        var list = data.ToList();
        var aArr = list.Select(x => double.TryParse(x.SaprobicValue, out var v) ? v : 0.0).ToArray();   // 오탁치 s
        var bArr = list.Select(x => int.TryParse(x.IndicatorWeight, out var v) ? v : 0).ToArray();       // 지표가중치 g
        var cArr = list.Select(x => x.RankScorer).ToArray();                                             // 순위점수 q

        int len = Math.Min(aArr.Length, Math.Min(bArr.Length, cArr.Length));
        if (len == 0) return (null, null);

        double sp3 = 0, sp2 = 0;
        for (int i = 0; i < len; i++)
        {
            sp3 += aArr[i] * bArr[i] * cArr[i];
            sp2 += bArr[i] * cArr[i];
        }
        if (sp2 == 0) return (null, null);

        double value = (4 - (sp3 / sp2)) * 25;
        double rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
        if (double.IsNaN(rounded) || double.IsInfinity(rounded)) return (null, null);

        string grade =
            rounded >= 80 ? "A" :
            rounded >= 65 ? "B" :
            rounded >= 50 ? "C" :
            rounded >= 35 ? "D" : "E";
        return (rounded, grade);
    }
}
