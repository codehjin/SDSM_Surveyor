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
        if (!string.IsNullOrWhiteSpace(_vm.ExchangeFolder))
            dlg.InitialDirectory = _vm.ExchangeFolder;
        if (dlg.ShowDialog() == true)
            _vm.ExchangeFolder = dlg.FolderName;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
