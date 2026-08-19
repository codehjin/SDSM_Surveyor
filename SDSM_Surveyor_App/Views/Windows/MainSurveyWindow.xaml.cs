using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>조사자 메인 입력 창 (WindowChrome + 분류군 탭).</summary>
public partial class MainSurveyWindow : Window, ISingletonService
{
    public MainSurveyWindow()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<MainSurveyWindowViewModel>();
        StateChanged += (_, _) => HandleMaximized();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState != WindowState.Normal ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // 동기화 창(교환소 폴더 · 버전 비교 · 가져오기)
    private void Sync_Click(object sender, RoutedEventArgs e)
        => new SyncWindow { Owner = this }.ShowDialog();

    // 최대화 시 WindowStyle 유지 (design.md §5.1)
    private void HandleMaximized()
    {
        if (WindowState == WindowState.Maximized)
            WindowStyle = WindowStyle.SingleBorderWindow;
    }
}
