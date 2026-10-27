using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Core.Helpers;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>
/// 지도 관리 — 추가·교체·이름변경·삭제·최신 지정·2점 보정.
///
/// 승인이나 잠금은 없다. 지도는 참고 자료이고, 조사자앱·관리자앱 어느 쪽에서도
/// 자유롭게 고칠 수 있다(부사장 확정 2026-09-09).
///
/// 남겨 둔 안전장치는 둘뿐이고 둘 다 <b>정확성</b> 문제라서 남겼다.
///  1. 교체한 이미지의 픽셀 크기가 다르면 보정을 지운다 — 안 그러면 마커가 엉뚱한 데 찍힌다
///  2. 보정 두 점이 너무 가까우면 경고한다 — 오차가 크게 벌어진다(막지는 않는다)
/// </summary>
public partial class MapManagerViewModel : ObservableObject, ITransientService
{
    private readonly IMapCatalogProvider _maps;

    // 지금 야장의 대분류·지점. [이 지점에 적용] 이 이 값을 지도의 적용 대상에 넣는다.
    private readonly SurveyMeta _meta;

    public MapManagerViewModel(IMapCatalogProvider maps, SurveyMeta meta)
    {
        _maps = maps;
        _meta = meta;
        Reload();
        // 폴더 경로는 창 아래에 항상 붙여 둔다(MapsDir). Message 는 방금 한 일만 말한다.
        Message = "지도를 골라 [보정 시작] 으로 위치를 맞추거나, [추가]·[교체] 로 이미지를 바꿉니다.";
    }

    /// <summary>지도 파일이 실제로 놓이는 폴더. 조사자가 직접 넣거나 확인할 수 있어야 한다.</summary>
    public string MapsDir => _maps.MapsDir;

    public RadObservableCollection<SiteMap> Maps { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedInfo))]
    [NotifyPropertyChangedFor(nameof(CalibrationText))]
    [NotifyPropertyChangedFor(nameof(CanApplyToCurrent))]
    private SiteMap? _selected;

    public bool HasSelection => Selected is not null;

    /// <summary>고른 지도의 상태 한 줄. 적용 권역까지 보여 준다 — 미지정이면 그 지점 지도로 안 쓰인다.</summary>
    public string SelectedInfo
    {
        get
        {
            if (Selected is null) return "지도를 고르세요.";

            var a = Selected.AppliesTo;
            var scope = string.IsNullOrWhiteSpace(a?.Project)
                ? "적용 권역 미지정"
                : (a!.Sites.Count > 0 ? $"{a.Project} · 지점 {a.Sites.Count}곳" : a.Project!);

            return $"{Selected.File} · {Selected.SizeText}px · "
                 + $"{(Selected.HasCalibration ? "보정 있음" : "보정 없음")} · {scope}"
                 + (Selected.IsLatest ? " · 최신" : string.Empty);
        }
    }

    /// <summary>지금 야장의 대분류를 이 지도에 붙일 수 있는지.</summary>
    public bool CanApplyToCurrent => Selected is not null && !string.IsNullOrWhiteSpace(_meta.Project);

    /// <summary>
    /// 지금 야장의 대분류(와 고른 지점)를 이 지도의 적용 대상에 넣는다.
    /// 이게 없으면 [추가]한 지도가 어느 지점에도 '딱 맞는 지도'가 되지 못해
    /// 팝업이 계속 "다른 권역 지도" 라고 안내한다.
    /// </summary>
    [RelayCommand]
    private void ApplyToCurrent()
    {
        if (Selected is null || string.IsNullOrWhiteSpace(_meta.Project)) return;

        Selected.AppliesTo ??= new MapAppliesTo();
        Selected.AppliesTo.Project = _meta.Project!.Trim();

        var site = _meta.SelectedSite?.SiteName?.Trim();
        if (!string.IsNullOrEmpty(site) && !Selected.AppliesTo.Sites.Contains(site))
            Selected.AppliesTo.Sites.Add(site);

        _maps.Save();
        RefreshRow(Selected);

        // ⚠ 적용 대상만 채우는 것으로는 기본 지도가 되지 않는다 — 그 권역에 이미 '최신'인 시드가
        //    있으면 Latest() 가 계속 그것을 고른다. 순서를 조사자에게 알려 준다.
        var latestNote = Selected.IsLatest ? string.Empty : " 기본으로 열리게 하려면 [최신으로] 도 눌러 주세요.";

        Message = string.IsNullOrEmpty(site)
            ? $"「{Selected.Display}」 을(를) '{_meta.Project}' 대분류 지도로 정했습니다.{latestNote}"
            : $"「{Selected.Display}」 을(를) '{_meta.Project} · {site}' 지도로 정했습니다.{latestNote}";
        OnPropertyChanged(nameof(SelectedInfo));
    }

    // 행을 갈아 끼우는 동안 그리드가 SelectedItem 을 잠깐 놓는다(TwoWay 라 그 값이 Selected 로
    // 되돌아온다). 그 중간값이 창까지 가면 창이 이미지를 비웠다 다시 읽어 확대·이동이 초기화된다
    // — 교체 구간에서는 Selected 알림만 삼키고, 끝난 뒤 원래 값으로 한 번만 알린다.
    private bool _swappingRow;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_swappingRow && e.PropertyName == nameof(Selected)) return;
        base.OnPropertyChanged(e);
    }

    // SiteMap 은 INotifyPropertyChanged 가 아니라 값을 고쳐도 그리드가 다시 그리지 않는다.
    // 그래서 컬렉션에서 그 행만 갈아 끼운다.
    private void RefreshRow(SiteMap m)
    {
        var i = Maps.IndexOf(m);
        if (i < 0) return;
        var keep = Selected;
        _swappingRow = true;
        try
        {
            Maps.RemoveAt(i);
            Maps.Insert(i, m);
        }
        finally { _swappingRow = false; }
        Selected = keep;   // 값이 원래대로 돌아오므로 창의 _shownPath 비교가 재로딩을 막아 준다
    }

    /// <summary>최신 표시는 여러 행에서 동시에 바뀐다(다른 지도의 표시가 내려간다).
    /// 한 행씩 갈아 끼우면 선택이 그만큼 흔들리므로 Reload 와 같은 방식으로 한 번에 다시 그린다.</summary>
    private void RefreshAllRows()
    {
        var keep = Selected;
        var all = Maps.ToList();
        _swappingRow = true;
        try
        {
            Maps.SuspendNotifications();
            Maps.Clear();
            foreach (var m in all) Maps.Add(m);
            Maps.ResumeNotifications();
        }
        finally { _swappingRow = false; }
        Selected = keep;
    }

    /// <summary>안내·오류 문구.</summary>
    [ObservableProperty] private string? _message;

    public void Reload()
    {
        _maps.Reload();
        Maps.SuspendNotifications();
        Maps.Clear();
        foreach (var m in _maps.Catalog.Maps) Maps.Add(m);
        Maps.ResumeNotifications();
        Selected = Maps.FirstOrDefault();
    }

    /// <summary>지도 파일의 실제 경로(미리보기용).</summary>
    public string? SelectedPath => Selected is null ? null : _maps.PathOf(Selected);

    /// <summary>설치 폴더 사본 경로(앱 데이터 쪽을 못 읽을 때 창이 재시도한다).</summary>
    public string? BundlePathOf(SiteMap map) => _maps.BundlePathOf(map);

    // ── 추가 · 교체 · 삭제 ────────────────────────────────────────────────────

    /// <summary>파일 대화상자에서 고른 이미지를 추가한다(창이 경로를 넘긴다).</summary>
    public void AddFile(string path)
    {
        var m = _maps.Add(path, System.IO.Path.GetFileNameWithoutExtension(path));
        if (m is null) { Message = "이미지를 읽지 못했습니다. png · jpg 원본인지 확인해 주세요."; return; }

        Maps.Add(m);
        Selected = m;
        Message = $"추가했습니다 — {m.SizeText}px. "
                + "[이 지점에 적용]으로 권역을 정하고 [보정 시작]으로 위치를 맞춰 주세요.";
    }

    /// <summary>고른 지도의 이미지 파일만 바꾼다.</summary>
    public void ReplaceFile(string path)
    {
        if (Selected is null) return;

        if (!_maps.Replace(Selected, path, out var cleared))
        {
            Message = "이미지를 바꾸지 못했습니다. 파일이 열려 있는지 확인해 주세요.";
            return;
        }

        Message = cleared
            ? "이미지를 바꿨습니다. 픽셀 크기가 달라져 보정을 지웠습니다 — 다시 보정해 주세요."
            : "이미지를 바꿨습니다. 보정은 그대로 씁니다.";

        RefreshRow(Selected!);
        OnPropertyChanged(nameof(SelectedInfo));
        OnPropertyChanged(nameof(CalibrationText));
    }

    /// <summary>지도 삭제 확인 글 — 지울 지도 이름 · 건수 · 되돌릴 수 없음(§32 · S4). 창 문구와 시험이 같은 글을 쓴다.</summary>
    public static string DeleteConfirmText(SiteMap m) =>
        $"'{m.Display}' 지도 1장을 지울까요?\n이미지 파일도 함께 지워지며 되돌릴 수 없습니다.";

    [RelayCommand]
    private void Delete()
    {
        if (Selected is null) return;
        var gone = Selected;
        _maps.Remove(gone);
        Maps.Remove(gone);
        Selected = Maps.FirstOrDefault();
        Message = $"지웠습니다 — {gone.Display}";
    }

    [RelayCommand]
    private void SaveName()
    {
        if (Selected is null) return;
        _maps.Save();

        RefreshRow(Selected!);

        Message = "이름을 저장했습니다.";
        OnPropertyChanged(nameof(SelectedInfo));
    }

    [RelayCommand]
    private void SetLatest()
    {
        if (Selected is null) return;
        _maps.SetLatest(Selected);
        RefreshAllRows();   // 다른 지도의 '최신' 표시도 함께 내려간다
        Message = $"「{Selected!.Display}」 을(를) 기본 지도로 정했습니다.";
        OnPropertyChanged(nameof(SelectedInfo));
    }

    // ── 2점 보정 ──────────────────────────────────────────────────────────────

    /// <summary>0 = 보정 아님 · 1 = 기준점 1 클릭 대기 · 2 = 기준점 2 클릭 대기 · 3 = 좌표 입력 대기.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCalibrating))]
    [NotifyPropertyChangedFor(nameof(CalibrationText))]
    private int _step;

    public bool IsCalibrating => Step > 0;

    private double? _p1X, _p1Y, _p2X, _p2Y;

    [ObservableProperty] private string? _lat1;
    [ObservableProperty] private string? _lon1;
    [ObservableProperty] private string? _lat2;
    [ObservableProperty] private string? _lon2;

    public string CalibrationText => Step switch
    {
        1 => "기준점 1 — 지도에서 위치를 아는 지점을 누르세요.",
        2 => "기준점 2 — 첫 점에서 멀리 떨어진 지점을 누르세요.",
        3 => "두 점의 위도·경도를 넣고 [보정 계산]을 누르세요.",
        _ => Selected is null ? string.Empty
             : Selected.HasCalibration ? "보정되어 있습니다. 다시 맞추려면 [보정 시작]을 누르세요."
             : "보정이 없어 위치 표시를 못 합니다. [보정 시작]을 누르세요."
    };

    [RelayCommand]
    private void StartCalibrate()
    {
        if (Selected is null) return;
        _p1X = _p1Y = _p2X = _p2Y = null;
        Lat1 = Lon1 = Lat2 = Lon2 = null;
        Step = 1;
        Message = "북쪽이 위인 지도에서만 정확합니다. 기울어지거나 비스듬히 찍은 지도는 맞지 않습니다.";
    }

    [RelayCommand]
    private void CancelCalibrate()
    {
        Step = 0;
        Message = null;
    }

    /// <summary>지도를 클릭했다(이미지 픽셀 좌표). 창이 넘긴다.</summary>
    public void Pick(double px, double py)
    {
        if (Step == 1) { _p1X = px; _p1Y = py; Step = 2; }
        else if (Step == 2) { _p2X = px; _p2Y = py; Step = 3; }
    }

    /// <summary>이미 찍은 점(창이 다시 그릴 때 쓴다).</summary>
    public (double X, double Y)? Point1 => _p1X is double x && _p1Y is double y ? (x, y) : null;
    public (double X, double Y)? Point2 => _p2X is double x && _p2Y is double y ? (x, y) : null;

    [RelayCommand]
    private void ApplyCalibrate()
    {
        if (Selected is null) return;
        if (_p1X is not double px1 || _p1Y is not double py1 || _p2X is not double px2 || _p2Y is not double py2)
        {
            Message = "지도에서 기준점 두 곳을 먼저 눌러 주세요.";
            return;
        }

        var la1 = Coordinate.ToDecimal(Lat1);
        var lo1 = Coordinate.ToDecimal(Lon1);
        var la2 = Coordinate.ToDecimal(Lat2);
        var lo2 = Coordinate.ToDecimal(Lon2);
        if (la1 is null || lo1 is null || la2 is null || lo2 is null)
        {
            Message = "위도·경도를 읽지 못했습니다 — 십진(36.763081) 또는 도분초(36°45'47.09\") 형식으로 넣어 주세요.";
            return;
        }

        var cal = MapProjection.FitTwoPoint(px1, py1, la1.Value, lo1.Value, px2, py2, la2.Value, lo2.Value);
        if (cal is null)
        {
            Message = "두 점의 위도(또는 경도)가 같아 계산할 수 없습니다. 대각선으로 떨어진 두 점을 골라 주세요.";
            return;
        }

        var warn = MapProjection.PointsTooClose(px1, py1, px2, py2, Selected.Width, Selected.Height)
            ? " ⚠ 두 점이 가까워 오차가 커질 수 있습니다."
            : string.Empty;

        Selected.Calibration = cal;
        Selected.Bounds = MapProjection.BoundsFrom(cal, Selected.Width, Selected.Height);
        Selected.FitMaxResidualPx = null;   // 2점 보정에는 잔차 개념이 없다
        _maps.Save();

        Step = 0;
        RefreshRow(Selected!);
        Message = "보정을 저장했습니다. 지도 팝업에서 위치가 맞는지 확인해 주세요." + warn;
        OnPropertyChanged(nameof(SelectedInfo));
        OnPropertyChanged(nameof(CalibrationText));
    }
}
