using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Ecology;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>수질 탭: 측정값 폼 입력 + 항목별 등급 실시간 산정.</summary>
public partial class WaterQualityEntryViewModel : ObservableObject, ITransientService
{
    private const string TaxonKey = "WaterQuality";
    private readonly ILocalDraftStore _draftStore;

    public WaterQualityEntryViewModel(ILocalDraftStore draftStore) => _draftStore = draftStore;

    private static double? P(string? s) => double.TryParse(s, out var d) ? d : null;

    // 공통 기본정보
    [ObservableProperty] private string _yearChsu = string.Empty;
    [ObservableProperty] private DateTime? _surveyDate = DateTime.Today;
    [ObservableProperty] private string? _majorRegion;
    [ObservableProperty] private string? _river;
    [ObservableProperty] private string? _site;
    [ObservableProperty] private string? _surveyor;
    [ObservableProperty] private string? _weather;
    public string[] Weathers { get; } = { "맑음", "흐림", "비(눈)" };

    // 측정 항목 (빈 문자열=미측정, 관리자와 동일하게 문자열로 다룸)
    [ObservableProperty] private string? _pH;
    [ObservableProperty] private string? _bod;
    [ObservableProperty] private string? _cod;
    [ObservableProperty] private string? _toc;
    [ObservableProperty] private string? _ss;
    [ObservableProperty] private string? _dox;     // DO
    [ObservableProperty] private string? _tp;
    [ObservableProperty] private string? _eColi;
    [ObservableProperty] private string? _ecotoxicity;   // 생태독성

    partial void OnPHChanged(string? v) => RaiseGrades();
    partial void OnBodChanged(string? v) => RaiseGrades();
    partial void OnCodChanged(string? v) => RaiseGrades();
    partial void OnTocChanged(string? v) => RaiseGrades();
    partial void OnSsChanged(string? v) => RaiseGrades();
    partial void OnDoxChanged(string? v) => RaiseGrades();
    partial void OnTpChanged(string? v) => RaiseGrades();
    partial void OnEColiChanged(string? v) => RaiseGrades();

    private void RaiseGrades()
    {
        OnPropertyChanged(nameof(PhGradeText));
        OnPropertyChanged(nameof(BodGradeText));
        OnPropertyChanged(nameof(CodGradeText));
        OnPropertyChanged(nameof(TocGradeText));
        OnPropertyChanged(nameof(SsGradeText));
        OnPropertyChanged(nameof(DoGradeText));
        OnPropertyChanged(nameof(TpGradeText));
        OnPropertyChanged(nameof(EColiGradeText));
        ExportExcelCommand.NotifyCanExecuteChanged();
    }

    // 실시간 등급(표시용)
    public string PhGradeText    => WaterQualityCalculator.PhGrade(P(PH))?.ToString() ?? "-";
    public string BodGradeText   => WaterQualityCalculator.BodGrade(P(Bod)) ?? "-";
    public string CodGradeText   => WaterQualityCalculator.CodGrade(P(Cod)) ?? "-";
    public string TocGradeText   => WaterQualityCalculator.TocGrade(P(Toc)) ?? "-";
    public string SsGradeText    => WaterQualityCalculator.SsGrade(P(Ss))?.ToString() ?? "-";
    public string DoGradeText    => WaterQualityCalculator.DoGrade(P(Dox)) ?? "-";
    public string TpGradeText    => WaterQualityCalculator.TpGrade(P(Tp)) ?? "-";
    public string EColiGradeText => WaterQualityCalculator.EColiGrade(P(EColi)) ?? "-";

    [ObservableProperty] private string _statusText = "임시 저장 없음";
    [ObservableProperty] private DateTime? _lastSavedTime;

    [RelayCommand]
    private async Task SaveTemporary()
    {
        await _draftStore.SaveDraftAsync(TaxonKey, new
        {
            YearChsu, SurveyDate, MajorRegion, River, Site, Surveyor, Weather,
            PH, Bod, Cod, Toc, Ss, Dox, Tp, EColi, Ecotoxicity
        });
        LastSavedTime = DateTime.Now;
        StatusText = $"임시 저장됨 · {LastSavedTime:HH:mm:ss}";
        WeakReferenceMessenger.Default.Send(new NotifyMessage(("임시 저장되었습니다.", true)));
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportExcel()
    {
        StatusText = "엑셀 내보내기: 다음 단계에서 연동 예정";
        return Task.CompletedTask;
    }

    private bool CanExport() =>
        new[] { PH, Bod, Cod, Toc, Ss, Dox, Tp, EColi }.Any(s => !string.IsNullOrWhiteSpace(s));
}
