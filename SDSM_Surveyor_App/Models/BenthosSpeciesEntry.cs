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

    // BMI 계산 시 VM이 채워 넣는 순위점수
    public int RankScorer { get; set; }

    partial void OnSelectedSpeciesChanged(BenthosSpeciesList? value)
    {
        if (value is null) return;
        SpeciesKo = value.SpeciesKo;
        OnPropertyChanged(nameof(IsProtected));
    }

    public bool IsProtected =>
           !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered1)
        || !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered2);

    // 입력 이상치 경고 여부
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
