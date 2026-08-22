using System.Windows;
using System.Windows.Controls;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 하단 상태 · 액션 바. 7개 화면이 공유한다(06_DESIGN_REBUILD §5-3).
///
/// <c>StatusText</c> 는 DataContext(= 화면 ViewModel)에서 바로 읽는다 —
/// <see cref="ViewModels.Base.TaxonEntryViewModelBase"/> 가 7개 분류군 전부에 제공한다.
/// 총계와 액션 버튼은 화면마다 달라 슬롯으로 받는다.
/// </summary>
public partial class TaxonStatusBar : UserControl
{
    public TaxonStatusBar() => InitializeComponent();

    /// <summary>좌측 총계 슬롯 (`총 12종 · 341개체` 등).</summary>
    public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(
        nameof(Summary), typeof(object), typeof(TaxonStatusBar), new PropertyMetadata(null));

    public object? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>우측 액션 버튼 슬롯. 위험 버튼을 왼쪽, 주요 버튼을 오른쪽에 둔다.</summary>
    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(TaxonStatusBar), new PropertyMetadata(null));

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>이상치 경고 문구. 비어 있으면 경고 영역이 통째로 숨는다.</summary>
    public static readonly DependencyProperty WarningTextProperty = DependencyProperty.Register(
        nameof(WarningText), typeof(string), typeof(TaxonStatusBar),
        new PropertyMetadata(null, OnWarningTextChanged));

    public string? WarningText
    {
        get => (string?)GetValue(WarningTextProperty);
        set => SetValue(WarningTextProperty, value);
    }

    /// <summary>경고 영역 표시 여부. 경고는 <b>비차단</b>이다 — 저장·내보내기를 막지 않는다(§3-5).</summary>
    public static readonly DependencyProperty HasWarningProperty = DependencyProperty.Register(
        nameof(HasWarning), typeof(bool), typeof(TaxonStatusBar), new PropertyMetadata(false));

    public bool HasWarning
    {
        get => (bool)GetValue(HasWarningProperty);
        set => SetValue(HasWarningProperty, value);
    }

    private static void OnWarningTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TaxonStatusBar)d).HasWarning = !string.IsNullOrWhiteSpace(e.NewValue as string);
}
