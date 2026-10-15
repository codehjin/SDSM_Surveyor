using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// Telerik 기본 영문 UI 문구를 한글로 치환한다.
/// (그리드 새 행 안내, 그룹 패널, 필터 메뉴 등)
/// ※ 알 수 없는 키는 base 로 넘겨 원문을 그대로 사용하므로, 키가 틀려도 예외가 나지 않는다.
/// </summary>
public sealed class KoreanLocalizationManager : LocalizationManager
{
    public override string GetStringOverride(string key)
    {
        LogKey(key);
        return Translate(key);
    }

    /// <summary>
    /// Telerik 이 실제로 요청한 리소스 키를 파일로 남긴다(Debug 전용).
    ///
    /// 문구가 영문 그대로 남을 때 **키 이름을 추측하지 않기 위해** 둔다.
    /// 버전마다 키가 달라 DLL 대조로는 특정되지 않는다 —
    /// 실행 폴더의 <c>localization_keys.log</c> 를 열어 해당 문구의 키를 찾아
    /// 아래 switch 에 추가하면 된다.
    /// </summary>
    /// <summary>
    /// 로깅 자체가 살아 있는지 확인하는 표식. 앱 시작 시 App 이 한 번 부른다.
    /// 이 줄만 있고 키가 하나도 없으면 → Telerik 이 LocalizationManager 를 아예 안 부르는 것이다.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void MarkStart() => LogKey("# --- 앱 시작 (아래는 Telerik 이 실제로 요청한 키) ---");

    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogKey(string key)
    {
        try
        {
            if (!_seen.Add(key)) return;
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "localization_keys.log");
            if (_seen.Count == 1 && System.IO.File.Exists(path)) System.IO.File.Delete(path);
            System.IO.File.AppendAllText(
                path, key + System.Environment.NewLine);
        }
        catch { /* 로그 실패가 화면을 막아서는 안 된다 */ }
    }

    private static readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    private string Translate(string key) => key switch
    {
        // RadGridView
        "GridViewGroupPanelText"      => "열 머리글을 여기로 끌어오면 해당 열로 묶어 볼 수 있습니다",
        // 새 행 안내("Click here to add new item").
        // ⚠ 키 이름이 GridViewAddNewRowString 이 아니라 **GridViewAlwaysVisibleNewRow** 다.
        //    DLL 문자열 대조로는 못 찾았고, LogKey 로 실제 요청 키를 받아 확인했다.
        //    문구를 새로 한글화할 때는 추측하지 말고 localization_keys.log 를 볼 것.
        "GridViewAlwaysVisibleNewRow" => "여기에 입력하거나 위 빠른추가로 종을 추가하세요",
        "GridViewColumnsSelectionButtonTooltip" => "열 선택",
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
