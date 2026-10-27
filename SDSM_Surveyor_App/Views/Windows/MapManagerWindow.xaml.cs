using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>
/// 지도 관리 창. 파일 대화상자와 지도 클릭(보정 기준점)은 창이 맡고,
/// 판단과 저장은 ViewModel 이 한다.
/// </summary>
public partial class MapManagerWindow : Window
{
    private readonly MapManagerViewModel _vm;

    public MapManagerWindow()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<MapManagerViewModel>();
        DataContext = _vm;

        Viewer.PointPicked += (_, p) =>
        {
            _vm.Pick(p.X, p.Y);
            DrawPicks();
        };

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MapManagerViewModel.Selected):
                    Render();
                    break;
                case nameof(MapManagerViewModel.Step):
                    Viewer.PickMode = _vm.IsCalibrating;
                    DrawPicks();
                    break;
            }
        };

        Loaded += (_, _) => Render();
    }

    private string? _shownPath;

    /// <summary>
    /// 같은 파일이면 이미지를 다시 읽지 않는다 — 다시 읽으면 조사자의 확대·이동이 초기화된다.
    /// [추가]·[교체]는 파일 내용이 바뀌었으므로 <paramref name="force"/> 로 부른다.
    /// </summary>
    private void Render(bool force = false)
    {
        var path = _vm.SelectedPath;
        if (force || !string.Equals(path, _shownPath, StringComparison.Ordinal))
        {
            var ok = Viewer.SetImage(path);
            var why = Viewer.LastError;

            // 앱 데이터 쪽을 못 읽으면 설치 폴더 사본으로 한 번 더 시도한다.
            if (!ok && _vm.Selected is not null)
            {
                var alt = _vm.BundlePathOf(_vm.Selected);
                if (alt is not null)
                {
                    ok = Viewer.SetImage(alt);
                    if (!ok) why += "\n[설치 폴더 사본도 실패] " + Viewer.LastError;
                }
            }

            if (!ok && why is not null) _vm.Message = why.Replace("\n", " · ");
            _shownPath = path;
        }
        Viewer.PickMode = _vm.IsCalibrating;
        DrawPicks();
    }

    private void DrawPicks()
    {
        Viewer.ClearPicks();
        if (_vm.Point1 is { } a) Viewer.SetPick(0, a.X, a.Y);
        if (_vm.Point2 is { } b) Viewer.SetPick(1, b.X, b.Y);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "지도 이미지 고르기",
            Filter = "이미지 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            InitialDirectory = Helpers.DialogFolder.Initial(),   // 시작 폴더(§32 · S4)
        };
        if (dlg.ShowDialog() != true) return;
        _vm.AddFile(dlg.FileName);
        Render(force: true);
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected is null) return;

        var dlg = new OpenFileDialog
        {
            Title = "바꿀 지도 이미지 고르기",
            Filter = "이미지 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            InitialDirectory = Helpers.DialogFolder.Initial(),   // 시작 폴더(§32 · S4)
        };
        if (dlg.ShowDialog() != true) return;
        _vm.ReplaceFile(dlg.FileName);
        Render(force: true);
    }

    // 삭제는 되돌릴 수 없으므로 반드시 확인을 받는다(자료함 삭제와 같은 방식 · 기본 단추 [취소] · §32 · S4).
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected is null) return;

        if (!DeleteConfirm.Ask(this, MapManagerViewModel.DeleteConfirmText(_vm.Selected), "지도 삭제")) return;

        _vm.DeleteCommand.Execute(null);
        Render(force: true);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
