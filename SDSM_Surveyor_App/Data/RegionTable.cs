namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 대권역·중권역 — <b>하천 단위로 고정</b>(C-6 · 지시서 §3-6). 키 = <c>sites.json</c> 의 <c>River</c>(과업대표하천).
///
/// 최종본 실입력에서 곡교천 권역 20건이 한강/안성천으로 잘못 들어가 있었다(손으로 적은 결과).
/// 지점을 고르면 하천·좌표와 함께 이 표로 채운다 — 조사자가 고칠 수 있고, 지점을 다시 고르면 다시 채운다.
/// 표에 없는 하천이면 비워 둔다(잘못된 값을 남기지 않는다).
/// ⚠ <c>sites.json</c> 이나 공유 모델(<c>SiteCatalog</c>)에는 필드를 넣지 않는다 — 조사자 앱 안의 표다.
/// </summary>
public static class RegionTable
{
    private static readonly Dictionary<string, (string Major, string Middle)> ByRiver = new(StringComparer.Ordinal)
    {
        ["곡교천"] = ("금강", "삽교천"),     // 13지점(곡교천·천안천·회룡천·공수리·북수리·성성공원)
        ["오산천"] = ("한강", "안성천"),     // 20지점
        ["원천리천"] = ("한강", "안성천"),   // 10지점
    };

    /// <summary>하천의 (대권역, 중권역). 표에 없으면 null.</summary>
    public static (string Major, string Middle)? Find(string? river) =>
        river is not null && ByRiver.TryGetValue(river.Trim(), out var r) ? r : null;
}
