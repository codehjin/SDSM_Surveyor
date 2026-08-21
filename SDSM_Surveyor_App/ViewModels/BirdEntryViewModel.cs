using System.Collections.Specialized;
using SDSM_Core.Helpers;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Models;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels.Base;
using Telerik.Windows.Data;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>조류 탭: 종별 관찰 입력(관리자 Bird 전 필드) + 총 종수/개체수.</summary>
public partial class BirdEntryViewModel : SpeciesEntryViewModelBase<BirdEntry, ObservedSpecies>, ISingletonService
{
    internal const string TaxonKey = "Bird";

    public BirdEntryViewModel(ISpeciesListProvider speciesProvider, ISessionService sessions, SurveyMeta meta)
        : base(sessions, meta)
    {
        SpeciesListSource = speciesProvider.GetBirdSpecies();
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
    }


    public string[] MigratoryTypes { get; } = { "텃새", "여름철새", "겨울철새", "나그네새", "길잃은새" };

    /// <summary>관찰 행. 기반 클래스의 <c>Rows</c> 를 화면 바인딩 이름으로 노출한다.</summary>
    public RadObservableCollection<BirdEntry> Entries => Rows;

    /// <summary>붙여넣기 한 줄 → 행.
    /// 열 순서 = 그리드 순서 : 국명 · 개체수 · 학명 · 목 · 과 · 도래유형 · 대항목 · 세부항목 · 서식유형 · 위도 · 경도 · 특징 · 특이사항.
    /// 국명으로 공식 종목록을 조회해 학명·목·과를 채우고, 붙여넣은 셀이 있으면 그 값이 우선한다.
    /// ([국명, 개체수] 2열만 붙여넣어도 된다)</summary>
    protected override BirdEntry CreateRowFromCells(string ko, string[] cells)
    {
        var entry = new BirdEntry
        {
            SpeciesKo = ko,
            IndividualCount = Pi(Cell(cells, 1)),
            MigratoryType = Cell(cells, 5),
            Category = Cell(cells, 6),
            CategoryDetail = Cell(cells, 7),
            HabitatType = Cell(cells, 8),
            Lat = Pd(Cell(cells, 9)),
            Lng = Pd(Cell(cells, 10)),
            Feature = Cell(cells, 11),
            Note = Cell(cells, 12),
        };
        entry.ApplySpecies(SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
        entry.SpeciesEn = Cell(cells, 2) ?? entry.SpeciesEn;   // 붙여넣은 값이 있으면 우선
        entry.OrderKo = Cell(cells, 3) ?? entry.OrderKo;
        entry.FamilyKo = Cell(cells, 4) ?? entry.FamilyKo;
        return entry;
    }


    /// <summary>국명을 공식 종목록과 대조해 학명·목·과·보호종 표기를 연결한다.
    /// 직접 입력·붙여넣기로 국명만 들어온 경우에도 자동으로 채워진다. 미매칭이면 국명만 남는다.</summary>
    private void ResolveSpecies(BirdEntry row)
    {
        var ko = row.SpeciesKo?.Trim();
        if (string.IsNullOrEmpty(ko)) return;
        if (row.MatchedSpecies?.SpeciesKo == ko) return;   // 이미 일치(자동완성 선택 등)
        row.ApplySpecies(SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
    }


    [ObservableProperty] private int _totalSpeciesCount;
    [ObservableProperty] private int _totalIndividualCount;



    protected override void Recalculate()
    {
        TotalSpeciesCount = Entries.Where(x => !string.IsNullOrWhiteSpace(x.SpeciesKo))
                                   .Select(x => x.SpeciesKo).Distinct().Count();
        // 결측치(null)와 0 엄격 구분 : 미입력은 합계에서 0으로 캐스팅
        TotalIndividualCount = Entries.Sum(x => x.IndividualCount ?? 0);
        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand] private void AddRow() => Entries.Add(new BirdEntry());
    [RelayCommand] private void RemoveRow(BirdEntry? row) { if (row is not null) Entries.Remove(row); }

    /// <summary>보고서·기록용 엑셀 내보내기(주력).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportExcel()
    {
        try
        {
            var saved = Export.BirdReportExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"보고서용 엑셀 완료 · {System.IO.Path.GetFileName(saved)}";
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("보고서용 엑셀을 내보냈습니다.", true)));
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    /// <summary>[레거시] 관리자 일괄입력 양식으로 내보내기(현행 관리자 Import 취합용).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportBulk()
    {
        try
        {
            var saved = Export.BirdExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"일괄입력용 완료 · {System.IO.Path.GetFileName(saved)}";
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("일괄입력용 엑셀을 내보냈습니다.", true)));
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    private bool CanExport() => Entries.Any(x => !string.IsNullOrWhiteSpace(x.SpeciesKo));

    // ── 기반 클래스가 요구하는 분류군 고유 동작 ──────────────────────────────

    protected override string? SpeciesKoOf(ObservedSpecies species) => species.SpeciesKo;

    protected override BirdEntry CreateRow(ObservedSpecies species, string? count)
    {
        var entry = new BirdEntry { SpeciesKo = species.SpeciesKo };
        entry.ApplySpecies(species);   // 학명·목·과 자동 채움
        if (int.TryParse(count, out var n)) entry.IndividualCount = n;
        return entry;
    }

    protected override bool IsRowEmpty(BirdEntry row) =>
        string.IsNullOrWhiteSpace(row.SpeciesKo) && row.IndividualCount is null;

    /// <summary>국명·개체수만 재계산에 영향을 준다(불필요한 재계산 방지).</summary>
    protected override bool AffectsRecalculation(string? propertyName) =>
        propertyName is nameof(BirdEntry.SpeciesKo) or nameof(BirdEntry.IndividualCount);

    /// <summary>국명만 채워진 행(직접 입력)도 공식 종목록과 자동 매칭한다.</summary>
    protected override void OnRowPropertyChanged(BirdEntry row, string? propertyName)
    {
        if (propertyName == nameof(BirdEntry.SpeciesKo)) ResolveSpecies(row);
    }
}
