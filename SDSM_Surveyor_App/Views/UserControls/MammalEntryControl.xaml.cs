using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>포유류 탭 입력 컨트롤(관찰형 기준 화면 · 06_DESIGN_REBUILD §5-2-2).
/// 빠른 추가 바의 포커스 이동은 <see cref="QuickAddBar"/> 가 맡는다.
/// 여기서는 그리드 Ctrl+V(엑셀 붙여넣기)만 처리한다.</summary>
public partial class MammalEntryControl : UserControl
{
    private readonly MammalEntryViewModel _vm;

    public MammalEntryControl()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<MammalEntryViewModel>();
        DataContext = _vm;
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
