using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Controls;   // RadGridView

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>저서동물 탭 입력 컨트롤(종 입력형 · 06_DESIGN_REBUILD §5-2-4).
/// 빠른 추가 바의 포커스 이동은 <see cref="QuickAddBar"/> 가 맡는다.
/// 여기서는 그리드 Ctrl+V(엑셀 붙여넣기)만 처리한다.</summary>
public partial class BenthosEntryControl : UserControl
{
    private readonly BenthosEntryViewModel _vm;

    public BenthosEntryControl()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<BenthosEntryViewModel>();
        DataContext = _vm;

        // 행을 지우기 직전에 셀 편집을 끝낸다 — 붙여넣기 경로가 CancelEdit 를 부르는 것과 같은 이유다.
        _vm.EndGridEdit = () => SpeciesGrid.CancelEdit();

        // 국명 편집 확정 → 목록 밖 국명 보존(B-4) · 같은 종 합산(B-1)
        Helpers.GridSpeciesCommit.Attach(SpeciesGrid, () => _vm);

        // 열 폭 감사(Debug 전용) — RadGridView 는 폭이 모자라면 열을 조용히 뭉갠다(design.md §7).
        Loaded += (_, _) => Helpers.LayoutAudit.CheckColumnWidths(SpeciesGrid, "저서동물");
        SpeciesGrid.SizeChanged += (_, _) => Helpers.LayoutAudit.CheckColumnWidths(SpeciesGrid, "저서동물");
        IsVisibleChanged += (_, _) => Helpers.LayoutAudit.CheckColumnWidths(SpeciesGrid, "저서동물");
    }


    /// <summary>
    /// 누른 행을 VM 에 알려 둔다. [행 삭제] 버튼과 우클릭 메뉴가 이 값을 쓴다.
    /// 우클릭은 Telerik 이 선택을 옮기지 않으므로 여기서 직접 잡는다.
    /// </summary>
    private void Grid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        => _vm.LastTouchedRow = Helpers.GridRowPicker.Resolve(e.OriginalSource);

    // 그리드 Ctrl+V : 엑셀 여러 줄을 직접 파싱해 행 추가
    // (RadGridView 기본 붙여넣기는 커스텀 편집기 컬럼과 충돌해 한 칸에 뭉침 — 90_TECH_NOTES §2)
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 키보드로 셀을 옮기면 '마지막으로 누른 행' 이 지금 보고 있는 행과 어긋난다 → [행 삭제]를 끈다.
        if (ObservationGrid.IsNavigationKey(e.Key)) _vm.LastTouchedRow = null;

        if (e.Key != Key.V || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
        // N1 : 다른 프로그램이 클립보드를 잡고 있으면 다시 시도하고, 그래도 못 읽으면 안내만(앱을 닫지 않는다)
        if (!Helpers.ClipboardText.TryRead(out var text)) { e.Handled = true; return; }
        if (text is null) return;

        if (sender is Telerik.Windows.Controls.RadGridView grid) grid.CancelEdit();
        Dispatcher.BeginInvoke(new Action(() => _vm.PasteRows(text)), DispatcherPriority.Background);
        e.Handled = true;
    }
}
