using System.IO;
using BaselineGen;
using SDSM_Surveyor_App.Export;
using SDSM_Surveyor_App.Models;
using SDSM_Surveyor_App.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
// 회귀 기준 엑셀 생성기 (05_REFACTORING §0-2 · R0)
//
// 고정 픽스처로 7개 분류군을 채우고 14개 exporter 를 직접 호출해 `docs\_baseline\` 에 저장한다.
// 리팩토링 후 같은 픽스처로 다시 만들어 **셀 단위**로 대조한다(`tools\baseline_diff.py`).
//
// ⚠ 실데이터 보호(§0-3)
//   · `%AppData%\SDSM_Surveyor\` 를 읽지도 쓰지도 않는다 — 제공자를 저장소 전용으로 갈아끼운다.
//   · `SDSMDB.sqlite` 를 건드리지 않는다.
//   · 출력은 저장소 안(`docs\_baseline\`)에만 쓴다.
// ─────────────────────────────────────────────────────────────────────────────

var repoRoot = FindRepoRoot();
var appDir = Path.Combine(repoRoot, "SDSM_Surveyor_App");

// 기본 출력은 저장소 안 `docs\_baseline\`. 리팩토링 후 대조할 때는 --out 으로 다른 폴더에 뽑아
// `toolsaseline_diff.py` 로 셀 단위 비교한다.
var outDir = Path.Combine(repoRoot, "docs", "_baseline");
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] is "--out" or "-o") outDir = Path.GetFullPath(args[i + 1]);
Directory.CreateDirectory(outDir);

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine($"저장소 : {repoRoot}");
Console.WriteLine($"출력   : {outDir}");
Console.WriteLine("※ AppData·DB 를 읽지도 쓰지도 않는다.\n");

var species = new RepoSpeciesProvider(Path.Combine(appDir, "species.json"));
var reference = new RepoReferenceProvider(Path.Combine(appDir, "reference.json"));
var sites = new RepoSiteProvider(Path.Combine(appDir, "sites.json"));
var noSession = new NoSessionService();

Console.WriteLine($"종목록 {species.Version ?? "(없음)"} · 기준자료 {reference.Version ?? "(없음)"} · 지점 {sites.Version ?? "(없음)"}({sites.All.Count}건)\n");

// ── 본 케이스 : 7개 분류군 전부 채운 하나의 조사 ──────────────────────────────
var meta = new SurveyMeta(sites);
Fixtures.FillMeta(meta);

var fish = new FishEntryViewModel(species, noSession, reference, meta);
var benthos = new BenthosEntryViewModel(species, noSession, reference, meta);
var bird = new BirdEntryViewModel(species, noSession, meta);
var mammal = new MammalEntryViewModel(species, noSession, meta);
var amphibian = new AmphibianReptileEntryViewModel(species, noSession, meta);
var habitat = new HabitatWaterEdgeEntryViewModel(noSession, meta);
var water = new WaterQualityEntryViewModel(noSession, meta);

Fixtures.FillFish(fish);
Fixtures.FillBenthos(benthos);
Fixtures.FillBird(bird);
Fixtures.FillMammal(mammal);
Fixtures.FillAmphibian(amphibian);
Fixtures.FillWaterQuality(water);
Fixtures.FillHabitat(habitat);

Console.WriteLine("=== 산출값 (리팩토링 후에도 같아야 한다) ===");
Console.WriteLine($"  어류      FAI {fish.FaiScore} · {fish.FaiGrade} · 경고 {fish.WarningCount}건");
Console.WriteLine($"  저서동물  BMI {benthos.BmiScore} · {benthos.BmiGrade} · 경고 {benthos.WarningCount}건");
Console.WriteLine($"  서식수변  HRI {habitat.ScoreText} · {habitat.GradeText}");
Console.WriteLine($"  수질      pH {water.PhGradeText} · BOD {water.BodGradeText} · DO {water.DoGradeText} · T-P {water.TpGradeText}");
Console.WriteLine($"  조류 {bird.Entries.Count}행 · 포유류 {mammal.Entries.Count}행 · 양서파충류 {amphibian.Entries.Count}행\n");

int n = 0;
void Write(string name, Action<string> writer)
{
    var path = Path.Combine(outDir, name);
    writer(path);
    n++;
    Console.WriteLine($"  {n,2}. {name}  ({new FileInfo(path).Length:N0} bytes)");
}

Console.WriteLine("=== 보고서용 7종 ===");
Write("fish_report.xlsx", p => FishReportExporter.Write(fish, p));
Write("benthos_report.xlsx", p => BenthosReportExporter.Write(benthos, p));
Write("bird_report.xlsx", p => BirdReportExporter.Write(bird, p));
Write("mammal_report.xlsx", p => MammalReportExporter.Write(mammal, p));
Write("amphibian_report.xlsx", p => AmphibianReptileReportExporter.Write(amphibian, p));
Write("water_report.xlsx", p => WaterQualityReportExporter.Write(water, p));
Write("habitat_report.xlsx", p => HabitatWaterEdgeReportExporter.Write(habitat, p));

Console.WriteLine("=== 일괄입력용 7종 ===");
Write("fish_bulk.xlsx", p => FishExcelExporter.Write(fish, p));
Write("benthos_bulk.xlsx", p => BenthosExcelExporter.Write(benthos, p));
Write("bird_bulk.xlsx", p => BirdExcelExporter.Write(bird, p));
Write("mammal_bulk.xlsx", p => MammalExcelExporter.Write(mammal, p));
Write("amphibian_bulk.xlsx", p => AmphibianReptileExcelExporter.Write(amphibian, p));
Write("water_bulk.xlsx", p => WaterQualityExcelExporter.Write(water, p));
Write("habitat_bulk.xlsx", p => HabitatWaterEdgeExcelExporter.Write(habitat, p));

// ── M8 상단 구간 : 비정상종 비율 > 1% → M8 0점 (12_CALC_FIX 회귀) ───────────
Console.WriteLine("\n=== M8 상단 구간 케이스 ===");
var metaM8 = new SurveyMeta(sites);
Fixtures.FillMeta(metaM8);
metaM8.YearChsu = "2026반기1차_M8";

var fishM8 = new FishEntryViewModel(species, noSession, reference, metaM8);
Fixtures.FillFishHighAbnormal(fishM8);
Console.WriteLine($"  어류 M8   FAI {fishM8.FaiScore} · {fishM8.FaiGrade}  (비정상 10/100 = 10% → M8 0점)");
Write("fish_m8_report.xlsx", p => FishReportExporter.Write(fishM8, p));
Write("fish_m8_bulk.xlsx", p => FishExcelExporter.Write(fishM8, p));

// ── 0종 케이스 : 어류 빈 조사 → 점수 0 · 등급 E (12_CALC_FIX 회귀) ───────────
Console.WriteLine("\n=== 0종(빈 조사) 케이스 ===");
var metaZero = new SurveyMeta(sites);
Fixtures.FillMeta(metaZero);
metaZero.YearChsu = "2026반기1차_0종";

var fishZero = new FishEntryViewModel(species, noSession, reference, metaZero);
Fixtures.FillFishNoSpecies(fishZero);
Console.WriteLine($"  어류 0종  FAI {fishZero.FaiScore} · {fishZero.FaiGrade}  (0 · E 여야 한다)");
Write("fish_zero_report.xlsx", p => FishReportExporter.Write(fishZero, p));
Write("fish_zero_bulk.xlsx", p => FishExcelExporter.Write(fishZero, p));

// ── 서식수변 전부 0점 : 합계 0 · 점수 0 · 등급 E (_excel_formula_audit §5-5 회귀) ──────
// 콘크리트로 직강화된 도시하천은 10개 항목이 전부 0점으로 나온다. 종전 `total != 0` 판정이면
// 미입력으로 뭉개져 `-` 가 됐다 — 가장 훼손된 하천이 평가를 못 받는 구조였다.
Console.WriteLine("\n=== 서식수변 전부 0점 케이스 ===");
var metaHri = new SurveyMeta(sites);
Fixtures.FillMeta(metaHri);
metaHri.YearChsu = "2026반기1차_HRI0";

var habitatZero = new HabitatWaterEdgeEntryViewModel(noSession, metaHri);
Fixtures.FillHabitatAllZero(habitatZero);
var hriDetail = habitatZero.ComputeDetail();
Console.WriteLine($"  서식수변  합계 {hriDetail.Total} · HRI {habitatZero.ScoreText} · {habitatZero.GradeText}  (0 · 0.0 · E 여야 한다)");
Write("habitat_zero_report.xlsx", p => HabitatWaterEdgeReportExporter.Write(habitatZero, p));
Write("habitat_zero_bulk.xlsx", p => HabitatWaterEdgeExcelExporter.Write(habitatZero, p));

Console.WriteLine($"\n기준 엑셀 {n}개 생성 완료 → {outDir}");
return 0;

// 저장소 루트(= SDSM_Surveyor) 를 찾는다. 실행 폴더는 bin 깊숙이 있다.
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "SDSM_Surveyor_App"))
            && Directory.Exists(Path.Combine(dir.FullName, "docs")))
            return dir.FullName;
        dir = dir.Parent;
    }
    throw new DirectoryNotFoundException("SDSM_Surveyor 저장소 루트를 찾지 못했습니다.");
}
