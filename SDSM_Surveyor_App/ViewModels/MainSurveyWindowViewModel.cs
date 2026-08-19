using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>메인 창(셸) ViewModel.</summary>
public partial class MainSurveyWindowViewModel : ObservableObject, ISingletonService
{
    [ObservableProperty] private string _windowTitle = "SDSM 조사자 입력";

    [ObservableProperty] private string _version =
        $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "0.1"}";
}
