using System.Windows;

namespace SDSM_Surveyor_App.Views;

/// <summary>
/// 되돌릴 수 없는 삭제 확인 — 자료함 [삭제] · 지도 관리 [삭제] 가 같이 쓴다(§32 · S4 · 작업보고 v052 8-4 #1).
/// 기본 단추는 [취소]: Enter · Esc 모두 지우지 않는다(종전에는 기본이 [확인]이라 Enter 한 번에 지워졌다).
/// 문구에 지울 대상 이름 · 건수 · '되돌릴 수 없음' 을 넣는 것은 부르는 쪽(각 ViewModel 의 DeleteConfirmText).
/// </summary>
public static class DeleteConfirm
{
    public static bool Ask(Window owner, string text, string title) =>
        MessageBox.Show(owner, text, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel)
        == MessageBoxResult.OK;
}
