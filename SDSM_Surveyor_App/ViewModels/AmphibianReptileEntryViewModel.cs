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

/// <summary>양서파충류 탭: 종별 관찰(흔적 6종 개체수) + 총 종수/개체수/건수.</summary>
public partial class AmphibianReptileEntryViewModel : SpeciesEntryViewModelBase<AmphibianReptileEntry, ObservedSpecies>, ISingletonService
{
    internal const string TaxonKey = "AmphibianReptile";

    public AmphibianReptileEntryViewModel(ISpeciesListProvider speciesProvider, ISessionService sessions, SurveyMeta meta)
        : base(sessions, meta)
    {
        SpeciesListSource = speciesProvider.GetAmphibianSpecies();
        // 종 추가는 상단 '빠른 추가' 바로 하므로 초기 빈 행을 넣지 않는다(빈 행 누적 방지).
    }


    /// <summary>서식지 유형 대분류(D6 · 양식 Y열). 중분류는 행마다 <see cref="AmphibianReptileEntry.MiddleOptions"/>.</summary>
    public IReadOnlyList<FormChoice> HabitatMajorChoices => FormChoices.AmphHabitatMajor;

    /// <summary>관찰 행. 기반 클래스의 <c>Rows</c> 를 화면 바인딩 이름으로 노출한다.</summary>
    public RadObservableCollection<AmphibianReptileEntry> Entries => Rows;

    /// <summary>붙여넣기 한 줄 → 행.
    /// 열 순서 = 그리드 순서 : 국명 · 학명 · 목 · 과 · 대분류 · 중분류 · 흔적 6종 · 위도 · 경도 · 특징 · 특이사항.</summary>
    protected override AmphibianReptileEntry CreateRowFromCells(string ko, string[] cells)
    {
        var entry = new AmphibianReptileEntry
        {
            SpeciesKo = ko,
            MajorCategory = Cell(cells, 4),
            MiddleCategory = Cell(cells, 5),
            Trace1 = Pi(Cell(cells, 6)),
            Trace2 = Pi(Cell(cells, 7)),
            Trace3 = Pi(Cell(cells, 8)),
            Trace4 = Pi(Cell(cells, 9)),
            Trace5 = Pi(Cell(cells, 10)),
            Trace6 = Pi(Cell(cells, 11)),
            Lat = Pd(Cell(cells, 12)),
            Lng = Pd(Cell(cells, 13)),
            Feature = Cell(cells, 14),
            Note = Cell(cells, 15),
        };
        entry.ApplySpecies(SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
        // 학명(D2) — 종목록에 있는 국명은 종목록대로. 붙여넣은 학명이 다르면 바꾸고 센다. 목록 밖 국명만 붙여넣은 학명을 쓴다.
        var pastedEn = Cell(cells, 1);
        if (ScientificNameRule.IsLocked(entry)) { if (ScientificNameRule.Differs(entry, pastedEn)) PasteScientificFixed++; }
        else entry.SpeciesEn = pastedEn ?? entry.SpeciesEn;
        entry.OrderKo = Cell(cells, 2) ?? entry.OrderKo;
        entry.FamilyKo = Cell(cells, 3) ?? entry.FamilyKo;
        return entry;
    }


    /// <summary>국명을 공식 종목록과 대조해 학명·목·과·보호종 표기를 연결한다.
    /// 직접 입력·붙여넣기로 국명만 들어온 경우에도 자동으로 채워진다. 미매칭이면 국명만 남는다.</summary>
    private void ResolveSpecies(AmphibianReptileEntry row)
    {
        var ko = row.SpeciesKo?.Trim();
        if (string.IsNullOrEmpty(ko)) return;
        if (row.MatchedSpecies?.SpeciesKo == ko) return;   // 이미 일치(자동완성 선택 등)
        row.ApplySpecies(SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
    }


    [ObservableProperty] private int _totalSpeciesCount;
    [ObservableProperty] private int _totalIndividualCount;

    // ── 본문 상단 요약 스트립 (관찰형은 결과 레일을 두지 않는다 · design.md §5.2) ──
    /// <summary>법정보호종 종수(멸종위기·천연기념물).</summary>
    [ObservableProperty] private int _protectedCount;

    /// <summary>생태계교란생물 종수. ⚠ 보호종과 **의미가 반대**라 한 숫자로 합치지 않는다.</summary>
    [ObservableProperty] private int _invasiveCount;

    /// <summary>행이 하나도 없는가(빈 상태 안내 · README §11.4).</summary>
    public bool HasNoRows => Entries.Count == 0;

    /// <summary>빠른추가로 들어온 행을 1.2초간 강조한다(기반 클래스가 타이머를 돌린다).</summary>
    protected override void SetJustAdded(AmphibianReptileEntry row, bool value) => row.IsJustAdded = value;
    [ObservableProperty] private int _totalObservations;


    protected override void Recalculate()
    {
        var named = Entries.Where(x => !string.IsNullOrWhiteSpace(x.SpeciesKo)).ToList();
        TotalSpeciesCount = named.Select(x => x.SpeciesKo).Distinct().Count();
        TotalObservations = named.Count;
        // 결측치(null)와 0 엄격 구분 : 미입력은 합계에서 0으로 캐스팅(TraceSum)
        TotalIndividualCount = named.Sum(x => x.TraceSum);
        var (prot, invasive) = ObservationSummary.Count(Entries);
        ObservationSummary.MarkSameSpecies(Entries);   // '같은 종 n행' 표시(B-1)
        ProtectedCount = prot;
        InvasiveCount = invasive;
        OnPropertyChanged(nameof(HasNoRows));

        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand] private void AddRow() => Entries.Add(new AmphibianReptileEntry());

    /// <summary>보고서·기록용 엑셀 내보내기(주력).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportExcel()
    {
        try
        {
            var saved = Export.AmphibianReptileReportExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"보고서용 엑셀 완료 · {System.IO.Path.GetFileName(saved)}";
                NotifyExported("보고서용 엑셀을 내보냈습니다.");
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
            var saved = Export.AmphibianReptileExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"일괄입력용 완료 · {System.IO.Path.GetFileName(saved)}";
                NotifyExported("일괄입력용 엑셀을 내보냈습니다.");
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    private bool CanExport() => Entries.Any(x => !string.IsNullOrWhiteSpace(x.SpeciesKo));

    // ── 기반 클래스가 요구하는 분류군 고유 동작 ──────────────────────────────

    protected override string? SpeciesKoOf(ObservedSpecies species) => species.SpeciesKo;

    protected override AmphibianReptileEntry CreateRow(ObservedSpecies species, string? count)
    {
        var entry = new AmphibianReptileEntry { SpeciesKo = species.SpeciesKo };
        entry.ApplySpecies(species);
        if (int.TryParse(count, out var n)) entry.Trace1 = n;   // 성체
        return entry;
    }

    protected override bool IsRowEmpty(AmphibianReptileEntry row) =>
        string.IsNullOrWhiteSpace(row.SpeciesKo) && row.TraceSum == 0;

    /// <summary>국명·개체수만 재계산에 영향을 준다(불필요한 재계산 방지).</summary>
    protected override bool AffectsRecalculation(string? propertyName) =>
        propertyName is nameof(AmphibianReptileEntry.SpeciesKo) or nameof(AmphibianReptileEntry.TraceSum);

    /// <summary>국명만 채워진 행(직접 입력)도 공식 종목록과 자동 매칭한다.</summary>
    protected override void OnRowPropertyChanged(AmphibianReptileEntry row, string? propertyName)
    {
        if (propertyName == nameof(AmphibianReptileEntry.SpeciesKo)) ResolveSpecies(row);
        else if (propertyName == nameof(AmphibianReptileEntry.SpeciesEn) && ScientificNameRule.Enforce(row)) NotifyScientificFixed(1);   // D2 직접 수정
        else if (propertyName == nameof(AmphibianReptileEntry.MajorCategory)) TrimMiddle(row);   // D6 대분류를 바꾸면 맞지 않는 중분류는 비운다
    }

    // ── 저장·내보내기 알림(2026-10-18 결정 D3 · 학명 D2) ──
    protected override string NoticeTaxon => "양서파충류";

    /// <summary>국명이 있는데 개체수·흔적이 비었거나 0 인 행(D3 — 내보내되 알린다).</summary>
    public override int EmptyObservationRows => Entries.Count(e => !string.IsNullOrWhiteSpace(e.SpeciesKo) && e.HasNoCount);

    /// <summary>서식지 유형이 양식에 없는 행(D6 · 예전 세션 `양서류` 등 · 지우지 않는다).</summary>
    public override string? OutOfFormNotice()
    {
        var n = Entries.Count(e => e.HabitatOutOfForm.Length > 0);
        return n == 0 ? null : $"양서파충류 서식지 유형 {n}행(양식에 없는 값)";
    }

    /// <summary>대분류에 맞지 않는 중분류를 뺀다(D6).</summary>
    private static void TrimMiddle(AmphibianReptileEntry row)
    {
        var options = row.MiddleOptions;
        var tokens = FormChoices.Split(row.MiddleCategory);
        var kept = tokens.Where(t => options.Contains(t)).ToList();
        if (kept.Count != tokens.Count)
            row.MiddleCategory = kept.Count == 0 ? null : string.Join(FormChoices.AmphHabitatMiddleSeparator, kept);
    }
}
