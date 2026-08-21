using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Ecology;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>수질 탭: 측정값 폼 입력(관리자 WaterQuality 전 필드) + 항목별 등급 실시간 산정.
/// 등급 8종(pH/BOD/COD/TOC/SS/DO/T-P/대장균군)은 자동계산이므로 입력하지 않는다.</summary>
public partial class WaterQualityEntryViewModel : ObservableObject, ISingletonService
{
    internal const string TaxonKey = "WaterQuality";
    private readonly ISessionService _sessions;

    public WaterQualityEntryViewModel(ISessionService sessions, SurveyMeta meta)
    {
        _sessions = sessions;
        Meta = meta;
    }

    private static double? P(string? s) => double.TryParse(s, out var d) ? d : null;

    // ── 공통 조사개황 : 모든 분류군 공유(SurveyOverviewControl에서 입력) ──
    public SurveyMeta Meta { get; }

    // ── 측정 항목(등급 산정 대상) : 빈 문자열=미측정, 관리자와 동일하게 문자열로 다룸 ──
    [ObservableProperty] private string? _pH;
    [ObservableProperty] private string? _bod;
    [ObservableProperty] private string? _cod;
    [ObservableProperty] private string? _toc;
    [ObservableProperty] private string? _ss;
    [ObservableProperty] private string? _dox;          // DO
    [ObservableProperty] private string? _tp;
    [ObservableProperty] private string? _eColi;
    [ObservableProperty] private string? _ecotoxicity;  // 생태독성(등급 없음)

    // ── 추가 항목(등급 없음 · 관리자 WaterQuality 확장 컬럼) ──
    // ⚠ 관리자 일괄입력 엑셀에서 이 항목들은 고정 행이 아니라 C열 라벨 텍스트로 찾는다(90_TECH_NOTES §4).
    // ※ 생성되는 공개 속성명이 관리자 모델(TN·EC·SO42)과 같도록 필드명 첫 글자만 소문자로 둔다.
    [ObservableProperty] private string? _tN;                // T-N (mg/L)
    [ObservableProperty] private string? _eC;                // 전기전도도
    [ObservableProperty] private string? _cl;                // 염소이온 (mg/L)
    [ObservableProperty] private string? _sO42;              // 황이온 (mg/L)
    [ObservableProperty] private string? _cu;                // 구리 (mg/L)
    [ObservableProperty] private string? _zn;                // 아연 (mg/L)
    [ObservableProperty] private string? _cr;                // 크롬 (mg/L)
    [ObservableProperty] private string? _turbidity;         // 탁도 (NTU)
    [ObservableProperty] private string? _chla;              // 클로로필a (mg/m3)
    [ObservableProperty] private string? _waterTemperature;  // 수온
    [ObservableProperty] private string? _waterDepth;        // 수심 (cm)
    [ObservableProperty] private string? _flowVelocity;      // 유속 (cm/s)
    [ObservableProperty] private string? _flowSec;           // 초당유량 (m3/sec)
    [ObservableProperty] private string? _flowDay;           // 일당유량 (m3/day)

    partial void OnPHChanged(string? value) => RaiseGrades();
    partial void OnBodChanged(string? value) => RaiseGrades();
    partial void OnCodChanged(string? value) => RaiseGrades();
    partial void OnTocChanged(string? value) => RaiseGrades();
    partial void OnSsChanged(string? value) => RaiseGrades();
    partial void OnDoxChanged(string? value) => RaiseGrades();
    partial void OnTpChanged(string? value) => RaiseGrades();
    partial void OnEColiChanged(string? value) => RaiseGrades();
    partial void OnEcotoxicityChanged(string? value) => RaiseGrades();   // 등급은 없지만 내보내기 활성 여부에 영향

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
        ExportBulkCommand.NotifyCanExecuteChanged();
    }

    // 실시간 등급(표시용) — 자동계산이므로 입력 필드가 아니다.
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
        // 분류군 하나가 아니라 세션(조사개황 + 7개 분류군) 전체를 저장한다.
        // 어느 탭에서 눌러도 같은 세션 파일이 갱신되므로 지점을 옮겨도 이전 자료가 사라지지 않는다.
        var idx = await _sessions.SaveCurrentAsync();

        LastSavedTime = DateTime.Now;
        StatusText = $"자료함 저장됨 · {idx.Site} {idx.YearChsu} · {LastSavedTime:HH:mm:ss}";
        WeakReferenceMessenger.Default.Send(new NotifyMessage(("자료함에 저장되었습니다.", true)));
    }

    /// <summary>보고서·기록용 엑셀 내보내기(주력).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportExcel()
    {
        try
        {
            var saved = Export.WaterQualityReportExporter.Export(this);
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
            var saved = Export.WaterQualityExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"일괄입력용 완료 · {System.IO.Path.GetFileName(saved)}";
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("일괄입력용 엑셀을 내보냈습니다.", true)));
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    // 측정값이 하나라도 있으면 내보내기 가능(추가항목만 측정한 회차도 있다).
    private bool CanExport() =>
        new[]
        {
            PH, Bod, Cod, Toc, Ss, Dox, Tp, EColi, Ecotoxicity,
            TN, EC, Cl, SO42, Cu, Zn, Cr, Turbidity, Chla,
            WaterTemperature, WaterDepth, FlowVelocity, FlowSec, FlowDay
        }.Any(s => !string.IsNullOrWhiteSpace(s));
}
