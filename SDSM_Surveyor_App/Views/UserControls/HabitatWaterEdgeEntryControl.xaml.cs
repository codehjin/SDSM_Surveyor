using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.UserControls;

public partial class HabitatWaterEdgeEntryControl : UserControl
{
    public HabitatWaterEdgeEntryControl()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<HabitatWaterEdgeEntryViewModel>();
    }

    /// <summary>'평가 내용' 열이 보이는가(넓은 창 · 10-17). 좁은 창에서는 숨기고 항목 이름 툴팁으로만 보인다.</summary>
    public bool IsDescriptionVisible { get; private set; }

    /// <summary>
    /// 평가 항목 격자의 남는 폭이 <c>HwDescMinWidth</c> 이상이면 '평가 내용' 열을 펼치고(남는 폭을 그 열이 받는다),
    /// 아니면 숨기고 남는 폭을 점수 열 뒤로 보낸다(콤보·점수가 왼쪽에 모여 오른쪽 여백이 줄어든다).
    /// </summary>
    private void ItemsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var cols = ItemsGrid.ColumnDefinitions;
        double fixedWidth = 0;
        for (int i = 0; i < cols.Count; i++)
            if (cols[i] != DescCol && cols[i] != FillerCol) fixedWidth += cols[i].ActualWidth;

        bool wide = ItemsGrid.ActualWidth - fixedWidth >= (double)FindResource("HwDescMinWidth");
        if (wide == IsDescriptionVisible && e.PreviousSize.Width > 0) return;
        IsDescriptionVisible = wide;

        DescCol.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        FillerCol.Width = wide ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        var v = wide ? Visibility.Visible : Visibility.Collapsed;
        DescHeader.Visibility = v;
        foreach (var d in new[] { Desc1, Desc2, Desc3, Desc4, Desc5, Desc6, Desc7, Desc8, Desc9, Desc10 }) d.Visibility = v;
    }
}
