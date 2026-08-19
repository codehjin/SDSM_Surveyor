using System.Windows.Controls;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 모든 분류군이 공유하는 조사개황 입력 UI. DataContext = SurveyMeta.
/// 연도차수 형식·라벨·드롭다운 전환 등 공통 규칙은 이 파일 한 곳에서 관리한다.
/// </summary>
public partial class SurveyOverviewControl : UserControl
{
    public SurveyOverviewControl() => InitializeComponent();
}
