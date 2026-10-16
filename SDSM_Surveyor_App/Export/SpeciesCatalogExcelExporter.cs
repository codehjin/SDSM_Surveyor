using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels;
using Telerik.Documents.Spreadsheet.Model;
using static SDSM_Surveyor_App.Export.ExcelStyle;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 종목록 팝업 → 엑셀 한 장.
///
/// 화면에 보이는 것이 아니라 <b>담당 분류군 종목록 전체</b>를 쓴다(검색으로 걸러도 전체가 나간다).
/// A1 에 종목록 버전을 남긴다 — 나중에 이 파일이 어느 버전인지 알 수 있어야 한다(CLAUDE.md §4).
/// </summary>
public static class SpeciesCatalogExcelExporter
{
    /// <summary>저장 대화상자를 띄우고 저장한다. 취소하면 null.</summary>
    public static string? Export(SpeciesCatalogViewModel vm, IReadOnlyList<SpeciesCatalogRow> all) =>
        ReportExporterBase.SaveWithDialog(
            "종목록 엑셀 저장",
            FileName(vm),
            path => Write(vm, all, path));

    /// <summary>`종목록_저서동물_nibr-20260820.xlsx`. 파일명 금지문자는 '_' 로 바꾼다.</summary>
    public static string FileName(SpeciesCatalogViewModel vm)
    {
        // FileToken 은 값 뒤에 '_' 를 붙여 돌려준다. 끝에 붙는 '_' 를 그대로 두면
        // `..._nibr-20260820_.xlsx` 가 되므로 잘라 낸다.
        var taxon = FileToken(vm.TaxonName).TrimEnd('_');
        var ver = FileToken(vm.Version).TrimEnd('_');
        return $"종목록_{taxon}_{ver}.xlsx";
    }

    /// <summary>대화상자 없이 지정 경로로 저장한다.</summary>
    public static void Write(SpeciesCatalogViewModel vm, IReadOnlyList<SpeciesCatalogRow> all, string path)
    {
        var wb = new Workbook();
        var ws = wb.Worksheets.Add();
        ws.Name = "종목록";

        ws.Cells[0, 0].SetValue($"{vm.TaxonName} 종목록 — 버전 {vm.Version} · {all.Count:N0}종");
        Title(ws.Cells[0, 0]);

        var cols = Columns(vm.Kind);
        const int HeaderRow = 2;
        WriteHeader(ws, HeaderRow, cols.Select(c => c.Header).ToArray());

        // ⚠ SpeciesCatalogRow.No 는 화면 필터 결과에만 다시 매겨진다.
        //    검색을 걸어 둔 채 저장하면 걸러진 행이 옛 번호를 그대로 들고 있으므로
        //    엑셀은 목록 순회 인덱스를 쓴다.
        var r = HeaderRow;
        var no = 0;
        foreach (var row in all)
        {
            r++;
            Num(ws, r, 0, ++no);
            for (int c = 1; c < cols.Count; c++)
            {
                var v = cols[c].Value(row);
                if (cols[c].IsNumber && double.TryParse(v, out var d)) Num(ws, r, c, d);
                else Str(ws, r, c, v);
            }
        }

        for (int c = 0; c < cols.Count; c++) Width(ws, c, cols[c].Width);
        FontAll(ws, r, cols.Count - 1);
        TryAutoFilter(ws, HeaderRow, r, cols.Count - 1);

        ReportExporterBase.Save(wb, path);
    }

    private sealed record Col(string Header, Func<SpeciesCatalogRow, string?> Value, double Width, bool IsNumber = false);

    /// <summary>분류군별 열 구성. 화면 그리드와 같은 순서를 쓴다.</summary>
    private static List<Col> Columns(TaxonKind kind)
    {
        // '#' 값은 Write() 가 순회 인덱스로 직접 채운다(위 주석 참조).
        var head = new List<Col> { new("#", _ => null, 44, true) };

        var tail = new List<Col>
        {
            new("국명", x => x.SpeciesKo, 150),
            new("학명", x => x.SpeciesEn, 200),
            new("구분", x => x.Tags, 150)
        };

        return kind switch
        {
            TaxonKind.Fish => head
                .Append(new Col("강", x => x.ClassKo, 90))
                .Append(new Col("목", x => x.OrderKo, 110))
                .Append(new Col("과", x => x.FamilyKo, 110))
                .Concat(tail)
                .Append(new Col("내성도", x => x.Tolerance, 80))
                .Append(new Col("섭식", x => x.Feeding, 80))
                .Append(new Col("서식지", x => x.Habitat, 80))
                .ToList(),

            TaxonKind.Benthos => head
                .Append(new Col("문", x => x.PhylumKo, 100))
                .Append(new Col("강", x => x.ClassKo, 90))
                .Append(new Col("목", x => x.OrderKo, 110))
                .Append(new Col("과", x => x.FamilyKo, 110))
                .Concat(tail)
                .Append(new Col("오탁치 s", x => x.Saprobic, 76, true))
                .Append(new Col("지표가중치 g", x => x.Weight, 96, true))
                .ToList(),

            _ => head
                .Append(new Col("목", x => x.OrderKo, 110))
                .Append(new Col("과", x => x.FamilyKo, 110))
                .Concat(tail)
                .Append(new Col("KTSN", x => x.Ktsn, 110))
                .ToList()
        };
    }
}
