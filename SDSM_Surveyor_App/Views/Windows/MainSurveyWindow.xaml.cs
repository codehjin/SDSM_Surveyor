using System.ComponentModel;
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
        var meta = App.Current.Services.GetRequiredService<Models.SurveyMeta>();
        MetaCard.DataContext = meta;

        DataContext = App.Current.Services.GetRequiredService<MainSurveyWindowViewModel>();

        // C-2 : 입력한 자료가 있는 채로 지점을 다른 등록 지점으로 바꾸면 묻는다(세션 불러오기·복사 중에는 SurveyMeta 가 묻지 않는다).
        Func<SDSM_Models.SurveySite, bool> guard = next =>
        {
            if (!App.Current.Services.GetRequiredService<ISessionService>().HasInputData()) return false;
            // 콤보 선택 처리 안에서 대화창을 띄우지 않는다 — 한 박자 뒤에 묻는다.
            _ = Dispatcher.BeginInvoke(new Action(() => AskSiteChange(meta, next)));
            return true;
        };
        meta.SiteChangeGuard = guard;
        Closed += (_, _) => { if (meta.SiteChangeGuard == guard) meta.SiteChangeGuard = null; };

        // 문제 1(2026-10-20) : 같은 키(대분류·연도차수·지점) 세션이 이미 있으면 덮어쓰지 않고 묻는다
        var sessionService = App.Current.Services.GetRequiredService<ISessionService>();
        Func<SessionSaveConflict, SessionConflictChoice> resolver = AskSaveConflict;
        sessionService.ResolveSaveConflict = resolver;
        Closed += (_, _) => { if (sessionService.ResolveSaveConflict == resolver) sessionService.ResolveSaveConflict = null; };

        StateChanged += (_, _) => HandleMaximized();
        Loaded += (_, _) => MigrateLegacyDrafts();
        Closing += OnClosing;
    }

    /// <summary>
    /// 같은 키 세션 저장 확인(문제 1). 기본은 [합치기] — 자료를 잃지 않는 쪽.
    /// 자료함 창에서 저장할 때도 이 창이 묻는다(앞에 떠 있는 창을 주인으로).
    /// </summary>
    private SessionConflictChoice AskSaveConflict(SessionSaveConflict c)
    {
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? this;
        static string List(IReadOnlyList<string> v) => v.Count == 0 ? "없음" : string.Join(" · ", v);
        var pick = ChoiceDialog.Ask(owner, "같은 자료가 이미 있습니다",
            $"자료함에 같은 대분류·연도차수·지점의 자료가 이미 있습니다 — {c.Display}\n" +
            $"이미 있는 자료: {List(c.ExistingTaxa)} (마지막 저장 {c.ExistingUpdatedAt:yyyy-MM-dd HH:mm})\n" +
            $"지금 화면: {List(c.CurrentTaxa)}\n\n" +
            "· 합치기 — 지금 화면에 자료가 있는 분류군만 새 값으로 바꾸고, 나머지 분류군은 그대로 둡니다.\n" +
            "   조사개황은 지금 값으로 하되 빈칸이면 이미 있는 값을 남깁니다.\n" +
            // N3 : 합치면 생기는 일 — 기존 분류군 조사일 유지 · 바뀌는 조사개황 칸(전 → 후)
            (c.MergeNotes is { Count: > 0 } notes ? $"   합치면: {string.Join(" / ", notes)}\n" : string.Empty) +
            "· 바꾸기 — 이미 있는 자료를 모두 지우고 지금 화면으로 바꿉니다.\n" +
            "· 취소 — 저장하지 않습니다.",
            new ChoiceDialog.Option("취소", IsCancel: true),
            new ChoiceDialog.Option("바꾸기", ToolTip: "이미 있는 분류군 자료가 지워집니다"),
            new ChoiceDialog.Option("합치기", IsPrimary: true, ToolTip: "이미 있는 분류군 자료를 살립니다"));
        return pick switch { 2 => SessionConflictChoice.Merge, 1 => SessionConflictChoice.Replace, _ => SessionConflictChoice.Cancel };
    }

    // 확인을 마치고 다시 닫는 중인가(재진입 방지).
    private bool _closeConfirmed;

    /// <summary>
    /// 닫기 전에 저장 안 한 변경을 묻는다(B-5). 바꾼 것이 없으면 묻지 않는다.
    /// [예] → 닫기를 멈추고 저장 → 성공하면 다시 닫는다 / 실패하면 사유를 알리고 창을 유지한다.
    /// [아니요] → 닫는다. [취소] → 창을 유지한다.
    /// </summary>
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed) return;
        var sessions = App.Current.Services.GetRequiredService<ISessionService>();
        if (!sessions.HasUnsavedChanges()) return;

        // 저장은 비동기라 일단 닫기를 멈춘다. 확인이 끝나면 아래에서 다시 닫는다.
        e.Cancel = true;
        try
        {
            if (!await UnsavedChangesPrompt.ConfirmAsync(this, sessions)) return;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"닫기 전에 확인하지 못했습니다 — {ex.Message}", "닫기",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // ⚠ [아니요]처럼 확인이 이 호출 안에서 바로 끝나면 아직 '닫는 중' 이다 — 여기서 Close() 를 부르면
        //    WPF 가 예외를 던진다. 닫기 처리가 끝난 다음 차례에 다시 닫는다.
        _closeConfirmed = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState != WindowState.Normal ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // 동기화 창(교환소 폴더 · 버전 비교 · 가져오기)
    private void Sync_Click(object sender, RoutedEventArgs e)
        => new SyncWindow { Owner = this }.ShowDialog();

    // 프로그램 정보(판 · 적용 지침 · 종목록 판 · 개발 · 연락처 — v0.5.0)
    private void Info_Click(object sender, RoutedEventArgs e)
        => new AppInfoWindow { Owner = this }.ShowDialog();

    // 사용설명서(§26-3 · v0.5.2) — 정보 창과 같은 방식. 못 열면 까닭을 알림 띠로
    private void Manual_Click(object sender, RoutedEventArgs e)
    {
        if (Helpers.AppInfo.OpenPdf(Helpers.AppInfo.ManualPdfPath, Helpers.AppInfo.ManualMissingText) is { } why)
            WeakReferenceMessenger.Default.Send(new NotifyMessage((why, false)));
    }

    // 자료함(조사 세션 목록 · 불러오기 · 복제 · 삭제)
    private void Sessions_Click(object sender, RoutedEventArgs e)
        => new SessionBrowserWindow { Owner = this }.ShowDialog();

    // 지점 지도(최신 지도 + 현재 입력 좌표 마커)
    private void Map_Click(object sender, RoutedEventArgs e)
        => new MapPopupWindow { Owner = this }.ShowDialog();

    /// <summary>
    /// [새 지점](C-2) — 저장 확인(B-5) → 7개 분류군 입력과 지점을 비움 → 조사개황 6칸은 압정과 관계없이 그대로(E4) → 지점 칸에 포커스.
    /// </summary>
    private async void NewSite_Click(object sender, RoutedEventArgs e)
    {
        var sessions = App.Current.Services.GetRequiredService<ISessionService>();
        if (!await UnsavedChangesPrompt.ConfirmAsync(this, sessions,
                "새 지점으로 시작하면 지금 화면의 입력과 지점이 비워집니다(대분류·연도차수·조사일자·날씨·조사자·조사기관은 압정과 관계없이 그대로).")) return;
        sessions.StartNewSite(null);
        FocusSiteBox();
    }

    /// <summary>조사개황을 펼치고 지점 칸에 포커스를 둔다.</summary>
    private void FocusSiteBox()
    {
        MetaToggle.IsChecked = true;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            var overview = FindChild<Views.UserControls.SurveyOverviewControl>(MetaCard);
            overview?.FocusSite();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (c is T t) return t;
            if (FindChild<T>(c) is T deeper) return deeper;
        }
        return null;
    }

    /// <summary>
    /// 지점 직접 변경 확인(C-2) — 입력한 자료가 있는 채로 다른 등록 지점을 골랐다.
    /// 기본은 [지점명만 고침(유지)] — 자료를 지우지 않는다. [새 지점으로 시작]이면 저장 확인(B-5) 뒤 입력을 비운다.
    /// </summary>
    private async void AskSiteChange(Models.SurveyMeta meta, SDSM_Models.SurveySite next)
    {
        var sessions = App.Current.Services.GetRequiredService<ISessionService>();
        var from = string.IsNullOrWhiteSpace(meta.SiteDisplay) ? "(지점 없음)" : meta.SiteDisplay;
        var to = meta.DisplayOf(next);
        var pick = ChoiceDialog.Ask(this, "지점 변경",
            $"입력한 자료가 있습니다. 지점을 「{from}」에서 「{to}」(으)로 바꿉니다.\n\n" +
            "· 지점명만 고침 — 입력한 자료를 그대로 두고 지점 이름만 바꿉니다(다음 저장부터 새 지점 이름으로 저장됩니다).\n" +
            "· 새 지점으로 시작 — 지금 입력을 비우고 새 지점에서 처음부터 입력합니다.",
            new ChoiceDialog.Option("취소(바꾸지 않음)", IsCancel: true),
            new ChoiceDialog.Option("새 지점으로 시작(입력 비움)"),
            new ChoiceDialog.Option("지점명만 고침(유지)", IsPrimary: true));

        switch (pick)
        {
            case 2:
                meta.SelectedSite = next;
                break;
            case 1:
                if (!await UnsavedChangesPrompt.ConfirmAsync(this, sessions, "새 지점으로 시작하면 지금 화면의 입력이 비워집니다.")) return;
                sessions.StartNewSite(next);
                break;
            default:
                return;   // 바꾸지 않는다 — 콤보는 지금 지점을 그대로 보인다
        }
    }

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
        finally
        {
            // 압정 주입·예시 시드가 끝난 지금 화면이 '저장 안 한 변경' 의 출발점이다(B-5).
            // 첫 화면에서 자료함 세션을 열고 들어왔으면 그 세션이 곧 이 상태다.
            App.Current.Services.GetRequiredService<ISessionService>().MarkClean();
        }
        OfferRecovery();
    }

    /// <summary>
    /// 지난 실행에서 오류로 남긴 복구본이 있으면 묻는다(N1). [불러오기] = 화면에 올리기만 한다(저장은 조사자가) ·
    /// 불러온 복구본은 자료함 <c>_recovery\loaded\</c> 로 옮겨 다시 묻지 않는다. [나중에] = 그대로 두고 다음 실행에 또 묻는다.
    /// </summary>
    private void OfferRecovery()
    {
        try
        {
            var store = App.Current.Services.GetRequiredService<ISessionStore>();
            var names = store.ListRecovery();
            if (names.Count == 0) return;
            var latest = store.LoadRecovery(names[0]);
            if (latest is null) return;
            var taxa = string.Join(" · ", latest.Taxa.Keys.Select(TaxonNames.Korean));
            var pick = ChoiceDialog.Ask(this, "복구본이 있습니다",
                $"지난번 오류 때 임시 저장한 화면이 있습니다 — {latest.SessionId} ({latest.UpdatedAt:yyyy-MM-dd HH:mm}) · {(taxa.Length > 0 ? taxa : "분류군 자료 없음")}" +
                (names.Count > 1 ? $"\n(복구본 {names.Count}개 중 가장 최근 것)" : string.Empty) + "\n\n" +
                "· 불러오기 — 화면에 올리기만 합니다. 자료함에 저장하려면 [임시 저장]을 누르세요(같은 자료가 있으면 합칠지 묻습니다).\n" +
                $"· 나중에 — 그대로 두고 다음 실행 때 다시 묻습니다(위치: {System.IO.Path.Combine(store.Location, "_recovery")}).",
                new ChoiceDialog.Option("나중에", IsCancel: true),
                new ChoiceDialog.Option("불러오기", IsPrimary: true));
            if (pick != 1) return;
            App.Current.Services.GetRequiredService<ISessionService>().ApplyRecovered(latest);
            store.SetRecoveryAside(names[0]);
            WeakReferenceMessenger.Default.Send(new NotifyMessage(("복구본을 화면에 올렸습니다 — 아직 저장하지 않았습니다. [임시 저장]으로 저장하세요.", false)));
        }
        catch (Exception ex)
        {
            // 복구본 확인이 앱 시작을 막아서는 안 된다 — 오류만 남긴다
            try { App.Current.Services.GetRequiredService<ISessionStore>().AppendErrorLog($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 복구본 확인 실패\n{ex}"); } catch { }
        }
    }

    // 최대화 시 WindowStyle 유지 (design.md §5.1)
    private void HandleMaximized()
    {
        if (WindowState == WindowState.Maximized)
            WindowStyle = WindowStyle.SingleBorderWindow;
    }
}
