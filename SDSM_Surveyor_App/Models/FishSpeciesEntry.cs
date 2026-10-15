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

    // 비고 : 조사자 메모.
    // ⚠ 관리자 `ImportFishSpecies` 에 대응 필드가 없어 **세션 저장 전용**이다(내보내기에 실리지 않는다).
    [ObservableProperty] private string? _note;

    // 그리드 `#` 열. 화면 표시용이며 VM 의 Recalculate 가 다시 매긴다.
    [ObservableProperty] private int _rowNumber;

    // 상대풍부도(%) — 읽기전용 파생값. 총 개체수가 0이면 null(미산출)이다.
    [ObservableProperty] private double? _relativeAbundance;

    // 방금 추가된 행(1.2초). 배경 강조 + 비고 칸의 「방금 추가」 표시에 쓴다.
    [ObservableProperty] private bool _isJustAdded;

    partial void OnSelectedSpeciesChanged(FishSpeciesList? value)
    {
        if (value is null) return;
        SpeciesKo = value.SpeciesKo;             // 공식 목록의 원문 종명 사용(오타 차단)
        RaiseTags();
    }

    partial void OnSpeciesKoChanged(string? value) => RaiseTags();

    private void RaiseTags()
    {
        OnPropertyChanged(nameof(IsProtected));
        OnPropertyChanged(nameof(IsInvasive));
        OnPropertyChanged(nameof(IsExotic));
        OnPropertyChanged(nameof(ShowExoticBadge));
        OnPropertyChanged(nameof(ProtectionBadgeText));
        OnPropertyChanged(nameof(ToleranceText));
        OnPropertyChanged(nameof(FeedingText));
        OnPropertyChanged(nameof(HabitatText));
        OnPropertyChanged(nameof(GuildTooltip));
    }

    // 법정보호종/천연기념물 여부 → UI 강조용
    public bool IsProtected =>
           !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered1)
        || !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered2)
        || !string.IsNullOrWhiteSpace(SelectedSpecies?.NaturalMonument);

    /// <summary>
    /// 생태계교란생물 여부.
    /// ⚠ 외래종(<see cref="IsExotic"/>)과 **다르다** — 떡붕어·이스라엘잉어는 외래종이지만 교란종이 아니다.
    /// 어류 종목록에는 교란종 필드가 없어 <see cref="InvasiveFish"/> 가 고시 목록으로 판정한다.
    /// </summary>
    public bool IsInvasive => InvasiveFish.IsInvasive(SelectedSpecies?.SpeciesKo ?? SpeciesKo);

    /// <summary>외래종 여부. FAI M1(국내종 종수)·M7(국내종 개체수)·M6(충식종 비율)이 이 값을 쓴다.</summary>
    public bool IsExotic => SelectedSpecies?.Exotic == "O";

    /// <summary>
    /// 외래종 뱃지를 띄울지. 교란종이면 그쪽 뱃지만 띄운다 — 뱃지 두 개가 겹치면 읽히지 않는다.
    /// 교란종 뱃지는 위험색, 외래종 뱃지는 중립 회색이다(보호종 강조색과도 갈린다).
    /// </summary>
    public bool ShowExoticBadge => IsExotic && !IsInvasive;

    /// <summary>보호종 뱃지 문구. 색만으로 구분하지 않도록 등급을 글자로 적는다(design.md §11).</summary>
    public string ProtectionBadgeText =>
        !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered1) ? "멸종위기Ⅰ"
        : !string.IsNullOrWhiteSpace(SelectedSpecies?.Endangered2) ? "멸종위기Ⅱ"
        : !string.IsNullOrWhiteSpace(SelectedSpecies?.NaturalMonument) ? "천연기념물"
        : string.Empty;

    // ── 자동 상속되는 형질값(종목록에서 따라온다 · FAI 근거) ──────────────
    public string ToleranceText => FishGuildText.Tolerance(SelectedSpecies?.ToleranceGuild);
    public string FeedingText   => FishGuildText.Feeding(SelectedSpecies?.FeedingGuild);
    public string HabitatText   => FishGuildText.Habitat(SelectedSpecies?.HabitatGuild);

    /// <summary>길드 열에 붙일 근거 설명(어느 지표에 쓰이는지).</summary>
    public string GuildTooltip => SelectedSpecies is null
        ? "종목록에서 종을 고르면 길드가 자동으로 채워집니다"
        : $"내성도 {ToleranceText}(M3·M4) · 섭식 {FeedingText}(M5·M6) · 서식지 {HabitatText}(M2)";

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
