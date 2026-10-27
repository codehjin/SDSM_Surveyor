using System.IO;
using System.Windows.Interop;
using System.Windows.Media;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 흰 화면 대비 스위치(지시서 §25 · v0.5.2). 평소엔 그래픽카드로 그린다.
/// 실행 폴더에 확장자를 뺀 이름이 <c>software_render</c> 인 파일(대소문자 · 확장자 · 내용 상관없음)이 있으면
/// 이 프로세스 전체를 소프트웨어로 그린다 — 그래픽카드 그리기가 아무것도 그리지 않는 PC(작업보고_v051 §6)에서 쓴다.
/// <list type="bullet">
/// <item>켜짐: <c>software_render</c> · <c>software_render.txt</c> · <c>software_render.txt.txt</c>(탐색기가 확장자를 숨겨 한 번 더 붙은 경우)</item>
/// <item>꺼짐: <c>software_render_old.txt</c> 처럼 이름이 다른 파일</item>
/// </list>
/// ⚠ App 생성자 맨 앞(테마 · 리소스 · 어떤 창보다 먼저)에서 <see cref="Apply"/> 한다. 폴더를 못 읽으면 그래픽카드 그대로 — 시작을 막지 않는다.
/// </summary>
public static class RenderModeSwitch
{
    public const string FileStem = "software_render";

    /// <summary>이번 실행이 소프트웨어 그리기인가(정보 창이 읽는다).</summary>
    public static bool IsSoftware { get; private set; }

    /// <summary>정보 창 「화면 그리기」 값.</summary>
    public static string Description => IsSoftware ? "소프트웨어(software_render 파일)" : "그래픽카드";

    /// <summary>파일 이름에서 확장자를 모두 뗀 것이 <see cref="FileStem"/> 인가(대소문자 무관).</summary>
    public static bool Matches(string fileName)
    {
        var name = Path.GetFileName(fileName);
        while (Path.HasExtension(name)) name = Path.GetFileNameWithoutExtension(name);
        return string.Equals(name, FileStem, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>폴더에 스위치 파일이 있는가. 읽기에 실패하면 false(그래픽카드).</summary>
    public static bool HasSwitchFile(string folder)
    {
        try { return Directory.EnumerateFiles(folder).Any(Matches); }
        catch { return false; }
    }

    /// <summary>실행 폴더를 보고 그리기 방식을 정한다. 프로세스에서 한 번, 어떤 창보다 먼저.</summary>
    public static void Apply()
    {
        IsSoftware = HasSwitchFile(AppContext.BaseDirectory);
        if (IsSoftware) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
    }
}
