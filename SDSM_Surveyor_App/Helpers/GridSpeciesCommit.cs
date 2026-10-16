using System.Windows;
using System.Windows.Media;
using SDSM_Surveyor_App.ViewModels.Base;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 그리드에서 국명 편집이 <b>확정</b>된 시점을 화면 VM 에 알린다. 종이 들어가는 5개 그리드가 같이 쓴다.
///
/// 1. 목록 밖 국명 보존(B-4) — 편집기는 목록에서 고른 종(<c>SelectedItem</c>)만 바인딩해서
///    목록에 없는 국명을 치면 친 글자가 버려졌다. 확정 순간 편집기의 검색 글자를 국명으로 넣는다.
///    ⚠ 검색 글자(<c>SearchText</c>)를 양방향 바인딩하지 않는 이유 — 커스텀 필터와 함께 쓰면
///      목록에서 고르는 데 두 번 클릭해야 한다(90_TECH_NOTES §2). 확정 때 한 번 읽기만 한다.
/// 2. 같은 종 처리(B-1) — 어류·저서는 합치고, 관찰형은 알린다(<see cref="IQuickAddHost.OnSpeciesCommitted"/>).
///    · 기존 행 : 국명 셀 편집이 확정될 때
///    · 맨 아래 새 행 : 셀이 아니라 <b>행이 확정될 때</b>(RowEditEnded · Insert). 국명 칸에서 Tab 으로
///      개체수 칸에 넘어간 순간 합쳐 버리면 치던 개체수가 갈 곳이 없어진다.
/// </summary>
public static class GridSpeciesCommit
{
    /// <summary>국명 열을 가리는 바인딩 경로. 5개 화면 모두 `DataMemberBinding="{Binding SpeciesKo}"` 다.</summary>
    private const string SpeciesPath = "SpeciesKo";

    /// <param name="grid">종 그리드.</param>
    /// <param name="host">화면 VM. DataContext 가 바뀔 수 있어 매번 묻는다.</param>
    public static void Attach(RadGridView grid, Func<IQuickAddHost?> host)
    {
        grid.CellEditEnded += (_, e) =>
        {
            if (e.EditAction != GridViewEditAction.Commit || host() is not { } h) return;
            if (e.Cell?.Column is not GridViewBoundColumnBase { DataMemberBinding.Path.Path: SpeciesPath }) return;
            var row = e.Cell.DataContext;
            if (row is null) return;

            if (FindEditor(e.EditingElement) is { } box) h.ApplyTypedSpecies(row, box.SearchText);

            // 새 행은 행 확정(RowEditEnded) 때 본다 — 위 주석 2.
            if (e.Cell.ParentRow is GridViewNewRow) return;
            h.OnSpeciesCommitted(row, isNewRow: false);
        };

        grid.RowEditEnded += (_, e) =>
        {
            if (e.EditAction != GridViewEditAction.Commit || e.EditedItem is not { } item || host() is not { } h) return;
            // 기존 행은 셀 확정 때 이미 처리했다. 여기서 한 번 더 불러도 VM 이 '국명이 바뀐 행' 표시로
            // 한 번만 처리한다(셀 확정 이벤트를 놓친 경우의 안전망).
            h.OnSpeciesCommitted(item, isNewRow: e.EditOperationType == GridViewEditOperationType.Insert);
        };
    }

    /// <summary>편집 요소에서 국명 자동완성 편집기를 찾는다(편집 템플릿의 뿌리이거나 그 안에 있다).</summary>
    private static RadAutoCompleteBox? FindEditor(object? editing)
    {
        if (editing is RadAutoCompleteBox box) return box;
        if (editing is not DependencyObject d) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            if (FindEditor(VisualTreeHelper.GetChild(d, i)) is { } found) return found;
        return null;
    }
}
