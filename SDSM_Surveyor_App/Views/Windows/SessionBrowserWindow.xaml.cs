using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
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

        _vm.LoadCompleted += (_, _) => Close();

        Loaded += async (_, _) => await _vm.RefreshAsync();
    }

    // 행 더블클릭 → 불러오기(그리드 빈 영역 더블클릭은 무시)
    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.LoadSessionCommand.CanExecute(null)) _vm.LoadSessionCommand.Execute(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
