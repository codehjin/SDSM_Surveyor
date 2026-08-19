using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>동기화 창: 교환소 폴더 설정 + 종목록/기준자료 버전 비교 + 가져오기.</summary>
public partial class SyncViewModel : ObservableObject, ITransientService
{
    private readonly ISettingsStore _settings;
    private readonly ISyncService _sync;

    public SyncViewModel(ISettingsStore settings, ISyncService sync)
    {
        _settings = settings;
        _sync = sync;
        _exchangeFolder = settings.Settings.ExchangeFolder;
        Refresh();
    }

    [ObservableProperty] private string? _exchangeFolder;
    [ObservableProperty] private string _speciesStatus = string.Empty;
    [ObservableProperty] private string _referenceStatus = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _canImport;

    partial void OnExchangeFolderChanged(string? value)
    {
        _settings.Settings.ExchangeFolder = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        _settings.Save();
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        var st = _sync.GetStatus();
        SpeciesStatus = Fmt("종목록", st.InstalledSpecies, st.ExchangeSpecies);
        ReferenceStatus = Fmt("기준자료", st.InstalledReference, st.ExchangeReference);
        CanImport = st.ExchangeSpecies.Exists || st.ExchangeReference.Exists;
    }

    private static string Fmt(string label, FileVersionInfo cur, FileVersionInfo ex)
    {
        string c = cur.Exists ? (cur.Version ?? "버전정보 없음") : "없음";
        string e = ex.Exists ? (ex.Version ?? "버전정보 없음") : "교환소에 없음";
        string mark = (cur.Exists && ex.Exists && cur.Version != ex.Version) ? "  ★ 갱신 가능" : string.Empty;
        return $"{label}   현재: {c}    →    교환소: {e}{mark}";
    }

    [RelayCommand]
    private void Import()
    {
        var n = _sync.ImportFromExchange();
        Message = n > 0
            ? $"{n}개 파일을 가져왔습니다. 앱을 다시 시작하면 반영됩니다."
            : "가져올 파일이 없습니다. 교환소 폴더에 species.json / reference.json 이 있는지 확인하세요.";
        Refresh();
    }
}
