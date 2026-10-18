using System.IO;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Export;

/// <summary>
/// 여러 지점(세션) 일괄 출력(C-8). 자료함에서 고른 세션을 차례로 화면 VM 에 불러와 각 exporter 의 <c>Write(vm, path)</c> 로 쓴다.
/// ⚠ 화면 VM 이 싱글턴이라 불러오면 보던 화면이 바뀐다 — 부르는 쪽이 <see cref="ISessionService.PreserveScreenAsync{T}"/> 로 감싼다.
///
/// <b>일괄입력용</b>: 고른 폴더에 세션 × 분류군(자료가 있는 것) 파일. 파일명 형식은 그대로(`{대분류}_{지점}_{분류군}_DB_v1.xlsx`).
/// 같은 이름이 생기면(같은 지점의 다른 연도차수) 그 세션들의 파일을 <c>연도차수</c> 하위 폴더로 나눈다(세션 단위 — 한 세션의 파일은 한 폴더에).
/// </summary>
internal static class BatchExporter
{
    /// <summary>분류군 키 ↔ 파일명에 쓰는 이름(각 exporter 와 같다) · 출력 순서.</summary>
    public static readonly (string Key, string Name)[] Taxa =
    {
        ("Fish", "어류"), ("Benthos", "저서동물"), ("Bird", "조류"), ("Mammal", "포유류"),
        ("AmphibianReptile", "양서파충류"), ("HabitatWaterEdge", "서식수변"), ("WaterQuality", "수질"),
    };

    /// <summary>쓸 파일 하나 — 폴더 기준 상대 경로.</summary>
    public sealed record Planned(SessionIndexEntry Entry, string TaxonKey, string TaxonName, string RelativePath);

    public sealed class Result
    {
        public List<string> Written { get; } = new();
        /// <summary>`파일: 이유`.</summary>
        public List<string> Failed { get; } = new();
        /// <summary>exporter 가 돌려준 안내(양식 외 종 덧붙임 등) — `파일: 안내`.</summary>
        public List<string> Notes { get; } = new();
        public int Sessions { get; set; }
    }

    /// <summary>
    /// 고른 세션의 일괄입력용 파일 계획. 세션 목록 항목의 `입력 분류군`으로 미리 정한다(같은 이름 판정이 먼저 필요해서).
    /// 목록에 분류군이 비어 있는 옛 항목은 7개 모두를 계획하고, 불러온 뒤 자료가 없는 분류군은 건너뛴다.
    /// </summary>
    public static List<Planned> PlanBulk(IEnumerable<SessionIndexEntry> entries)
    {
        var raw = new List<(SessionIndexEntry E, string Key, string Name, string File)>();
        foreach (var e in entries)
        {
            var names = e.Taxa.Count == 0 ? Taxa.Select(t => t.Name).ToHashSet() : e.Taxa.ToHashSet();
            foreach (var (key, name) in Taxa.Where(t => names.Contains(t.Name)))
                raw.Add((e, key, name, ReportExporterBase.BulkFileName(e.Project, SiteKeys.Of(e.Project, e.Site, e.River), name)));
        }

        // 같은 이름이 하나라도 생긴 세션은 그 세션 파일 전부를 연도차수 폴더로 옮긴다 — 한 세션의 파일이 위·아래로 갈라지지 않게.
        var split = raw.GroupBy(r => r.File, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
                       .SelectMany(g => g.Select(r => r.E.SessionId)).ToHashSet(StringComparer.Ordinal);
        return raw.Select(r => new Planned(r.E, r.Key, r.Name,
                    split.Contains(r.E.SessionId) ? Path.Combine(SafeFolder(r.E.YearChsu), r.File) : r.File))
                  .ToList();
    }

    /// <summary>계획대로 쓴다. 세션 하나를 불러와 그 세션 파일을 다 쓰고 다음 세션으로 간다. 실패는 그 파일만 건너뛴다.</summary>
    public static async Task<Result> WriteBulkAsync(ISessionService sessions, IReadOnlyList<Planned> plan, string folder)
    {
        var result = new Result();
        var sp = App.Current.Services;
        foreach (var group in plan.GroupBy(p => p.Entry.SessionId))
        {
            if (!await sessions.LoadAsync(group.Key))
            {
                foreach (var p in group) result.Failed.Add($"{p.RelativePath}: 세션 파일을 찾지 못했습니다");
                continue;
            }
            result.Sessions++;

            foreach (var p in group)
            {
                var path = Path.Combine(folder, p.RelativePath);
                try
                {
                    if (!HasData(sp, p.TaxonKey)) continue;   // 옛 목록 항목 — 불러와 보니 자료 없음
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var note = WriteOne(sp, p.TaxonKey, path);
                    result.Written.Add(p.RelativePath);
                    if (!string.IsNullOrEmpty(note)) result.Notes.Add($"{p.RelativePath}: {note}");
                }
                catch (BulkTemplateException ex) { result.Failed.Add($"{p.RelativePath}: 양식을 쓸 수 없습니다({ex.Message})"); }
                catch (IOException ex) { result.Failed.Add($"{p.RelativePath}: 저장하지 못했습니다(열려 있는 파일인지 확인 · {ex.Message})"); }
                catch (Exception ex) { result.Failed.Add($"{p.RelativePath}: {ex.Message}"); }
            }
        }
        return result;
    }

    /// <summary>
    /// 보고서 일괄(C-8 · 사용자 선택 C안) — 고른 세션의 보고서를 <b>통합문서 하나</b>로 합쳐 <paramref name="path"/> 에 쓴다.
    /// 세션 순서: 대분류 → 하천·지점(C-7 정렬) → 연도차수. 분류군 순서: 어류 · 저서동물 · 조류 · 포유류 · 양서파충류 · 서식수변 · 수질.
    /// 지점 하나의 내용은 지점별 보고서와 같다(같은 <c>Build</c>). 세션을 못 찾으면 그 세션만 빠지고 요약에 적는다.
    /// </summary>
    public static async Task<Result> WriteReportAsync(ISessionService sessions, IReadOnlyList<SessionIndexEntry> entries, string path)
    {
        var result = new Result();
        var sp = App.Current.Services;
        var parts = Taxa.ToDictionary(t => t.Key, _ => new List<CombinedReport.Part>());

        foreach (var e in Order(entries))
        {
            if (!await sessions.LoadAsync(e.SessionId))
            {
                result.Failed.Add($"{e.Site} · {e.YearChsu}: 세션 파일을 찾지 못했습니다");
                continue;
            }
            result.Sessions++;
            var label = $"{e.Site ?? "(지점 없음)"} · {e.YearChsu ?? "(차수 없음)"}";
            foreach (var (key, name) in Taxa)
            {
                if (!HasData(sp, key)) continue;
                try
                {
                    parts[key].Add(new CombinedReport.Part(label, BuildReport(sp, key)));
                    result.Written.Add($"{name} · {label}");
                }
                catch (Exception ex) { result.Failed.Add($"{name} · {label}: {ex.Message}"); }
            }
        }

        var book = CombinedReport.Merge(Taxa.Select(t => (t.Name, (IReadOnlyList<CombinedReport.Part>)parts[t.Key])).ToList());
        if (book.Worksheets.Count == 0) throw new InvalidOperationException("고른 세션에 보고서로 낼 자료가 없습니다.");
        ReportExporterBase.Save(book, path);
        return result;
    }

    /// <summary>보고서 일괄의 세션 순서 — 대분류 → 하천(가나다) → 지점(자연 정렬) → 연도차수.</summary>
    private static IEnumerable<SessionIndexEntry> Order(IEnumerable<SessionIndexEntry> entries) =>
        entries.OrderBy(e => e.Project ?? string.Empty, StringComparer.Ordinal)
               .ThenBy(e => e.River ?? string.Empty, StringComparer.Ordinal)
               .ThenBy(e => e.Site, SiteOrdering.NaturalComparer.Instance)
               .ThenBy(e => e.YearChsu, SiteOrdering.NaturalComparer.Instance);

    private static Telerik.Documents.Spreadsheet.Model.Workbook BuildReport(IServiceProvider sp, string key) => key switch
    {
        "Fish" => FishReportExporter.Build(sp.GetRequiredService<FishEntryViewModel>()),
        "Benthos" => BenthosReportExporter.Build(sp.GetRequiredService<BenthosEntryViewModel>()),
        "Bird" => BirdReportExporter.Build(sp.GetRequiredService<BirdEntryViewModel>()),
        "Mammal" => MammalReportExporter.Build(sp.GetRequiredService<MammalEntryViewModel>()),
        "AmphibianReptile" => AmphibianReptileReportExporter.Build(sp.GetRequiredService<AmphibianReptileEntryViewModel>()),
        "HabitatWaterEdge" => HabitatWaterEdgeReportExporter.Build(sp.GetRequiredService<HabitatWaterEdgeEntryViewModel>()),
        "WaterQuality" => WaterQualityReportExporter.Build(sp.GetRequiredService<WaterQualityEntryViewModel>()),
        _ => throw new InvalidOperationException($"알 수 없는 분류군 {key}"),
    };

    /// <summary>보고서 일괄 파일 이름 — `일괄_{대분류}[_{연도차수}]_조사결과.xlsx`(연도차수는 하나일 때만).</summary>
    public static string ReportFileName(IReadOnlyList<SessionIndexEntry> entries)
    {
        var projects = entries.Select(e => e.Project).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        var chasu = entries.Select(e => e.YearChsu).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        var name = "일괄_" + (projects.Count == 0 ? "" : string.Join("·", projects) + "_") + (chasu.Count == 1 ? chasu[0] + "_" : "") + "조사결과.xlsx";
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private static bool HasData(IServiceProvider sp, string key) => key switch
    {
        "Fish" => ((Data.ITaxonSession)sp.GetRequiredService<FishEntryViewModel>()).HasData,
        "Benthos" => ((Data.ITaxonSession)sp.GetRequiredService<BenthosEntryViewModel>()).HasData,
        "Bird" => ((Data.ITaxonSession)sp.GetRequiredService<BirdEntryViewModel>()).HasData,
        "Mammal" => ((Data.ITaxonSession)sp.GetRequiredService<MammalEntryViewModel>()).HasData,
        "AmphibianReptile" => ((Data.ITaxonSession)sp.GetRequiredService<AmphibianReptileEntryViewModel>()).HasData,
        "HabitatWaterEdge" => ((Data.ITaxonSession)sp.GetRequiredService<HabitatWaterEdgeEntryViewModel>()).HasData,
        "WaterQuality" => ((Data.ITaxonSession)sp.GetRequiredService<WaterQualityEntryViewModel>()).HasData,
        _ => false,
    };

    /// <summary>분류군 하나를 쓴다. 돌려주는 값은 저장 완료 안내에 붙이던 한 줄(없으면 null).</summary>
    private static string? WriteOne(IServiceProvider sp, string key, string path)
    {
        switch (key)
        {
            case "Fish": FishExcelExporter.Write(sp.GetRequiredService<FishEntryViewModel>(), path); return null;
            case "Benthos":
                var b = BenthosExcelExporter.Write(sp.GetRequiredService<BenthosEntryViewModel>(), path);
                return b.Count == 0 ? null : $"양식 종목록에 없어 끝에 추가한 종: {string.Join(", ", b)}";
            case "Bird": BirdExcelExporter.Write(sp.GetRequiredService<BirdEntryViewModel>(), path); return null;
            case "Mammal": MammalExcelExporter.Write(sp.GetRequiredService<MammalEntryViewModel>(), path); return null;
            case "AmphibianReptile": AmphibianReptileExcelExporter.Write(sp.GetRequiredService<AmphibianReptileEntryViewModel>(), path); return null;
            case "HabitatWaterEdge": HabitatWaterEdgeExcelExporter.Write(sp.GetRequiredService<HabitatWaterEdgeEntryViewModel>(), path); return null;
            case "WaterQuality":
                var w = WaterQualityExcelExporter.Write(sp.GetRequiredService<WaterQualityEntryViewModel>(), path);
                return w.Count == 0 ? null : $"양식에 없어 덧붙인 항목: {string.Join(", ", w)}";
            default: throw new InvalidOperationException($"알 수 없는 분류군 {key}");
        }
    }

    /// <summary>연도차수 하위 폴더 이름(파일 이름에 못 쓰는 문자는 `_`).</summary>
    private static string SafeFolder(string? yearChsu)
    {
        var v = string.IsNullOrWhiteSpace(yearChsu) ? "연도차수없음" : yearChsu.Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) v = v.Replace(c, '_');
        return v;
    }
}
