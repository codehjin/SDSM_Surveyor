using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using SDSM_Models;
using SDSM_Surveyor_App.Helpers;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 지점 지도 카탈로그. <c>%AppData%\SDSM_Surveyor\maps\maps.json</c> 을 읽고 쓴다.
///
/// 첫 실행에는 실행폴더에 함께 배포한 <c>maps\</c> 를 앱 데이터로 **복사**한다.
/// 그래야 [교체]·[삭제] 가 설치 폴더를 건드리지 않는다(<c>sites.json</c> 과 다른 점이다 —
/// 지도는 조사자가 직접 고치는 자료다).
///
/// ⚠ 파일이 없거나 깨져도 예외를 던지지 않는다. 빈 카탈로그로 열고 화면이 안내한다.
/// ⚠ 지도 폴더는 사용자 실자료다. 이 클래스는 폴더 자체를 지우지 않는다(CLAUDE.md §6).
/// </summary>
public class MapCatalogProvider : IMapCatalogProvider, ISingletonService
{
    private static readonly JsonSerializerOptions ReadOpts = new() { PropertyNameCaseInsensitive = true };

    // 사람이 열어 고치는 파일이라 한글을 \uXXXX 로 이스케이프하지 않는다.
    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private MapCatalog _cat = new();

    public MapCatalogProvider() => Reload();

    // ⚠ getter 에서 폴더를 만들지 않는다. PathOf 는 화면 그리기 경로에서 try/catch 없이 불리는데,
    //    AppData 가 정책으로 잠긴 PC 에서 CreateDirectory 가 던지면 입력 중이던 야장째로 앱이 죽는다.
    //    폴더 생성은 쓰기 직전에만 한다(EnsureDir).
    public string MapsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SDSM_Surveyor", "maps");

    private static void EnsureDir(string dir)
    {
        try { Directory.CreateDirectory(dir); }
        catch { /* 못 만들면 이어지는 파일 작업이 실패하고 그쪽에서 안내한다 */ }
    }

    private string CatalogPath => Path.Combine(MapsDir, "maps.json");

    private static string BundleDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "maps");

    public MapCatalog Catalog => _cat;

    /// <summary>
    /// <b>읽기 전용 경로</b>. 앱 데이터에 없으면 설치 폴더 시드까지 본다(복사가 중간에 끊긴 경우).
    ///
    /// ⚠ 이 값을 쓰기 대상으로 쓰면 안 된다 — 설치 폴더의 시드를 덮거나 지우게 된다.
    ///   [교체]·[삭제]는 <see cref="WritePathOf"/> 를 쓴다.
    /// </summary>
    public string PathOf(SiteMap map)
    {
        var inData = WritePathOf(map);
        if (inData.Length == 0 || File.Exists(inData)) return inData;

        // ⚠ 시드로 되돌아보는 건 **기본 탑재 지도만** 이다(복사가 중간에 끊긴 경우 대비).
        //    사용자가 추가한 지도까지 파일명만 보고 폴백하면, 이름이 겹칠 때 다른 그림이
        //    그 지도의 보정으로 그려진다 — 마커가 엉뚱한 데 찍히는데 오류도 안 뜬다.
        if (!string.Equals(map.Source, "기본탑재", StringComparison.Ordinal)) return inData;

        var inBundle = Path.Combine(BundleDir, Path.GetFileName(inData));
        return File.Exists(inBundle) ? inBundle : inData;
    }

    /// <summary>
    /// 설치 폴더 사본. 앱 데이터 쪽 파일을 못 읽을 때(권한·디코딩 실패) 화면이 여기로 재시도한다.
    /// 읽기 전용으로만 쓴다 — 절대 여기에 쓰지 않는다.
    /// </summary>
    public string? BundlePathOf(SiteMap map)
    {
        var name = Path.GetFileName(map.File ?? string.Empty);
        if (name.Length == 0) return null;
        var p = Path.Combine(BundleDir, name);
        return File.Exists(p) ? p : null;
    }

    /// <summary>
    /// <b>쓰기 경로</b>. 언제나 앱 데이터 <c>maps\</c> 안이다 — 설치 폴더는 절대 건드리지 않는다.
    /// 시드 지도를 [교체]하면 앱 데이터에 사용자 사본이 새로 생기고, 원본 시드는 그대로 남는다.
    /// </summary>
    private string WritePathOf(SiteMap map)
    {
        // 파일명만 쓴다 — maps.json 을 손으로 고치다 경로가 섞여 들어와도 폴더 밖으로 나가지 않는다.
        var name = Path.GetFileName(map.File ?? string.Empty);
        return string.IsNullOrEmpty(name) ? string.Empty : Path.Combine(MapsDir, name);
    }

    public void Reload()
    {
        try
        {
            EnsureDir(MapsDir);
            SeedFromBundleIfEmpty();

            if (File.Exists(CatalogPath))
            {
                var json = File.ReadAllText(CatalogPath);
                _cat = JsonSerializer.Deserialize<MapCatalog>(json, ReadOpts) ?? new MapCatalog();
            }
            else
            {
                _cat = new MapCatalog();
            }
        }
        catch
        {
            // 손상된 maps.json 때문에 앱이 멈추면 안 된다. 화면이 "등록된 지도 없음"으로 안내한다.
            _cat = new MapCatalog();
        }

        _cat.Maps ??= new List<SiteMap>();
        foreach (var m in _cat.Maps) m.Source ??= "기본탑재";

        UpgradeSeedIfNewer();
        RepairMissingFromBundle();
    }

    /// <summary>앱 데이터에 maps.json 이 없고 실행폴더에 시드가 있으면 통째로 복사한다.</summary>
    private void SeedFromBundleIfEmpty()
    {
        if (File.Exists(CatalogPath)) return;
        if (!Directory.Exists(BundleDir)) return;

        foreach (var src in Directory.EnumerateFiles(BundleDir))
        {
            var dst = Path.Combine(MapsDir, Path.GetFileName(src));
            if (!File.Exists(dst)) File.Copy(src, dst);
        }
    }

    /// <summary>
    /// 설치 폴더 시드의 <c>version</c> 이 앱 데이터 쪽과 다르면 기본탑재 지도를 통째로 갈아 끼운다.
    ///
    /// 왜 필요한가 — 조사자 PC 에는 작년 카탈로그가 이미 앱 데이터에 들어 있다.
    /// <see cref="SeedFromBundleIfEmpty"/> 는 maps.json 이 <b>없을 때만</b> 돌기 때문에,
    /// 새 지도를 넣은 앱을 새로 배포해도 앱 데이터가 옛 카탈로그를 계속 붙들고 있다.
    /// 매해 지도를 메일로 주고받는 운영 방식에서 이 경로가 없으면 갱신이 영영 반영되지 않는다.
    ///
    /// ⚠ 조사자가 [추가]한 지도(<c>사용자추가</c>)는 그대로 살린다 — 그 사람 자료다.
    /// ⚠ 기본탑재 지도에 조사자가 손으로 넣은 보정은 새 시드 값으로 덮인다. 같은 지도의
    ///   새 판이 온 것이므로 옛 보정을 유지하는 편이 오히려 위험하다.
    /// </summary>
    private void UpgradeSeedIfNewer()
    {
        try
        {
            var seedPath = Path.Combine(BundleDir, "maps.json");
            if (!File.Exists(seedPath)) return;

            var seed = JsonSerializer.Deserialize<MapCatalog>(File.ReadAllText(seedPath), ReadOpts);
            if (seed?.Maps is not { Count: > 0 }) return;

            var seedVer = seed.Version?.Trim() ?? string.Empty;
            var haveVer = _cat.Version?.Trim() ?? string.Empty;
            if (seedVer.Length == 0 || string.Equals(seedVer, haveVer, StringComparison.Ordinal)) return;

            // 조사자가 직접 넣은 지도만 남기고, 기본탑재는 새 시드로 교체한다.
            var mine = _cat.Maps
                .Where(m => !string.Equals(m.Source, "기본탑재", StringComparison.Ordinal))
                .ToList();

            foreach (var m in seed.Maps) m.Source ??= "기본탑재";

            _cat.Version = seed.Version;
            _cat.Note = seed.Note;
            _cat.Maps = seed.Maps.Concat(mine).ToList();

            EnsureDir(MapsDir);
            foreach (var src in Directory.EnumerateFiles(BundleDir))
            {
                var dst = Path.Combine(MapsDir, Path.GetFileName(src));
                try { File.Copy(src, dst, overwrite: true); }
                catch { /* 한 장이 잠겨 있어도 나머지는 계속 */ }
            }

            Save();
        }
        catch
        {
            // 갱신에 실패해도 옛 카탈로그로 계속 쓴다. 조사를 막지 않는다.
        }
    }

    /// <summary>
    /// 카탈로그에 적힌 png 가 앱 데이터에 없으면 설치 폴더 사본에서 다시 채운다.
    ///
    /// 왜 필요한가 — 시드 복사가 중간에 끊기거나, 조사자가 폴더를 정리하다 png 만 지우거나,
    /// 파일이 접근 불가 상태(권한)로 남으면 <c>File.Exists</c> 가 false 가 되어 그림이 안 뜬다.
    /// 그때마다 [교체]를 요구하는 대신, 실행할 때 조용히 되메운다.
    /// ⚠ 있는 파일은 건드리지 않는다 — 조사자가 바꿔 넣은 지도를 시드로 되돌리면 안 된다.
    /// </summary>
    private void RepairMissingFromBundle()
    {
        if (!Directory.Exists(BundleDir)) return;

        foreach (var m in _cat.Maps)
        {
            try
            {
                var name = Path.GetFileName(m.File ?? string.Empty);
                if (name.Length == 0) continue;

                var dst = Path.Combine(MapsDir, name);
                if (File.Exists(dst)) continue;

                var src = Path.Combine(BundleDir, name);
                if (File.Exists(src)) File.Copy(src, dst, overwrite: true);
            }
            catch
            {
                // 한 장을 못 되메워도 나머지는 계속한다. 화면이 그 한 장의 이유를 따로 안내한다.
            }
        }
    }

    public void Save()
    {
        try
        {
            EnsureDir(MapsDir);
            File.WriteAllText(CatalogPath, JsonSerializer.Serialize(_cat, WriteOpts));
        }
        catch
        {
            // 저장 실패(권한·잠금)로 조사를 막지 않는다. 다음 저장에서 다시 시도된다.
        }
    }

    // ── 조회 ──────────────────────────────────────────────────────────────────

    public IReadOnlyList<SiteMap> ForSite(SurveySite? site, string? project)
    {
        // ① 그 지점에 딱 맞는 것
        var hits = _cat.Maps.Where(m => AppliesExactly(m, site, project)).ToList();

        // ② 없으면 대분류만 맞는 것
        if (hits.Count == 0)
            hits = _cat.Maps.Where(m => ProjectMatches(m, project)).ToList();

        // ③ 그것도 없으면 전부. 빈 목록을 주면 조사자가 [지도 관리]로 갈 이유를 알 수 없다.
        if (hits.Count == 0)
            hits = _cat.Maps.ToList();

        // ④ 적용 대상을 아직 안 정한 지도([추가] 직후)는 어느 단에서 빠졌든 후보로 되살린다.
        //    ①이 채워지면 ②·③이 아예 돌지 않으므로, 이게 없으면 방금 추가한 지도가
        //    지도 고르기 목록에서 통째로 사라진다. 1순위가 되는 것은 AppliesExactly 가 계속 막는다.
        foreach (var m in _cat.Maps)
            if (m.AppliesTo is null && !hits.Contains(m)) hits.Add(m);

        // 최신 지도를 맨 앞으로. 나머지는 파일에 적힌 순서를 유지한다.
        return hits.OrderByDescending(m => m.IsLatest).ToList();
    }

    public bool AppliesExactly(SiteMap map, SurveySite? site, string? project)
    {
        var a = map.AppliesTo;

        // ⚠ 적용 대상이 비어 있으면 '어디에나 딱 맞는 지도' 가 아니라 '아직 권역을 안 정한 지도' 다.
        //    true 로 두면 [추가] 직후의 지도가 모든 지점에서 1순위가 되고 '다른 권역' 경고까지 꺼진다.
        //    목록에서 사라지지 않게 하는 것은 ForSite ④단이 맡는다(②·③은 ①이 빌 때만 돈다).
        if (a is null) return false;
        if (!ProjectMatches(map, project)) return false;

        if (a.Sites is not { Count: > 0 }) return true;

        // 지점을 아직 안 골랐으면 지점 조건은 볼 수 없다 — 맞다고 단정하지 않는다.
        var name = site?.SiteName?.Trim();
        if (string.IsNullOrEmpty(name)) return false;

        return a.Sites.Any(s => string.Equals(s?.Trim(), name, StringComparison.Ordinal));
    }

    private static bool ProjectMatches(SiteMap m, string? project)
    {
        var want = m.AppliesTo?.Project?.Trim();
        if (string.IsNullOrEmpty(want)) return true;             // 대분류를 안 정한 지도는 어디서나
        if (string.IsNullOrWhiteSpace(project)) return true;     // 대분류를 아직 안 골랐으면 거르지 않는다
        return string.Equals(want, project.Trim(), StringComparison.Ordinal);
    }

    public SiteMap? Latest(SurveySite? site, string? project)
    {
        var list = ForSite(site, project);
        return list.FirstOrDefault(m => m.IsLatest) ?? list.FirstOrDefault();
    }

    // ── 편집 (승인·잠금 없음) ──────────────────────────────────────────────────

    public SiteMap? Add(string sourceFilePath, string name)
    {
        try
        {
            if (!File.Exists(sourceFilePath)) return null;

            // ⚠ 복사보다 먼저 읽는다. WPF 가 못 읽는 파일(확장자만 png · CMYK jpg 등)은 (0,0) 이
            //    돌아오는데, 그대로 넣으면 BoundsFrom 이 퇴화 범위를 만들어 보정도 마커도
            //    영영 안 되는 지도가 카탈로그에 남는다.
            var (w, h) = ImageSize.Read(sourceFilePath);
            if (w <= 0 || h <= 0) return null;

            EnsureDir(MapsDir);

            var fileName = UniqueFileName(Path.GetFileName(sourceFilePath));
            File.Copy(sourceFilePath, Path.Combine(MapsDir, fileName), overwrite: false);

            var map = new SiteMap
            {
                Id = UniqueId(Path.GetFileNameWithoutExtension(fileName)),
                Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(fileName) : name.Trim(),
                File = fileName,
                Width = w,
                Height = h,
                Source = "사용자추가"
            };
            _cat.Maps.Add(map);
            Save();
            return map;
        }
        catch
        {
            return null;
        }
    }

    public bool Replace(SiteMap map, string sourceFilePath, out bool calibrationCleared)
    {
        calibrationCleared = false;
        try
        {
            if (!File.Exists(sourceFilePath)) return false;

            // ⚠ 반드시 덮어쓰기 **전에** 읽는다. 못 읽는 파일이면 (0,0) 이 돌아오는데,
            //    복사를 먼저 하면 원본을 잃은 뒤에 보정까지 지우게 되어 되돌릴 수 없다.
            var (w, h) = ImageSize.Read(sourceFilePath);
            if (w <= 0 || h <= 0) return false;

            // ⚠ 반드시 쓰기 경로다. PathOf 를 쓰면 설치 폴더의 시드 png 를 덮어쓴다.
            var dst = WritePathOf(map);
            if (dst.Length == 0) return false;
            EnsureDir(MapsDir);
            File.Copy(sourceFilePath, dst, overwrite: true);

            // 픽셀 크기가 달라지면 옛 보정은 전부 틀린 값이 된다 — 지우고 재보정을 요구한다.
            if (w != map.Width || h != map.Height)
            {
                map.Calibration = null;
                map.Bounds = null;
                map.FitMaxResidualPx = null;
                calibrationCleared = true;
            }

            map.Width = w;
            map.Height = h;
            Save();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Remove(SiteMap map)
    {
        _cat.Maps.Remove(map);
        try
        {
            // ⚠ 앱 데이터의 사본만 지운다. 설치 폴더 시드를 지우면 재설치 없이는 되돌릴 수 없다.
            var p = WritePathOf(map);
            if (p.Length > 0 && File.Exists(p)) File.Delete(p);
        }
        catch
        {
            // 파일이 잠겨 있으면 카탈로그에서만 빠진다. 남은 파일은 다음 실행에서 지워도 된다.
        }
        Save();
    }

    public void SetLatest(SiteMap map)
    {
        // 같은 후보 그룹의 최신 표시를 내린다.
        // ⚠ 대분류가 비어 있는 지도는 Matches 가 '모든 권역의 후보' 로 취급한다.
        //    그래서 비교를 대분류 문자열로만 하면, 대분류 없이 추가한 지도를 최신으로 지정해도
        //    시드 지도의 최신 표시가 남아 '최신'이 둘이 되고 지정이 조용히 무시된다.
        // ⚠ 대분류가 빈 지도를 최신으로 지정하면 모든 대분류의 최신 표시가 내려간다
        //    (그 지도가 모든 권역의 후보이기 때문이다). 의도한 동작이다.
        static string Key(SiteMap m) => m.AppliesTo?.Project?.Trim() ?? string.Empty;

        var target = Key(map);
        foreach (var m in _cat.Maps)
            if (target.Length == 0 || Key(m).Length == 0 || string.Equals(Key(m), target, StringComparison.Ordinal))
                m.IsLatest = false;

        map.IsLatest = true;
        Save();
    }

    private string UniqueFileName(string name)
    {
        var safe = Path.GetFileName(name);
        var stem = Path.GetFileNameWithoutExtension(safe);
        var ext = Path.GetExtension(safe);
        var n = 1;
        while (File.Exists(Path.Combine(MapsDir, safe)))
            safe = $"{stem}_{++n}{ext}";
        return safe;
    }

    private string UniqueId(string stem)
    {
        var id = stem;
        var n = 1;
        while (_cat.Maps.Any(m => string.Equals(m.Id, id, StringComparison.Ordinal)))
            id = $"{stem}_{++n}";
        return id;
    }
}
