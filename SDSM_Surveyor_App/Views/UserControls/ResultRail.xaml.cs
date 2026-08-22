using System.Windows;
using System.Windows.Controls;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>
/// 결과 레일 (06_DESIGN_REBUILD §5-1-3). 종 입력형·폼형이 공유한다.
/// 안의 내용은 화면마다 다르므로 <see cref="Body"/> 슬롯으로 받는다
/// (UserControl 의 <c>Content</c> 는 이미 레일 껍데기가 쓰고 있다).
/// </summary>
public partial class ResultRail : UserControl
{
    public ResultRail() => InitializeComponent();

    /// <summary>레일 제목 (`실시간 지수` · `수질 등급 요약` 등).</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(ResultRail), new PropertyMetadata(string.Empty));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>레일 본문 슬롯. 지수 카드·등급 목록 등 화면별 내용을 넣는다.</summary>
    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(object), typeof(ResultRail), new PropertyMetadata(null));

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }
}
