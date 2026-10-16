namespace SDSM_Surveyor_App.Models;

/// <summary>선택지 한 줄 — 저장값과 화면 글자. 이름을 저장하는 항목은 둘이 같다.</summary>
public sealed record FormChoice(string Value, string Label)
{
    public static FormChoice Name(string name) => new(name, name);
}

/// <summary>
/// 선택지 — 2026-03 일괄입력 최종본 양식의 정의 그대로(C-4 · 지시서 §3-3·§3-4·§5 C-4).
/// 이 파일 하나가 화면 선택지의 출처다. 저장 규칙(지시서 §3-5):
/// <list type="bullet">
/// <item>관리자 코드표에 있는 항목은 <b>이름</b>을 저장한다(코드 `1` 을 쓰면 저서 하천유형이 오타 `산강형` 으로 저장된다).</item>
/// <item>여러 개는 양식 순서대로 공백 없이 `,` 로 잇는다(`흐름,소` · <see cref="SDSM_Surveyor_App.Views.UserControls.MultiChoiceBox"/>).</item>
/// <item>식생 수피도는 <b>코드 숫자</b>(`1`)를 저장한다 — 실입력이 코드이고 관리자 변환표 `_camopyCovers` 가 이름으로 바꾼다(10-16 정정).</item>
/// <item>투명도·냄새는 <b>정수 글자</b>(`"1"`~`"5"`)를 저장한다 — 관리자 모델이 `int?` 다. 일괄입력용에는 숫자로 나간다.</item>
/// </list>
/// 세션 형식은 그대로 문자열이다. 예전 세션의 양식 외 값은 지우지 않고 화면에서 경고한다.
/// </summary>
public static class FormChoices
{
    private static FormChoice[] Names(params string[] names) => names.Select(FormChoice.Name).ToArray();

    // ── 조사개황 (어류·저서 양식 8행) ──
    /// <summary>하천유형(개황) — 산간형 / 평지형 / 강 · 단일 · 이름.</summary>
    public static IReadOnlyList<FormChoice> RiverType { get; } = Names("산간형", "평지형", "강");

    // ── 어류 `어류_입력` ──
    /// <summary>채집도구(15행) — 투망/족대 · 통발 + 직접입력 · 복수 · 이름.</summary>
    public static IReadOnlyList<string> FishCollectionTool { get; } = new[] { "투망/족대", "통발" };

    /// <summary>흐름상태(채집)(16행) — 여울 / 흐름 / 소 · 복수 · 이름(B-8).</summary>
    public static IReadOnlyList<string> FlowState { get; } = new[] { "여울", "흐름", "소" };

    /// <summary>하천형태(27행) — 자연형 / 직강화 / 복합형 / 댐/보/교각 / 공사 · 복수 · 이름.</summary>
    public static IReadOnlyList<string> FishRiverForm { get; } = new[] { "자연형", "직강화", "복합형", "댐/보/교각", "공사" };

    /// <summary>흐름상태(유속)(28행) — 아주빠름 / 빠름 / 보통 / 느림 / 정체 · 단일 · 이름.</summary>
    public static IReadOnlyList<FormChoice> FishVelocity { get; } = Names("아주빠름", "빠름", "보통", "느림", "정체");

    // ── 저서동물 `입력` ──
    /// <summary>식생 수피도(20행) — 1 없음 / 2 부분적 / 3 짙음 · 단일 · <b>코드 숫자</b>.</summary>
    public static IReadOnlyList<FormChoice> CanopyCover { get; } = new[]
    {
        new FormChoice("1", "1 없음"), new FormChoice("2", "2 부분적"), new FormChoice("3", "3 짙음"),
    };

    /// <summary>범람원의 이용(21행) — 자연형 / 농경지 / 도로 / 주차장 / 산책로 / 기타 · 복수 · 이름.</summary>
    public static IReadOnlyList<string> Floodplain { get; } = new[] { "자연형", "농경지", "도로", "주차장", "산책로", "기타" };

    /// <summary>제방 좌·우(22·23행) — 자연형 / 석축 / 돌망태 / 콘크리트 + 직접입력 · 복수 · 이름.</summary>
    public static IReadOnlyList<string> Levee { get; } = new[] { "자연형", "석축", "돌망태", "콘크리트" };

    /// <summary>하천유형(서식처)(33행) — 산간형 / 평지형 / 강 / 인공구조물 · 단일 · 이름.</summary>
    public static IReadOnlyList<FormChoice> HabitatRiverType { get; } = Names("산간형", "평지형", "강", "인공구조물");

    /// <summary>투명도(41행) — 매우 맑음 1 ~ 5 매우 탁함 · 단일 · <b>정수</b>.</summary>
    public static IReadOnlyList<FormChoice> Transparency { get; } = new[]
    {
        new FormChoice("1", "1 매우 맑음"), new FormChoice("2", "2"), new FormChoice("3", "3"),
        new FormChoice("4", "4"), new FormChoice("5", "5 매우 탁함"),
    };

    /// <summary>냄새(42행) — 없음 1 ~ 5 매우 심함 · 단일 · <b>정수</b>.</summary>
    public static IReadOnlyList<FormChoice> Smell { get; } = new[]
    {
        new FormChoice("1", "1 없음"), new FormChoice("2", "2"), new FormChoice("3", "3"),
        new FormChoice("4", "4"), new FormChoice("5", "5 매우 심함"),
    };

    /// <summary>단일 선택 칸의 값이 선택지 밖이면 그 값(앞뒤 공백 제거), 아니면 null. 비어 있으면 null.</summary>
    public static string? OutOfForm(string? value, IReadOnlyList<FormChoice> options)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return options.Any(o => o.Value == v) ? null : v;
    }

    private static readonly char[] Separators = { ',', '.' };

    /// <summary>복수 선택 저장 문자열을 항목으로 나눈다(`,`·`.` — 관리자 Import 와 같은 구분자 · 앞뒤 공백 제거 · 빈 항목 무시).</summary>
    public static List<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(Separators).Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

    /// <summary>복수 선택 칸의 값 중 선택지 밖 항목. 직접입력을 허용하는 칸이면 늘 빈 목록.</summary>
    public static IReadOnlyList<string> OutOfForm(string? value, IReadOnlyList<string> options, bool allowCustom = false) =>
        allowCustom
            ? Array.Empty<string>()
            : Split(value).Where(t => !options.Contains(t)).Distinct().ToList();

    /// <summary>경고 문구 한 덩이 — 예: `양식 외 값: 하천형태 평지형 · 유속 중간`. 없으면 빈 글자.</summary>
    public static string Describe(params (string Label, IEnumerable<string> Extras)[] fields)
    {
        var parts = fields.Where(f => f.Extras.Any()).Select(f => $"{f.Label} {string.Join(",", f.Extras)}").ToList();
        return parts.Count == 0 ? string.Empty : $"양식 외 값: {string.Join(" · ", parts)} — 지우지 않음, 확인하세요";
    }
}
