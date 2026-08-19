using CommunityToolkit.Mvvm.ComponentModel;
using SDSM_Models;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 초고속 입력 그리드의 한 행. 종명 선택 시 공식 종목록에서 길드·보호종 정보를 자동 상속한다.
/// </summary>
public partial class FishSpeciesEntry : ObservableObject
{
    // 초성 자동완성으로 선택된 공식 종목록 항목
    [ObservableProperty] private FishSpeciesList? _selectedSpecies;

    // 국명(표시/입력) — 종명은 원문 그대로(Trim 외 정규화 금지, CLAUDE.md §7.3)
    [ObservableProperty] private string? _speciesKo;

    // 개체수 : 결측치(null, 미기입)와 0(실측 부재)을 엄격히 구분 (CLAUDE.md §7.2)
    [ObservableProperty] private int? _individualCount;

    // 논블로킹 검증 결과(비어 있으면 정상). 그리드에서 붉은 툴팁으로 표시.
    [ObservableProperty] private string _errorContent = string.Empty;

    partial void OnSelectedSpeciesChanged(FishSpeciesList? value)
    {
        if (value is null) return;
        SpeciesKo = value.SpeciesKo;             // 공식 목록의 원문 종명 사용(오타 차단)
        OnPropertyChanged(nameof(IsProtected));  // 보호종 강조 갱신
    }

    // 법정보호종/천연기념물 여부 → UI 강조용
    public bool IsProtected =>
           !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered1)
        || !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered2)
        || !string.IsNullOrWhiteSpace(SelectedSpecies?.NaturalMonument);

    // 입력 이상치 경고 여부(ErrorContent 비어있지 않으면 경고)
    public bool HasWarning => !string.IsNullOrEmpty(ErrorContent);
    partial void OnErrorContentChanged(string value) => OnPropertyChanged(nameof(HasWarning));

    /// <summary>계산·검증·엑셀 내보내기에 넘길 표준 모델로 변환.</summary>
    public ImportFishSpecies ToImport() => new()
    {
        SpeciesKo       = SelectedSpecies?.SpeciesKo ?? SpeciesKo,
        SpeciesEn       = SelectedSpecies?.SpeciesEn,
        Exotic          = SelectedSpecies?.Exotic,
        Endemic         = SelectedSpecies?.Endemic,
        Endangered1     = SelectedSpecies?.Endangered1,
        Endangered2     = SelectedSpecies?.Endangered2,
        NaturalMonument = SelectedSpecies?.NaturalMonument,
        ToleranceGuild  = SelectedSpecies?.ToleranceGuild,
        FeedingGuild    = SelectedSpecies?.FeedingGuild,
        HabitatGuild    = SelectedSpecies?.HabitatGuild,
        ClassKo         = SelectedSpecies?.ClassKo,
        OrderKo         = SelectedSpecies?.OrderKo,
        FamilyKo        = SelectedSpecies?.FamilyKo,
        IndividualCount = IndividualCount,       // null 그대로 전달(0으로 치환 금지)
        ErrorContent    = ErrorContent
    };
}
