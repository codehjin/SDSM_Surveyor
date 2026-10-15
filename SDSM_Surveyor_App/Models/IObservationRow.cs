namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 관찰형(조류·포유류·양서파충류) 행의 공통 얼굴.
///
/// 세 모델은 서로 다른 열을 갖지만(조류는 개체수, 포유류·양서파충류는 흔적 12칸)
/// **화면 요약에 필요한 것은 같다** — 종명과 보호종·교란종 여부다.
/// 종전에는 세 화면이 같은 집계를 각자 세고 있었다.
///
/// ⚠ 보호종과 생태계교란생물은 **의미가 반대**다. 한 숫자로 합치지 않는다(design.md §11).
/// </summary>
public interface IObservationRow
{
    string? SpeciesKo { get; }
    bool IsProtected { get; }
    bool IsInvasive { get; }
}

/// <summary>관찰형 3개 화면이 함께 쓰는 요약 집계.</summary>
public static class ObservationSummary
{
    /// <summary>종명이 적힌 행 기준으로 보호종·교란종 종수를 센다(중복 종명은 한 번만).</summary>
    public static (int Protected, int Invasive) Count(IEnumerable<IObservationRow> rows)
    {
        var named = rows.Where(r => !string.IsNullOrWhiteSpace(r.SpeciesKo)).ToList();
        int p = named.Where(r => r.IsProtected).Select(r => r.SpeciesKo).Distinct().Count();
        int i = named.Where(r => r.IsInvasive).Select(r => r.SpeciesKo).Distinct().Count();
        return (p, i);
    }
}
