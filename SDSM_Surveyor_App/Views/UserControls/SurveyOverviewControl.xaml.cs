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
    // RadComboBox 에는 TextChanged 이벤트가 없어 포커스 이탈·Enter·Tab 시점에 해석한다.
    //
    // E2(2026-10-18 (2) · D1 과 같은 규칙) — Enter·Tab 에서
    //   ① 친 글자와 같은 이름(St.번호·옛 표기 포함)이 있으면 그 지점
    //   ② 방향키·마우스로 고른 후보가 있으면 그 지점(콤보 기본 동작)
    //   ③ 그 밖에는 고르지 않는다 — 콤보 기본 동작(걸러진 첫 후보 자동 선택: `진위` → `진위천8`)을 막는다.
    //      방류하천은 '목록에서 고르세요' 안내, 생태현황은 '목록 밖 지점으로 입력됩니다' 확인([아니요]면 칸으로 돌아감).
    //   St.번호 후보가 여럿이면 자동으로 고르지 않고 후보를 펼친다(C-7 그대로).

    private bool _navigated;   // 방향키로 후보를 움직였다(②)
    private bool _asking;      // 확인 창이 떠 있다(포커스 이탈로 다시 묻지 않게)

    private SurveyMeta? Meta => DataContext as SurveyMeta;

    /// <summary>글자를 친다 — 콤보가 스스로 고르는 항목을 조사개황에 넘기지 않는다(<see cref="SurveyMeta.HoldSiteSelection"/>).</summary>
    private void SiteCombo_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        _navigated = false;
        if (Meta is { } m) m.HoldSiteSelection = true;
    }

    /// <summary>마우스로 항목을 고르거나 칸을 누른다 — 고른 것은 바로 받는다(②).</summary>
    private void SiteCombo_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Meta is { } m) m.HoldSiteSelection = false;
    }

    private void SiteCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown)
        {
            _navigated = true;                                     // ② 방향키로 고른 후보는 바로 받는다
            if (Meta is { } m) m.HoldSiteSelection = false;
            return;
        }
        if (e.Key is Key.Back or Key.Delete)
        {
            _navigated = false;
            if (Meta is { } m) m.HoldSiteSelection = true;
            return;
        }
        if (e.Key is not (Key.Enter or Key.Tab)) return;
        if (_navigated) { _navigated = false; return; }            // ② 고른 후보 — 콤보가 처리한다
        if (Decide(fromKey: true) && e.Key == Key.Enter) e.Handled = true;
        else if (_handledTab && e.Key == Key.Tab) { e.Handled = true; _handledTab = false; }
    }

    private bool _handledTab;

    /// <summary>미리보기에서 처리하지 않은 Enter·Tab(① 같은 이름 Tab 등) — 같은 규칙으로 한 번 더(이미 고른 항목이면 아무것도 안 함).</summary>
    private void SiteCombo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab && Decide(fromKey: e.Key == Key.Enter) && e.Key == Key.Enter) e.Handled = true;
    }

    private void SiteCombo_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_asking || SiteCombo.IsKeyboardFocusWithin || SiteCombo.IsDropDownOpen) return;
        Decide(fromKey: false);
    }

    /// <summary>①~③ 규칙으로 정한다. 콤보 기본 동작을 막아야 하면 true(Enter 를 처리함으로 표시).</summary>
    private bool Decide(bool fromKey)
    {
        if (DataContext is not SurveyMeta meta) return false;
        var raw = SiteCombo.Text ?? string.Empty;
        var text = raw.Trim();
        var held = meta.HoldSiteSelection;
        meta.HoldSiteSelection = false;
        if (held && !ReferenceEquals(SiteCombo.SelectedItem, meta.SelectedSiteChoice))
        {
            // 치는 동안 콤보가 스스로 고른 항목을 걷어 내고 친 글자를 되돌린다(조사개황 선택이 기준)
            meta.HoldSiteSelection = true;
            SiteCombo.SelectedItem = meta.SelectedSiteChoice;
            SiteCombo.Text = raw;
            meta.HoldSiteSelection = false;
        }
        if (SiteCombo.SelectedItem is SurveyMeta.SiteChoice sel && sel.Display == SiteCombo.Text) return false;   // 목록에서 고른 그대로
        if (text.Length == 0) return false;
        if (!meta.IsSiteSelectable) return false;

        // ① 같은 이름 · St.번호 · 옛 표기
        var hit = meta.ResolveSiteText(text);
        if (meta.IsShowingCandidates)
        {
            // C-7 : St.번호 후보가 여럿 — 친 글자(St.4)로 거르면 후보가 하나도 안 보이므로 글자를 비우고 후보를 펼친다.
            SiteCombo.Text = string.Empty;
            SiteCombo.IsDropDownOpen = true;
            return true;
        }
        if (hit is not null)
        {
            SiteCombo.IsDropDownOpen = false;
            if (meta.SelectedSiteChoice is { } c && !ReferenceEquals(SiteCombo.SelectedItem, c)) SiteCombo.SelectedItem = c;
            return true;
        }

        // ③ 고르지 않는다
        SiteCombo.IsDropDownOpen = false;
        if (string.Equals(text, meta.Site, StringComparison.Ordinal) && meta.SelectedSite is null) return true;   // 이미 목록 밖 지점으로 넣은 이름
        if (!SurveyMeta.IsEcoStatus(meta.Project))
        {
            meta.SiteEntryNotice = $"'{text}' 은(는) {meta.Project} 지점 목록에 없습니다 — 목록에서 고르세요.";
            meta.HoldSiteSelection = true;     // 칸으로 돌아가 이어 친다
            if (fromKey) { _handledTab = true; FocusSiteText(); }
            return true;
        }
        _asking = true;
        int pick;
        try
        {
            pick = Windows.ChoiceDialog.Ask(System.Windows.Window.GetWindow(this), "목록 밖 지점",
                $"'{text}' 은(는) 생태현황 {SurveyMeta.TaxonKo(meta.CurrentTaxon)} 지점 목록에 없습니다.\n목록 밖 지점으로 입력됩니다 — 하천명·좌표를 직접 적어 주세요.",
                new Windows.ChoiceDialog.Option("예", IsPrimary: true),
                new Windows.ChoiceDialog.Option("아니요", IsCancel: true));
        }
        finally { _asking = false; }
        if (pick == 0)
        {
            meta.CommitOffListSite(text);
            if (fromKey)
            {
                // Tab 이면 다음 칸으로 — 콤보 기본 Tab 처리(강조된 후보 선택)는 막고 포커스만 옮긴다
                _handledTab = true;
                Dispatcher.BeginInvoke(() => (Keyboard.FocusedElement as System.Windows.UIElement)?.MoveFocus(
                    new TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next)), System.Windows.Threading.DispatcherPriority.Input);
            }
            return true;
        }
        _handledTab = fromKey;                 // [아니요] — 입력 칸으로 돌아간다
        meta.HoldSiteSelection = true;
        FocusSiteText();
        return true;
    }

    /// <summary>지점 칸 글상자로 돌아가 글자를 모두 고른다(고쳐 치기 쉽게).</summary>
    private void FocusSiteText()
    {
        Dispatcher.BeginInvoke(() =>
        {
            SiteCombo.Focus();
            if (SiteCombo.Template?.FindName("PART_EditableTextBox", SiteCombo) is TextBox tb) { tb.Focus(); tb.SelectAll(); }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }
}
