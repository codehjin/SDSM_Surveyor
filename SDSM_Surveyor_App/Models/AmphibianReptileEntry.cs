using CommunityToolkit.Mvvm.ComponentModel;

namespace SDSM_Surveyor_App.Models;

/// <summary>양서파충류 관찰 1행. 관리자 <c>AmphibianReptile</c> 모델과 1:1(자동계산 없음).
/// 흔적 6종은 관리자와 동일하게 <b>체크(bool)가 아니라 개체수/횟수(int?)</b> 이다.</summary>
public partial class AmphibianReptileEntry : ObservableObject
{
    [ObservableProperty] private string? _speciesKo;        // 일반명(초성검색)
    [ObservableProperty] private string? _speciesEn;        // 학명

    [ObservableProperty] private string? _majorCategory;    // 대분류(양서류/파충류)
    [ObservableProperty] private string? _middleCategory;   // 중분류

    // 흔적 6종 (관리자 AmphibianReptile.Trace1~6). 미입력은 null 유지 — 0(실측 부재)과 구분한다.
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace1;   // 성체
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace2;   // 유생
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace3;   // 알
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace4;   // 울음소리
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace5;   // 로드킬
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace6;   // 기타

    // 기록별 좌표 : 조사개황(지점) 좌표와 별개로 개체를 관찰한 지점을 남긴다.
    [ObservableProperty] private double? _lat;              // 위도
    [ObservableProperty] private double? _lng;              // 경도

    [ObservableProperty] private string? _feature;          // 특징
    [ObservableProperty] private string? _note;             // 특이사항

    /// <summary>흔적 6종 개체수 합계(행 단위 총 개체수). 미입력(null)은 집계에서 0으로 캐스팅한다.</summary>
    public int TraceSum =>
        (Trace1 ?? 0) + (Trace2 ?? 0) + (Trace3 ?? 0) + (Trace4 ?? 0) + (Trace5 ?? 0) + (Trace6 ?? 0);
}
