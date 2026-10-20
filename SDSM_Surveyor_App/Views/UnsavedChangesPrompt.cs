using System.Windows;
using SDSM_Surveyor_App.Data;

namespace SDSM_Surveyor_App.Views;

/// <summary>
/// 저장 안 한 변경 확인(B-5). 창 닫기와, 지금 화면을 덮어쓰는 자료함 동작
/// ([불러오기]·[지점 복사]·[차수 복제])이 같은 확인을 쓴다.
///
/// 바꾼 것이 없으면 묻지 않는다 — 비교는 <see cref="ISessionService.HasUnsavedChanges"/> 의 스냅샷이 한다.
/// </summary>
public static class UnsavedChangesPrompt
{
    public const string Question = "현재 입력 사항을 저장할까요?";

    /// <summary>
    /// 묻고, [예]면 저장한다.
    /// </summary>
    /// <param name="consequence">질문 아래에 붙이는 한 줄(무엇이 바뀌는지). 닫기는 null.</param>
    /// <returns>
    /// 하던 동작을 계속해도 되면 true — 바뀐 것이 없거나, [아니요], 또는 [예]로 저장에 성공.
    /// 멈춰야 하면 false — [취소], 저장 키(대분류·연도차수·지점)가 비었거나, 저장 실패.
    /// </returns>
    public static async Task<bool> ConfirmAsync(Window owner, ISessionService sessions, string? consequence = null)
    {
        if (!sessions.HasUnsavedChanges()) return true;

        var text = consequence is null ? Question : $"{Question}\n{consequence}";
        var answer = MessageBox.Show(owner, text, "저장 확인",
                                     MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (answer == MessageBoxResult.No) return true;
        if (answer != MessageBoxResult.Yes) return false;

        // 저장 키가 비어도 SaveCurrentAsync 는 '무제_…' 로 저장해 버린다(다른 무제 세션을 덮어쓸 수 있다).
        // 여기서는 저장하기 전에 무엇이 필요한지 알리고 멈춘다.
        var missing = sessions.MissingKeyFields();
        if (missing.Count > 0)
        {
            MessageBox.Show(owner,
                $"저장하려면 조사개황의 대분류·연도차수·지점이 있어야 합니다.\n비어 있는 칸: {string.Join(" · ", missing)}\n\n" +
                "칸을 채운 뒤 다시 하세요. 저장하지 않고 진행하려면 [아니요]를 고르세요.",
                "저장할 수 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        try
        {
            // 같은 키 세션이 있어 [취소]를 고르면 저장하지 않았다 — 하던 동작도 멈춘다(문제 1)
            if (await sessions.SaveCurrentAsync() is null) return false;
            if (sessions.LastSaveNotice is { } notice)
                MessageBox.Show(owner, notice, "저장", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"저장하지 못했습니다 — {ex.Message}\n지금 화면은 그대로 둡니다.",
                            "저장 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}
