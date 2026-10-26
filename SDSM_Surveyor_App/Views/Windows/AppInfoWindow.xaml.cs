using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>프로그램 정보 창(v0.5.0 · §21-5) — 분류군 고르기 창 · 야장 창 위쪽 [정보] 단추가 연다.</summary>
public partial class AppInfoWindow : Window, ITransientService
{
    public AppInfoWindow()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<AppInfoWindowViewModel>();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
