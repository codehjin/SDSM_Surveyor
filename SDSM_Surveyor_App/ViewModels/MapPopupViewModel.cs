using CommunityToolkit.Mvvm.ComponentModel;
using SDSM_Core.Helpers;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>
/// 지점 지도 팝업. 지금 입력되어 있는 좌표 <b>한 점만</b> 지도 위에 표시한다.
/// 지도에 이미 인쇄되어 있는 다른 지점 표시는 배경 그대로 둔다.
///
/// 기본은 그 권역의 최신 지도 한 장이고, 과거 지도는 참고로 고를 수 있다.
/// </summary>
public partial class MapPopupViewModel : ObservableObject, ITransientService
{
    // 한반도 범위. 이 밖이면 좌표를 잘못 넣은 것이다.
    private const double LatMin = 33, LatMax = 39, LonMin = 124, LonMax = 132;

    private readonly IMapCatalogProvider _maps;

    public MapPopupViewModel(IMapCatalogProvider maps, SurveyMeta meta)
    {
        _maps = maps;
        Meta = meta;
        Reload();
    }

    public SurveyMeta Meta { get; }

    /// <summary>이 지점에 보여 줄 지도 목록(최신이 맨 앞).</summary>
    public List<SiteMap> Maps { get; private set; } = new();

    [ObservableProperty] private SiteMap? _selected;

    public bool HasMaps => Maps.Count > 0;

    /// <summary>
    /// 고른 지도가 이 지점 것이 아닌지(딱 맞는 지도가 없어 대신 보여 주는 것인지).
    ///
    /// ⚠ <b>지점을 골랐을 때만</b> 판정한다. 지점을 안 고른 상태에서는 대상 지점 목록을 가진 지도가
    ///   모두 '안 맞는 지도' 로 나오는데, 실제로는 그 권역 지도가 맞을 수 있다.
    ///   그 상태에서 "다른 권역 지도" 라고 알리면 마커가 제대로 찍히는 화면과 말이 어긋난다.
    /// </summary>
    public bool IsOtherArea
    {
        get
        {
            if (Selected is null) return false;

            // 대분류는 지점 없이도 판정된다. 생태현황처럼 등록 지점이 하나도 없는 대분류에서는
            // SelectedSite 가 영원히 null 이라, 지점 선택에 매달리면 방류하천 지도를 띄워 놓고
            // 경고를 한 번도 못 띄운다.
            if (!ProjectMatches(Selected)) return true;

            return Meta.SelectedSite is not null
                   && !_maps.AppliesExactly(Selected, Meta.SelectedSite, Meta.Project);
        }
    }

    /// <summary>고른 지도의 대분류가 지금 대분류와 맞는지. 어느 쪽이 비면 거르지 않는다.</summary>
    private bool ProjectMatches(SiteMap map)
    {
        var want = map.AppliesTo?.Project?.Trim();
        if (string.IsNullOrEmpty(want)) return true;
        if (string.IsNullOrWhiteSpace(Meta.Project)) return true;
        return string.Equals(want, Meta.Project!.Trim(), StringComparison.Ordinal);
    }

    /// <summary>고른 지도가 최신이 아니면 참고용 띠를 띄운다.</summary>
    public bool IsReference => Selected is not null && !Selected.IsLatest && Maps.Count > 1
                               && !ReferenceEquals(Selected, Maps.FirstOrDefault());

    public string ReferenceText => Selected is null ? string.Empty : $"참고용 · {Selected.Display}";

    /// <summary>창 제목줄 — `지도 · 오산천3 (St.3)`.</summary>
    public string HeaderText
    {
        get
        {
            var site = Meta.SiteDisplay;
            return string.IsNullOrWhiteSpace(site) ? "지도" : $"지도 · {site}";
        }
    }

    /// <summary>마커 옆 라벨 — `오산천3 (37.219075, 127.088778)`.</summary>
    public string MarkerLabel
    {
        get
        {
            var site = Meta.SiteDisplay;
            var coord = Meta.CoordinateText;
            return string.IsNullOrWhiteSpace(site) ? coord : $"{site}  ·  {coord}";
        }
    }

    /// <summary>지도를 다시 읽고 최신 지도를 고른다.</summary>
    public void Reload()
    {
        _maps.Reload();
        Maps = _maps.ForSite(Meta.SelectedSite, Meta.Project).ToList();
        OnPropertyChanged(nameof(Maps));
        OnPropertyChanged(nameof(HasMaps));
        Selected = _maps.Latest(Meta.SelectedSite, Meta.Project) ?? Maps.FirstOrDefault();
        RaiseAll();
    }

    partial void OnSelectedChanged(SiteMap? value)
    {
        LoadError = null;
        RaiseAll();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(IsOtherArea));
        OnPropertyChanged(nameof(IsReference));
        OnPropertyChanged(nameof(ReferenceText));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(MarkerLabel));
        OnPropertyChanged(nameof(NoticeText));
        OnPropertyChanged(nameof(HasNotice));
    }

    // ── 좌표 판정 ─────────────────────────────────────────────────────────────

    /// <summary>현재 입력 좌표(십진). 못 읽으면 null.</summary>
    public (double Lat, double Lon)? Coord
    {
        get
        {
            var la = Coordinate.ToDecimal(Meta.Lat);
            var ln = Coordinate.ToDecimal(Meta.Lng);
            return la is double a && ln is double b ? (a, b) : null;
        }
    }

    private static bool InKorea(double lat, double lon) =>
        lat >= LatMin && lat <= LatMax && lon >= LonMin && lon <= LonMax;

    /// <summary>마커를 찍을 이미지 픽셀 좌표. 못 찍는 경우는 null 이고 <see cref="NoticeText"/> 가 이유를 말한다.</summary>
    public (double X, double Y)? MarkerPixel
    {
        get
        {
            if (Selected is null || !Selected.HasCalibration) return null;

            // 다른 권역 지도 위에 이 좌표를 찍으면 아무 의미 없는 자리에 점이 생긴다.
            // 안내 문구도 "위치가 표시되지 않습니다" 라고 말한다 — 화면과 말을 맞춘다.
            if (IsOtherArea) return null;

            if (Coord is not { } c) return null;
            var (lat, lon) = c;
            if (!InKorea(lat, lon)) return null;
            if (!MapProjection.InBounds(Selected, lat, lon)) return null;
            return MapProjection.ToPixel(Selected, lat, lon);
        }
    }

    /// <summary>이미지 파일을 못 읽었을 때 창이 넣는다(카탈로그에는 있는데 png 가 없어진 경우).</summary>
    [ObservableProperty] private string? _loadError;

    partial void OnLoadErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(NoticeText));
        OnPropertyChanged(nameof(HasNotice));
    }

    public bool HasNotice => !string.IsNullOrEmpty(NoticeText);

    /// <summary>마커를 못 찍는 이유(또는 빈 문자열).</summary>
    public string NoticeText
    {
        get
        {
            if (!string.IsNullOrEmpty(LoadError)) return LoadError!;

            if (!HasMaps)
                return "이 권역에 등록된 지도가 없습니다. [지도 관리]에서 추가할 수 있습니다.";

            // ⚠ 좌표 검사보다 먼저다. 대분류를 바꾸면 좌표가 비워지므로(OnProjectChanged),
            //    좌표 검사를 먼저 두면 이 안내가 거의 뜨지 않는다.
            if (IsOtherArea)
            {
                // 지점을 못 고르는 대분류(등록 지점 0)도 있으므로 주어를 대분류로 떨어뜨린다.
                var where = !string.IsNullOrWhiteSpace(Meta.SiteDisplay) ? Meta.SiteDisplay
                          : !string.IsNullOrWhiteSpace(Meta.Project) ? $"「{Meta.Project}」"
                          : "이 지점";
                return $"{where}에 등록된 지도가 없습니다. 아래는 다른 권역 지도라 위치가 표시되지 않습니다. "
                     + "[지도 관리]에서 이 권역 지도를 추가해 주세요.";
            }

            if (Coord is not { } c)
            {
                var noLat = string.IsNullOrWhiteSpace(Meta.Lat);
                var noLng = string.IsNullOrWhiteSpace(Meta.Lng);

                // 보정이 없는 지도는 좌표를 넣어도 표시할 수 없다 — 지킬 수 없는 약속을 하지 않는다.
                if (noLat && noLng)
                    return Selected?.HasCalibration == true
                        ? "좌표가 아직 없습니다. 지점을 고르거나 위·경도를 넣으면 지도에 위치가 표시됩니다."
                        : "이 지도는 위치 표시를 지원하지 않습니다(보정 없음). 그림만 보여 줍니다.";
                if (noLat) return "위도가 비어 있습니다.";
                if (noLng) return "경도가 비어 있습니다.";
                return "좌표 형식을 읽을 수 없습니다 — 십진(36.763081) 또는 도분초(36°45'47.09\") 형식만 됩니다.";
            }

            var (lat, lon) = c;
            if (!InKorea(lat, lon))
                return InKorea(lon, lat)
                    ? "위도·경도가 바뀐 것 같습니다. 두 칸을 확인해 주세요."
                    : "좌표가 한국 범위를 벗어났습니다. 위도 33~39 · 경도 124~132 안이어야 합니다.";

            if (Selected is null) return string.Empty;

            if (!Selected.HasCalibration)
                return "이 지도는 위치 표시를 지원하지 않습니다(보정 없음). 그림만 보여 줍니다.";

            if (!MapProjection.InBounds(Selected, lat, lon))
                return $"이 좌표는 「{Selected.Display}」 범위 밖입니다. 다른 지도를 고르거나 [지도 관리]에서 추가하세요.";

            return string.Empty;
        }
    }
}
