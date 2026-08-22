using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>포유류 탭 입력 컨트롤(관찰형 기준 화면 · 06_DESIGN_REBUILD §5-2-2).
/// 빠른 추가 바의 포커스 이동은 <see cref="QuickAddBar"/> 가 맡는다.
/// 그리드 설정·Ctrl+V 는 <see cref="ObservationGrid"/> 가 맡는다 — 코드비하인드에 남는 것이 없다.</summary>
public partial class MammalEntryControl : UserControl
{
    private readonly MammalEntryViewModel _vm;

    public MammalEntryControl()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<MammalEntryViewModel>();
        DataContext = _vm;
    }
}
