using System.Windows;
using System.Windows.Controls;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 분류군 조사일 칸(N3). 7개 분류군 화면이 같은 칸을 쓴다 — DataContext(분류군 VM)의
/// <c>TaxonSurveyDate</c>·<c>TaxonDateWatermark</c>·<c>TaxonDateHint</c> 를 본다
/// (<see cref="ViewModels.Base.TaxonEntryViewModelBase"/>).
/// </summary>
public partial class TaxonDateBar : UserControl
{
    public TaxonDateBar() => InitializeComponent();

    /// <summary>칸 옆 안내 글자를 보일지. 좁은 요약줄(관찰형)에서는 끄고 툴팁으로만 보인다.</summary>
    public static readonly DependencyProperty ShowHintProperty = DependencyProperty.Register(
        nameof(ShowHint), typeof(bool), typeof(TaxonDateBar), new PropertyMetadata(true));

    public bool ShowHint
    {
        get => (bool)GetValue(ShowHintProperty);
        set => SetValue(ShowHintProperty, value);
    }
}
