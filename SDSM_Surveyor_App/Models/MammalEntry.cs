using CommunityToolkit.Mvvm.ComponentModel;

namespace SDSM_Surveyor_App.Models;

/// <summary>포유류 관찰 1행. 관리자 <c>Mammal</c> 모델과 1:1(자동계산 없음).
/// 흔적 12종은 관리자와 동일하게 <b>체크(bool)가 아니라 개체수/횟수(int?)</b> 이다.</summary>
public partial class MammalEntry : ObservableObject
{
    [ObservableProperty] private string? _speciesKo;         // 일반명(초성검색)
    [ObservableProperty] private string? _speciesEn;         // 학명
    [ObservableProperty] private string? _observationSite;   // 관찰지 유형

    // 흔적 12종 (관리자 Mammal.Trace1~12). 미입력은 null 유지 — 0(실측 부재)과 구분한다.
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace1;   // 포획
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace2;   // 관찰
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace3;   // 울음
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace4;   // 사체
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace5;   // 족적
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace6;   // 털
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace7;   // 식흔
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace8;   // 굴
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace9;   // 번식지
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace10;  // 배설물
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace11;  // 카메라
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TraceSum))] private int? _trace12;  // 기타(탐문 등)

    // 기록별 좌표 : 조사개황(지점) 좌표와 별개로 개체를 관찰한 지점을 남긴다.
    [ObservableProperty] private double? _lat;               // 위도
    [ObservableProperty] private double? _lng;               // 경도

    [ObservableProperty] private string? _feature;           // 특징
    [ObservableProperty] private string? _note;              // 특이사항

    /// <summary>흔적 12종 개체수 합계(행 단위 총 개체수). 미입력(null)은 집계에서 0으로 캐스팅한다.</summary>
    public int TraceSum =>
        (Trace1 ?? 0) + (Trace2 ?? 0) + (Trace3 ?? 0) + (Trace4 ?? 0) + (Trace5 ?? 0) + (Trace6 ?? 0) +
        (Trace7 ?? 0) + (Trace8 ?? 0) + (Trace9 ?? 0) + (Trace10 ?? 0) + (Trace11 ?? 0) + (Trace12 ?? 0);
}
