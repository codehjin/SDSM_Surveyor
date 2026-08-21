using System.Text.Json;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>조류 탭의 세션 저장·복원.</summary>
public partial class BirdEntryViewModel : ITaxonSession
{
    string ITaxonSession.Key => TaxonKey;

    bool ITaxonSession.HasData => Entries.Any(e => !string.IsNullOrWhiteSpace(e.SpeciesKo));

    object ITaxonSession.CaptureState() => new BirdDraft { Rows = Entries.ToList() };

    void ITaxonSession.RestoreState(JsonElement json)
    {
        var d = json.Deserialize<BirdDraft>(SessionJson.Options);
        if (d is null) return;

        Entries.Clear();
        foreach (var e in d.Rows)
        {
            // 보호종·교란종 표기는 저장본이 아니라 공식 종목록에서 다시 잇는다.
            if (!string.IsNullOrWhiteSpace(e.SpeciesKo))
                e.ApplySpecies(SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == e.SpeciesKo));
            Entries.Add(e);
        }
    }

    void ITaxonSession.ClearData() => Entries.Clear();
}

/// <summary>조류 세션 자료(관찰 행 목록). 구버전 임시저장 파일의 조사개황 항목은 무시된다.</summary>
public sealed class BirdDraft
{
    public List<BirdEntry> Rows { get; init; } = new();
}
