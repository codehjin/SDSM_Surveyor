using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 모든 분류군이 공유하는 조사개황 입력 UI. DataContext = SurveyMeta.
/// 연도차수 형식·라벨·드롭다운 전환 등 공통 규칙은 이 파일 한 곳에서 관리한다.
/// </summary>
public partial class SurveyOverviewControl : UserControl
{
    public SurveyOverviewControl()
    {
        InitializeComponent();
        SiteCombo.Loaded += (_, _) => HookSiteText();
    }

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
    //   ② 방향키·마우스로 고른 후보가 있으면 그 지점
    //      G1(2026-10-18 (3)) : ↓ 는 친 글자로 거른 후보(같은 이름 → 앞부분 → 포함)를 펼치고 그 안에서 ↑↓ 로 움직인다.
    //      글자가 없으면 목록 전부. 움직이는 동안은 조사개황에 넣지 않고(선택 보류) Enter·Tab 으로 확정한다.
    //      Esc 는 목록만 닫고 친 글자는 남긴다. 콤보 자체의 거르기(IsFilteringEnabled)는 끄고 여기서 거른다.
    //      G3(2026-10-18 (4)) : 목록이 열려 있으면(▼·↓) 글자를 칠 때마다 같은 규칙으로 다시 거른다 — 강조는 두지 않는다
    //      (맨 앞 후보를 강조하면 Enter 가 첫 후보를 고르게 되어 E2 를 어긴다). 강조는 ↑↓·마우스로만. 닫힌 채 치면 열지 않는다.
    //      칸 글상자의 TextChanged 로 거르므로 한글 조합 중인 글자도 들어간다(조합이 글상자에 보이는 동안의 글자).
    //      거르는 동안에는 칸 글자를 건드리지 않는다(조합이 깨지지 않게) — 강조·선택은 치기 전(PreviewKeyDown·ImeProcessed)에 떼어 둔다.
    //   ③ 그 밖에는 고르지 않는다 — 콤보 기본 동작(걸러진 첫 후보 자동 선택: `진위` → `진위천8`)을 막는다.
    //      방류하천은 '목록에서 고르세요' 안내, 생태현황은 '목록 밖 지점으로 입력됩니다' 확인([아니요]면 칸으로 돌아감).
    //   St.번호 후보가 여럿이면 자동으로 고르지 않고 후보를 펼친다(C-7 그대로).

    private bool _asking;      // 확인 창이 떠 있다(포커스 이탈로 다시 묻지 않게)

    // G1 ↓ 후보 목록 상태 — null 이면 후보 목록을 펼치지 않은 상태
    private List<SurveyMeta.SiteChoice>? _cands;
    private int _candIndex = -1;
    private string _typed = string.Empty;   // ↓ 를 누를 때 칸에 있던 글자(Esc 로 되돌린다)
    private bool _exiting;
    private bool _programmatic;            // 앱이 칸 글자·항목을 바꾸는 중(TextChanged 로 다시 거르지 않게)
    private TextBox? _siteText;            // 콤보 안 글상자
    private bool _typingPending;           // 글자 키·Back·Delete·한글 입력기 키가 눌렸다 — 다음 글자 바뀜은 조사자가 친 것

    /// <summary>콤보 안 글상자에 TextChanged 를 건다. 틀이 다시 만들어지면 새 글상자로 옮긴다.</summary>
    private void HookSiteText()
    {
        var tb = FindChild<TextBox>(SiteCombo);
        if (ReferenceEquals(tb, _siteText)) return;
        if (_siteText is not null) _siteText.TextChanged -= SiteText_TextChanged;
        _siteText = tb;
        if (_siteText is not null) _siteText.TextChanged += SiteText_TextChanged;
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t) return t;
            if (FindChild<T>(c) is T d) return d;
        }
        return null;
    }

    /// <summary>
    /// G3 — 목록이 열려 있으면 조사자가 친 글자로 칸이 바뀔 때마다 다시 거른다(강조 없음 · 조사개황 그대로).
    /// 콤보가 항목을 골라 칸 글자를 바꾸는 경우(마우스로 후보 고름 등)는 거르지 않는다 — 치기 직전 키가 눌렸을 때만.
    /// </summary>
    private void SiteText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_programmatic || _exiting || !_typingPending || Meta is not { } meta || _siteText is null) return;
        if (!SiteCombo.IsDropDownOpen && _cands is null) return;     // 닫힌 채 치면 열지 않는다
        Refilter(meta, _siteText.Text ?? string.Empty);
    }

    private void Refilter(SurveyMeta meta, string text)
    {
        _programmatic = true;
        try
        {
            meta.PreviewSiteChoice(null);                            // 강조 없음
            meta.ShowSiteChoices(meta.SiteCandidates(text));         // 같은 이름 → 앞부분 → 포함 · 빈 글자면 목록 전부
            _cands = meta.SiteChoices.ToList();
            _candIndex = -1;
            _typed = text;
            if (_siteText is not null && _siteText.Text != text) { _siteText.Text = text; _siteText.CaretIndex = text.Length; }
            if (!SiteCombo.IsDropDownOpen) SiteCombo.IsDropDownOpen = true;
        }
        finally { _programmatic = false; }
    }

    /// <summary>
    /// 글자를 치기 직전(글자 키·Back·Delete·한글 입력기 키) — 강조된 후보를 보던 중이면 친 글자로 돌아가고,
    /// ▼ 로 연 목록에 조사개황 지점이 골라져 있으면 떼어 둔다(그 뒤 치는 글자·조합을 건드리지 않게).
    /// </summary>
    private void PrepareTyping()
    {
        if (Meta is not { } meta) return;
        HookSiteText();
        _typingPending = true;
        meta.HoldSiteSelection = true;
        if (_siteText is null) return;
        if (_cands is not null && _candIndex >= 0)
        {
            _programmatic = true;
            try
            {
                meta.PreviewSiteChoice(null);
                _candIndex = -1;
                _siteText.Text = _typed;
                _siteText.CaretIndex = _typed.Length;
            }
            finally { _programmatic = false; }
            return;
        }
        if (SiteCombo.IsDropDownOpen && SiteCombo.SelectedItem is not null)
        {
            var t = _siteText.Text; int start = _siteText.SelectionStart, len = _siteText.SelectionLength;
            _programmatic = true;
            try
            {
                meta.PreviewSiteChoice(null);
                if (_siteText.Text != t) { _siteText.Text = t; _siteText.Select(start, len); }
            }
            finally { _programmatic = false; }
        }
    }

    private SurveyMeta? Meta => DataContext as SurveyMeta;

    /// <summary>글자를 친다 — 콤보가 스스로 고르는 항목을 조사개황에 넘기지 않는다(<see cref="SurveyMeta.HoldSiteSelection"/>).</summary>
    private void SiteCombo_PreviewTextInput(object sender, TextCompositionEventArgs e) => PrepareTyping();

    /// <summary>마우스로 항목을 고르거나 칸을 누른다 — 고른 것은 바로 받는다(②).</summary>
    private void SiteCombo_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _typingPending = false;                                     // 마우스 — 콤보가 바꾸는 칸 글자는 친 글자가 아니다
        if (Meta is { } m) m.HoldSiteSelection = false;
    }

    private void SiteCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Enter or Key.Tab or Key.Escape)
            _typingPending = false;   // 이 뒤 콤보가 칸 글자를 바꿔도(강조 후보 표기 등 · 늦게 올 수 있다) 친 글자가 아니다
        if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown)
        {
            // G1 : 콤보의 기본 방향키(전체 목록에서 바로 고름) 대신 거른 후보 안에서 움직인다
            int step = e.Key switch { Key.Up => -1, Key.Down => 1, Key.PageUp => -5, _ => 5 };
            MoveCandidate(step);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && _cands is not null)
        {
            ExitCandidates(restoreText: true);                     // 목록만 닫고 친 글자는 남긴다
            e.Handled = true;
            return;
        }
        if (e.Key is not (Key.Enter or Key.Tab))
        {
            // 글자 키·Back·Delete·한글 입력기(ImeProcessed) — 치기 전에 강조·선택을 떼어 둔다(G3)
            if (e.Key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                              or Key.Left or Key.Right or Key.Home or Key.End or Key.Escape)) PrepareTyping();
            return;
        }
        if (_cands is not null)
        {
            if (_candIndex >= 0)
            {
                CommitCandidate();                                 // ② 방향키로 고른 후보 → 그 지점
                if (e.Key == Key.Enter) e.Handled = true;
                return;
            }
            ExitCandidates(restoreText: true);                     // 후보를 펼치기만 했다 — 친 글자로 ①·③
        }
        if (Decide(fromKey: true) && e.Key == Key.Enter) e.Handled = true;
        else if (_handledTab && e.Key == Key.Tab) { e.Handled = true; _handledTab = false; }
    }

    private bool _handledTab;

    /// <summary>미리보기에서 처리하지 않은 Enter·Tab(① 같은 이름 Tab 등) — 같은 규칙으로 한 번 더(이미 고른 항목이면 아무것도 안 함).</summary>
    private void SiteCombo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab && Decide(fromKey: e.Key == Key.Enter) && e.Key == Key.Enter) e.Handled = true;
    }

    // ── G1 ↓ 후보 목록 ─────────────────────────────────────────────────────

    /// <summary>↓/↑ — 처음이면 친 글자로 거른 후보를 펼치고, 그 안에서 강조를 옮긴다(조사개황에는 아직 넣지 않는다).</summary>
    private void MoveCandidate(int step)
    {
        if (Meta is not { } meta || !meta.IsSiteSelectable) return;
        if (_cands is null)
        {
            var raw = SiteCombo.Text ?? string.Empty;
            // 고른 지점의 표기가 그대로 있으면 '친 글자 없음' — 목록 전부를 펼치고 고른 지점에서 시작한다
            var current = SiteCombo.SelectedItem as SurveyMeta.SiteChoice;
            bool asTyped = !(current is not null && current.Display == raw && !meta.HoldSiteSelection);
            var query = asTyped ? raw : string.Empty;
            _typed = raw;
            _programmatic = true;
            try
            {
                meta.PreviewSiteChoice(null);
                meta.ShowSiteChoices(meta.SiteCandidates(query));
                _cands = meta.SiteChoices.ToList();
                _candIndex = asTyped ? -1 : _cands.FindIndex(c => ReferenceEquals(c.Site, current!.Site));
                SiteCombo.Text = raw;
                SiteCombo.IsDropDownOpen = true;
            }
            finally { _programmatic = false; }
            if (_cands.Count == 0) return;
        }
        if (_cands.Count == 0) return;
        _candIndex = _candIndex < 0 && step < 0 ? _cands.Count - 1 : Math.Clamp(_candIndex + step, 0, _cands.Count - 1);
        var item = _cands[_candIndex];
        _programmatic = true;
        try { meta.PreviewSiteChoice(item); }                     // 목록에서 강조되고 칸에 그 후보가 보인다 — 조사개황은 그대로(보류)
        finally { _programmatic = false; }
        Dispatcher.BeginInvoke(() =>
        {
            if (SiteCombo.ItemContainerGenerator.ContainerFromItem(item) is System.Windows.FrameworkElement fe) fe.BringIntoView();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>강조된 후보로 확정한다(E2 ②).</summary>
    private void CommitCandidate()
    {
        if (Meta is not { } meta || _cands is null || _candIndex < 0) return;
        var choice = _cands[_candIndex];
        ExitCandidates(restoreText: false);
        meta.HoldSiteSelection = false;
        meta.SelectedSiteChoice = meta.SiteChoices.FirstOrDefault(c => ReferenceEquals(c.Site, choice.Site)) ?? choice;
        if (meta.SelectedSiteChoice is { } c && !ReferenceEquals(SiteCombo.SelectedItem, c)) SiteCombo.SelectedItem = c;
    }

    /// <summary>후보 목록을 닫고 항목을 지금 목록 전부로 되돌린다. <paramref name="restoreText"/> 면 조사개황 선택·친 글자로 되돌린다.</summary>
    private void ExitCandidates(bool restoreText)
    {
        if (_cands is null || _exiting || Meta is not { } meta) return;
        _exiting = true;
        try
        {
            var raw = _typed;
            _cands = null; _candIndex = -1;
            var held = meta.HoldSiteSelection;
            meta.HoldSiteSelection = true;                         // 목록을 바꿔 끼우는 동안 콤보 선택이 조사개황에 가지 않게
            meta.EndSitePreview();
            SiteCombo.IsDropDownOpen = false;
            meta.RestoreSiteChoices();
            if (restoreText)
            {
                SiteCombo.SelectedItem = meta.SelectedSiteChoice;
                SiteCombo.Text = raw;
                CaretToEnd();
            }
            meta.HoldSiteSelection = restoreText || held;
        }
        finally { _exiting = false; }
    }

    private void CaretToEnd()
    {
        if (SiteCombo.Template?.FindName("PART_EditableTextBox", SiteCombo) is TextBox tb) tb.CaretIndex = tb.Text.Length;
    }

    /// <summary>목록이 닫혔다 — 후보를 보던 중이면 끝낸다(마우스로 고른 것은 그대로 · 그 밖에는 친 글자로).</summary>
    private void SiteCombo_DropDownClosed(object sender, EventArgs e)
    {
        _typingPending = false;
        if (_cands is null || _exiting || Meta is not { } meta) return;
        bool mousePicked = !meta.HoldSiteSelection && meta.SelectedSite is not null
                           && SiteCombo.SelectedItem is SurveyMeta.SiteChoice c && ReferenceEquals(c.Site, meta.SelectedSite);
        ExitCandidates(restoreText: !mousePicked);
        if (mousePicked && meta.SelectedSiteChoice is { } sc) SiteCombo.SelectedItem = sc;
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
