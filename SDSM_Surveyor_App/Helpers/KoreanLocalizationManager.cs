using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// Telerik 기본 영문 UI 문구를 한글로 치환한다.
/// (그리드 새 행 안내, 그룹 패널, 필터 메뉴 등)
/// ※ 알 수 없는 키는 base 로 넘겨 원문을 그대로 사용하므로, 키가 틀려도 예외가 나지 않는다.
/// </summary>
public sealed class KoreanLocalizationManager : LocalizationManager
{
    public override string GetStringOverride(string key) => key switch
    {
        // RadGridView
        "GridViewGroupPanelText"      => "열 머리글을 여기로 끌어오면 해당 열로 묶어 볼 수 있습니다",
        "GridViewAddNewRowString"     => "여기를 클릭하면 새 행이 추가됩니다",
        "GridViewNoResultsText"       => "표시할 자료가 없습니다",
        "GridViewFilterShowRowsWithValueThat" => "다음 조건의 행 표시",
        "GridViewFilterSelectAll"     => "전체 선택",
        "GridViewFilterIsCaseSensitive" => "대소문자 구분",
        "GridViewFilterMatchCase"     => "대소문자 구분",
        "GridViewFilterAnd"           => "그리고",
        "GridViewFilterOr"            => "또는",
        "GridViewFilterButton"        => "필터",
        "GridViewClearFilter"         => "필터 해제",
        "GridViewGroupByThisColumn"   => "이 열로 묶기",
        "GridViewUngroup"             => "묶기 해제",
        "GridViewSortAscending"       => "오름차순 정렬",
        "GridViewSortDescending"      => "내림차순 정렬",
        "GridViewClearSort"           => "정렬 해제",
        "GridViewColumnChooser"       => "열 선택",
        "GridViewDeleteSelectedRows"  => "선택한 행 삭제",
        "GridViewSelectAll"           => "전체 선택",

        // 공통 (필터/달력 등)
        "Contains"                    => "포함",
        "DoesNotContain"              => "포함하지 않음",
        "StartsWith"                  => "다음으로 시작",
        "EndsWith"                    => "다음으로 끝남",
        "IsEqualTo"                   => "같음",
        "IsNotEqualTo"                => "같지 않음",
        "IsGreaterThan"               => "초과",
        "IsGreaterThanOrEqualTo"      => "이상",
        "IsLessThan"                  => "미만",
        "IsLessThanOrEqualTo"         => "이하",
        "IsEmpty"                     => "비어 있음",
        "IsNotEmpty"                  => "비어 있지 않음",
        "IsNull"                      => "값 없음",
        "IsNotNull"                   => "값 있음",
        "Today"                       => "오늘",
        "Clear"                       => "지우기",
        "Cancel"                      => "취소",
        "Ok"                          => "확인",
        "OK"                          => "확인",

        _ => base.GetStringOverride(key)
    };
}
