using System.Windows;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 일괄입력용에서 빠지는 종(개체수 0·빈칸) — 2026-10-20 결정(문제 5).
/// 어류·저서 일괄입력용은 관측된 종(개체수 &gt; 0)만 낸다(규칙 유지). 빈칸은 입력 누락일 수 있으므로 내보내기 전에 묻는다.
/// 관찰형 3종은 빈칸·0 도 내보낸다(D3) — 여기 해당하지 않는다.
/// </summary>
public static class BulkSkippedSpecies
{
    /// <summary>`국명 0` · `국명 빈칸` 목록(입력 순서).</summary>
    public static List<string> Of(FishEntryViewModel vm) =>
        vm.SpeciesEntries
          .Select(e => (Ko: (e.SelectedSpecies?.SpeciesKo ?? e.SpeciesKo)?.Trim(), Count: (double?)e.IndividualCount))
          .Where(x => !string.IsNullOrEmpty(x.Ko) && (x.Count ?? 0) <= 0)
          .Select(Describe).ToList();

    public static List<string> Of(BenthosEntryViewModel vm) =>
        vm.SpeciesEntries
          .Select(e => (Ko: (e.SelectedSpecies?.SpeciesKo ?? e.SpeciesKo)?.Trim(), Count: e.IndividualCount))
          .Where(x => !string.IsNullOrEmpty(x.Ko) && (x.Count ?? 0) <= 0)
          .Select(Describe).ToList();

    private static string Describe((string? Ko, double? Count) x) => x.Count is null ? $"{x.Ko} 빈칸" : $"{x.Ko} {x.Count:0.###}";

    /// <summary>질문 글 — `내보내지 않는 종 n건(종명…) — 계속할까요?`.</summary>
    public static string Question(IReadOnlyList<string> skipped, int show = 8) =>
        $"내보내지 않는 종 {skipped.Count}건({string.Join(" · ", skipped.Take(show))}{(skipped.Count > show ? $" 외 {skipped.Count - show}건" : "")}) — 계속할까요?\n\n" +
        "일괄입력용에는 개체수가 1 이상인 종만 나갑니다. 빈칸은 입력을 빠뜨린 것일 수 있습니다 — [취소]를 누르고 개체수를 넣을 수 있습니다.";

    /// <summary>지점 하나 내보내기 — 빠지는 종이 있으면 묻는다([확인] 계속 · [취소] 멈춤). 계속하면 true.</summary>
    public static bool Confirm(IReadOnlyList<string> skipped)
    {
        if (skipped.Count == 0) return true;
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        return Ask(owner, Question(skipped));
    }

    /// <summary>[확인]/[취소] 로 묻는다(Esc = 취소). 여러 지점 일괄도 같은 상자를 쓴다.</summary>
    public static bool Ask(Window? owner, string text) =>
        (owner is null
            ? MessageBox.Show(text, "내보내지 않는 종", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.OK)
            : MessageBox.Show(owner, text, "내보내지 않는 종", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.OK))
        == MessageBoxResult.OK;
}
