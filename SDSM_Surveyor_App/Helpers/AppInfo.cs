using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 프로그램 정보 한 곳 — 정보 창 · 창 제목 옆 판 표시가 모두 여기서 읽는다(2026-10-26 v0.5.0 · §21-5 · §22-4).
/// 관리자 `SDSM_App\Commons\AppInfo.cs` 와 같은 내용(적용 지침 · 개발 · 연락처 글자 그대로)이다 — 고치면 둘 다 고친다.
/// 판 = SDSM_Surveyor_App.csproj 의 &lt;Version&gt;, 커밋 = 빌드 때 SDK 가 제품 버전 뒤에 붙이는 +해시(앞 7자리),
/// 빌드 날짜 = csproj 의 AssemblyMetadata("BuildDate").
/// </summary>
public static class AppInfo
{
    public const string ProgramName = "삼성 DS 모니터링 야장";
    public const string ProgramKind = "조사자 앱(현장 입력 · 오프라인)";

    // 적용 지침(관리자와 같은 글자)
    public const string GuidelineTitle = "「수생태계 현황 조사 및 건강성 평가 방법 등에 관한 지침(하천편)」";
    public const string GuidelineNotice = "국립환경과학원고시 제2026-30호(2026. 7. 27. 개정)";

    /// <summary>앱에 함께 넣은 지침 원본(PDF) — 실행폴더 기준. [지침 원본 보기]가 기본 PDF 프로그램으로 연다.</summary>
    public const string GuidelinePdfRelative = @"guideline\guideline_2026-30.pdf";

    /// <summary>사용설명서(PDF) — 실행폴더 기준(§26-3 · §27-6). 이름에 판을 넣지 않는다(판은 표지 · 정보 창).</summary>
    public const string ManualPdfRelative = @"manual\surveyor_manual.pdf";

    /// <summary>사용설명서 파일이 없을 때 안내.</summary>
    public const string ManualMissingText = @"사용설명서 파일이 없습니다 — 프로그램 폴더의 manual\surveyor_manual.pdf 를 확인하세요";

    // 종목록 기준(판 · 종 수는 정보 창이 ISpeciesListProvider 에서 읽는다)
    public const string SpeciesListBasis = "어류 · 저서동물 = 관리자 종목록(지침 별표 2 · 3 에 맞춰 정비 · 2026-10-22)과 같음 · 조류 · 포유류 · 양서파충류 = 국가생물종목록";

    // 개발(관리자 AppInfo.Developer · Contact 와 같은 글자)
    public const string Developer = "주식회사 에코앤지오";
    /// <summary>연락처 — 관리자 AppInfo.Contact 와 같은 글자. 정보 창은 ' · ' 마다 줄을 나눠 보인다.</summary>
    public const string Contact = "www.econgeo.com · 032-888-2205 · evicdatalab@gmail.com";

    private static readonly Assembly Asm = typeof(AppInfo).Assembly;

    private static string Informational =>
        Asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

    /// <summary>판 — 예: 0.5.0</summary>
    public static string Version => Informational.Split('+')[0] is { Length: > 0 } v ? v : Asm.GetName().Version?.ToString(3) ?? "-";

    /// <summary>창 제목 옆 판 표시 — 예: v0.5.0</summary>
    public static string VersionText => $"v{Version}";

    /// <summary>제품 버전의 커밋 해시(앞 7자리). 없으면 '-'.</summary>
    public static string Commit
    {
        get
        {
            var parts = Informational.Split('+');
            return parts.Length > 1 && parts[1].Length > 0 ? parts[1][..Math.Min(7, parts[1].Length)] : "-";
        }
    }

    /// <summary>빌드 날짜(yyyy-MM-dd) — 없으면 실행 파일 수정 날짜.</summary>
    public static string BuildDate =>
        Asm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "BuildDate")?.Value
        ?? (string.IsNullOrEmpty(Asm.Location) ? "-" : File.GetLastWriteTime(Asm.Location).ToString("yyyy-MM-dd"));

    /// <summary>지침 원본 PDF 전체 경로(실행폴더).</summary>
    public static string GuidelinePdfPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GuidelinePdfRelative);

    /// <summary>사용설명서 PDF 전체 경로(실행폴더).</summary>
    public static string ManualPdfPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ManualPdfRelative);

    /// <summary>
    /// PDF 를 기본 PDF 프로그램으로 연다(지침 원본 · 사용설명서 같은 방식). 열었으면 null, 못 열었으면 까닭.
    /// </summary>
    public static string? OpenPdf(string path, string missingText)
    {
        if (!File.Exists(path)) return missingText;
        try
        {
            OpenWithShell(path);
            return null;
        }
        catch (Win32Exception ex)
        {
            // PDF 를 여는 프로그램이 없는 PC — 경로를 알려 직접 열게 한다
            return $"PDF 를 여는 프로그램이 없어 열지 못했습니다({ex.Message}) — 파일 위치: {path}";
        }
    }

    /// <summary>
    /// 파일을 기본 프로그램으로 연다. 시험은 이 칸을 바꿔 끼워 실제 PDF 프로그램을 띄우지 않고 경로만 받는다.
    /// </summary>
    public static Func<string, bool> OpenWithShell { get; set; } = path =>
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return true;
    };
}
