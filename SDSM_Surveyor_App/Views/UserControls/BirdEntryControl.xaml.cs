using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.UserControls;

public partial class BirdEntryControl : UserControl
{
    public BirdEntryControl()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<BirdEntryViewModel>();
    }
}
