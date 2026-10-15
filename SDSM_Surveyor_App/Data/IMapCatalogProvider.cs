using SDSM_Models;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>지점 지도 카탈로그 제공자. 조사자앱과 관리자앱이 같은 폴더 구조를 쓴다.</summary>
public interface IMapCatalogProvider
{
    /// <summary>
    /// 지도 폴더(<c>%AppData%\SDSM_Surveyor\maps</c>). 경로만 돌려준다 —
    /// 폴더 생성은 쓰기 직전에만 한다(읽기 경로에서 던지면 앱이 죽는다).
    /// </summary>
    string MapsDir { get; }

    /// <summary>현재 카탈로그. 파일이 없으면 빈 카탈로그다(예외를 던지지 않는다).</summary>
    MapCatalog Catalog { get; }

    /// <summary>지도 하나의 실제 파일 경로(앱 데이터 우선).</summary>
    string PathOf(SiteMap map);

    /// <summary>
    /// 설치 폴더에 함께 배포한 사본 경로. 없으면 null.
    /// 앱 데이터 쪽 파일을 못 읽을 때 화면이 여기로 한 번 더 시도한다.
    /// </summary>
    string? BundlePathOf(SiteMap map);

    /// <summary>
    /// 이 지점(과 대분류)에 보여 줄 지도 목록. 최신 지도가 맨 앞이다.
    ///
    /// 딱 맞는 지도가 없으면 **빈 목록을 돌려주지 않는다** — 대분류만 맞는 것, 그것도 없으면 전체를 준다.
    /// 지도를 아예 안 보여 주면 조사자가 [지도 관리]로 갈 이유를 알 수 없다.
    /// 그 지점 것이 맞는지는 <see cref="AppliesExactly"/> 로 판단해 화면이 안내한다.
    /// </summary>
    IReadOnlyList<SiteMap> ForSite(SurveySite? site, string? project);

    /// <summary>이 지도의 적용 대상에 그 지점이 실제로 들어 있는지.</summary>
    bool AppliesExactly(SiteMap map, SurveySite? site, string? project);

    /// <summary>기본으로 열 지도. <c>IsLatest</c> 가 없으면 목록 첫 장.</summary>
    SiteMap? Latest(SurveySite? site, string? project);

    /// <summary>파일에서 다시 읽는다.</summary>
    void Reload();

    /// <summary>카탈로그를 파일에 쓴다.</summary>
    void Save();

    /// <summary>png/jpg 를 지도 폴더로 복사하고 카탈로그에 넣는다. 실패하면 null.</summary>
    SiteMap? Add(string sourceFilePath, string name);

    /// <summary>이미지 파일만 바꾼다. 픽셀 크기가 달라지면 보정을 지운다(재보정이 필요하다).</summary>
    bool Replace(SiteMap map, string sourceFilePath, out bool calibrationCleared);

    /// <summary>
    /// 카탈로그에서 빼고 이미지 파일도 지운다(기본 탑재 지도도 마찬가지다).
    /// 지운 뒤에는 maps.json 이 남아 있으므로 다음 실행에 시드가 되살아나지 않는다 —
    /// 되살리려면 앱 데이터의 maps 폴더를 통째로 지우고 다시 실행해야 한다.
    /// </summary>
    void Remove(SiteMap map);

    /// <summary>이 지도를 그 권역의 기본 지도로 삼는다.</summary>
    void SetLatest(SiteMap map);
}
