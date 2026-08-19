using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Views.Windows;
using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App;

/// <summary>
/// SDSM 조사자 앱 진입점. Telerik Windows11 테마 + DI(Scrutor) 구성.
/// </summary>
public partial class App : Application
{
    public new static App Current => (App)Application.Current;

    public System.IServiceProvider Services { get; }

    public App()
    {
        // 테마를 먼저 설정한 뒤 App.xaml 로드(캡션 스타일이 Windows11Resource 사용)
        StyleManager.ApplicationTheme = new Windows11Theme();
        Windows11Palette.Palette.FocusVisualMargin = new Thickness(0);

        // Telerik 기본 영문 문구 → 한글
        LocalizationManager.Manager = new Helpers.KoreanLocalizationManager();

        Services = ConfigureServices();

        InitializeComponent();

        // App.xaml 리소스 로드 후: 타이포·팔레트 적용 (design.md §1·§4, 관리자 값 계승)
        Windows11Palette.Palette.FontFamily = (FontFamily)FindResource("DefaultFontFamily");
        Windows11Palette.Palette.FontSize = (double)FindResource("DefaultFontSize");

        // 강조색을 앱 디자인 강조색으로 통일 → 기존 화면의 AccentBrush가 일괄 반영됨
        Windows11Palette.Palette.AccentColor = (Color)ColorConverter.ConvertFromString("#FF0381FE");
        Windows11Palette.Palette.AccentMouseOverColor = (Color)ColorConverter.ConvertFromString("#E60381FE");
        Windows11Palette.Palette.AccentPressedColor = (Color)ColorConverter.ConvertFromString("#CC0381FE");

        Windows11Palette.Palette.PrimaryBorderColor = (Color)ColorConverter.ConvertFromString("#20000000");
        Windows11Palette.Palette.SecondaryForegroundColor = (Color)ColorConverter.ConvertFromString("#BB000000");
        Windows11Palette.Palette.PrimarySolidBorderColor = (Color)ColorConverter.ConvertFromString("#FFE5E5E5");
        Windows11Palette.Palette.SelectedColor = (Color)ColorConverter.ConvertFromString("#FFE5E5E5");
        Windows11Palette.Palette.ReadOnlyOpacity = 1;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 메인 창을 DI로 생성해 표시
        var window = Services.GetRequiredService<MainSurveyWindow>();
        window.Show();
    }

    private static System.IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // 마커 인터페이스 기반 자동 등록 (CLAUDE.md §2.3)
        services.Scan(scan => scan
            .FromAssemblyOf<IInjectablesService>()
                .AddClasses(c => c.AssignableTo<ITransientService>())
                    .AsSelfWithInterfaces().WithTransientLifetime()
                .AddClasses(c => c.AssignableTo<IScopedService>())
                    .AsSelfWithInterfaces().WithScopedLifetime()
                .AddClasses(c => c.AssignableTo<ISingletonService>())
                    .AsSelfWithInterfaces().WithSingletonLifetime());

        return services.BuildServiceProvider();
    }
}
