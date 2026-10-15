using System.IO;
using System.Windows.Media.Imaging;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 지도 이미지 읽기. <b>파일을 잠그지 않는다</b> — 잠그면 [교체]·[삭제]가
/// `다른 프로세스가 사용 중입니다` 로 실패한다.
///
/// ⚠ <b><c>BitmapCreateOptions.IgnoreImageCache</c> 를 절대 쓰지 말 것.</b>
///   그 옵션은 <c>UriSource</c> 로 읽을 때만 쓸 수 있다. 여기처럼 <c>StreamSource</c> 로 읽으면
///   WPF 가 URI 로 캐시 키를 만들려다 <c>ArgumentNullException: Value cannot be null. (Parameter 'key')</c>
///   를 던진다 — 파일·권한은 멀쩡한데 그림만 안 뜨는 증상이 되어 원인 찾기가 아주 어렵다
///   (2026-09-10 현장 확인). 스트림은 매번 파일에서 새로 읽으므로 캐시를 끌 이유도 없다.
///
/// ⚠ WPF 는 <c>Width/Height</c>(DIP)를 `PixelWidth * 96 / DpiX` 로 계산한다.
///   시드 지도 중 <c>곡교천_26_기준.png</c> 는 82 DPI 라 521px 이 610 DIP 로 그려진다.
///   지도 위에 픽셀 좌표로 마커를 찍으므로 <b>반드시 PixelWidth/PixelHeight 를 써야 한다.</b>
/// </summary>
public static class ImageSize
{
    /// <summary>이미지의 실제 픽셀 크기. 못 읽으면 (0,0).</summary>
    public static (int W, int H) Read(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);

            // ⚠ IgnoreImageCache 를 주면 안 된다 — 아래 Load 와 같은 이유로 터진다.
            //    WPF 의 이미지 캐시는 URI 로 키를 만드는데 스트림에는 URI 가 없다.
            var frame = BitmapFrame.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// 화면에 띄울 비트맵. 스트림을 다 읽고 닫으므로 파일이 잠기지 않는다.
    /// <c>Freeze</c> 까지 해 두어 다시 써도 안전하다.
    /// </summary>
    public static BitmapImage? Load(string path) => Load(path, out _);

    /// <summary>
    /// 화면에 띄울 비트맵. 실패하면 null 이고 <paramref name="reason"/> 에 이유가 담긴다.
    ///
    /// ⚠ 이유를 밖으로 내보내는 것이 중요하다 — 종전에는 전부 삼켜서 "이미지를 찾지 못했습니다"
    ///   한 줄만 남았고, 경로가 틀린 것인지 · 권한인지 · 디코딩 실패인지 알 수 없었다.
    /// </summary>
    public static BitmapImage? Load(string path, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "경로가 비어 있습니다 — maps.json 의 file 값이 없습니다.";
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                var dir = Path.GetDirectoryName(path);
                var dirOk = dir is not null && Directory.Exists(dir);
                reason = $"파일을 찾지 못했습니다.\n경로: {path}\n폴더 있음: {(dirOk ? "예" : "아니오")}";
                if (dirOk)
                {
                    try
                    {
                        var n = Directory.GetFiles(dir!, "*.png").Length;
                        reason += $" · 그 폴더의 png {n}개";
                    }
                    catch (Exception ex) { reason += $" · 폴더를 읽지 못함({ex.GetType().Name})"; }
                }
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;   // 먼저 세워야 효력이 있다
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex)
        {
            long len = -1;
            try { len = new FileInfo(path).Length; } catch { /* 크기도 못 읽으면 -1 로 둔다 */ }
            reason = $"{ex.GetType().Name}: {ex.Message}\n경로: {path}\n크기: {len} bytes";
            return null;
        }
    }
}
