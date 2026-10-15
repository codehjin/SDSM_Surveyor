using CommunityToolkit.Mvvm.ComponentModel;
using SDSM_Models;

namespace SDSM_Surveyor_App.Models;

/// <summary>저서동물 입력 그리드의 한 행. 종 선택 시 오탁치·지표가중치·보호종 정보 자동 상속.</summary>
public partial class BenthosSpeciesEntry : ObservableObject
{
    [ObservableProperty] private BenthosSpeciesList? _selectedSpecies;
    [ObservableProperty] private string? _speciesKo;
    [ObservableProperty] private double? _individualCount;   // 결측 null vs 0 구분
    [ObservableProperty] private string _errorContent = string.Empty;

    // 비고 : 조사자 메모. 관리자 모델에 대응 필드가 없어 **세션 저장 전용**(어류와 같은 규칙).
    [ObservableProperty] private string? _note;

    // 그리드 `#` 열 · 상대풍부도(읽기전용 파생) · 방금 추가(1.2초)
    [ObservableProperty] private int _rowNumber;
    [ObservableProperty] private double? _relativeAbundance;
    [ObservableProperty] private bool _isJustAdded;

    /// <summary>보호종 뱃지 문구. 색만으로 구분하지 않도록 등급을 글자로 적는다(design.md §11).</summary>
    public string ProtectionBadgeText =>
        !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered1) ? "멸종위기Ⅰ"
        : !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered2) ? "멸종위기Ⅱ"
        : string.Empty;

    /// <summary>한국고유종 여부(보호종과 다른 표기 — 중립 뱃지).</summary>
    public bool IsEndemic => !string.IsNullOrWhiteSpace(SelectedSpecies?.Endemic);

    // ── BMI 가 읽는 종별 형질값(종목록에서 자동 상속) ──────────────────
    // BMI = (4 − Σ(s·g·h)/Σ(g·h)) × 25 — 오탁치 s 와 지표가중치 g 가 그 입력이다.
    public string SaprobicText =>
        SelectedSpecies?.SaprobicValue is double v ? v.ToString("0.0") : "·";

    public string IndicatorWeightText =>
        SelectedSpecies?.IndicatorWeight is int g ? g.ToString() : "·";

    public string TraitTooltip => SelectedSpecies is null
        ? "종목록에서 종을 고르면 오탁치·지표가중치가 자동으로 채워집니다"
        : $"오탁치 s={SaprobicText} · 지표가중치 g={IndicatorWeightText} · 순위점수 h={RankScorer} → BMI 입력";

    // BMI 계산 시 VM이 채워 넣는 순위점수
    public int RankScorer { get; set; }

    partial void OnSelectedSpeciesChanged(BenthosSpeciesList? value)
    {
        if (value is null) return;
        SpeciesKo = value.SpeciesKo;   // 공식 목록의 원문 종명 사용(오타 차단)
        RaiseTags();
    }

    partial void OnSpeciesKoChanged(string? value) => RaiseTags();

    public bool IsProtected =>
           !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered1)
        || !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered2);

    // 입력 이상치 경고 여부

    private void RaiseTags()
    {
        OnPropertyChanged(nameof(IsProtected));
        OnPropertyChanged(nameof(IsEndemic));
        OnPropertyChanged(nameof(ProtectionBadgeText));
        OnPropertyChanged(nameof(SaprobicText));
        OnPropertyChanged(nameof(IndicatorWeightText));
        OnPropertyChanged(nameof(TraitTooltip));
    }

    public bool HasWarning => !string.IsNullOrEmpty(ErrorContent);
    partial void OnErrorContentChanged(string value) => OnPropertyChanged(nameof(HasWarning));

    /// <summary>계산·검증에 넘길 표준 모델로 변환(오탁치/지표가중치는 문자열로).</summary>
    public ImportBenthosSpecies ToImport() => new()
    {
        SpeciesKo       = SelectedSpecies?.SpeciesKo ?? SpeciesKo,
        SpeciesEn       = SelectedSpecies?.SpeciesEn,
        SaprobicValue   = SelectedSpecies?.SaprobicValue?.ToString(),
        IndicatorWeight = SelectedSpecies?.IndicatorWeight?.ToString(),
        Endangered1     = SelectedSpecies?.Endangered1,
        Endangered2     = SelectedSpecies?.Endangered2,
        Endemic         = SelectedSpecies?.Endemic,
        PhylumKo        = SelectedSpecies?.PhylumKo,
        ClassKo         = SelectedSpecies?.ClassKo,
        OrderKo         = SelectedSpecies?.OrderKo,
        FamilyKo        = SelectedSpecies?.FamilyKo,
        IndividualCount = IndividualCount,
        RankScorer      = RankScorer,
        ErrorContent    = ErrorContent
    };
}
