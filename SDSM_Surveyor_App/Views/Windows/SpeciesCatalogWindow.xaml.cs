using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels;
using SDSM_Surveyor_App.ViewModels.Base;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>
/// 종목록 창 — 담당 분류군의 등록 종 전체를 보고, 야장에 넣고, 엑셀로 내려받는다.
///
/// 열은 분류군마다 다르므로 **코드비하인드가 만든다.** RadGridView 의 컬럼은 시각 트리 밖이라
/// <c>RelativeSource</c> 바인딩으로 보이기/숨기기를 할 수 없다(90_TECH_NOTES §2 계열 함정).
///
/// ⚠ 폭은 전부 고정이다. RadGridView 는 폭 합이 뷰포트를 넘으면 가로 스크롤을 만들지 않고
///   열을 균등하게 뭉갠다. 창 폭 1080(여백 32 제외 ≈1032) 안에 들어가도록 잡았다.
/// </summary>
public partial class SpeciesCatalogWindow : Window
{
    private readonly SpeciesCatalogViewModel _vm;
    private readonly IQuickAddHost? _host;

    public SpeciesCatalogWindow(IQuickAddHost? host = null)
    {
        InitializeComponent();
        _host = host;
        _vm = App.Current.Services.GetRequiredService<SpeciesCatalogViewModel>();
        DataContext = _vm;

        BuildColumns(_vm.Kind);

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SpeciesCatalogViewModel.LastSavedPath)) return;
            SaveHint.Text = string.IsNullOrEmpty(_vm.LastSavedPath) ? string.Empty : $"저장됨 · {_vm.LastSavedPath}";
        };

        Loaded += (_, _) => SearchBox.Focus();
    }

    // ── 열 구성 ────────────────────────────────────────────────────────────────

    private void BuildColumns(TaxonKind kind)
    {
        CatalogGrid.Columns.Clear();
        Add("#", nameof(SpeciesCatalogRow.No), 44, TextAlignment.Right);

        switch (kind)
        {
            case TaxonKind.Fish:
                Add("강", nameof(SpeciesCatalogRow.ClassKo), 80);
                Add("목", nameof(SpeciesCatalogRow.OrderKo), 100);
                Add("과", nameof(SpeciesCatalogRow.FamilyKo), 100);
                Add("국명", nameof(SpeciesCatalogRow.SpeciesKo), 150);
                Add("학명", nameof(SpeciesCatalogRow.SpeciesEn), 200);
                Add("구분", nameof(SpeciesCatalogRow.Tags), 130);
                Add("내성도", nameof(SpeciesCatalogRow.Tolerance), 74);
                Add("섭식", nameof(SpeciesCatalogRow.Feeding), 74);
                Add("서식지", nameof(SpeciesCatalogRow.Habitat), 74);
                break;

            case TaxonKind.Benthos:
                Add("문", nameof(SpeciesCatalogRow.PhylumKo), 90);
                Add("강", nameof(SpeciesCatalogRow.ClassKo), 80);
                Add("목", nameof(SpeciesCatalogRow.OrderKo), 100);
                Add("과", nameof(SpeciesCatalogRow.FamilyKo), 100);
                Add("국명", nameof(SpeciesCatalogRow.SpeciesKo), 150);
                Add("학명", nameof(SpeciesCatalogRow.SpeciesEn), 200);
                Add("구분", nameof(SpeciesCatalogRow.Tags), 110);
                Add("오탁치 s", nameof(SpeciesCatalogRow.Saprobic), 70, TextAlignment.Right);
                Add("지표가중치 g", nameof(SpeciesCatalogRow.Weight), 84, TextAlignment.Right);
                break;

            default:   // 조류 · 포유류 · 양서파충류 (관찰형 · ObservedSpecies)
                Add("목", nameof(SpeciesCatalogRow.OrderKo), 100);
                Add("과", nameof(SpeciesCatalogRow.FamilyKo), 100);
                Add("국명", nameof(SpeciesCatalogRow.SpeciesKo), 160);
                Add("학명", nameof(SpeciesCatalogRow.SpeciesEn), 240);
                Add("구분", nameof(SpeciesCatalogRow.Tags), 150);
                Add("KTSN", nameof(SpeciesCatalogRow.Ktsn), 110);
                break;
        }
    }

    private void Add(string header, string path, double width, TextAlignment align = TextAlignment.Left)
        => CatalogGrid.Columns.Add(new GridViewDataColumn
        {
            Header = header,
            DataMemberBinding = new Binding(path),
            Width = new GridViewLength(width),
            TextAlignment = align,
            IsFilterable = false,
            IsSortable = true
        });

    // ── 야장에 넣기 ────────────────────────────────────────────────────────────

    // 행 더블클릭 → 그 종을 야장에 추가하고 창을 닫는다.
    // ⚠ MouseDoubleClick 은 열 머리글·빈 영역에서도 뜬다. 눌린 곳이 실제 데이터 행인지 확인하지 않으면
    //    머리글을 두 번 눌러 정렬하려다 종이 들어가고 창이 닫힌다.
    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_host is null) return;

        // 데이터 셀 위에서 눌렀을 때만. 열 머리글은 GridViewHeaderCell 이라 여기 걸리지 않는다.
        var cell = (e.OriginalSource as DependencyObject)?.ParentOfType<GridViewCell>();
        if (cell?.DataContext is not SpeciesCatalogRow hit) return;

        _host.AddFromCatalog(hit.Source);
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
