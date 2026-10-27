using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>동기화 창(교환소 폴더 지정 + 버전 비교 + 가져오기).</summary>
public partial class SyncWindow : Window
{
    private readonly SyncViewModel _vm;

    public SyncWindow()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<SyncViewModel>();
        DataContext = _vm;
    }

    // .NET 8 WPF 기본 폴더 선택 대화상자
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "교환소 폴더 선택" };
        // 교환소는 공유 · 클라우드 폴더일 수 있다 — 이미 정한 폴더가 있으면 그곳, 없으면 안전한 시작 폴더(§32 · S4)
        dlg.InitialDirectory = !string.IsNullOrWhiteSpace(_vm.ExchangeFolder) ? _vm.ExchangeFolder : Helpers.DialogFolder.Initial();
        if (dlg.ShowDialog() == true)
            _vm.ExchangeFolder = dlg.FolderName;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
