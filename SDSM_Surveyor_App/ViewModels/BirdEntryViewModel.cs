using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>조류 탭: 종별 관찰 입력 + 총 종수/개체수.</summary>
public partial class BirdEntryViewModel : ObservableObject, ITransientService
{
    private const string TaxonKey = "Bird";
    private readonly ISpeciesListProvider _speciesProvider;
    private readonly ILocalDraftStore _draftStore;

    public BirdEntryViewModel(ISpeciesListProvider speciesProvider, ILocalDraftStore draftStore)
    {
        _speciesProvider = speciesProvider;
        _draftStore = draftStore;
        SpeciesListSource = _speciesProvider.GetBirdSpecies();
        Entries.CollectionChanged += OnEntriesChanged;
        Entries.Add(new BirdEntry());
    }

    [ObservableProperty] private string _yearChsu = string.Empty;
    [ObservableProperty] private DateTime? _surveyDate = DateTime.Today;
    [ObservableProperty] private string? _weather;
    [ObservableProperty] private string? _river;
    [ObservableProperty] private string? _site;
    [ObservableProperty] private string? _surveyor;

    public string[] Weathers { get; } = { "맑음", "흐림", "비(눈)" };
    public string[] MigratoryTypes { get; } = { "텃새", "여름철새", "겨울철새", "나그네새", "길잃은새" };

    public RadObservableCollection<BirdEntry> Entries { get; } = new();
    [ObservableProperty] private string[] _speciesListSource = System.Array.Empty<string>();

    [ObservableProperty] private int _totalSpeciesCount;
    [ObservableProperty] private int _totalIndividualCount;

    [ObservableProperty] private string _statusText = "임시 저장 없음";
    [ObservableProperty] private DateTime? _lastSavedTime;

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (BirdEntry r in e.OldItems) r.PropertyChanged -= OnRowChanged;
        if (e.NewItems is not null) foreach (BirdEntry r in e.NewItems) r.PropertyChanged += OnRowChanged;
        Recalculate();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BirdEntry.SpeciesKo) or nameof(BirdEntry.IndividualCount))
            Recalculate();
    }

    private void Recalculate()
    {
        TotalSpeciesCount = Entries.Where(x => !string.IsNullOrWhiteSpace(x.SpeciesKo))
                                   .Select(x => x.SpeciesKo).Distinct().Count();
        TotalIndividualCount = Entries.Sum(x => x.IndividualCount ?? 0);
        ExportExcelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand] private void AddRow() => Entries.Add(new BirdEntry());
    [RelayCommand] private void RemoveRow(BirdEntry? row) { if (row is not null) Entries.Remove(row); }

    [RelayCommand]
    private async Task SaveTemporary()
    {
        await _draftStore.SaveDraftAsync(TaxonKey,
            new { YearChsu, SurveyDate, Weather, River, Site, Surveyor, Rows = Entries.ToList() });
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

    private bool CanExport() => Entries.Any(x => !string.IsNullOrWhiteSpace(x.SpeciesKo));
}
