using System.Windows;
using System.Windows.Controls;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 접힘 요약줄(36px) + 펼친 카드. 7개 분류군이 공유한다(README §구현 순서 2).
///
/// 화면이 넘기는 것 : <see cref="Title"/> · <see cref="Caption"/> ·
/// <see cref="SummarySlot"/>(접힘 한 줄) · <see cref="ExpandedSlot"/>(펼침 본문)
/// 컨트롤이 소유하는 것 : 껍데기 · 접기/펼치기 링크 · 전환.
///
/// ⚠ <see cref="IsExpanded"/> 는 분류군 VM 에 TwoWay 로 묶고 **세션에 저장한다** —
///   다음 실행에도 접힘 상태가 유지돼야 한다(README §상태).
/// </summary>
public partial class CollapsibleSummary : UserControl
{
    public CollapsibleSummary() => InitializeComponent();

    /// <summary>블록 제목(「조사환경」·「건강성」).</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(CollapsibleSummary), new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>펼친 카드 머리글 옆 보조 설명(선택).</summary>
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(CollapsibleSummary), new PropertyMetadata(string.Empty));

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>접힘 상태에서 보여 줄 한 줄 요약(라벨·값 쌍).</summary>
    public static readonly DependencyProperty SummarySlotProperty = DependencyProperty.Register(
        nameof(SummarySlot), typeof(object), typeof(CollapsibleSummary), new PropertyMetadata(null));

    public object? SummarySlot
    {
        get => GetValue(SummarySlotProperty);
        set => SetValue(SummarySlotProperty, value);
    }

    /// <summary>펼침 상태 카드 본문(폼 · 지수 카드 등).</summary>
    public static readonly DependencyProperty ExpandedSlotProperty = DependencyProperty.Register(
        nameof(ExpandedSlot), typeof(object), typeof(CollapsibleSummary), new PropertyMetadata(null));

    public object? ExpandedSlot
    {
        get => GetValue(ExpandedSlotProperty);
        set => SetValue(ExpandedSlotProperty, value);
    }

    /// <summary>펼쳐져 있는지. 분류군 VM 에 TwoWay 로 묶어 세션에 저장한다.</summary>
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(CollapsibleSummary),
        new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    // 스크린리더용 이름은 XAML 에서 Title 에 StringFormat 을 걸어 만든다
    // (「조사환경 펼치기」). 계산 속성을 두면 Title 변경이 바인딩에 전달되지 않는다.

    private void Toggle_Click(object sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;
}
