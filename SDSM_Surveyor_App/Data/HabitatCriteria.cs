using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 서식수변 평가항목 1~10 의 평가내용·판정점수 글자(10-17 화면 검토).
/// 2026-03 최종본 서식수변 양식 `서식 및 수변환경` 시트 17~26행 C열(평가내용)·D열(판정점수)을 <c>tools\TemplateGen</c> 이
/// 한 번 뽑아 둔 <c>templates\bulk\habitat_criteria.json</c> 을 그대로 읽는다(손으로 다시 치지 않는다).
/// 화면 표시용이다 — 일괄입력용·보고서 엑셀에는 쓰지 않는다. 파일이 없으면 빈 글자(화면만 비고 앱은 그대로 동작).
/// </summary>
public static class HabitatCriteria
{
    /// <summary>항목 하나. <see cref="Content"/>·<see cref="Scoring"/> 은 원본 글자, <c>…Line</c> 은 줄바꿈을 한 칸 띄우기로 이은 것.</summary>
    public sealed record Item(string No, string Name, string Content, string Scoring)
    {
        /// <summary>평가내용을 한 줄로(원본 셀 안 줄바꿈·겹친 공백 → 공백 하나). 화면 '평가 내용' 열·항목 툴팁.</summary>
        public string ContentLine => OneLine(Content);

        /// <summary>판정점수 — 원본 그대로(셀 안 줄바꿈 유지). 선택지 툴팁.</summary>
        public string ScoringText => Scoring.Trim();
    }

    private static readonly Lazy<IReadOnlyList<Item>> _items = new(Load);

    /// <summary>항목 1~10(순서대로). 파일이 없으면 빈 글자 10개.</summary>
    public static IReadOnlyList<Item> Items => _items.Value;

    public static string OneLine(string? s) => Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim();

    private static IReadOnlyList<Item> Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "templates", "bulk", "habitat_criteria.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var list = doc.RootElement.GetProperty("items").EnumerateArray()
                .Select(e => new Item(Str(e, "no"), Str(e, "name"), Str(e, "content"), Str(e, "scoring")))
                .ToList();
            if (list.Count == 10) return list;
        }
        catch
        {
            // 화면 설명용 글자일 뿐이다 — 없어도 입력·계산·내보내기는 그대로다.
        }
        return Enumerable.Range(1, 10).Select(i => new Item(i.ToString(), string.Empty, string.Empty, string.Empty)).ToList();

        static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
    }
}
