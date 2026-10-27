using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 앱 전역 미처리 예외 처리(2026-10-20 연계 시험 2차 N1).
///
/// 종전에는 처리기가 없어 그리드 붙여넣기 같은 화면 예외 하나로 앱이 닫히고 저장 안 한 입력이 사라졌다.
/// 예외가 오면 ① 자료함 <c>logs\error_yyMMdd.log</c> 에 남기고 ② 화면에 입력이 있으면 지금 세션을
/// 자료함 <c>_recovery\</c> 에 세션 형식 그대로 쓰고(정식 세션 파일은 덮지 않음) ③ 그 위치를 알린다.
/// 화면(Dispatcher) 예외는 처리한 것으로 표시해 앱을 닫지 않는다. 다음 실행 때 창이 복구본을 묻는다.
/// 기록·복구본 경로는 <see cref="ISessionStore"/> 를 따른다(시험에서는 가짜 자료함에만 쓰인다).
/// </summary>
public sealed class CrashGuard : ISingletonService
{
    private readonly ISessionStore _store;
    private readonly ISessionService _sessions;
    private int _handling;
    private bool _installed;

    public CrashGuard(ISessionStore store, ISessionService sessions)
    {
        _store = store;
        _sessions = sessions;
    }

    /// <summary>마지막으로 처리한 예외의 기록 파일 · 복구본 · 안내 문구(시험이 읽는다).</summary>
    public string? LastLogPath { get; private set; }
    public string? LastRecoveryPath { get; private set; }
    public string? LastNotice { get; private set; }

    /// <summary>안내 창을 띄울지. 시험이 끌 수 있다(끄면 <see cref="LastNotice"/> 만 남는다).</summary>
    public bool ShowMessage { get; set; } = true;

    /// <summary>처리기 3종을 단다(두 번 불러도 한 번만).</summary>
    public void Install(Application app)
    {
        if (_installed) return;
        _installed = true;
        app.DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;   // 화면 예외로 앱을 닫지 않는다
            Handle(e.Exception, "화면");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Handle(e.ExceptionObject as Exception, e.IsTerminating ? "백그라운드(프로그램이 닫힙니다)" : "백그라운드");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Handle(e.Exception, "비동기 작업");
        };
    }

    private static string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";

    /// <summary>예외 하나를 처리한다. 처리 중에 또 예외가 나면 그것은 기록만 하고 넘어간다(무한 반복 방지).</summary>
    public void Handle(Exception? ex, string where)
    {
        if (Interlocked.Exchange(ref _handling, 1) == 1)
        {
            TryLog($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {where} · 처리 중 또 오류\n{ex}");
            return;
        }
        try
        {
            LastLogPath = TryLog($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {where} · SDSM_Surveyor {Version}\n{ex}");

            LastRecoveryPath = null;
            try
            {
                // 화면 VM 은 UI 스레드에서만 읽는다(백그라운드 예외면 UI 스레드에 맡긴다 · 멈춰 있으면 5초 뒤 포기)
                LastRecoveryPath = OnUi(() => _sessions.HasInputData() ? _store.SaveRecovery(_sessions.CaptureCurrentSession()) : null);
            }
            catch (Exception saveEx)
            {
                TryLog($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 복구본을 쓰지 못함\n{saveEx}");
            }

            var lines = new List<string> { "예기치 않은 오류가 났습니다." };
            if (where == "화면") lines.Add("프로그램은 닫지 않았습니다. 지금 화면의 입력은 그대로 있습니다.");
            lines.Add(string.Empty);
            lines.Add($"오류 기록 : {LastLogPath ?? "(쓰지 못함)"}");
            lines.Add(LastRecoveryPath is not null
                ? $"지금 화면 임시 저장 : {LastRecoveryPath}\n(자료함의 정식 자료는 덮지 않았습니다 — 다음 실행 때 [불러오기]로 화면에 올릴 수 있습니다)"
                : "화면에 입력한 분류군 자료가 없어 임시 저장은 하지 않았습니다.");
            if (where == "화면") lines.Add("\n계속 쓰기 전에 [임시 저장]으로 저장해 두세요.");
            LastNotice = string.Join("\n", lines);

            if (ShowMessage)
                OnUi(() => MessageBox.Show(LastNotice, $"오류 — {Helpers.AppInfo.ProgramName}", MessageBoxButton.OK, MessageBoxImage.Warning));
        }
        catch
        {
            // 처리기 안의 실패로 앱을 더 망가뜨리지 않는다(기록은 위에서 이미 시도했다)
        }
        finally
        {
            Interlocked.Exchange(ref _handling, 0);
        }
    }

    private string? TryLog(string text)
    {
        try { return _store.AppendErrorLog(text); }
        catch { return null; }
    }

    private static T? OnUi<T>(Func<T?> work)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) return work();
        return d.Invoke(work, DispatcherPriority.Send, CancellationToken.None, TimeSpan.FromSeconds(5));
    }
}
