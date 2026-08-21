using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Data;
using SDSM_Core.Ecology;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;
using SDSM_Surveyor_App.Models;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>서식·수변환경 탭 : [별표5] 항목별 허용점수 드롭다운 + 좌/우안 평균 → 실시간 HRI.</summary>
public partial class HabitatWaterEdgeEntryViewModel : ObservableObject, ISingletonService
{
    internal const string TaxonKey = "HabitatWaterEdge";
    private readonly ISessionService _sessions;

    public HabitatWaterEdgeEntryViewModel(ISessionService sessions, SurveyMeta meta)
    {
        _sessions = sessions;
        Meta = meta;
    }

    // ── 공통 조사개황 : 모든 분류군 공유(SurveyOverviewControl에서 입력) ──
    public SurveyMeta Meta { get; }

    // 서식수변 고유 입력(등급 계산에 영향하므로 Meta가 아닌 이곳에 둔다)
    [ObservableProperty] private string? _surveyUnavailableReason;   // 조사불가시
    [ObservableProperty] private string? _note;                      // 비고(특이사항)

    // ── 평가항목 : 선택된 옵션(HriOption) 자체를 바인딩(SelectedItem) ──
    [ObservableProperty] private HriOption? _s1;    // 1 자연적 종횡사주
    [ObservableProperty] private HriOption? _b2L;   // 2 하도 자연성(좌)
    [ObservableProperty] private HriOption? _b2R;   // 2 하도 자연성(우)
    [ObservableProperty] private HriOption? _s3;    // 3 유속 다양성
    [ObservableProperty] private HriOption? _s4;    // 4 하천변 폭
    [ObservableProperty] private HriOption? _b5L;   // 5 저수로 하안공(좌)
    [ObservableProperty] private HriOption? _b5R;   // 5 저수로 하안공(우)
    [ObservableProperty] private HriOption? _b6L;   // 6 제방하안 재료(좌)
    [ObservableProperty] private HriOption? _b6R;   // 6 제방하안 재료(우)
    [ObservableProperty] private HriOption? _s7;    // 7 저질 상태
    [ObservableProperty] private HriOption? _s8;    // 8 횡구조물
    [ObservableProperty] private HriOption? _b9L;   // 9 제외지 토지이용(좌)
    [ObservableProperty] private HriOption? _b9R;   // 9 제외지 토지이용(우)
    [ObservableProperty] private HriOption? _b10L;  // 10 제내지 토지이용(좌)
    [ObservableProperty] private HriOption? _b10R;  // 10 제내지 토지이용(우)

    partial void OnS1Changed(HriOption? value) => RaiseResult();
    partial void OnB2LChanged(HriOption? value) => RaiseResult();
    partial void OnB2RChanged(HriOption? value) => RaiseResult();
    partial void OnS3Changed(HriOption? value) => RaiseResult();
    partial void OnS4Changed(HriOption? value) => RaiseResult();
    partial void OnB5LChanged(HriOption? value) => RaiseResult();
    partial void OnB5RChanged(HriOption? value) => RaiseResult();
    partial void OnB6LChanged(HriOption? value) => RaiseResult();
    partial void OnB6RChanged(HriOption? value) => RaiseResult();
    partial void OnS7Changed(HriOption? value) => RaiseResult();
    partial void OnS8Changed(HriOption? value) => RaiseResult();
    partial void OnB9LChanged(HriOption? value) => RaiseResult();
    partial void OnB9RChanged(HriOption? value) => RaiseResult();
    partial void OnB10LChanged(HriOption? value) => RaiseResult();
    partial void OnB10RChanged(HriOption? value) => RaiseResult();
    partial void OnSurveyUnavailableReasonChanged(string? value) => RaiseResult();

    // 좌/우안 산술 평균
    private static double? Avg(HriOption? l, HriOption? r)
    {
        double? lv = l?.Score, rv = r?.Score;
        if (lv is null && rv is null) return null;
        if (lv is null) return rv;
        if (rv is null) return lv;
        return (lv + rv) / 2;
    }

    private (double? score, string? grade) Compute()
    {
        var d = ComputeDetail();
        return (d.Score, d.Grade);
    }

    /// <summary>평가항목 1~10 점수·합계·평가점수·등급(보고서 엑셀에서 계산과정 수록에 사용).</summary>
    public HriResult ComputeDetail()
    {
        // 항목1~10 순서(좌/우안 항목은 평균)
        var eff = new double?[]
        {
            S1?.Score, Avg(B2L, B2R), S3?.Score, S4?.Score, Avg(B5L, B5R),
            Avg(B6L, B6R), S7?.Score, S8?.Score, Avg(B9L, B9R), Avg(B10L, B10R)
        };
        return HabitatEvaluator.EvaluateDetail(eff, SurveyUnavailableReason);
    }

    /// <summary>평가항목 1~10 이름(보고서 엑셀 헤더용). 관리자 HabitatWaterEdge 모델 순서와 동일.</summary>
    public static readonly string[] ItemNames =
    {
        "자연적 종횡사주", "하도 자연성", "유속 다양성", "하천변 폭", "저수로 하안공",
        "제방하안 재료", "저질 상태", "횡구조물", "제외지 토지이용", "제내지 토지이용"
    };

    public string ScoreText => Compute().score?.ToString("N1") ?? "-";
    public string GradeText => Compute().grade ?? "-";

    private void RaiseResult()
    {
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(GradeText));
        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportBulkCommand.NotifyCanExecuteChanged();
    }

    // ── [별표5] 항목별 허용점수 옵션 (드롭다운 소스) ──
    public HriOption[] Opt1 { get; } = { new(10,"4회 이상"), new(5,"3회"), new(3,"2회"), new(1,"1회"), new(0,"없음") };
    public HriOption[] Opt2 { get; } = { new(25,"정비X 자연사행"), new(15,"정비, 사행유지"), new(10,"직강화·저수로사행"), new(5,"직강화(폭변화유지)"), new(0,"인공 직강화") };
    public HriOption[] Opt3 { get; } = { new(30,"유속변화 5~6회↑"), new(25,"3~4회"), new(15,"1~2회"), new(5,"거의 없음"), new(0,"건천화") };
    public HriOption[] Opt4 { get; } = { new(10,"비율 > 2.0"), new(5,"1.5~2.0"), new(3,"1.0~1.5"), new(1,"0.5~1.0"), new(0,"≤ 0.5") };
    public HriOption[] Opt5 { get; } = { new(25,"자연상태"), new(10,"자연소재+인공식생"), new(5,"사석/석축+인공식생"), new(3,"사석/석축(투수)"), new(0,"콘크리트(불투수)") };
    public HriOption[] Opt6 { get; } = { new(20,"인공제방 없음"), new(10,"인공 흙제방"), new(5,"사석+인공식생"), new(3,"사석(투수)"), new(0,"하안블록/콘크리트") };
    public HriOption[] Opt7 { get; } = { new(30,"거암/둥근돌"), new(20,"자갈-호박돌"), new(10,"잔자갈-모래"), new(3,"실트/진흙"), new(0,"콘크리트") };
    public HriOption[] Opt8 { get; } = { new(30,"없음"), new(20,"완경사수로"), new(10,"어도 길고완만"), new(3,"어도 급낙차"), new(0,"어도없음/파손") };
    public HriOption[] Opt9 { get; } = { new(10,"자연식생"), new(5,"자연+인공식생"), new(3,"경작지"), new(1,"공원/운동장"), new(0,"주차장/불투수") };
    public HriOption[] Opt10 { get; } = { new(10,"초지/관목"), new(5,"인공+자연녹지"), new(3,"경작지/공원"), new(1,"일부 시가지"), new(0,"1/2↑ 시가지") };

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
            var saved = Export.HabitatWaterEdgeReportExporter.Export(this);
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
            var saved = Export.HabitatWaterEdgeExcelExporter.Export(this);
            if (saved is not null)
            {
                StatusText = $"일괄입력용 완료 · {System.IO.Path.GetFileName(saved)}";
                WeakReferenceMessenger.Default.Send(new NotifyMessage(("일괄입력용 엑셀을 내보냈습니다.", true)));
            }
        }
        catch (Exception ex) { StatusText = $"엑셀 내보내기 실패: {ex.Message}"; }
    }

    private bool CanExport() =>
        new[] { S1, B2L, B2R, S3, S4, B5L, B5R, B6L, B6R, S7, S8, B9L, B9R, B10L, B10R }
            .Any(x => x is not null);
}

/// <summary>서식수변 평가항목 드롭다운 옵션(점수 + 설명).</summary>
public record HriOption(double Score, string Desc)
{
    public string Display => $"{Score:0} · {Desc}";
}
