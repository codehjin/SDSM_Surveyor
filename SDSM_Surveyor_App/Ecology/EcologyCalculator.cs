using SDSM_Models;

namespace SDSM_Surveyor_App.Ecology;

/// <summary>
/// 어류평가지수(FAI) 산출. 관리자 앱 GB.GetFAI 로직을 그대로 이식하여 결과가 동일하도록 유지한다.
/// (CLAUDE.md §7.2: 결측치 null과 0 구분, MidpointRounding.AwayFromZero, 조사불가 사유 "-")
/// 추후 SDSM_Core로 통합 예정.
/// </summary>
public static class EcologyCalculator
{
    private static readonly string[] UnavailableReasons = { "접근불가", "건천화", "준설", "공사중" };

    /// <summary>FAI 점수와 등급만 반환(실시간 표시용).</summary>
    public static (double? score, string? grade) CalculateFai(
        IEnumerable<ImportFishSpecies> speciesData,
        string? surveyUnavailableReason,
        int abnormalCount,
        int chasu)
    {
        var d = CalculateFaiDetail(speciesData, surveyUnavailableReason, abnormalCount, chasu);
        return (d.Score, d.Grade);
    }

    /// <summary>FAI 계산 과정 전체(메트릭 산출값·M1~M8 점수·총점·등급)를 반환. 보고서 내보내기용.</summary>
    public static FaiResult CalculateFaiDetail(
        IEnumerable<ImportFishSpecies> speciesData,
        string? surveyUnavailableReason,
        int abnormalCount,
        int chasu)
    {
        // 조사불가 사유가 있으면 등급 "-"
        if (!string.IsNullOrWhiteSpace(surveyUnavailableReason))
        {
            var reason = surveyUnavailableReason.Replace(" ", "");
            if (UnavailableReasons.Contains(reason))
                return new FaiResult { Unavailable = true, Grade = "-" };
        }

        var result = new FaiResult { Chasu = chasu };
        if (speciesData is null) return result;
        var list = speciesData.ToList();

        // 0 초과 개체만 유효(결측 null은 0 처리 후 필터)
        int totalSpecies = list.Count(x => x.IndividualCount is > 0);
        int totalIndiv = list.Sum(x => x.IndividualCount) ?? 0;

        int exoticSpecies = list.Count(x => x.Exotic == "O" && x.IndividualCount is > 0);
        int exoticIndiv = list.Where(x => x.Exotic == "O" && x.IndividualCount is > 0).Sum(x => x.IndividualCount ?? 0);

        int domesticSpecies = totalSpecies - exoticSpecies;
        int domesticIndiv = totalIndiv - exoticIndiv;

        int riffleBenthic = list.Count(x => x.HabitatGuild == "RB" && x.IndividualCount is > 0);
        int sensitive = list.Count(x => x.ToleranceGuild == "SS" && x.IndividualCount is > 0);

        int tolerantSum = list.Where(x => x.ToleranceGuild == "TS" && x.IndividualCount is > 0).Sum(x => x.IndividualCount ?? 0);
        double tolerantRatio = totalIndiv != 0 ? (double)tolerantSum / totalIndiv * 100 : 0;

        int omnivoreSum = list.Where(x => x.FeedingGuild == "O" && x.IndividualCount is > 0).Sum(x => x.IndividualCount ?? 0);
        double omnivoreRatio = totalIndiv != 0 ? (double)omnivoreSum / totalIndiv * 100 : 0;

        int insectSum = list.Where(x => x.FeedingGuild == "I" && x.IndividualCount is > 0).Sum(x => x.IndividualCount ?? 0);
        int insectExoticSum = list.Where(x => x.FeedingGuild == "I" && x.Exotic == "O" && x.IndividualCount is > 0).Sum(x => x.IndividualCount ?? 0);
        double insectRatio = totalIndiv != 0 ? (double)(insectSum - insectExoticSum) / totalIndiv * 100.0 : 0;

        // 비정상 개체수 비율 (관리자 로직 그대로 — 정수 나눗셈 유지)
        double abnormalRatio = abnormalCount != 0 ? (abnormalCount / totalIndiv) * 100 : 0;

        result.TotalSpecies = totalSpecies;
        result.TotalIndiv = totalIndiv;
        result.DomesticSpecies = domesticSpecies;
        result.DomesticIndiv = domesticIndiv;
        result.ExoticSpecies = exoticSpecies;
        result.ExoticIndiv = exoticIndiv;
        result.RiffleBenthic = riffleBenthic;
        result.Sensitive = sensitive;
        result.TolerantRatio = tolerantRatio;
        result.OmnivoreRatio = omnivoreRatio;
        result.InsectRatio = insectRatio;
        result.AbnormalRatio = abnormalRatio;

        result.M1 = GetM1(chasu, domesticSpecies);
        result.M2 = GetM2(chasu, riffleBenthic);
        result.M3 = GetM3(chasu, sensitive);
        result.M4 = GetM4(tolerantRatio);
        result.M5 = GetM5(omnivoreRatio);
        result.M6 = GetM6(insectRatio);
        result.M7 = GetM7(chasu, domesticIndiv);
        result.M8 = GetM8(abnormalRatio);

        double sum = result.M1 + result.M2 + result.M3 + result.M4 + result.M5 + result.M6 + result.M7 + result.M8;
        double rounded = Math.Round(sum, 1, MidpointRounding.AwayFromZero);

        if (totalSpecies == 0 || double.IsNaN(rounded) || double.IsInfinity(rounded))
        {
            result.Score = totalSpecies == 0 ? null : rounded;
            result.Grade = totalSpecies == 0 ? null : "E";
            return result;
        }

        result.Score = rounded;
        result.Grade =
            rounded >= 80 ? "A" :
            rounded >= 60 ? "B" :
            rounded >= 40 ? "C" :
            rounded >= 20 ? "D" : "E";
        return result;
    }

    // M1 : 국내종 총 종수
    private static double GetM1(int chasu, int v) => chasu switch
    {
        1 => v <= 1 ? 0 : (v == 2 ? 6.25 : 12.5),
        2 => v <= 2 ? 0 : (v <= 5 ? 6.25 : 12.5),
        3 => v <= 4 ? 0 : (v <= 8 ? 6.25 : 12.5),
        4 => v <= 5 ? 0 : (v <= 11 ? 6.25 : 12.5),
        5 => v <= 7 ? 0 : (v <= 14 ? 6.25 : 12.5),
        6 => v <= 9 ? 0 : (v <= 18 ? 6.25 : 12.5),
        7 => v <= 11 ? 0 : (v <= 22 ? 6.25 : 12.5),
        _ => 0
    };

    // M2 : 여울성 저서종수
    private static double GetM2(int chasu, int v) => chasu switch
    {
        1 => v == 0 ? 0 : (v == 1 ? 6.25 : 12.5),
        2 => v == 0 ? 0 : (v <= 2 ? 6.25 : 12.5),
        3 => v == 0 ? 0 : (v <= 2 ? 6.25 : 12.5),
        4 => v <= 1 ? 0 : (v <= 3 ? 6.25 : 12.5),
        5 => v == 0 ? 0 : (v <= 2 ? 6.25 : 12.5),
        6 => v == 0 ? 0 : (v <= 2 ? 6.25 : 12.5),
        7 => v == 0 ? 0 : (v == 1 ? 6.25 : 12.5),
        _ => 0
    };

    // M3 : 민감종수
    private static double GetM3(int chasu, int v) => chasu switch
    {
        1 => v == 0 ? 0 : (v <= 2 ? 6.25 : 12.5),
        2 => v == 0 ? 0 : (v <= 3 ? 6.25 : 12.5),
        3 => v <= 1 ? 0 : (v <= 4 ? 6.25 : 12.5),
        4 => v <= 1 ? 0 : (v <= 4 ? 6.25 : 12.5),
        5 => v <= 1 ? 0 : (v <= 4 ? 6.25 : 12.5),
        6 => v == 0 ? 0 : (v <= 3 ? 6.25 : 12.5),
        7 => v == 0 ? 0 : (v <= 2 ? 6.25 : 12.5),
        _ => 0
    };

    // M4 : 내성종 개체수 비율(%)
    private static double GetM4(double v) => v > 70 ? 0 : (v >= 30 ? 6.25 : 12.5);

    // M5 : 잡식종 개체수 비율(%)
    private static double GetM5(double v) => v > 70 ? 0 : (v >= 30 ? 6.25 : 12.5);

    // M6 : 충식종 개체수 비율(%)
    private static double GetM6(double v) => v < 20 ? 0 : (v <= 45 ? 6.25 : 12.5);

    // M7 : 채집된 국내종 총 개체수
    private static double GetM7(int chasu, int v) => chasu switch
    {
        1 => v <= 10 ? 0 : (v <= 20 ? 6.25 : 12.5),
        2 => v <= 30 ? 0 : (v <= 55 ? 6.25 : 12.5),
        3 => v <= 50 ? 0 : (v <= 90 ? 6.25 : 12.5),
        4 => v <= 60 ? 0 : (v <= 115 ? 6.25 : 12.5),
        5 => v <= 80 ? 0 : (v <= 160 ? 6.25 : 12.5),
        6 => v <= 100 ? 0 : (v <= 200 ? 6.25 : 12.5),
        7 => v <= 120 ? 0 : (v <= 240 ? 6.25 : 12.5),
        _ => 0
    };

    // M8 : 비정상종 개체수 비율(%)
    private static double GetM8(double v) => v > 1 ? 0 : (v > 0 ? 6.25 : 12.5);
}

/// <summary>FAI 계산 과정·결과(보고서 내보내기용). 각 메트릭 산출값과 M1~M8 점수, 총점·등급.</summary>
public sealed class FaiResult
{
    public bool Unavailable { get; set; }
    public int Chasu { get; set; }
    public int TotalSpecies { get; set; }     // 총 출현종수
    public int TotalIndiv { get; set; }       // 총 개체수
    public int DomesticSpecies { get; set; }  // 국내종 종수
    public int DomesticIndiv { get; set; }    // 국내종 개체수
    public int ExoticSpecies { get; set; }    // 외래종 종수
    public int ExoticIndiv { get; set; }      // 외래종 개체수
    public int RiffleBenthic { get; set; }    // 여울성 저서종수
    public int Sensitive { get; set; }        // 민감종수
    public double TolerantRatio { get; set; } // 내성종 개체수비율(%)
    public double OmnivoreRatio { get; set; } // 잡식종 개체수비율(%)
    public double InsectRatio { get; set; }   // 충식종 개체수비율(%)
    public double AbnormalRatio { get; set; } // 비정상종 개체수비율(%)
    public double M1 { get; set; }
    public double M2 { get; set; }
    public double M3 { get; set; }
    public double M4 { get; set; }
    public double M5 { get; set; }
    public double M6 { get; set; }
    public double M7 { get; set; }
    public double M8 { get; set; }
    public double? Score { get; set; }
    public string? Grade { get; set; }
}
