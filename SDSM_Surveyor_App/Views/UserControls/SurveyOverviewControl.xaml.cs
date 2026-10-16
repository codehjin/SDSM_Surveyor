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

    // 지도 팝업. 창을 여는 것은 View 의 일이다 — SurveyMeta 는 Model 이라 Window 를 모른다.
    // 좌표가 없어도 열린다 — 지도 관리로 가는 입구가 이 버튼뿐이다(XAML 주석 참조).
    private void Map_Click(object sender, System.Windows.RoutedEventArgs e)
        => new Windows.MapPopupWindow { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog();

    /// <summary>지점 칸에 포커스(C-2 [새 지점] 뒤).</summary>
    public void FocusSite() => SiteCombo.Focus();

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

        if (meta.IsShowingCandidates)
        {
            // C-7 : St.번호 후보가 여럿 — 친 글자(St.4)로 거르면 후보가 하나도 안 보이므로 글자를 비우고 후보를 펼친다.
            SiteCombo.Text = string.Empty;
            SiteCombo.IsDropDownOpen = true;
            return;
        }

        // 해석에 성공했으면 콤보 텍스트를 정규화된 표기로 맞춘다(곡교천1 (St.1))
        if (meta.SelectedSiteChoice is not null) SiteCombo.SelectedItem = meta.SelectedSiteChoice;
    }
}
