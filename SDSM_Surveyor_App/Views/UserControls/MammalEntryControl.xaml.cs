using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.UserControls;

public partial class MammalEntryControl : UserControl
{
    public MammalEntryControl()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<MammalEntryViewModel>();
    }
}
