using CommunityToolkit.Mvvm.Input;

namespace SDSM_Surveyor_App.ViewModels.Base;

/// <summary>
/// 빠른 추가 바가 포커스를 옮기기 위해 구독하는 알림.
///
/// <see cref="SpeciesEntryViewModelBase{TEntry, TSpecies}"/> 가 제네릭이라
/// <c>QuickAddBar</c> 컨트롤이 타입을 특정할 수 없다. 이 비제네릭 인터페이스로 구독한다.
///
/// 종전에는 5개 화면의 코드비하인드가 같은 구독 코드를 각자 들고 있었다
/// (06_DESIGN_REBUILD §5-3 — QuickAddBar 로 모은다).
/// </summary>
public interface IQuickAddHost
{
    /// <summary>행 추가가 끝났다 → 검색창으로 포커스 복귀(연속 입력).</summary>
    event EventHandler? QuickAddCompleted;

    /// <summary>종을 골랐다 → 수량 칸으로 포커스 이동.</summary>
    event EventHandler? QuickSpeciesPicked;

    /// <summary>수량 칸에서 Enter 를 눌렀을 때 실행할 행 추가 명령(`[RelayCommand]` 생성 속성).</summary>
    IRelayCommand AddQuickCommand { get; }

    /// <summary>
    /// 엑셀에서 복사한 여러 줄을 행으로 붙여넣는다(<c>ObservationGrid</c> 의 Ctrl+V 가 호출).
    /// RadGridView 기본 붙여넣기는 커스텀 편집기 컬럼과 충돌해 한 칸에 뭉친다(90_TECH_NOTES §2).
    /// </summary>
    void PasteRows(string clipboard);

    /// <summary>
    /// 종목록 팝업에서 고른 종을 야장에 넣는다(개체수는 비운다).
    /// 이미 있는 종이면 새 행을 만들지 않고 그 행을 '방금 추가' 로 표시한다.
    ///
    /// 종 타입이 분류군마다 달라(<c>FishSpeciesList</c>·<c>BenthosSpeciesList</c>·<c>ObservedSpecies</c>)
    /// 비제네릭 <c>object</c> 로 받는다 — 받는 쪽에서 타입을 확인한다.
    /// </summary>
    void AddFromCatalog(object species);

    /// <summary>
    /// 마지막으로 누른 행. 그리드가 클릭·우클릭 때 넣어 준다.
    /// 삭제 버튼과 우클릭 메뉴가 이 값을 지운다.
    /// </summary>
    object? LastTouchedRow { get; set; }

    /// <summary><see cref="LastTouchedRow"/> 를 지우는 명령(빠른추가 바 버튼·우클릭 메뉴가 쓴다).</summary>
    IRelayCommand DeleteRowCommand { get; }

    /// <summary>
    /// 그리드에서 국명 편집이 확정됐다(셀 편집 종료 · 맨 아래 새 행 확정 때 그리드 코드비하인드가 부른다).
    /// 같은 종이 이미 있으면 분류군 규칙대로 합치거나 알린다(B-1).
    /// </summary>
    void OnSpeciesCommitted(object row, bool isNewRow);

    /// <summary>
    /// 그리드 국명 편집기에 친 글자를 행 국명으로 넣는다(목록 밖 국명 보존 · B-4).
    /// 편집 확정 때 <see cref="OnSpeciesCommitted"/> 보다 먼저 부른다 — 합산·경고가 이 국명을 키로 쓴다.
    /// </summary>
    void ApplyTypedSpecies(object row, string? typed);

    /// <summary>
    /// 행을 지우기 직전에 그리드 편집을 끝내는 훅(그리드 코드비하인드가 채운다).
    /// 셀 편집이 열린 채로 원본 컬렉션을 건드리면 Telerik 이 예외를 던진다 —
    /// 붙여넣기 경로가 <c>CancelEdit()</c> 를 부르는 것과 같은 이유다.
    /// </summary>
    Action? EndGridEdit { get; set; }
}
