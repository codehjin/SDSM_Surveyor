using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Core.Helpers;
using SDSM_Models;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Export;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Models;
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>
/// 종목록 팝업 — 담당 분류군의 등록 종 전체를 보여 주고 엑셀로 내려받는다.
///
/// ⚠ 종목록은 <see cref="ISpeciesListProvider"/> 에서 **직접** 읽는다.
///   화면 VM 의 <c>SpeciesListSource</c> 를 읽으면 안 된다 —
///   저서동물 VM 이 기반 클래스의 같은 이름 속성을 가리고 있어 빈 목록이 올 수 있다.
///
/// ⚠ 담당 분류군만 보여 준다(발주처 확정, 2026-09-09). 다른 분류군 종목록은 열지 않는다.
/// 수질·서식수변은 종목록이 없다 — 이 두 화면에는 빠른추가 바 자체가 없으므로 팝업도 뜨지 않는다.
/// </summary>
public partial class SpeciesCatalogViewModel : ObservableObject, ITransientService
{
    private readonly ISpeciesListProvider _species;
    private readonly TaxonSelection _taxon;
    private List<SpeciesCatalogRow> _all = new();

    public SpeciesCatalogViewModel(ISpeciesListProvider species, TaxonSelection taxon)
    {
        _species = species;
        _taxon = taxon;
        Load();
    }

    /// <summary>담당 분류군.</summary>
    public TaxonKind Kind => _taxon.Current;

    /// <summary>분류군명(어류 · 저서동물 …).</summary>
    public string TaxonName => _taxon.Name;

    /// <summary>종목록 버전(species.json 의 Version). 지수가 관리자와 일치하는지의 근거다.</summary>
    public string Version => string.IsNullOrWhiteSpace(_species.Version) ? "-" : _species.Version!;

    /// <summary>창 제목줄 — `종목록 (저서동물) · nibr-20260820 · 803종`.</summary>
    public string HeaderText => $"종목록 ({TaxonName}) · {Version} · {_all.Count:N0}종";

    /// <summary>목록 아래 상태 문구.</summary>
    public string StatusText => Rows.Count == _all.Count
        ? $"전체 {_all.Count:N0}종"
        : $"{Rows.Count:N0}종 표시 · 전체 {_all.Count:N0}종";

    /// <summary>화면에 보이는 행(필터 결과).</summary>
    public RadObservableCollection<SpeciesCatalogRow> Rows { get; } = new();

    [ObservableProperty] private SpeciesCatalogRow? _selected;

    /// <summary>국명·학명·초성 검색어.</summary>
    [ObservableProperty] private string? _search;

    partial void OnSearchChanged(string? value) => ApplyFilter();

    // ── 로드 ──────────────────────────────────────────────────────────────────

    private void Load()
    {
        _all = Kind switch
        {
            TaxonKind.Fish => _species.GetFishSpecies().Select(FromFish).ToList(),
            TaxonKind.Benthos => _species.GetBenthosSpecies().Select(FromBenthos).ToList(),
            TaxonKind.Bird => _species.GetBirdSpecies().Select(FromObserved).ToList(),
            TaxonKind.Mammal => _species.GetMammalSpecies().Select(FromObserved).ToList(),
            TaxonKind.AmphibianReptile => _species.GetAmphibianSpecies().Select(FromObserved).ToList(),
            _ => new List<SpeciesCatalogRow>()
        };
        ApplyFilter();
    }

    /// <summary>국명이 <c>-</c> 뿐인 행은 검색 대상에서 뺀다(그 한 글자로 목록 전체가 걸린다).</summary>
    private static string SearchableKo(string? ko)
    {
        var t = ko?.Trim();
        return string.IsNullOrEmpty(t) || t == "-" ? string.Empty : t;
    }

    private static string? Tag(params (string? Value, string Label)[] parts)
    {
        var on = parts.Where(p => !string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Label).ToList();
        return on.Count == 0 ? null : string.Join(" · ", on);
    }

    private static SpeciesCatalogRow FromFish(FishSpeciesList s) => new()
    {
        Source = s,
        ClassKo = s.ClassKo,
        OrderKo = s.OrderKo,
        FamilyKo = s.FamilyKo,
        SpeciesKo = s.SpeciesKo,
        SpeciesEn = s.SpeciesEn,
        Tolerance = s.ToleranceGuild,
        Feeding = s.FeedingGuild,
        Habitat = s.HabitatGuild,
        Tags = Tag((s.Endangered1, "멸종위기Ⅰ"), (s.Endangered2, "멸종위기Ⅱ"),
                   (s.NaturalMonument, "천연기념물"), (s.Endemic, "고유종"), (s.Exotic, "외래종")),
        SearchKo = SearchableKo(s.SpeciesKo),
        SearchEn = s.SpeciesEn?.Trim() ?? string.Empty
    };

    private static SpeciesCatalogRow FromBenthos(BenthosSpeciesList s) => new()
    {
        Source = s,
        PhylumKo = s.PhylumKo,
        ClassKo = s.ClassKo,
        OrderKo = s.OrderKo,
        FamilyKo = s.FamilyKo,
        SpeciesKo = s.SpeciesKo,
        SpeciesEn = s.SpeciesEn,
        Saprobic = s.SaprobicValue is double v ? v.ToString("0.0") : null,
        Weight = s.IndicatorWeight is int g ? g.ToString() : null,
        Tags = Tag((s.Endangered1, "멸종위기Ⅰ"), (s.Endangered2, "멸종위기Ⅱ"), (s.Endemic, "고유종")),
        SearchKo = SearchableKo(s.SpeciesKo),
        SearchEn = s.SpeciesEn?.Trim() ?? string.Empty
    };

    private static SpeciesCatalogRow FromObserved(ObservedSpecies s) => new()
    {
        Source = s,
        OrderKo = s.OrderKo,
        FamilyKo = s.FamilyKo,
        SpeciesKo = s.SpeciesKo,
        SpeciesEn = s.SpeciesEn,
        Ktsn = s.Ktsn,
        Tags = Tag((s.Endangered1, "멸종위기Ⅰ"), (s.Endangered2, "멸종위기Ⅱ"),
                   (s.NaturalMonument, "천연기념물"), (s.Invasive, "생태계교란")),
        SearchKo = SearchableKo(s.SpeciesKo),
        SearchEn = s.SpeciesEn?.Trim() ?? string.Empty
    };

    // ── 필터 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 국명·학명·초성을 함께 본다. 빈 검색어면 전체.
    /// 초성 판정은 빠른추가 바와 같은 <see cref="ChosungHelper"/> 를 쓴다.
    /// </summary>
    private void ApplyFilter()
    {
        var q = Search?.Trim();
        var list = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(r => (r.SearchKo.Length > 0 && ChosungHelper.IsMatch(r.SearchKo, q))
                           || (r.SearchEn.Length > 0 && r.SearchEn.Contains(q, StringComparison.OrdinalIgnoreCase)))
                  .ToList();

        Rows.SuspendNotifications();
        Rows.Clear();
        var n = 0;
        foreach (var r in list) { r.No = ++n; Rows.Add(r); }
        Rows.ResumeNotifications();

        OnPropertyChanged(nameof(StatusText));
    }

    [RelayCommand]
    private void ClearSearch() => Search = null;

    // ── 엑셀 ──────────────────────────────────────────────────────────────────

    /// <summary>저장 결과 경로(성공 시). 창이 안내 문구를 띄우는 데 쓴다.</summary>
    public string? LastSavedPath { get; private set; }

    [RelayCommand]
    private void ExportExcel()
    {
        LastSavedPath = SpeciesCatalogExcelExporter.Export(this, _all);
        OnPropertyChanged(nameof(LastSavedPath));
    }
}
