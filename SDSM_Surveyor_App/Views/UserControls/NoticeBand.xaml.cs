using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.UserControls;

/// <summary>알림 띠(§27-2). 7개 화면의 상태 바가 하나씩 품고, 모두 같은 <see cref="NoticeBandViewModel"/> 를 본다.</summary>
public partial class NoticeBand : UserControl
{
    public NoticeBand()
    {
        InitializeComponent();
        // 디자이너(App 이 아님)에서는 비워 둔다
        if (System.Windows.Application.Current is App app)
            DataContext = app.Services.GetRequiredService<NoticeBandViewModel>();
    }
}
