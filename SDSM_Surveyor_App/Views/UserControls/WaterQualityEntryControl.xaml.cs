using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.UserControls;

public partial class WaterQualityEntryControl : UserControl
{
    public WaterQualityEntryControl()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<WaterQualityEntryViewModel>();
    }
}
