using System.Windows.Controls;
using System.Windows.Input;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 모든 분류군이 공유하는 조사개황 입력 UI. DataContext = SurveyMeta.
/// 연도차수 형식·라벨·드롭다운 전환 등 공통 규칙은 이 파일 한 곳에서 관리한다.
/// </summary>
public partial class SurveyOverviewControl : UserControl
{
    public SurveyOverviewControl() => InitializeComponent();

    // 지점 콤보는 편집 가능(IsEditable)이라 조사자가 `ST1` 처럼 칠 수 있다.
    // RadComboBox 에는 TextChanged 이벤트가 없어 포커스 이탈·Enter 시점에 해석한다.
    private void SiteCombo_LostFocus(object sender, System.Windows.RoutedEventArgs e) => ResolveSite();

    private void SiteCombo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab) ResolveSite();
    }

    private void ResolveSite()
    {
        if (DataContext is not SurveyMeta meta) return;
        if (SiteCombo.SelectedItem is not null) return;   // 목록에서 고른 경우는 그대로

        meta.ResolveSiteText(SiteCombo.Text);

        // 해석에 성공했으면 콤보 텍스트를 정규화된 표기로 맞춘다(곡교천1 (St.1))
        if (meta.SelectedSite is not null) SiteCombo.SelectedItem = meta.SelectedSite;
    }
}
