using SDSM_Models;
using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels;

namespace BaselineGen;

/// <summary>
/// 회귀 기준 픽스처. **전부 고정값이다** — 난수·`DateTime.Now`·`DateTime.Today` 를 쓰지 않는다
/// (05_REFACTORING §0-2). 값을 바꾸면 기준 엑셀이 통째로 달라지므로 함부로 손대지 말 것.
///
/// 각 분류군에 반드시 포함하는 것
///  · 5~10행
///  · 이상치 경고가 걸리는 값 1행 (reference.json 범위를 크게 벗어난 개체수)
///  · `null`(미조사)과 `0`(실측 부재)이 섞인 행 각 1개
///  · 어류는 비정상종(DE/EF/LE/TU)이 있는 케이스 — M8 실수 나눗셈 회귀를 잡는 유일한 케이스
///  · 0종(빈 조사) 케이스 — 12_CALC_FIX 의 `0.0 · E등급` 판정 회귀 확인용
/// </summary>
internal static class Fixtures
{
    /// <summary>기준 조사일. 고정값이어야 엑셀 셀이 매번 같다.</summary>
    public static readonly DateTime SurveyDate = new(2026, 5, 14);

    /// <summary>공통 조사개황. 지점은 sites.json 의 실제 지점을 쓴다(대분류 격리 확인 겸).</summary>
    public static void FillMeta(SurveyMeta m)
    {
        m.Project = "방류하천";
        m.SurveyYear = "2026";
        m.YearChsu = "2026반기1차";
        m.SurveyDate = SurveyDate;
        m.MajorRegion = "금강";
        m.MiddleRegion = "삽교천";
        m.RiverType = "평지형";
        m.Weather = "맑음";
        m.SurveyAgency = "한국환경지리연구소";
        m.Surveyor = "기준픽스처";

        // 지점을 고르면 하천·사업장·좌표가 따라 채워진다.
        m.ResolveSiteText("St.1");
        if (m.SelectedSite is null)
        {
            // 지점 마스터가 없는 환경에서도 기준 파일이 만들어지도록 직접 채운다.
            m.Site = "곡교천1";
            m.River = "곡교천";
            m.Workplace = "온양";
            m.Lat = "36.763081";
            m.Lng = "127.090606";
        }
    }

    // ── 어류 ────────────────────────────────────────────────────────────────
    // 비정상종 DE/EF/LE/TU 합 7마리 / 총개체수 → M8 이 정수 나눗셈이면 0% 로 무너진다.
    public static void FillFish(FishEntryViewModel vm)
    {
        vm.CollectionTool = "투망/족대";
        vm.CollectionTime = "30";
        vm.CollectionFlowState = "여울,소";
        vm.RiverChasu = 3;

        vm.Bedrock = "5"; vm.Concrete = "0"; vm.Mud = "10"; vm.Sand = "25";
        vm.FineGravel = "20"; vm.Gravel = "20"; vm.SmallStone = "15"; vm.BigStone = "5";
        vm.HabitatRiverType = "자연형";
        vm.HabitatFlowState = "보통";
        vm.Note = "기준 픽스처";

        // 비정상종 — M8 회귀 케이스
        vm.DeCount = "3"; vm.EfCount = "2"; vm.LeCount = "1"; vm.TuCount = "1";

        AddFish(vm, "피라미", 120);        // 다수
        AddFish(vm, "붕어", 15);
        AddFish(vm, "참갈겨니", 40);
        AddFish(vm, "밀어", 9999);          // ⚠ 이상치 경고 유발
        AddFish(vm, "돌고기", 0);           // 0 = 실측 부재
        AddFish(vm, "버들치", null);        // null = 미조사
        AddFish(vm, "블루길", 6);           // 외래종
    }

    private static void AddFish(FishEntryViewModel vm, string ko, int? count)
    {
        var e = new FishSpeciesEntry();
        var match = vm.SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko);
        if (match is not null) e.SelectedSpecies = match; else e.SpeciesKo = ko;
        e.IndividualCount = count;
        vm.SpeciesEntries.Add(e);
    }

    /// <summary>
    /// 비정상종 비율이 **1% 를 넘는** 케이스. M8 구간은 `>1% → 0점`, `0% 초과 → 6.25점`, `0% → 12.5점` 이라
    /// 세 구간을 모두 덮어야 정수 나눗셈 회귀가 확실히 잡힌다.
    /// (정수 나눗셈이면 비정상/총개체수가 언제나 0 이 되어 12.5 점으로 붙는다.)
    /// </summary>
    public static void FillFishHighAbnormal(FishEntryViewModel vm)
    {
        vm.CollectionTool = "투망/족대";
        vm.CollectionTime = "30";
        vm.RiverChasu = 3;
        vm.Note = "비정상종 비율 1% 초과 케이스";

        vm.DeCount = "4"; vm.EfCount = "3"; vm.LeCount = "2"; vm.TuCount = "1";   // 합 10

        AddFish(vm, "피라미", 60);
        AddFish(vm, "붕어", 20);
        AddFish(vm, "참갈겨니", 15);
        AddFish(vm, "돌고기", 5);      // 총 100 → 비정상 10/100 = 10% → M8 0점
    }

    /// <summary>0종(빈 조사) — 점수 0 · 등급 E 판정 회귀 확인용.</summary>
    public static void FillFishNoSpecies(FishEntryViewModel vm)
    {
        vm.CollectionTool = "투망/족대";
        vm.RiverChasu = 3;
        vm.NoSpeciesDeclared = true;
        vm.Note = "출현종 없음(0종) 선언";
    }

    // ── 저서동물 ────────────────────────────────────────────────────────────
    public static void FillBenthos(BenthosEntryViewModel vm)
    {
        vm.Surbernet30 = "3"; vm.Surbernet50 = "0"; vm.Dredge = "0"; vm.Ekman = "0";
        vm.Watershed = "농경지"; vm.PollutionSource = "없음"; vm.CanopyCover = "20";
        vm.Floodplain = "있음"; vm.LeveeLeft = "자연"; vm.LeveeRight = "사석";
        vm.Bedrock = "0"; vm.Concrete = "0"; vm.Mud = "20"; vm.Sand = "20";
        vm.FineGravel = "20"; vm.Gravel = "20"; vm.SmallStone = "15"; vm.BigStone = "5";
        vm.HabitatRiverType = "자연형";
        vm.RiverWidth = "25"; vm.WaterWidth = "12"; vm.AverageDepth = "35";
        vm.AverageVelocity = "0.4"; vm.AirTemperature = "24.5"; vm.WaterTemperature = "19.2";
        vm.FlowState = "보통"; vm.Transparency = "양호"; vm.Smell = "없음";
        vm.Note = "기준 픽스처";

        AddBenthos(vm, "깔따구류", 250);
        AddBenthos(vm, "물벌레", 30);
        AddBenthos(vm, "실지렁이", 5000);   // ⚠ 이상치 경고 유발
        AddBenthos(vm, "옆새우류", 12);
        AddBenthos(vm, "잠자리류", 0);       // 0 = 실측 부재
        AddBenthos(vm, "하루살이류", null);  // null = 미조사
    }

    private static void AddBenthos(BenthosEntryViewModel vm, string ko, double? count)
    {
        var e = new BenthosSpeciesEntry();
        var match = vm.SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko);
        if (match is not null) e.SelectedSpecies = match; else e.SpeciesKo = ko;
        e.IndividualCount = count;
        vm.SpeciesEntries.Add(e);
    }

    // ── 조류 ────────────────────────────────────────────────────────────────
    public static void FillBird(BirdEntryViewModel vm)
    {
        AddBird(vm, "붉은머리오목눈이", 42, "텃새", "관목", "덤불", "하천");
        AddBird(vm, "흰뺨검둥오리", 18, "텃새", "수면", "개방수면", "하천");
        AddBird(vm, "왜가리", 3, "텃새", "수변", "얕은물", "하천");
        AddBird(vm, "황조롱이", 1, "텃새", "상공", "비행", "하천");   // 천연기념물
        AddBird(vm, "까치", 0, "텃새", "수변", "교목", "하천");        // 0 = 실측 부재
        AddBird(vm, "물총새", null, "여름철새", "수변", "횃대", "하천"); // null = 미조사
    }

    private static void AddBird(BirdEntryViewModel vm, string ko, int? count,
                                string migratory, string cat, string detail, string habitat)
    {
        var e = new BirdEntry { SpeciesKo = ko, IndividualCount = count, MigratoryType = migratory,
                                Category = cat, CategoryDetail = detail, HabitatType = habitat,
                                Lat = 36.763081, Lng = 127.090606, Feature = "-", Note = "-" };
        e.ApplySpecies(vm.SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
        vm.Entries.Add(e);
    }

    // ── 포유류 ──────────────────────────────────────────────────────────────
    // 흔적 12종 중 일부만 채우고 나머지는 null 로 둔다(미조사 vs 0 구분 회귀).
    public static void FillMammal(MammalEntryViewModel vm)
    {
        AddMammal(vm, "고라니", trace1: 2, trace2: 5, trace3: null, trace4: 0);
        AddMammal(vm, "너구리", trace1: 1, trace2: null, trace3: 3, trace4: null);
        AddMammal(vm, "수달", trace1: null, trace2: 2, trace3: null, trace4: 1);   // 멸종위기
        AddMammal(vm, "멧토끼", trace1: 0, trace2: 0, trace3: null, trace4: null); // 전부 0/null
        AddMammal(vm, "두더지", trace1: 4, trace2: null, trace3: null, trace4: null);
    }

    private static void AddMammal(MammalEntryViewModel vm, string ko,
                                  int? trace1, int? trace2, int? trace3, int? trace4)
    {
        var e = new MammalEntry
        {
            SpeciesKo = ko, ObservationSite = "하천변",
            Trace1 = trace1, Trace2 = trace2, Trace3 = trace3, Trace4 = trace4,
            Lat = 36.763081, Lng = 127.090606, Feature = "-", Note = "-"
        };
        e.ApplySpecies(vm.SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
        vm.Entries.Add(e);
    }

    // ── 양서파충류 ──────────────────────────────────────────────────────────
    public static void FillAmphibian(AmphibianReptileEntryViewModel vm)
    {
        AddAmphibian(vm, "참개구리", "양서류", "무미목", t1: 12, t2: 3, t3: null);
        AddAmphibian(vm, "청개구리", "양서류", "무미목", t1: 8, t2: null, t3: 0);
        AddAmphibian(vm, "금개구리", "양서류", "무미목", t1: 1, t2: null, t3: null);   // 멸종위기II
        AddAmphibian(vm, "붉은귀거북", "파충류", "거북목", t1: 2, t2: 0, t3: null);     // 생태계교란
        AddAmphibian(vm, "줄장지뱀", "파충류", "뱀목", t1: null, t2: 1, t3: null);
    }

    private static void AddAmphibian(AmphibianReptileEntryViewModel vm, string ko,
                                     string major, string middle, int? t1, int? t2, int? t3)
    {
        var e = new AmphibianReptileEntry
        {
            SpeciesKo = ko, MajorCategory = major, MiddleCategory = middle,
            Trace1 = t1, Trace2 = t2, Trace3 = t3,
            Lat = 36.763081, Lng = 127.090606, Feature = "-", Note = "-"
        };
        e.ApplySpecies(vm.SpeciesListSource.FirstOrDefault(s => s.SpeciesKo == ko));
        vm.Entries.Add(e);
    }

    // ── 수질 ────────────────────────────────────────────────────────────────
    // 등급 8종은 서로 다른 등급이 나오도록 값을 흩뜨린다. 미측정(null)도 섞는다.
    public static void FillWaterQuality(WaterQualityEntryViewModel vm)
    {
        vm.PH = "7.4";        // 정상
        vm.Bod = "1.8";       // 좋음
        vm.Cod = "5.2";
        vm.Toc = "3.1";
        vm.Ss = "12";
        vm.Dox = "9.6";
        vm.Tp = "0.045";
        vm.EColi = "980";
        vm.Ecotoxicity = "0";          // 0 = 실측 부재
        vm.TN = "2.35";
        vm.EC = "180";
        vm.Cl = null;                  // 미측정
        vm.SO42 = "22.4";
        vm.Cu = "0.005";
        vm.Zn = null;                  // 미측정
        vm.Cr = "0";
        vm.Turbidity = "8.2";
        vm.Chla = "6.4";
        vm.WaterTemperature = "19.2";
        vm.WaterDepth = "35";
        vm.FlowVelocity = "40";
        vm.FlowSec = "1.68";
        vm.FlowDay = "145152";
    }

    // ── 서식·수변환경 ───────────────────────────────────────────────────────
    // 좌·우안이 갈리는 항목과 미입력 항목을 섞는다.
    public static void FillHabitat(HabitatWaterEdgeEntryViewModel vm)
    {
        vm.S1 = Pick(vm.Opt1, 5);
        vm.B2L = Pick(vm.Opt2, 15); vm.B2R = Pick(vm.Opt2, 10);
        vm.S3 = Pick(vm.Opt3, 25);
        vm.S4 = Pick(vm.Opt4, 3);
        vm.B5L = Pick(vm.Opt5, 10); vm.B5R = Pick(vm.Opt5, 5);
        vm.B6L = Pick(vm.Opt6, 10); vm.B6R = null;      // 한쪽만 입력
        vm.S7 = Pick(vm.Opt7, 20);
        vm.S8 = Pick(vm.Opt8, 30);
        vm.B9L = Pick(vm.Opt9, 3);  vm.B9R = Pick(vm.Opt9, 1);
        vm.B10L = Pick(vm.Opt10, 5); vm.B10R = Pick(vm.Opt10, 3);
        vm.Note = "기준 픽스처";
    }

    /// <summary>
    /// 평가항목 **10개를 전부 0점**으로 고른 케이스 — 콘크리트로 직강화된 도시하천이 실제로 이렇게 나온다.
    ///
    /// 기대값: 합계 <b>0</b> · 평가점수 <b>0</b> · 등급 <b>E</b>.
    /// 종전 판정 기준(<c>total != 0</c>)이면 미입력으로 뭉개져 점수·등급이 `-` 로 빠졌다
    /// (docs\_excel_formula_audit.md §5-2). 이 케이스가 없으면 그 기준이 되살아나도 아무도 모른다 —
    /// 어류 M8 케이스를 남겨 둔 것과 같은 이유다.
    ///
    /// ⚠ <b>전부 미선택</b>(15개가 전부 null)과는 다르다. 그쪽은 지금도 `-` 가 맞다.
    /// </summary>
    public static void FillHabitatAllZero(HabitatWaterEdgeEntryViewModel vm)
    {
        vm.S1 = Pick(vm.Opt1, 0);                                 // 없음
        vm.B2L = Pick(vm.Opt2, 0); vm.B2R = Pick(vm.Opt2, 0);     // 인공 직강화
        vm.S3 = Pick(vm.Opt3, 0);                                 // 건천화
        vm.S4 = Pick(vm.Opt4, 0);                                 // ≤ 0.5
        vm.B5L = Pick(vm.Opt5, 0); vm.B5R = Pick(vm.Opt5, 0);     // 콘크리트(불투수)
        vm.B6L = Pick(vm.Opt6, 0); vm.B6R = Pick(vm.Opt6, 0);     // 하안블록/콘크리트
        vm.S7 = Pick(vm.Opt7, 0);                                 // 콘크리트
        vm.S8 = Pick(vm.Opt8, 0);                                 // 어도없음/파손
        vm.B9L = Pick(vm.Opt9, 0); vm.B9R = Pick(vm.Opt9, 0);     // 주차장/불투수
        vm.B10L = Pick(vm.Opt10, 0); vm.B10R = Pick(vm.Opt10, 0); // 1/2↑ 시가지
        vm.Note = "평가항목 10개 전부 0점 — 합계 0 · 점수 0 · 등급 E 여야 한다";
    }

    private static HriOption? Pick(HriOption[] options, double score)
        => options.FirstOrDefault(o => o.Score == score);
}
