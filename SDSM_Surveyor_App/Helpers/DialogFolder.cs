using System.IO;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.Data;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 저장 · 열기 · 폴더 고르기 대화상자의 시작 폴더 한 곳(§32 · S4 · 작업보고 v052 8-4 #5).
/// 시작 폴더를 정하지 않으면 Windows 가 지난번 폴더를 연다 — 그 폴더가 네트워크 드라이브 · OneDrive 면
/// 대화상자가 '응답 없음' 이 되었다(2026-10-27 밤 · 어류 [일괄입력용]).
/// 지난 내보내기 폴더(설정 <c>LastExportFolder</c>)가 로컬 고정 드라이브에 실제로 있으면 그곳, 아니면 '문서'.
/// 네트워크 드라이브 · OneDrive · 없는 폴더는 쓰지 않는다('문서' 가 OneDrive 로 옮겨져 있으면 사용자 폴더 → 시스템 드라이브).
/// </summary>
public static class DialogFolder
{
    /// <summary>설정 저장소 — 시험이 바꿔 끼운다.</summary>
    internal static Func<ISettingsStore?> Store = () => (System.Windows.Application.Current as App)?.Services.GetService<ISettingsStore>();

    /// <summary>대화상자를 열 폴더.</summary>
    public static string Initial()
    {
        var last = Store()?.Settings.LastExportFolder;
        if (IsSafe(last)) return last!;
        foreach (var f in new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.UserProfile })
        {
            var p = Environment.GetFolderPath(f);
            if (IsSafe(p)) return p;
        }
        return Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    }

    /// <summary>내보낸 파일(또는 고른 폴더)의 폴더를 기억한다. 쓰지 않을 폴더(네트워크 · OneDrive 등)는 기억하지 않는다.</summary>
    public static void Remember(string? fileOrFolder)
    {
        if (string.IsNullOrWhiteSpace(fileOrFolder)) return;
        var dir = Directory.Exists(fileOrFolder) ? fileOrFolder : Path.GetDirectoryName(fileOrFolder);
        if (!IsSafe(dir)) return;
        var store = Store();
        if (store is null || string.Equals(store.Settings.LastExportFolder, dir, StringComparison.OrdinalIgnoreCase)) return;
        store.Settings.LastExportFolder = dir;
        try { store.Save(); } catch (IOException) { /* 설정을 못 써도 내보내기는 끝났다 — 다음에 '문서' 에서 연다 */ }
    }

    /// <summary>로컬 고정 드라이브에 실제로 있는 폴더이고 OneDrive 밖인가.</summary>
    public static bool IsSafe(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        try
        {
            if (!Path.IsPathFullyQualified(dir) || dir.StartsWith(@"\\", StringComparison.Ordinal)) return false;   // UNC
            var root = Path.GetPathRoot(dir);
            if (string.IsNullOrEmpty(root) || new DriveInfo(root).DriveType != DriveType.Fixed) return false;   // 네트워크 · 이동식 · CD
            if (IsOneDrive(dir)) return false;
            return Directory.Exists(dir);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static readonly string[] OneDriveVars = { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" };

    /// <summary>OneDrive 동기화 폴더 안인가(환경 변수가 가리키는 폴더 · 이름이 'OneDrive' 로 시작하는 폴더).</summary>
    internal static bool IsOneDrive(string dir)
    {
        var full = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var v in OneDriveVars)
        {
            var od = Environment.GetEnvironmentVariable(v);
            if (string.IsNullOrWhiteSpace(od)) continue;
            var root = Path.GetFullPath(od).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return full.Split(Path.DirectorySeparatorChar)
                   .Any(seg => seg.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase));
    }
}
