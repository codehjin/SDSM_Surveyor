using System.Diagnostics;
using System.IO;
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
        Helpers.KoreanLocalizationManager.MarkStart();   // Debug 전용 · 키 로그 시작 표식

        Services = ConfigureServices();

        // 드롭다운이 열렸을 때 휠은 목록만 · 목록 밖 휠은 닫기 · 닫힌 콤보 휠로 값 안 바뀜(모든 창)
        Behaviors.DropDownWheelGuard.Register();

        StartBindingErrorLog();

        InitializeComponent();

        // App.xaml 리소스 로드 후: 타이포·팔레트 적용 (design.md §1·§4, 관리자 값 계승)
        Windows11Palette.Palette.FontFamily = (FontFamily)FindResource("DefaultFontFamily");
        Windows11Palette.Palette.FontSize = (double)FindResource("DefaultFontSize");

        // 강조색을 앱 디자인 강조색으로 통일 → 기존 화면의 AccentBrush가 일괄 반영됨
        Windows11Palette.Palette.AccentColor = (Color)ColorConverter.ConvertFromString("#FF0381FE");
        Windows11Palette.Palette.AccentMouseOverColor = (Color)ColorConverter.ConvertFromString("#E60381FE");
        Windows11Palette.Palette.AccentPressedColor = (Color)ColorConverter.ConvertFromString("#CC0381FE");

        ApplyPaletteFromTokens();

        LogResolvedFont();
    }

    /// <summary>
    /// Pretendard 가 실제로 실렸는지 파일로 남긴다(Debug 전용).
    ///
    /// pack:// URI 가 틀리거나 ttf 가 Resource 로 안 들어가면 WPF 는 **조용히**
    /// 폴백(맑은 고딕)으로 그린다 — 화면만 봐서는 구분이 어렵다.
    /// <see cref="FontFamily.GetTypefaces"/> 가 실제 해석 결과를 돌려주므로 그것을 적는다.
    /// </summary>
    [Conditional("DEBUG")]
    private void LogResolvedFont()
    {
        try
        {
            var ff = (FontFamily)FindResource("DefaultFontFamily");
            var names = string.Join(" / ", ff.FamilyNames.Select(kv => $"{kv.Key}={kv.Value}"));
            var faces = ff.GetTypefaces()
                          .Select(t => $"{t.Weight}·{t.Style}")
                          .Distinct()
                          .ToList();

            var lines = new List<string>
            {
                $"# 서체 확인 · {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Source        : {ff.Source}",
                $"FamilyNames   : {names}",
                $"BaseUri       : {ff.BaseUri}",
                $"Typefaces({faces.Count}) : {string.Join(", ", faces)}",
                string.Empty,
                names.Contains("Pretendard", StringComparison.OrdinalIgnoreCase)
                    ? "판정 : Pretendard 로드됨"
                    : "판정 : ⚠ Pretendard 아님 — 폴백(맑은 고딕)으로 그려지고 있다",
            };

            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "font_check.log"), lines);
        }
        catch
        {
            // 서체 로그가 앱 시작을 막아서는 안 된다.
        }
    }

    /// <summary>
    /// Telerik Windows11 팔레트를 <c>Styles/Colors.xaml</c> 의 토큰으로 덮어쓴다.
    ///
    /// ⚠ **값을 여기에 hex 로 적지 않는다.** 종전에는 App.xaml.cs 와
    /// <c>tools/contrast_audit.py</c> 두 곳에 같은 값이 베껴져 있었고, 어긋나면
    /// "감사는 통과하는데 화면은 미달"인 상태가 만들어진다. 이제 양쪽이 Colors.xaml 을 읽는다.
    ///
    /// 여기서 손대는 것은 **테마가 화면 전체에 뿌리는 값**이다 —
    /// 그리드 셀 글자, 입력칸 테두리처럼 우리 Style 이 닿지 않는 곳이 이 팔레트를 따른다.
    /// </summary>
    private void ApplyPaletteFromTokens()
    {
        Color Token(string key) => (Color)FindResource(key);

        // 본문 글자 : 테마 기본값은 회색기가 돌아 흰 배경에서 흐리다.
        // ⚠ Main*Color 는 Windows11Palette 에 없다(다른 Telerik 팔레트의 속성이다).
        //    팔레트마다 노출 속성이 다르므로 없는 이름을 쓰면 컴파일이 막힌다.
        Windows11Palette.Palette.PrimaryForegroundColor = Token("ThemePrimaryForegroundColor");
        Windows11Palette.Palette.SecondaryForegroundColor = Token("ThemeSecondaryForegroundColor");

        // 입력 요소 테두리 : **4면을 같은 색으로** 만든다.
        // Windows11 테마 기본은 아래쪽만 진한 밑줄형이라 "그림자처럼 4면이 다르게" 보인다.
        // 테두리 계열을 한 값으로 묶어 균일한 사각 테두리로 되돌린다.
        var border = Token("ThemeInputBorderColor");
        Windows11Palette.Palette.PrimaryBorderColor   = border;
        Windows11Palette.Palette.SecondaryBorderColor = border;
        Windows11Palette.Palette.TertiaryBorderColor  = border;
        Windows11Palette.Palette.ReadOnlyBorderColor  = border;

        Windows11Palette.Palette.PrimarySolidBorderColor = Token("ThemeSelectedColor");
        Windows11Palette.Palette.SelectedColor           = Token("ThemeSelectedColor");
        Windows11Palette.Palette.ReadOnlyOpacity = 1;
    }

    /// <summary>
    /// 바인딩 오류를 파일로 남긴다(Debug 빌드 전용).
    ///
    /// design.md §8 은 「출력창 바인딩 오류 0」을 완료 조건으로 둔다. 그런데 출력창은
    /// 사람이 보고 있어야만 세어지고, 화면을 몇 번 오간 뒤에는 스크롤 밖으로 밀린다.
    /// 그래서 실행 폴더의 <c>binding_errors.log</c> 에 그대로 받아 둔다 —
    /// 앱을 한 번 돌리고 이 파일이 비어 있으면 그 조건은 통과다.
    ///
    /// ⚠ Release 빌드에는 들어가지 않는다(현장 PC 에 로그 파일을 만들지 않는다).
    /// </summary>
    [Conditional("DEBUG")]
    private static void StartBindingErrorLog()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "binding_errors.log");

            // 이전 실행이 파일을 물고 있어도 실패하지 않게 공유 모드로 연다.
            var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            var writer = new StreamWriter(stream) { AutoFlush = true };
            writer.WriteLine($"# SDSM_Surveyor 바인딩 추적 · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine("# 아래가 비어 있으면 바인딩 오류 0 (design.md §8 완료 조건)");

            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(writer));
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning | SourceLevels.Error;
        }
        catch
        {
            // 추적 로그가 앱 시작을 막아서는 안 된다.
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ⚠ 선택 창이 닫히는 순간 열린 창이 0개가 된다.
        //    기본 ShutdownMode(OnLastWindowClose)면 메인 창을 띄우기 전에 앱이 끝난다.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 첫 실행 — 담당 분류군 선택(README §1). '다음 실행에도 …' 를 켜 두었으면 건너뛴다.
        var selection = Services.GetRequiredService<Data.TaxonSelection>();
        if (!selection.SkipPicker)
        {
            var picked = Services.GetRequiredService<TaxonPickerWindow>().ShowDialog();
            if (picked != true)
            {
                Shutdown();      // 분류군을 고르지 않고 닫았다 — 야장을 열지 않는다
                return;
            }
        }

        // 메인 창을 DI로 생성해 표시
        var window = Services.GetRequiredService<MainSurveyWindow>();
        MainWindow = window;
        window.Show();

        ShutdownMode = ShutdownMode.OnLastWindowClose;
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
