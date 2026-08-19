using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>저서동물 탭 입력 컨트롤. ViewModel은 DI로 주입.
/// 빠른 추가 바 포커스 이동과 그리드 Ctrl+V(엑셀 붙여넣기)만 코드비하인드에서 처리.</summary>
public partial class BenthosEntryControl : UserControl
{
    private readonly BenthosEntryViewModel _vm;

    public BenthosEntryControl()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<BenthosEntryViewModel>();
        DataContext = _vm;

        // 추가 완료 후 검색 콤보로 포커스 복귀 → 연속 입력
        _vm.QuickAddCompleted += (_, _) =>
            Dispatcher.BeginInvoke(new Action(() => QuickSpeciesBox.Focus()), DispatcherPriority.Input);

        // 종을 고르면 개체수 칸으로 자동 이동
        _vm.QuickSpeciesPicked += (_, _) =>
            Dispatcher.BeginInvoke(new Action(() => QuickCountBox.Focus()), DispatcherPriority.Input);
    }

    // 개체수 칸에서 Enter → 행 추가
    private void QuickCountBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_vm.AddQuickCommand.CanExecute(null)) _vm.AddQuickCommand.Execute(null);
            e.Handled = true;
        }
    }

    // 그리드 Ctrl+V : 엑셀 [국명, 개체수] 여러 줄을 직접 파싱해 행 추가
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (Clipboard.ContainsText())
            {
                if (sender is Telerik.Windows.Controls.RadGridView grid) grid.CancelEdit();
                var text = Clipboard.GetText();
                Dispatcher.BeginInvoke(new Action(() => _vm.PasteRows(text)), DispatcherPriority.Background);
                e.Handled = true;
            }
        }
    }
}
