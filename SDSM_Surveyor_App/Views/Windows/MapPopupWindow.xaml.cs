using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>
/// 지점 지도 창. 최신 지도 한 장 위에 현재 입력 좌표를 찍는다.
///
/// 그리기는 코드비하인드가 한다 — 픽셀 좌표를 Canvas 에 놓는 일이라
/// 바인딩으로 옮기면 오히려 어긋나기 쉽다.
/// </summary>
public partial class MapPopupWindow : Window
{
    private readonly MapPopupViewModel _vm;
    private readonly IMapCatalogProvider _maps;

    public MapPopupWindow()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<MapPopupViewModel>();
        _maps = App.Current.Services.GetRequiredService<IMapCatalogProvider>();
        DataContext = _vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MapPopupViewModel.Selected)) Render();
        };

        Loaded += (_, _) => Render();
    }

    /// <summary>고른 지도를 띄우고 마커를 찍는다.</summary>
    private void Render()
    {
        if (_vm.Selected is null)
        {
            Viewer.SetImage(null);
            return;
        }

        var ok = Viewer.SetImage(_maps.PathOf(_vm.Selected));
        var why = Viewer.LastError;

        // 앱 데이터 쪽을 못 읽으면 설치 폴더에 함께 배포한 사본으로 한 번 더 시도한다.
        if (!ok)
        {
            var alt = _maps.BundlePathOf(_vm.Selected);
            if (alt is not null)
            {
                ok = Viewer.SetImage(alt);
                if (!ok) why += "\n\n[설치 폴더 사본도 실패]\n" + Viewer.LastError;
            }
            else
            {
                why += "\n\n설치 폴더에도 사본이 없습니다.";
            }
        }

        if (!ok)
        {
            // ⚠ 이유를 그대로 보여 준다. "찾지 못했습니다" 한 줄만 남으면
            //    경로가 틀린 것인지 · 권한인지 · 디코딩 실패인지 가릴 수 없다.
            _vm.LoadError = $"「{_vm.Selected.Display}」 이미지를 열지 못했습니다.\n{why}";
            Viewer.HideMarker();
            return;
        }
        _vm.LoadError = null;

        var p = _vm.MarkerPixel;
        if (p is null) Viewer.HideMarker();
        else Viewer.ShowMarker(p.Value.X, p.Value.Y, _vm.MarkerLabel);
    }

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        Viewer.Fit();
        Render();
    }

    private void Manage_Click(object sender, RoutedEventArgs e)
    {
        new MapManagerWindow { Owner = this }.ShowDialog();
        _vm.Reload();     // 관리 창에서 바꾼 것을 반영한다
        Render();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
