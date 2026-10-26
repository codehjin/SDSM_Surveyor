using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Data;
using SDSM_Core.Ecology;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels.Base;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>수질 탭: 측정값 폼 입력(관리자 WaterQuality 전 필드) + 항목별 등급 실시간 산정.
/// 등급 8종(pH/BOD/COD/TOC/SS/DO/T-P/대장균군)은 자동계산이므로 입력하지 않는다.</summary>
public partial class WaterQualityEntryViewModel : TaxonEntryViewModelBase, ISingletonService
{
    internal const string TaxonKey = "WaterQuality";

    /// <summary>양식에 조사개황 하천유형이 있다 — 내보내기 때 '양식에 없는 값'을 알린다(D7).</summary>
    protected override bool UsesRiverTypeWater => true;   // 하천유형 = 수질·서식수변 칸(E1)

    public WaterQualityEntryViewModel(ISessionService sessions, SurveyMeta meta)
        : base(sessions, meta) { }

    private static double? P(string? s) => double.TryParse(s, out var d) ? d : null;


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

    // ⚠ 추가 항목 14칸에도 알림을 건다 — 종전에는 추가항목만 입력한 회차에서
    //    내보내기 단추가 잠긴 채였다(CanExport 는 추가항목까지 보는데 알림이 없었다).
    partial void OnTNChanged(string? value) => RaiseExtras();
    partial void OnECChanged(string? value) => RaiseExtras();
    partial void OnClChanged(string? value) => RaiseExtras();
    partial void OnSO42Changed(string? value) => RaiseExtras();
    partial void OnCuChanged(string? value) => RaiseExtras();
    partial void OnZnChanged(string? value) => RaiseExtras();
    partial void OnCrChanged(string? value) => RaiseExtras();
    partial void OnTurbidityChanged(string? value) => RaiseExtras();
    partial void OnChlaChanged(string? value) => RaiseExtras();
    partial void OnWaterTemperatureChanged(string? value) => RaiseExtras();
    partial void OnWaterDepthChanged(string? value) => RaiseExtras();
    partial void OnFlowVelocityChanged(string? value) => RaiseExtras();
    partial void OnFlowSecChanged(string? value) => RaiseExtras();
    partial void OnFlowDayChanged(string? value) => RaiseExtras();

    partial void OnPHChanged(string? value) => RaiseGrades();
    partial void OnBodChanged(string? value) => RaiseGrades();
    partial void OnCodChanged(string? value) => RaiseGrades();
    partial void OnTocChanged(string? value) => RaiseGrades();
    partial void OnSsChanged(string? value) => RaiseGrades();
    partial void OnDoxChanged(string? value) => RaiseGrades();
    partial void OnTpChanged(string? value) => RaiseGrades();
    partial void OnEColiChanged(string? value) => RaiseGrades();
    partial void OnEcotoxicityChanged(string? value) => RaiseGrades();   // 등급은 없지만 내보내기 활성 여부에 영향

    // ── 숫자가 아닌 측정값(v0.5.1) ─────────────────────────────────────────
    // 관리자 WaterQuality 의 측정 칸은 모두 double? — 글자('<0.5' · 'ND' · '18.3Z' 등)가 들어가면 일괄등록이 그 기록을 '형변환실패'로 막는다.
    // 새로 치는 값은 DecimalInput 이 막고, 예전 세션 값은 지우지 않고 경고 · [일괄입력용] 막힘으로 알린다(보고서는 그대로 낸다).

    private (string Label, string? Value)[] NumberFields => new[]
    {
        ("pH", PH), ("BOD", Bod), ("COD", Cod), ("TOC", Toc), ("SS", Ss), ("DO", Dox), ("T-P", Tp), ("대장균군", EColi), ("생태독성", Ecotoxicity),
        ("T-N", TN), ("전기전도도", EC), ("염소이온", Cl), ("황이온", SO42), ("구리", Cu), ("아연", Zn), ("크롬", Cr), ("탁도", Turbidity), ("클로로필a", Chla),
        ("수온", WaterTemperature), ("수심", WaterDepth), ("유속", FlowVelocity), ("초당유량", FlowSec), ("일당유량", FlowDay),
    };

    /// <summary>숫자가 아닌 측정 칸 — `수질 BOD 「&lt;0.5」` 꼴.</summary>
    public IReadOnlyList<string> NumberInvalid => DecimalNumber.Invalid("수질", NumberFields);

    /// <summary>검출한계 미만을 적는 방법(§24-4) — 측정 칸은 비우고(그 항목 등급도 빈칸) 비고(특이사항)에 'ND' 또는 '&lt;값'.</summary>
    public const string NdGuide = "검출한계 미만은 칸을 비우고 비고(특이사항)에 ND 또는 <값을 적으세요";

    /// <summary>측정값 경고 줄(측정 항목 머리 · 접힌 요약줄) — 검출한계 미만 적는 방법을 함께.</summary>
    public string NumberWarning => NumberInvalid.Count == 0 ? string.Empty : $"{DecimalNumber.Warning(NumberInvalid)} · {NdGuide}";

    public bool HasNumberWarning => NumberInvalid.Count > 0;

    /// <summary>일괄입력용을 만들 수 없는 까닭(없으면 빈 문자열 · 어류 · 저서와 같은 방식) — 끝 줄에 검출한계 미만 적는 방법(§24-4).</summary>
    public string BulkBlockReason => NumberInvalid.Count == 0 ? string.Empty : $"{DecimalNumber.BlockReason(NumberInvalid)}\n{NdGuide}";

    // ── 비고(특이사항) — 검출한계 미만 등(§24-4 · 2026-10-27) ──
    // 일괄입력 양식 · 관리자 WaterQuality 에는 칸이 없다 → 세션 · 보고서 엑셀(조사개황 시트 [ 특이사항 ])에만 남는다.
    [ObservableProperty] private string? _note;

    private void RaiseNumberWarning()
    {
        OnPropertyChanged(nameof(NumberInvalid));
        OnPropertyChanged(nameof(NumberWarning));
        OnPropertyChanged(nameof(HasNumberWarning));
        OnPropertyChanged(nameof(BulkBlockReason));
    }

    private void RaiseGrades()
    {
        RaiseNumberWarning();
        OnPropertyChanged(nameof(PhGradeText));
        OnPropertyChanged(nameof(BodGradeText));
        OnPropertyChanged(nameof(CodGradeText));
        OnPropertyChanged(nameof(TocGradeText));
        OnPropertyChanged(nameof(SsGradeText));
        OnPropertyChanged(nameof(DoGradeText));
        OnPropertyChanged(nameof(TpGradeText));
        OnPropertyChanged(nameof(EColiGradeText));
        OnPropertyChanged(nameof(UnfilledGradeCount));
        OnPropertyChanged(nameof(UnfilledGradeText));
        OnPropertyChanged(nameof(HasUnfilledGrades));
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

    // ── 등급 요약 레일 (06_DESIGN_REBUILD §5-2-3) ─────────────────────────
    // 등급 8종 중 몇 개가 비었는지는 조사자에게 중요하다 — 종전에는 화면 어디에도 없었다.
    // ⚠ 미입력(`-`)과 실측값이 있는 등급을 구분해서 센다. 0 을 적은 것은 미입력이 아니다.

    /// <summary>등급 8종 중 아직 값이 없는 항목 수.</summary>
    public int UnfilledGradeCount => new[]
    {
        PhGradeText, BodGradeText, CodGradeText, TocGradeText,
        SsGradeText, DoGradeText, TpGradeText, EColiGradeText,
    }.Count(g => g == "-");

    /// <summary>요약줄·상태바 안내 문구.</summary>
    public string UnfilledGradeText =>
        UnfilledGradeCount == 0 ? "등급 8종 모두 산정됨" : $"미입력 {UnfilledGradeCount}항목";

    /// <summary>등급 8종 중 빈 항목이 있는가(요약줄 경고 아이콘).</summary>
    public bool HasUnfilledGrades => UnfilledGradeCount > 0;

    /// <summary>펼친 카드 머리글 옆 캡션 — 계산 출처를 밝힌다.</summary>
    public string GradeCaption =>
        "관리자 SDSM_Core 와 동일 계산 · 하천 생활환경기준(1a~6) · 생태독성(TU)은 등급 산정 대상이 아닙니다";


    // ── 접힘 상태 (세션 저장 · 기본 접힘) ────────────────────────────────
    // ⚠ 접힘은 숨기기가 아니다 — 접힌 36px 줄이 등급 8종을 계속 보여 준다.
    [ObservableProperty] private bool _isGradeExpanded;
    [ObservableProperty] private bool _isExtraExpanded;


    // ── 추가 항목 요약 (등급 산정 대상이 아닌 확장 측정값 14칸) ──────────
    private string?[] ExtraValues => new[]
    {
        TN, EC, Cl, SO42, Cu, Zn, Cr, Turbidity, Chla,
        WaterTemperature, WaterDepth, FlowVelocity, FlowSec, FlowDay
    };

    /// <summary>추가 항목 14칸 중 값이 들어간 칸 수. **0 을 적은 것은 미입력이 아니다.**</summary>
    public int ExtraFilledCount => ExtraValues.Count(v => !string.IsNullOrWhiteSpace(v));

    public string ExtraSummaryText => $"14칸 중 {ExtraFilledCount}칸 입력";

    /// <summary>접힌 줄에 값을 그대로 보이는 현장 측정 3종(측정하면 늘 적는 값).</summary>
    public string FieldMeasureSummary
    {
        get
        {
            static string V(string? s) => string.IsNullOrWhiteSpace(s) ? "·" : s!;
            return $"수온 {V(WaterTemperature)} · 수심 {V(WaterDepth)} · 유속 {V(FlowVelocity)}";
        }
    }

    private void RaiseExtras()
    {
        RaiseNumberWarning();
        OnPropertyChanged(nameof(ExtraFilledCount));
        OnPropertyChanged(nameof(ExtraSummaryText));
        OnPropertyChanged(nameof(FieldMeasureSummary));
        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
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
                NotifyExported("보고서용 엑셀을 내보냈습니다.");
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    /// <summary>[레거시] 관리자 일괄입력 양식으로 내보내기(현행 관리자 Import 취합용).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportBulk()
    {
        // 관리자 일괄등록이 막을 값(측정 칸에 숫자가 아닌 글자)이면 만들지 않고 칸 · 값을 알린다(v0.5.1)
        if (BulkBlockReason is { Length: > 0 } block)
        {
            StatusText = "일괄입력용을 만들지 않았습니다 — " + block.Replace("\n", " · ");
            WeakReferenceMessenger.Default.Send(new NotifyMessage(($"일괄입력용을 만들지 않았습니다.\n{block}", false)));
            return;
        }
        try
        {
            var saved = Export.WaterQualityExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"일괄입력용 완료 · {System.IO.Path.GetFileName(saved)}";
                NotifyExported("일괄입력용 엑셀을 내보냈습니다.");
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
