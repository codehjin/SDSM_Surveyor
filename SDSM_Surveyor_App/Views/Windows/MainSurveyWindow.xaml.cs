using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
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
        Loaded += (_, _) => MigrateLegacyDrafts();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState != WindowState.Normal ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // 동기화 창(교환소 폴더 · 버전 비교 · 가져오기)
    private void Sync_Click(object sender, RoutedEventArgs e)
        => new SyncWindow { Owner = this }.ShowDialog();

    // 자료함(조사 세션 목록 · 불러오기 · 복제 · 삭제)
    private void Sessions_Click(object sender, RoutedEventArgs e)
        => new SessionBrowserWindow { Owner = this }.ShowDialog();

    /// <summary>구버전 단일 슬롯 임시저장(drafts\*.json)을 세션 하나로 편입한다(최초 1회).</summary>
    private async void MigrateLegacyDrafts()
    {
        try
        {
            var sessions = App.Current.Services.GetRequiredService<ISessionService>();
            var moved = await sessions.MigrateLegacyDraftsAsync();
            if (moved > 0)
                WeakReferenceMessenger.Default.Send(new NotifyMessage(
                    ($"이전 임시저장 {moved}건을 자료함 세션으로 옮겼습니다.", true)));
        }
        catch
        {
            // 편입 실패가 앱 시작을 막아서는 안 된다 — 원본 draft 파일은 그대로 남는다.
        }
    }

    // 최대화 시 WindowStyle 유지 (design.md §5.1)
    private void HandleMaximized()
    {
        if (WindowState == WindowState.Maximized)
            WindowStyle = WindowStyle.SingleBorderWindow;
    }
}
