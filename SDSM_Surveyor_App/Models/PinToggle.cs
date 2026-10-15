using CommunityToolkit.Mvvm.ComponentModel;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 조사개황 한 칸의 압정(값 고정) 상태. <see cref="SurveyPins"/> 가 6개를 들고 있다.
///
/// 화면은 이 객체만 본다 — 라벨 옆 토글의 <c>IsChecked</c> 와 입력칸 배경색이
/// <see cref="IsOn"/> 하나에 묶인다.
/// </summary>
public partial class PinToggle : ObservableObject
{
    private readonly Action<PinToggle>? _changed;

    /// <param name="key">SurveyMeta 프로퍼티 이름과 같은 키(Project · YearChsu · …).</param>
    /// <param name="label">사람이 읽는 칸 이름(대분류 · 연도차수 · …).</param>
    /// <param name="isOn">초기 상태. 설정에서 읽은 값을 그대로 넣는다.</param>
    /// <param name="changed">토글이 바뀌면 부를 콜백(설정 저장·값 갈무리).</param>
    public PinToggle(string key, string label, bool isOn, Action<PinToggle>? changed = null)
    {
        Key = key;
        Label = label;
        _isOn = isOn;      // 세터를 타지 않는다 — 생성 중에 설정을 되쓰면 안 된다
        _changed = changed;
    }

    public string Key { get; }
    public string Label { get; }

    /// <summary>고정 켜짐 여부.</summary>
    [ObservableProperty] private bool _isOn;

    /// <summary>고정된 값의 표시 문자열(안내 문구용). 꺼져 있으면 null.</summary>
    [ObservableProperty] private string? _valueText;

    /// <summary>고정된 값이 실제로 있는지(빈 값을 고정해 두면 안내가 이상해진다).</summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(ValueText);

    /// <summary>도움말 — `대분류 값 고정`.</summary>
    public string ToolTipText => IsOn
        ? $"{Label} 고정됨 · 새 야장에 이 값이 들어갑니다. 누르면 해제됩니다."
        : $"{Label} 값을 고정합니다. 앱을 다시 켜도 새 야장에 자동으로 들어갑니다.";

    partial void OnIsOnChanged(bool value)
    {
        OnPropertyChanged(nameof(ToolTipText));
        _changed?.Invoke(this);
    }

    partial void OnValueTextChanged(string? value) => OnPropertyChanged(nameof(HasValue));
}
