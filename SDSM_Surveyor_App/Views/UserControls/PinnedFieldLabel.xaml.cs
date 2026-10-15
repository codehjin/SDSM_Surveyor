using System.Windows;
using System.Windows.Controls;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 조사개황 칸 라벨 + 압정 토글. 라벨 문구와 <see cref="PinToggle"/> 하나만 받는다.
///
/// 6개 칸에서 이 컨트롤을 쓰면 라벨 줄이 한 줄로 끝난다 —
/// 칸마다 Grid 를 손으로 다시 짜면 조사개황 XAML 이 여섯 배로 길어진다.
/// </summary>
public partial class PinnedFieldLabel : UserControl
{
    public PinnedFieldLabel() => InitializeComponent();

    /// <summary>칸 이름(대분류 · 연도차수 …).</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(PinnedFieldLabel), new PropertyMetadata(string.Empty));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>이 칸의 압정 상태(<c>SurveyMeta.Pins</c> 의 6개 중 하나).</summary>
    public static readonly DependencyProperty PinProperty = DependencyProperty.Register(
        nameof(Pin), typeof(PinToggle), typeof(PinnedFieldLabel), new PropertyMetadata(null));

    public PinToggle? Pin
    {
        get => (PinToggle?)GetValue(PinProperty);
        set => SetValue(PinProperty, value);
    }
}
