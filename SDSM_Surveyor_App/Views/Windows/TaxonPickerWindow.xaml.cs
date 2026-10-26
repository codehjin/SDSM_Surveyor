using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>
/// 첫 실행 — 담당 분류군 선택 창(README §1 · 시안 3c).
/// 「야장 열기」로 닫히면 <c>DialogResult = true</c>, 창을 닫으면 false 다.
/// </summary>
public partial class TaxonPickerWindow : Window, ITransientService
{
    private readonly TaxonPickerViewModel _vm;

    public TaxonPickerWindow()
    {
        InitializeComponent();

        _vm = App.Current.Services.GetRequiredService<TaxonPickerViewModel>();
        DataContext = _vm;

        _vm.RequestClose += OnRequestClose;
        _vm.RequestSessionBrowser += OnRequestSessionBrowser;
        Closed += (_, _) =>
        {
            _vm.RequestClose -= OnRequestClose;
            _vm.RequestSessionBrowser -= OnRequestSessionBrowser;
        };
    }

    /// <summary>캡션 바 버전 표기(셸과 같은 값).</summary>
    public string Version { get; } = Helpers.AppInfo.VersionText;   // 판 표시 한 곳(v0.5.0)

    private void OnRequestClose(object? sender, bool open)
    {
        DialogResult = open;
        Close();
    }

    /// <summary>
    /// 자료함에서 이어하기 — 세션을 불러온 뒤 그대로 야장으로 들어간다.
    /// 불러오기를 취소했으면 이 화면에 남는다(분류군을 다시 고를 수 있게).
    /// </summary>
    private void OnRequestSessionBrowser(object? sender, EventArgs e)
    {
        var loaded = new SessionBrowserWindow { Owner = this }.ShowDialog();
        if (loaded == true) OnRequestClose(this, true);
    }

    // 프로그램 정보(v0.5.0)
    private void Info_Click(object sender, RoutedEventArgs e) => new AppInfoWindow { Owner = this }.ShowDialog();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState != WindowState.Normal ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
