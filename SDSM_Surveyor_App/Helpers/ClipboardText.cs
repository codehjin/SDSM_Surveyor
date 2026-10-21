using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Messengers;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 붙여넣기용 클립보드 읽기(2026-10-20 연계 시험 2차 N1).
///
/// 다른 프로그램(클립보드 기록·원격 데스크톱·메신저 등)이 클립보드를 잠깐 잡고 있으면
/// <see cref="Clipboard.ContainsText()"/>·<see cref="Clipboard.GetText()"/> 가 <c>CLIPBRD_E_CANT_OPEN</c> 예외를 던진다.
/// 처리하지 않으면 그리드 Ctrl+V 에서 앱이 닫혔다. 짧게 다시 시도하고, 그래도 안 되면 안내만 하고 입력은 그대로 둔다.
/// </summary>
public static class ClipboardText
{
    public const string FailText = "클립보드를 읽지 못했습니다 — 잠시 뒤 다시 붙여넣으세요";

    private const int Tries = 5;
    private const int WaitMs = 50;

    /// <summary>클립보드 글자 읽기(글자가 없으면 null). 시험이 다른 프로세스 점유를 흉내 낼 때만 바꾼다.</summary>
    internal static Func<string?> Reader = () =>
    {
        // WPF 클립보드는 잡혀 있으면 안에서 약 1초 동안 다시 시도한 뒤 실패한다 — 5번이면 화면이 5초 멈춘다.
        // 먼저 Win32 로 우리 창 이름으로 열어 보아(바로 실패) 잡혀 있으면 곧장 다음 시도로 넘긴다.
        // ⚠ 창 없이(NULL) 열면 다른 프로그램이 NULL 로 연 것과 겹쳐도 성공하고, 닫을 때 그 프로그램의 열기까지 닫는다 — 반드시 우리 창으로.
        var hwnd = OwnWindow();
        if (hwnd != IntPtr.Zero)
        {
            if (!OpenClipboard(hwnd)) throw new COMException("CLIPBRD_E_CANT_OPEN", ClipboardCantOpen);
            CloseClipboard();
        }
        return Clipboard.ContainsText() ? Clipboard.GetText() : null;
    };

    private static IntPtr OwnWindow()
    {
        var w = Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? Application.Current?.MainWindow;
        return w is null ? IntPtr.Zero : new System.Windows.Interop.WindowInteropHelper(w).Handle;
    }

    private const int ClipboardCantOpen = unchecked((int)0x800401D0);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();

    /// <summary>
    /// 클립보드 글자를 읽는다. 읽었으면 true(글자가 없으면 <paramref name="text"/> 가 null).
    /// 다시 시도해도 못 읽으면 안내를 띄우고 false — 부르는 쪽은 아무것도 바꾸지 않는다.
    /// </summary>
    public static bool TryRead(out string? text)
    {
        for (int i = 0; ; i++)
        {
            try
            {
                text = Reader();
                return true;
            }
            catch (ExternalException) when (i < Tries - 1)   // COMException 은 ExternalException 의 파생
            {
                Thread.Sleep(WaitMs);
            }
            catch (ExternalException)
            {
                text = null;
                WeakReferenceMessenger.Default.Send(new NotifyMessage((FailText, false)));
                return false;
            }
        }
    }
}
