using System.Windows;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 마우스로 누른 지점이 어느 행인지 되짚는다. [행 삭제] 버튼과 우클릭 메뉴가 이 값을 쓴다.
///
/// ⚠ <see cref="GridViewCell"/> 로만 찾으면 <b>2단 행</b>(RowDetails)을 못 잡는다.
///   RowDetails 안의 컨트롤은 셀이 아니라 <c>PART_DetailsPresenter</c> 아래에 있어
///   시각 트리를 거슬러 올라가도 셀을 만나지 않는다. 반면 그 프레젠터는
///   <see cref="GridViewRow"/> 템플릿의 일부라 행에서는 반드시 걸린다.
///   → 행을 먼저 찾고, 못 찾으면 셀로 한 번 더 본다.
///
/// ⚠ <b>못 찾으면 null 을 돌려준다</b>. 머리글이나 빈 곳을 눌렀는데 직전 값이 남아 있으면
///   [행 삭제]가 엉뚱한 행을 지운다 — 값을 비워 명령이 꺼지게 하는 편이 안전하다.
///
/// ⚠ 인서트 행(<c>GridViewNewRow</c>)도 <see cref="GridViewRow"/> 를 상속한다.
///   여기서는 걸러 내지 않고, 지우는 쪽이 `Rows.Contains` 로 확인한다
///   (아직 컬렉션에 없는 행이라 자연히 걸러진다).
/// </summary>
public static class GridRowPicker
{
    /// <summary>마우스 이벤트의 <c>OriginalSource</c> 로 누른 행의 DataContext 를 찾는다. 없으면 null.</summary>
    public static object? Resolve(object? originalSource)
    {
        if (originalSource is not DependencyObject d) return null;

        // ParentOfType 은 자기 자신을 보지 않는다 — 행 여백을 바로 눌렀을 때를 위해 먼저 확인한다.
        var row = d as GridViewRow ?? d.ParentOfType<GridViewRow>();
        if (row is not null) return row.DataContext;

        var cell = d as GridViewCell ?? d.ParentOfType<GridViewCell>();
        return cell?.DataContext;
    }
}
