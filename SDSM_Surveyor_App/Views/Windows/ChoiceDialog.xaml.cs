using System.Windows;
using System.Windows.Controls;
using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>
/// 버튼 글자가 곧 동작인 작은 선택 대화창(C-1 저장 후 열기 · C-2 지점 변경).
/// <code>
/// int i = ChoiceDialog.Ask(owner, "저장했습니다", "…",
///     new ChoiceDialog.Option("닫기", IsCancel: true),
///     new ChoiceDialog.Option("폴더 열기"),
///     new ChoiceDialog.Option("파일 열기", IsPrimary: true));
/// </code>
/// 돌려주는 값은 고른 버튼의 순번. Esc·닫기(×)는 <c>IsCancel</c> 버튼의 순번(없으면 -1).
/// </summary>
public partial class ChoiceDialog : Window
{
    /// <summary>버튼 하나. <paramref name="IsPrimary"/> = Enter·채운 강조색, <paramref name="IsCancel"/> = Esc·닫기(×).</summary>
    public sealed record Option(string Label, bool IsPrimary = false, bool IsCancel = false, string? ToolTip = null);

    private int _picked = -1;

    public ChoiceDialog() => InitializeComponent();

    /// <summary>고른 버튼의 순번.</summary>
    public int Picked => _picked;

    /// <summary>대화창의 버튼 글자(자동 검증이 찾는다).</summary>
    public IReadOnlyList<string> Labels { get; private set; } = Array.Empty<string>();

    /// <summary>순번 <paramref name="index"/> 버튼을 누른 것과 같다(자동 검증용 · 화면 동작과 같은 길).</summary>
    public void Choose(int index)
    {
        _picked = index;
        DialogResult = true;
    }

    public static int Ask(Window? owner, string heading, string message, params Option[] options)
    {
        var d = new ChoiceDialog { Title = heading };
        if (owner is not null && owner.IsVisible) d.Owner = owner;
        else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        d.Heading.Text = heading;
        d.Message.Text = message;
        d.Labels = options.Select(o => o.Label).ToList();

        int cancel = Array.FindIndex(options, o => o.IsCancel);
        for (int i = 0; i < options.Length; i++)
        {
            var o = options[i];
            int index = i;
            var label = new TextBlock { Text = o.Label, VerticalAlignment = VerticalAlignment.Center };
            Button b;
            if (o.IsPrimary)
            {
                // 주요 : 채운 강조색(글자색은 버튼 템플릿이 상태별로 물려준다)
                b = new Button { Content = label, Style = (Style)d.FindResource("PrimaryActionButtonStyle"), IsDefault = true };
            }
            else
            {
                b = new RadButton { Content = label };
            }
            b.Padding = (Thickness)d.FindResource("ActionButtonPadding");
            b.Margin = i == 0 ? new Thickness(0) : new Thickness((double)d.FindResource("SpaceSm"), 0, 0, 0);
            if (o.ToolTip is not null) b.ToolTip = o.ToolTip;
            System.Windows.Automation.AutomationProperties.SetName(b, o.Label);
            if (o.IsCancel) b.IsCancel = true;
            b.Click += (_, _) => d.Choose(index);
            d.Buttons.Children.Add(b);
        }

        d.Loaded += (_, _) =>
        {
            // 첫 포커스는 주요 버튼 — Enter 한 번으로 기본 동작
            var primary = d.Buttons.Children.OfType<Button>().FirstOrDefault(x => x.IsDefault);
            (primary as UIElement ?? d.Buttons.Children.OfType<UIElement>().FirstOrDefault())?.Focus();
        };

        d.ShowDialog();
        return d._picked >= 0 ? d._picked : cancel;
    }
}
