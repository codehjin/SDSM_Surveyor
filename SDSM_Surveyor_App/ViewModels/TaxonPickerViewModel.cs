using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>첫 실행 화면의 타일 한 칸.</summary>
public partial class TaxonTileViewModel : ObservableObject
{
    public TaxonTileViewModel(TaxonInfo info) => Info = info;

    public TaxonInfo Info { get; }
    public string Name => Info.Name;
    public string TypeCaption => Info.TypeCaption;

    /// <summary>흰 배경 위 색점 브러시 키(타일 배경은 흰색·AccentSurfaceSoft 라 기본 7색을 쓴다).</summary>
    public string DotBrushKey => Info.DotBrushKey;

    [ObservableProperty] private bool _isSelected;
}

/// <summary>8번째 칸의 점선 안내(선택 대상이 아니다).</summary>
public sealed class TaxonHintViewModel
{
    public string Text { get; } = "여러 분류군을 맡았다면 나중에 바꿀 수 있습니다";
}

/// <summary>
/// 첫 실행 — 담당 분류군 선택 화면.
/// 고른 분류군의 야장만 열리고 상단 분류군 탭 행(44px)은 표시되지 않는다.
/// </summary>
public partial class TaxonPickerViewModel : ObservableObject, ITransientService
{
    private readonly TaxonSelection _selection;

    public TaxonPickerViewModel(TaxonSelection selection)
    {
        _selection = selection;

        Tiles = TaxonCatalog.All.Select(t => new TaxonTileViewModel(t)).ToList();
        foreach (var tile in Tiles)
        {
            tile.IsSelected = tile.Info.Kind == selection.Current;
            tile.PropertyChanged += OnTileChanged;
        }

        // 7개 타일 + 8번째 점선 안내. ItemsControl 이 DataType 으로 템플릿을 고른다.
        Cells = Tiles.Cast<object>().Append(new TaxonHintViewModel()).ToList();

        _rememberOnStartup = selection.RememberOnStartup;
        _selectedName = selection.Name;
    }

    public IReadOnlyList<TaxonTileViewModel> Tiles { get; }
    public IReadOnlyList<object> Cells { get; }

    /// <summary>선택된 분류군명. 하단 문구 두 곳이 이 값을 쓴다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenButtonText))]
    [NotifyPropertyChangedFor(nameof(RememberText))]
    private string _selectedName;

    /// <summary>체크하면 다음 실행부터 이 화면을 건너뛴다(칩에서 언제든 바꿀 수 있다).</summary>
    [ObservableProperty] private bool _rememberOnStartup;

    public string OpenButtonText => $"{SelectedName} 야장 열기";
    public string RememberText => $"다음 실행에도 {SelectedName}로 시작";

    public string Description =>
        "고른 분류군의 야장만 열립니다. 상단 분류군 탭은 표시되지 않고, 필요하면 캡션 바에서 언제든 바꿀 수 있습니다.";

    private void OnTileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TaxonTileViewModel.IsSelected)) return;
        if (sender is not TaxonTileViewModel { IsSelected: true } tile) return;

        _selection.Current = tile.Info.Kind;
        SelectedName = tile.Info.Name;
    }

    /// <summary>창을 닫아 달라는 신호(true = 야장 열기).</summary>
    public event EventHandler<bool>? RequestClose;

    /// <summary>자료함을 열어 달라는 신호(코드비하인드가 창을 띄운다).</summary>
    public event EventHandler? RequestSessionBrowser;

    [RelayCommand]
    private void Open()
    {
        CommitSelection();
        RequestClose?.Invoke(this, true);
    }

    /// <summary>고른 분류군 · 「다음 실행에도 …」 를 설정에 저장한다 — [야장 열기] · [자료함에서 이어하기] 둘 다(§27-7 ②).</summary>
    public void CommitSelection()
    {
        _selection.RememberOnStartup = RememberOnStartup;
        _selection.Commit();
    }

    [RelayCommand]
    private void ContinueFromSessions() => RequestSessionBrowser?.Invoke(this, EventArgs.Empty);
}
