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
}
