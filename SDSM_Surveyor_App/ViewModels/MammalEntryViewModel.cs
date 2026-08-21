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

/// <summary>포유류 탭: 종별 관찰(흔적 12종 개체수) + 총 종수/개체수/건수.</summary>
public partial class MammalEntryViewModel : SpeciesEntryViewModelBase<MammalEntry, ObservedSpecies>, ISingletonService
{
    internal const string TaxonKey = "Mammal";

    public MammalEntryViewModel(ISpeciesListProvider speciesProvider, ISessionService sessions, SurveyMeta meta)
        : base(sessions, meta)
    {
        SpeciesListSource = speciesProvider.GetMammalSpecies();
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
    }


    /// <summary>관찰 행. 기반 클래스의 <c>Rows</c> 를 화면 바인딩 이름으로 노출한다.</summary>
    public RadObservableCollection<MammalEntry> Entries => Rows;

    /// <summary>붙여넣기 한 줄 → 행.
    /// 열 순서 = 그리드 순서 : 국명 · 학명 · 목 · 과 · 관찰지유형 · 흔적 12종 · 위도 · 경도 · 특징 · 특이사항.</summary>
    protected override MammalEntry CreateRowFromCells(string ko, string[] cells)
    {
        var entry = new MammalEntry
        {
            SpeciesKo = ko,
            ObservationSite = Cell(cells, 4),
            Trace1 = Pi(Cell(cells, 5)),
            Trace2 = Pi(Cell(cells, 6)),
            Trace3 = Pi(Cell(cells, 7)),
            Trace4 = Pi(Cell(cells, 8)),
            Trace5 = Pi(Cell(cells, 9)),
            Trace6 = Pi(Cell(cells, 10)),
            Trace7 = Pi(Cell(cells, 11)),
            Trace8 = Pi(Cell(cells, 12)),
            Trace9 = Pi(Cell(cells, 13)),
            Trace10 = Pi(Cell(cells, 14)),
            Trace11 = Pi(Cell(cells, 15)),
            Trace12 = Pi(Cell(cells, 16)),
            Lat = Pd(Cell(cells, 17)),
            Lng = Pd(Cell(cells, 18)),
            Feature = Cell(cells, 19),
            Note = Cell(cells, 20),
        };
        entry.ApplySpecies(SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
        entry.SpeciesEn = Cell(cells, 1) ?? entry.SpeciesEn;
        entry.OrderKo = Cell(cells, 2) ?? entry.OrderKo;
        entry.FamilyKo = Cell(cells, 3) ?? entry.FamilyKo;
        return entry;
    }


    /// <summary>국명을 공식 종목록과 대조해 학명·목·과·보호종 표기를 연결한다.
    /// 직접 입력·붙여넣기로 국명만 들어온 경우에도 자동으로 채워진다. 미매칭이면 국명만 남는다.</summary>
    private void ResolveSpecies(MammalEntry row)
    {
        var ko = row.SpeciesKo?.Trim();
        if (string.IsNullOrEmpty(ko)) return;
        if (row.MatchedSpecies?.SpeciesKo == ko) return;   // 이미 일치(자동완성 선택 등)
        row.ApplySpecies(SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
    }


    [ObservableProperty] private int _totalSpeciesCount;
    [ObservableProperty] private int _totalIndividualCount;
    [ObservableProperty] private int _totalObservations;



    protected override void Recalculate()
    {
        var named = Entries.Where(x => !string.IsNullOrWhiteSpace(x.SpeciesKo)).ToList();
        TotalSpeciesCount = named.Select(x => x.SpeciesKo).Distinct().Count();
        TotalObservations = named.Count;
        // 결측치(null)와 0 엄격 구분 : 미입력은 합계에서 0으로 캐스팅(TraceSum)
        TotalIndividualCount = named.Sum(x => x.TraceSum);
        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand] private void AddRow() => Entries.Add(new MammalEntry());
    [RelayCommand] private void RemoveRow(MammalEntry? row) { if (row is not null) Entries.Remove(row); }

    /// <summary>보고서·기록용 엑셀 내보내기(주력).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportExcel()
    {
        try
        {
            var saved = Export.MammalReportExporter.Export(this);
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
            var saved = Export.MammalExcelExporter.Export(this);
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

    protected override MammalEntry CreateRow(ObservedSpecies species, string? count)
    {
        var entry = new MammalEntry { SpeciesKo = species.SpeciesKo };
        entry.ApplySpecies(species);
        if (int.TryParse(count, out var n)) entry.Trace2 = n;   // 관찰
        return entry;
    }

    protected override bool IsRowEmpty(MammalEntry row) =>
        string.IsNullOrWhiteSpace(row.SpeciesKo) && row.TraceSum == 0;

    /// <summary>국명·개체수만 재계산에 영향을 준다(불필요한 재계산 방지).</summary>
    protected override bool AffectsRecalculation(string? propertyName) =>
        propertyName is nameof(MammalEntry.SpeciesKo) or nameof(MammalEntry.TraceSum);

    /// <summary>국명만 채워진 행(직접 입력)도 공식 종목록과 자동 매칭한다.</summary>
    protected override void OnRowPropertyChanged(MammalEntry row, string? propertyName)
    {
        if (propertyName == nameof(MammalEntry.SpeciesKo)) ResolveSpecies(row);
    }
}
