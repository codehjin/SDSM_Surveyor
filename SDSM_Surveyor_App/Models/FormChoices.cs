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

    // ── 조사개황 하천유형 — 분류군 양식마다 다르다(2026-10-18 결정 D7) ──
    /// <summary>하천유형(개황) — 어류·저서 양식 8행 '산간형/평지형/강' · 단일 · 이름.</summary>
    public static IReadOnlyList<FormChoice> RiverType { get; } = Names("산간형", "평지형", "강");

    /// <summary>하천유형(개황) — 수질·서식수변 양식 8행 '본류/지류/기타' · 단일 · 이름(D7).</summary>
    public static IReadOnlyList<FormChoice> RiverTypeWater { get; } = Names("본류", "지류", "기타");

    /// <summary>분류군 양식의 하천유형 선택지. 조류·포유류·양서파충류 양식에는 하천유형이 없다 → null(화면에서 숨김).</summary>
    public static IReadOnlyList<FormChoice>? RiverTypeFor(TaxonKind kind) => kind switch
    {
        TaxonKind.Fish or TaxonKind.Benthos => RiverType,
        TaxonKind.WaterQuality or TaxonKind.HabitatWaterEdge => RiverTypeWater,
        _ => null,
    };

    // ── 조류 `조류` T열 도래유형 — 'RES :텃새 / SV :여름철새 / WV:겨울철새 / PM:통과철새'(D5) ──
    /// <summary>도래유형 · 단일 · <b>코드</b> 저장(관리자 DB 와 같다). 화면 글자 `RES · 텃새`.
    /// 양식 정의에는 4개뿐이다(지시서 §12 의 `Vag.` 은 양식에 없어 넣지 않았다 — 예전 값은 '양식 외'로 둔다).</summary>
    public static IReadOnlyList<FormChoice> MigratoryType { get; } = new[]
    {
        new FormChoice("RES", "RES · 텃새"), new FormChoice("SV", "SV · 여름철새"),
        new FormChoice("WV", "WV · 겨울철새"), new FormChoice("PM", "PM · 통과철새"),
    };

    private static readonly Dictionary<string, string> MigratoryNames = new()
    {
        ["텃새"] = "RES", ["여름철새"] = "SV", ["겨울철새"] = "WV", ["통과철새"] = "PM", ["나그네새"] = "PM",
    };

    /// <summary>도래유형 글자 → 양식 코드. 코드(대소문자 무시)·이름(텃새…)·`RES :텃새`·`RES · 텃새` 꼴을 받는다.
    /// 모르는 값은 앞뒤 공백만 지우고 그대로 둔다(양식 외 — 지우지 않는다). 빈 값은 null.</summary>
    public static string? MigratoryCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        foreach (var c in MigratoryType)
            if (string.Equals(v, c.Value, StringComparison.OrdinalIgnoreCase)) return c.Value;
        if (MigratoryNames.TryGetValue(v, out var byName)) return byName;
        var head = v.Split(new[] { ':', '·', ' ', '(' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (head is not null)
            foreach (var c in MigratoryType)
                if (string.Equals(head, c.Value, StringComparison.OrdinalIgnoreCase)) return c.Value;
        return v;
    }

    // ── 조류 W열 서식유형 — 양식 W3 '1) 번식확실, 2) 번식추축, 3) 월동확실, 4) 채식 및 휴식, 5) 소리, 6) 이동'(2026-10-20 · 문제 18) ──
    /// <summary>서식유형 이름(번호 순). 2번은 양식 글자 '번식추축' 이 아니라 관리자 DB 에 쓰인 '번식추측'(169건)으로 둔다.</summary>
    public static IReadOnlyList<string> BirdHabitatTypes { get; } = new[] { "번식확실", "번식추측", "월동확실", "채식 및 휴식", "소리", "이동" };

    /// <summary>
    /// 서식유형 번호(`3` · `3)` · `3.`) → 이름. 이름은 띄어쓰기를 무시하고 양식 이름으로 맞춘다(`번식 추측`·양식 오타 `번식추축` → `번식추측`).
    /// 그 밖의 값은 앞뒤 공백만 지우고 그대로 둔다(지우지 않는다). 빈 값은 null.
    /// ⚠ 대항목(U)·세부(V)의 번호는 바꾸지 않는다 — 양식 U3·V3 의 번호 순서가 서로 달라 발주처 확인 전까지 그대로.
    /// </summary>
    public static string? BirdHabitatName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        var num = v.TrimEnd(')', '.', ' ');
        if (num.Length == 1 && num[0] is >= '1' and <= '6') return BirdHabitatTypes[num[0] - '1'];
        var flat = v.Replace(" ", string.Empty);
        if (flat == "번식추축") return "번식추측";
        foreach (var n in BirdHabitatTypes)
            if (flat == n.Replace(" ", string.Empty)) return n;
        return v;
    }

    // ── 양서파충류 Y·Z열 서식지 유형(D6) ──
    /// <summary>서식지 유형 대분류(Y열) — '①농림지역, ②산림지역, ③습지대, ④기타 지역' · 단일 · 이름.</summary>
    public static IReadOnlyList<FormChoice> AmphHabitatMajor { get; } = Names("농림지역", "산림지역", "습지대", "기타 지역");

    /// <summary>
    /// 서식지 유형 중분류(Z열) — 대분류별 항목 · 복수 · 이름을 `, ` 로 잇는다(양식 예 `묵논_경작지, 수로`).
    /// 양식의 `논(저지대 or 산림)` 처럼 둘 중 하나를 고르는 항목은 예시 꼴대로 `논_저지대`·`논_산림` 으로 나눴다.
    /// 양식 Z열의 첫 묶음 제목은 '농업지역'이지만 Y열(대분류)은 '농림지역'이다 — 대분류 글자(Y열)에 붙였다.
    /// </summary>
    public static IReadOnlyList<string> AmphHabitatMiddle(string? major) => major?.Trim() switch
    {
        "농림지역" => new[] { "논_저지대", "논_산림", "밭_저지대", "밭_산림", "과수원", "초지대", "수로", "웅덩이" },
        "산림지역" => new[] { "계곡", "사방댐", "임도", "초지대", "수로", "웅덩이" },
        "습지대" => new[] { "하천", "저수지", "묵논_경작지", "묵논_산림", "초지대", "수로", "웅덩이" },
        "기타 지역" => new[] { "해안", "시가화지역", "도로(Roadkill)" },
        _ => Array.Empty<string>(),
    };

    /// <summary>중분류를 잇는 글자(양식 예시 `묵논_경작지, 수로`).</summary>
    public const string AmphHabitatMiddleSeparator = ", ";

    // ── 어류 `어류_입력` ──
    /// <summary>채집도구(15행) — 투망/족대 · 통발 + 직접입력 · 복수 · 이름.</summary>
    public static IReadOnlyList<string> FishCollectionTool { get; } = new[] { "투망/족대", "통발" };

    /// <summary>흐름상태(채집)(16행) — 여울 / 흐름 / 소 · 복수 · 이름(B-8).</summary>
    public static IReadOnlyList<string> FlowState { get; } = new[] { "여울", "흐름", "소" };

    /// <summary>하천형태(27행) — 자연형 / 직강화 / 복합형 / 댐/보/교각 / 공사 · 복수 · 이름.</summary>
    public static IReadOnlyList<string> FishRiverForm { get; } = new[] { "자연형", "직강화", "복합형", "댐/보/교각", "공사" };

    /// <summary>흐름상태(유속)(28행) — 아주빠름 / 빠름 / 보통 / 느림 / 정체 · 단일 · 이름.</summary>
    public static IReadOnlyList<FormChoice> FishVelocity { get; } = Names("아주빠름", "빠름", "보통", "느림", "정체");

    // ── 어류 · 저서 조사불가 사유 ──
    /// <summary>
    /// 조사불가 사유(어류 양식 29행 · 저서 44행) — 접근불가 / 건천화 / 준설 / 공사중 · 단일 · 이름(§27-4 · v0.5.2).
    /// 관리자 SDSM_Core 판정 낱말과 글자까지 같다 — 고르면 등급이 '-'(미평가). 덧붙일 말은 비고 · 특이사항에.
    /// </summary>
    public static IReadOnlyList<FormChoice> UnavailableReason { get; } = Names("접근불가", "건천화", "준설", "공사중");

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
