using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Ecology;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>서식·수변환경 탭 : [별표5] 항목별 허용점수 드롭다운 + 좌/우안 평균 → 실시간 HRI.</summary>
public partial class HabitatWaterEdgeEntryViewModel : ObservableObject, ITransientService
{
    private const string TaxonKey = "HabitatWaterEdge";
    private readonly ILocalDraftStore _draftStore;

    public HabitatWaterEdgeEntryViewModel(ILocalDraftStore draftStore) => _draftStore = draftStore;

    // 공통 기본정보
    [ObservableProperty] private string _yearChsu = string.Empty;
    [ObservableProperty] private DateTime? _surveyDate = DateTime.Today;
    [ObservableProperty] private string? _river;
    [ObservableProperty] private string? _site;
    [ObservableProperty] private string? _surveyor;
    [ObservableProperty] private string? _weather;
    [ObservableProperty] private string? _surveyUnavailableReason;
    public string[] Weathers { get; } = { "맑음", "흐림", "비(눈)" };

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

    partial void OnS1Changed(HriOption? v) => RaiseResult();
    partial void OnB2LChanged(HriOption? v) => RaiseResult();
    partial void OnB2RChanged(HriOption? v) => RaiseResult();
    partial void OnS3Changed(HriOption? v) => RaiseResult();
    partial void OnS4Changed(HriOption? v) => RaiseResult();
    partial void OnB5LChanged(HriOption? v) => RaiseResult();
    partial void OnB5RChanged(HriOption? v) => RaiseResult();
    partial void OnB6LChanged(HriOption? v) => RaiseResult();
    partial void OnB6RChanged(HriOption? v) => RaiseResult();
    partial void OnS7Changed(HriOption? v) => RaiseResult();
    partial void OnS8Changed(HriOption? v) => RaiseResult();
    partial void OnB9LChanged(HriOption? v) => RaiseResult();
    partial void OnB9RChanged(HriOption? v) => RaiseResult();
    partial void OnB10LChanged(HriOption? v) => RaiseResult();
    partial void OnB10RChanged(HriOption? v) => RaiseResult();
    partial void OnSurveyUnavailableReasonChanged(string? v) => RaiseResult();

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
        // 항목1~10 순서(좌/우안 항목은 평균)
        var eff = new double?[]
        {
            S1?.Score, Avg(B2L, B2R), S3?.Score, S4?.Score, Avg(B5L, B5R),
            Avg(B6L, B6R), S7?.Score, S8?.Score, Avg(B9L, B9R), Avg(B10L, B10R)
        };
        return HabitatEvaluator.Evaluate(eff, SurveyUnavailableReason);
    }

    public string ScoreText => Compute().score?.ToString("N1") ?? "-";
    public string GradeText => Compute().grade ?? "-";

    private void RaiseResult()
    {
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(GradeText));
        ExportExcelCommand.NotifyCanExecuteChanged();
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
        await _draftStore.SaveDraftAsync(TaxonKey, new
        {
            YearChsu, SurveyDate, River, Site, Surveyor, Weather, SurveyUnavailableReason,
            S1 = S1?.Score, B2L = B2L?.Score, B2R = B2R?.Score, S3 = S3?.Score, S4 = S4?.Score,
            B5L = B5L?.Score, B5R = B5R?.Score, B6L = B6L?.Score, B6R = B6R?.Score,
            S7 = S7?.Score, S8 = S8?.Score, B9L = B9L?.Score, B9R = B9R?.Score,
            B10L = B10L?.Score, B10R = B10R?.Score
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
        new[] { S1, B2L, B2R, S3, S4, B5L, B5R, B6L, B6R, S7, S8, B9L, B9R, B10L, B10R }
            .Any(x => x is not null);
}

/// <summary>서식수변 평가항목 드롭다운 옵션(점수 + 설명).</summary>
public record HriOption(double Score, string Desc)
{
    public string Display => $"{Score:0} · {Desc}";
}
