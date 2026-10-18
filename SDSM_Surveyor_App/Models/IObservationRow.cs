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

    /// <summary>국명이 같은 행 수(화면 표시용 · 저장 안 함). <see cref="ObservationSummary.MarkSameSpecies"/> 가 채운다.</summary>
    int SameSpeciesCount { get; set; }

    /// <summary>학명. 종목록에 있는 국명이면 종목록대로(2026-10-18 결정 D2 · <see cref="ScientificNameRule"/>).</summary>
    string? SpeciesEn { get; set; }

    /// <summary>공식 종목록에서 찾은 종(없으면 목록 밖 국명).</summary>
    SDSM_Models.ObservedSpecies? MatchedSpecies { get; }

    /// <summary>개체수(조류)·흔적(포유류·양서파충류)이 모두 비었거나 0 — 일괄입력용에는 그대로 내고 저장 때 알린다(D3).</summary>
    bool HasNoCount { get; }
}

/// <summary>
/// 학명 규칙(2026-10-18 결정 D2) — 종목록에 있는 국명은 학명을 <b>종목록대로</b> 넣는다.
/// 붙여넣기·직접 수정·예전 세션 불러오기에서 다른 학명이 오면 종목록 학명으로 바꾸고 바꾼 건수를 알린다.
/// 목록 밖 국명만 학명을 직접 입력한다. 종목록에 학명이 비어 있는 종은 직접 입력을 막지 않는다.
/// </summary>
public static class ScientificNameRule
{
    /// <summary>그 행에 맞는 종목록 학명. 목록 밖 국명이거나 종목록 학명이 비었으면 null.</summary>
    public static string? CatalogName(IObservationRow r) =>
        string.IsNullOrWhiteSpace(r.MatchedSpecies?.SpeciesEn) ? null : r.MatchedSpecies!.SpeciesEn;

    /// <summary>학명 칸을 잠그는가(종목록 학명이 있는 종).</summary>
    public static bool IsLocked(IObservationRow r) => CatalogName(r) is not null;

    /// <summary>주어진 학명이 종목록 학명과 다른가(비어 있으면 다르지 않다 — 채우는 것이다).</summary>
    public static bool Differs(IObservationRow r, string? given) =>
        CatalogName(r) is string c && !string.IsNullOrWhiteSpace(given) && !string.Equals(given.Trim(), c.Trim(), StringComparison.Ordinal);

    /// <summary>종목록 학명으로 맞춘다. 바꿨으면 true.</summary>
    public static bool Enforce(IObservationRow r)
    {
        if (CatalogName(r) is not string c) return false;
        if (string.Equals(r.SpeciesEn?.Trim(), c.Trim(), StringComparison.Ordinal)) return false;
        r.SpeciesEn = c;
        return true;
    }

    public static string Notice(int n) => $"학명 {n}건을 종목록대로 바꿨습니다";
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

    /// <summary>
    /// 행마다 '국명이 같은 행이 몇 개인지' 를 적는다(B-1). 관찰형은 같은 종 여러 행이 정상일 수 있어
    /// 합치지 않고 표시만 한다 — 위치·흔적이 다른 관찰인지, 실수로 두 번 넣은 것인지는 조사자가 판단한다.
    /// 국명은 <c>Trim()</c> 만 하고 대소문자·띄어쓰기는 그대로 비교한다(종명 정규화 규칙).
    /// </summary>
    public static void MarkSameSpecies(IReadOnlyCollection<IObservationRow> rows)
    {
        var count = rows.Select(r => r.SpeciesKo?.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .GroupBy(k => k!, StringComparer.Ordinal)
                        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var r in rows)
        {
            var key = r.SpeciesKo?.Trim();
            r.SameSpeciesCount = !string.IsNullOrEmpty(key) && count.TryGetValue(key, out var n) ? n : 0;
        }
    }
}
