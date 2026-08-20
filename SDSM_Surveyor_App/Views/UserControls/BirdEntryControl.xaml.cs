using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>조류 탭 입력 컨트롤. ViewModel은 DI로 주입.
/// 빠른 추가 바 포커스 이동과 그리드 Ctrl+V(엑셀 붙여넣기)만 코드비하인드에서 처리.</summary>
public partial class BirdEntryControl : UserControl
{
    private readonly BirdEntryViewModel _vm;

    public BirdEntryControl()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<BirdEntryViewModel>();
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

    // 그리드 Ctrl+V : 엑셀 여러 줄을 직접 파싱해 행 추가
    // (RadGridView 기본 붙여넣기는 커스텀 편집기 컬럼과 충돌해 한 칸에 뭉침)
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (Clipboard.ContainsText())
            {
                // 인서트 행이 편집 상태면 컬렉션에 추가돼도 화면이 안 그려짐
                // → 편집 종료 후, 다음 레이아웃 사이클에서 추가(그리드가 편집상태를 완전히 벗어난 뒤)
                if (sender is Telerik.Windows.Controls.RadGridView grid) grid.CancelEdit();
                var text = Clipboard.GetText();
                Dispatcher.BeginInvoke(new Action(() => _vm.PasteRows(text)), DispatcherPriority.Background);
                e.Handled = true;
            }
        }
    }
}
