using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>자료함 창 — 조사 세션 목록 보기·불러오기·삭제·복제.</summary>
public partial class SessionBrowserWindow : Window
{
    private readonly SessionBrowserViewModel _vm;

    public SessionBrowserWindow()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<SessionBrowserViewModel>();
        DataContext = _vm;

        // 삭제는 되돌릴 수 없으므로 반드시 확인을 받는다.
        _vm.ConfirmDelete = e => MessageBox.Show(
            $"'{e.Site} · {e.YearChsu}' 세션을 삭제할까요?\n입력한 7개 분류군 자료가 함께 지워집니다.",
            "세션 삭제", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;

        // 지금 화면을 덮어쓰는 동작([불러오기]·[지점 복사]·[차수 복제]) 전에 저장 안 한 변경을 묻는다(B-5).
        var sessions = App.Current.Services.GetRequiredService<ISessionService>();
        _vm.ConfirmUnsaved = consequence => UnsavedChangesPrompt.ConfirmAsync(this, sessions, consequence);

        // 불러오기가 끝나면 창을 닫는다. 두 호출처가 모두 ShowDialog 라 DialogResult 를 세워도 안전하다 —
        // 첫 실행 화면의 「자료함에서 이어하기」가 이 값으로 '세션을 실제로 열었는지'를 판단한다.
        _vm.LoadCompleted += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        Loaded += async (_, _) => await _vm.RefreshAsync();
    }

    // 행 더블클릭 → 불러오기(그리드 빈 영역 더블클릭은 무시)
    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.LoadSessionCommand.CanExecute(null)) _vm.LoadSessionCommand.Execute(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
