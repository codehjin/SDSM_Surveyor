using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Windows.Controls;   // RadGridView

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>어류 탭 입력 컨트롤. ViewModel은 DI로 주입.
/// 빠른 추가 바의 포커스 이동은 <see cref="QuickAddBar"/> 가 맡는다(06_DESIGN_REBUILD §5-3) —
/// 종전에는 5개 화면이 같은 구독 코드를 각자 들고 있었다.
/// 여기서는 그리드 Ctrl+V(엑셀 붙여넣기)만 처리한다.</summary>
public partial class FishEntryControl : UserControl
{
    private readonly FishEntryViewModel _vm;

    public FishEntryControl()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<FishEntryViewModel>();
        DataContext = _vm;

        // 행을 지우기 직전에 셀 편집을 끝낸다 — 붙여넣기 경로가 CancelEdit 를 부르는 것과 같은 이유다.
        _vm.EndGridEdit = () => SpeciesGrid.CancelEdit();

        // 열 폭 감사(Debug 전용) — 창 폭이 바뀔 때마다 실제 폭을 파일로 남긴다.
        // RadGridView 는 폭이 모자라면 가로 스크롤 대신 열을 뭉개므로 눈으로는 걸러지지 않는다(§7).
        Loaded += (_, _) => Helpers.LayoutAudit.CheckColumnWidths(SpeciesGrid, "어류");
        SpeciesGrid.SizeChanged += (_, _) => Helpers.LayoutAudit.CheckColumnWidths(SpeciesGrid, "어류");
        // 단일 분류군 모드에서는 이 컨트롤이 숨은 채로 만들어질 수 있다.
        // 숨어 있는 동안에는 열이 측정되지 않으므로, 보이게 된 시점에 다시 잰다.
        IsVisibleChanged += (_, _) => Helpers.LayoutAudit.CheckColumnWidths(SpeciesGrid, "어류");
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
        if (!Clipboard.ContainsText()) return;

        // 인서트 행이 편집 상태면 컬렉션에 추가돼도 화면이 안 그려짐
        // → 편집 종료 후, 다음 레이아웃 사이클에서 추가
        if (sender is Telerik.Windows.Controls.RadGridView grid) grid.CancelEdit();
        var text = Clipboard.GetText();
        Dispatcher.BeginInvoke(new Action(() => _vm.PasteRows(text)), DispatcherPriority.Background);
        e.Handled = true;
    }
}
