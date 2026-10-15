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

        // 조사개황 카드는 셸이 소유한다(06_DESIGN_REBUILD §5-1-4). SurveyMeta 는 싱글턴이라
        // 7개 탭이 같은 인스턴스를 본다 — 어느 탭에서 지점을 골라도 나머지에 그대로 보인다.
        // ⚠ 창 DataContext 보다 **먼저** 넣는다. 순서가 반대면 그 사이 한 프레임 동안
        //    MetaCard 하위가 창 VM 을 상속받아 SurveyMeta 전용 바인딩이 전부 오류로 찍힌다.
        MetaCard.DataContext = App.Current.Services.GetRequiredService<Models.SurveyMeta>();

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

    // 지점 지도(최신 지도 + 현재 입력 좌표 마커)
    private void Map_Click(object sender, RoutedEventArgs e)
        => new MapPopupWindow { Owner = this }.ShowDialog();

    /// <summary>구버전 단일 슬롯 임시저장(drafts\*.json)을 세션 하나로 편입한다(최초 1회).</summary>
    private async void MigrateLegacyDrafts()
    {
        try
        {
            var sessions = App.Current.Services.GetRequiredService<ISessionService>();

            // ⚠ 편입이 **먼저**다. 구버전 임시저장이 있는 PC 는 편입으로 세션이 한 건 생기고,
            //    그러면 시드가 스스로 물러난다("자료함이 비어 있을 때만"). 순서를 바꾸면
            //    조사자의 실제 야장과 예시 자료가 섞인다.
            var moved = await sessions.MigrateLegacyDraftsAsync();
            if (moved > 0)
                WeakReferenceMessenger.Default.Send(new NotifyMessage(
                    ($"이전 임시저장 {moved}건을 자료함 세션으로 옮겼습니다.", true)));

            // 테스트 배포본에서만 — 자료함이 비어 있으면 예시 세션을 채운다.
            // ⚠ NotifyMessage 토스트는 아직 받는 곳이 없다(발신 18곳 · 수신 0곳).
            //    테스터가 예시가 들어온 걸 알아야 하므로 여기서는 대화상자로 알린다.
            var seeded = await sessions.SeedTestDataIfEmptyAsync();
            if (seeded > 0)
                MessageBox.Show(this,
                    $"테스트용 예시 자료 {seeded}건을 자료함에 넣었습니다.\n상단 [자료함] 에서 열어 보세요.",
                    "테스트본", MessageBoxButton.OK, MessageBoxImage.Information);
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
