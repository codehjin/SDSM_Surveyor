# CLAUDE.md — 조사자 앱 개발·아키텍처 가이드 (SDSM_Surveyor)

> SDSM 조사자용 데스크톱 앱(.NET 8 WPF). 현장 조사자가 오프라인으로 자료를 입력·검증하고,
> 관리자 표준 엑셀로 내보내 제출한다. UI 규칙은 `design.md` 참조.
> 관리자 앱과 검증·계산 로직을 **동일**하게 유지하는 것이 최우선.

---

## 0. AI 협업 원칙
- 이 저장소의 **기존 패턴을 최우선**으로 따른다. 새 패턴·라이브러리는 먼저 제안·합의.
- 작성자는 생태 도메인 전문가, 코딩은 보조. 변경 시 *무엇을·왜* 한국어로 간단히 설명.
- 생태 계산(지수·등급)은 **관리자 로직이 절대 기준**. 임의 개선 금지, 관리자 `GB` 정의 그대로 이식.
- 파괴적 변경(대량 삭제·스키마 변경)은 실행 전 고지.

---

## 1. 기술 스택 · 프로젝트 구조

| 항목 | 값 |
|------|-----|
| 런타임 | .NET 8 (`net8.0-windows`, WPF) |
| UI | Telerik UI for WPF `AllControls.Xaml` 2026.3.1007 (Windows11 테마) + `Telerik.Licensing` 1.9.3 |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection + Scrutor |
| 아이콘 | MahApps.Metro.IconPacks |
| 저장 | 로컬 JSON(AppData) — `System.Text.Json` |
| 공유 | `SDSM_Models`(관리자와 동일 POCO) 참조 |

**폴더 구조 (`SDSM_Surveyor_App/`)**
```
App.xaml(.cs)          진입점(Windows11 테마 + DI)
InjectableServices/    DI 마커 인터페이스
Helpers/               SpeciesNameRank(후보 순서 같음→앞부분→포함 · D1) · GridSpeciesCommit · GridRowPicker 등 (초성 검색은 SDSM_Core.Helpers.ChosungHelper)
Behaviors/             ChosungFilteringBehavior · ExactNameHighlightBehavior(그리드 국명 칸 Enter = 똑같은 이름만 · D1)
                       · IntegerInput · NestedWheelScroll(바깥 스크롤 휠 넘김)
                       · DropDownWheelGuard(목록 열린 채 휠 = 목록만 · 밖이면 닫기 · 닫힌 콤보 휠로 값 안 바뀜 · 앱 전체)
Models/                *Entry / *SpeciesEntry (화면 행 모델) · FormChoices(양식 선택지 출처 · C-4 · D5~D7)
                       · SiteKeys(세션 키·파일명의 지점 자리 · 조사연도 · D4)
Data/                  ISpeciesListProvider · SpeciesListProvider(시드)
                       · ISiteListProvider · SiteListProvider(지점 마스터) · SiteOrdering(정렬·St.번호 · C-7) · RegionTable(권역 · C-6)
                       · ISessionStore · SessionStore(세션 파일) · SessionService(화면↔세션) · SessionArchive(자료함 zip · C-9)
                       · ITaxonSession(분류군별 저장/복원 규약)
Messengers/            NotifyMessage (토스트)
ViewModels/            분류군별 EntryViewModel (+ Sessions/*.Session.cs 저장·복원)
Export/                분류군별 exporter 14종 · ExcelStyle · SiteColumns(구분 4열) · BulkTemplate(일괄입력 내장본)
                       · BatchExporter · CombinedReport(여러 지점 일괄 출력 · C-8)
Views/Windows/         MainSurveyWindow · SyncWindow · SessionBrowserWindow(자료함) · ChoiceDialog(버튼=동작 확인창)
Views/UserControls/    분류군별 EntryControl · FillPanel(화면 전체 스크롤 · C-3) · MultiChoiceBox · SingleChoiceBox
```
> 생태 계산은 **`..\SDSM\SDSM_Core\Ecology\` 로 이관 완료**(2026-08-21 · R1).
> 관리자 `GB` 도 같은 코드를 호출한다. 계산식을 고치면 **양쪽이 함께 바뀐다.**
> 향후 `Helpers`/`Behaviors` 도 `SDSM_Core` 로 이관 예정(R3).

---

## 2. MVVM 규칙 (관리자와 동일)
- ViewModel: `ObservableObject` 상속 + `partial class`. 상태는 `[ObservableProperty]`, 명령은 `[RelayCommand]`(+ `CanExecute = nameof(CanXxx)`).
- View는 코드비하인드에서 **DI로 VM 주입**: `DataContext = App.Current.Services.GetRequiredService<XxxViewModel>();`
- DI 등록은 **마커 인터페이스 자동 스캔**(Scrutor): 화면/VM/Provider에 `ITransientService` 또는 `ISingletonService` 부여.
- **7개 분류군 EntryViewModel과 `SurveyMeta` 는 싱글턴이다.** 세션이 7개 탭 전체를 한 번에 저장·복원해야 하고,
  조사개황은 세션당 한 벌이기 때문이다. 그래서 어느 탭에서 지점을 적어도 나머지 6개 탭에 그대로 보인다.
- `SessionService` 는 7개 VM을 **`IServiceProvider`로 늦게** 가져온다. 각 VM이 `ISessionService`를 주입받으므로
  생성자 주입은 순환 의존이 되어 앱이 아예 시작되지 않는다.
- 컬렉션은 `RadObservableCollection<T>`. 토스트는 `WeakReferenceMessenger` + `NotifyMessage`.

---

## 3. 화면 패턴 3종 (7개 분류군)

| 패턴 | 분류군 | 구성 | 실시간 산출 |
|------|--------|------|-------------|
| **A. 종목록형** | 어류, 저서동물 | 기본정보 + 종 그리드(초성검색·개체수) + 지수 패널 | FAI / DI·H'·R1·J'·BMI |
| **B. 관찰나열형** | 조류, 포유류, 양서파충류 | 기본정보 + 관찰 그리드(종·개체수·흔적·분류) | 총 종수/개체수 |
| **C. 항목측정형** | 수질, 서식수변 | 기본정보 + 항목 폼 | 항목별 등급 / 평가점수·등급 |

새 분류군/화면은 위 패턴 중 하나를 복제해 만든다.

---

## 4. 생태 계산 — 관리자와 동일해야 함 (핵심)

> **확인된 사실:** 관리자 앱은 엑셀에 적힌 지수 "값"을 쓰지 않고, **원자료(종별 개체수 + 종속성 + 하천차수 등)로 재계산**한다(BulkFish:379, BulkBenthos:362~366, Editor에서도). 따라서 조사자 앱의 **실시간 재계산 방식이 관리자와 일치**한다.

- **계산 코드는 이제 한 곳뿐이다** — `..\SDSM\SDSM_Core\Ecology\`. 관리자 `GB` 는 이 코드를 호출하는 어댑터다.
  고치기 전후로 `SDSM	ools\CalcParity` 대조와 `tools\BaselineGen` 기준 파일 대조를 **둘 다** 돌린다.
- 계산기 위치(`SDSM_Core\Ecology\`)와 관리자 원본:
  - `EcologyCalculator.CalculateFai` ← `GB.GetFAI` (M1~M8, 등급 A~E, 조사불가 "-")
  - `BenthosCalculator.GetDI/GetH/GetR1/GetJ/GetBMI/GetRankScorer` ← `GB.*`
  - `WaterQualityCalculator.*Grade` ← `GB.Get*Grade` (pH/BOD/COD/TOC/SS/DO/TP/대장균)
  - `HabitatEvaluator.Evaluate` ← `GetGrade`(10항목 합÷2, A~E, 접근불가 "-")
- 규칙(반드시 준수):
  - **결측치(null) vs 0 엄격 구분.** 개체수/측정값 미입력은 `null`, 실측 부재만 `0`. 집계는 `?? 0` 후 `> 0` 필터.
  - 반올림 `MidpointRounding.AwayFromZero`, NaN/Infinity·분모 0 가드, 조사불가 사유(`접근불가/건천화/준설/공사중`) 시 등급 `"-"`.
  - 지수 입력에 쓰는 **종속성(길드·오탁치·지표가중치)은 종목록에서 자동 상속**한다. → 지수가 관리자와 일치하려면 **종목록(기준자료) 버전이 관리자와 동일**해야 한다.
- 계산식 변경 금지. 고쳐야 하면 `SDSM_Core` 한 곳만 고치고 회귀 대조를 돌린다(양쪽이 동시에 바뀐다).

---

## 5. 종명 · 초성 검색
- 종명 입력은 `RadAutoCompleteBox` + `ChosungFilteringBehavior`(초성 `ㅋㅈㄴ` + 부분일치). Telerik `IFilteringBehavior.FindMatchingItems` 구현(검증됨).
- **국명 칸 Enter(2026-10-18 결정 D1)** — ① 친 글자와 똑같은 이름 → 그 종 ② 방향키·마우스로 고른 후보 → 그 종 ③ 그 밖 → 친 글자 그대로(목록 밖 국명 경고).
  첫 후보로 자동 치환하지 않는다(`ExactNameHighlightBehavior`). 후보 순서는 같은 이름 → 앞부분 일치 → 포함(`SpeciesNameRank` · 빠른추가도 같음).
- **종명은 원문 그대로**(`Trim`만, 대소문자·띄어쓰기 보존; CLAUDE 규칙). 길드/보호종 코드성 값만 정규화.
- 종목록형은 모델 기반 목록(`FishSpeciesList`/`BenthosSpeciesList`), **관찰형은 `ObservedSpecies`**(국명·학명·목·과·보호종·교란종).
- `SpeciesListProvider`는 `species.json`을 읽는다(AppData 교체본 → 실행폴더 번들 순). 파일이 없을 때만 최소 시드로 대체.
  - 어류·저서동물 = 관리자 마스터 내보내기, **관찰형 3종 = 국가생물종목록**(`tools\build_species_json.py`로 생성).
  - 구형 `species.json`(관찰형이 국명 문자열 배열)도 하위 호환으로 읽힌다.
- 관찰형은 종 선택·붙여넣기 시 학명·목·과가 자동으로 채워진다. **학명은 종목록대로(2026-10-18 결정 D2)** —
  종목록 국명이면 학명 칸이 잠기고, 붙여넣기·직접 수정·예전 세션 불러오기에서 다른 학명이 들어오면 종목록 학명으로 바꾸고
  "학명 N건을 종목록대로 바꿨습니다" 로 알린다(`ScientificNameRule`). **목록 밖 국명만 학명을 직접 입력**한다. 목·과는 덮어쓸 수 있다.
  보호종(멸종위기·천연기념물)과 **생태계교란생물은 의미가 반대이므로 표기·색을 분리**한다.

---

## 6. 입력 범위 · 오프라인 저장 · 내보내기 (정책)
- **입력 범위:** 관리자 그리드에서 **자동계산 항목을 제외한 모든 항목**을 조사자가 입력(기존 분류군별 엑셀 양식 기준 아님).
- **오프라인 저장(세션 단위):** `ISessionStore` → `%AppData%\SDSM_Surveyor\sessions\{sessionId}.json` + `index.json`.
  `sessionId` = `대분류_연도차수_지점`. **세션 하나가 공통 조사개황 + 7개 분류군 자료를 함께 담는다** —
  어느 탭에서 [임시 저장]을 눌러도 세션 전체가 저장되므로 지점을 옮겨도 이전 자료가 사라지지 않는다.
  구버전 `drafts\{taxon}.json` 은 첫 실행 시 세션 하나로 편입된다(1회, 표식 파일로 재실행 방지).
  실행폴더가 아닌 AppData를 쓰는 이유는 전과 같다(업데이트·재설치에도 데이터 보존).
  ⚠ 이 폴더는 **사용자 실자료**다. 검증 스크립트가 폴더를 옮기거나 지우지 않게 할 것(90_TECH_NOTES §8).
- **엑셀 내보내기(2종, 구현 완료):**
  - ① 일괄입력용 — 관리자 Import에 필요한 **필수자료만**.
  - ② 열람·보관용 — **모든 수식 포함**.
  - 생물종별 개별 엑셀 지원. 내보내기 엑셀에 구분 컬럼: 프로젝트명·사업장·과업대표하천·지점명(= `SiteDivision`).
- 관찰형 3종(조류·포유류·양서파충류) 보고서 조사개황에는 **하천유형 행이 없다**(그 양식에 하천유형이 없음 · 2026-10-18 (3) 결정 G2).
- **하천유형은 두 칸(2026-10-18 (2) 결정 E1)** — 어류·저서 `RiverType`(산간형/평지형/강) · 수질·서식수변 `RiverTypeWater`(본류/지류/기타).
  조사개황에는 보고 있는 분류군 탭에 맞는 칸 하나만 보이고(조류·포유류·양서파충류는 숨김) 탭을 바꿔도 비우지 않는다. [새 지점]에서는 둘 다 비운다.
  각 양식 파일(일괄입력용·보고서)에 각자 값을 낸다. 세션 조사개황에 `RiverTypeWater` 칸 하나를 더했다 — **세션 형식 원칙의 유일한 예외(추가만)**.
  예전 세션은 `RiverType` 이 본류/지류/기타면 새 칸으로 옮겨 읽고, 그 밖은 원래 칸에 둔다(어느 쪽 선택지에도 없으면 '양식에 없는 값').

---

## 7. 조사지점 (구현 완료 · 2026-08-21)
- 대분류 **방류하천 / 생태현황** 으로 지점 목록이 걸러진다.
  ⚠ **두 대분류는 절대 섞어 보여주지 않는다.** 같은 이름(`오산천1` 등)이 양쪽에 있고 **3.6~11.5km 떨어진 다른 장소**다
  (`..\SDSM\docs\_ecostatus_sites_extracted.md` §8-2). 그래서:
  · 대분류가 비면 지점 목록은 **빈 목록**이고 드롭다운이 **잠긴다**
  · 대분류를 바꾸면 **선택 지점·하천·사업장·좌표를 즉시 비운다**
  · `Resolve()` 는 **그 대분류 안에서만** 찾는다(다른 대분류로 넘어가지 않는다)
- **생태현황 지점(2026-10-18 결정 D4)** — `sites.json` 에 149곳(어류 118 · 조류 28 · 포유류 3, `Taxa` = 기록된 분류군).
  원천은 `..\SDSM\docs\_ecostatus_sites_extracted.md` §3(§8·§9 판정) · `tools/build_sites_json.py --ecostatus`. 좌표·하천명 이상은 `Desc` 의 `보완 필요: …`.
  같은 이름이 해마다 다른 장소면 `ValidFrom`/`ValidTo` 로 나눈다(진위천1~5·오산천2) — 목록은 **조사연도(없으면 연도차수 앞 네 자리)로 거르고**, 연도를 모르면 `[2023~2024]` 구간을 병기한다.
  생태현황은 **목록 밖 지점을 직접 입력**한다(지점명·하천명·좌표 손입력, 권역은 표에 없으면 목록에서 고름, '목록 밖 지점' 표시).
  지점명이 비어도 하천명(대상지명)이 있으면 저장된다 — 세션 키·파일명의 지점 자리 = `하천명(지점명 없음)`(`SiteKeys`). **방류하천은 목록에서만** 고른다.
- **생태현황 지점 목록은 보고 있는 분류군 지점만(E3)** — `Taxa` 로 거르고 이름 옆 분류군 표시는 없다. 양서파충류는 등록 지점이 없어 직접 입력만(안내 문구).
  탭을 바꿔도 이미 고른 지점은 지우지 않는다(그 분류군 목록에 없으면 `천안01 (조류 지점)` 처럼 표시만).
- **지점 칸 Enter·Tab(E2 · D1 과 같은 규칙)** — ① 같은 이름(St.번호·옛 표기 포함) → 그 지점 ② 방향키·마우스로 고른 후보 → 그 지점
  ③ 그 밖에는 고르지 않는다(걸러진 첫 후보 자동 선택 금지). 방류하천은 '목록에서 고르세요' 안내, 생태현황은 '목록 밖 지점으로 입력됩니다' 확인([아니요]면 칸으로 돌아감).
  St.번호 후보가 여럿이면 자동으로 고르지 않는다(C-7 그대로). 치는 동안 콤보가 스스로 고른 항목은 조사개황에 넘기지 않는다(`SurveyMeta.HoldSiteSelection`).
  **↓ 는 친 글자로 거른 후보(같음 → 앞부분 → 포함)를 펼쳐 그 안에서 움직인다(G1)** — 움직이는 동안은 조사개황에 넣지 않고 Enter·Tab 으로 확정, Esc 는 목록만 닫음. 글자가 없으면 목록 전부.
  **목록이 열려 있으면(▼·↓) 치는 대로 다시 거른다(G3)** — 강조는 두지 않는다(강조는 ↑↓·마우스로만 · 거른 채 Enter 는 E2 그대로). 닫힌 채 치면 열지 않는다.
- **[새 지점](C-2)은 압정 6칸을 압정이 꺼져 있어도 모두 유지한다(2026-10-18 (2) 결정 E4 · 설계)** — 지점 칸(지점·하천·사업장·좌표·권역)·하천유형 두 칸·7개 분류군 자료만 비운다.
  압정은 앱을 새로 켤 때 칸을 채우는 기능이고, [새 지점]은 같은 조사의 다음 지점을 잇는 기능이라 둘을 묶지 않는다.
- 조사자는 방류하천에서 **등록된 지점만 선택**(`sites.json` 기반 드롭다운). `ST1`/`St.1`/`st 1` 입력 → **`곡교천1`로 정규화** 저장, 화면에는 `곡교천1 (St.1)` 병기.
  미등록 값을 치면 지우지 않고 경고만 띄운다(오타를 조사자가 알아채도록).
- 지점을 고르면 **하천명·사업장·좌표**가 자동으로 채워지고 [지도] 버튼으로 기본 브라우저에서 위치를 연다.
- `sites.json` 은 제출 엑셀의 `조사지점` 시트에서 만든다(`tools/build_sites_json.py`).
  ⚠ **열 배치가 하천마다 다르므로 고정 열 인덱스를 쓰지 말 것** — 변환기는 2행 머리글 텍스트로 열을 찾는다.
- 로드 규칙은 `species.json` 과 동일(AppData 교체본 → 실행폴더 번들). 공유 모델은 `SDSM_Models/SiteCatalog.cs`.
- 관리자 지점 관리 화면 구현됨(2026-08-21) — 관리자 앱 [설정] → [조사지점 관리]. 등록·수정·삭제·병합·사진·연도구간 + `sites.json` 내보내기.
- 데이터 기준: `SiteDivision`(Project=프로젝트명, Workplace=사업장, WorkplaceRiver=과업대표하천, WorkplaceSite=지점명).
  내보내기 보고서 시트는 이 4단계를 표 앞머리 4열로 싣는다(`Export/SiteColumns.cs`).

---

## 8. 예외·로깅 관례
- UI 흐름 예외는 사용자에게 일반화된 메시지, 파일 I/O·DB는 별도 안내. 예외를 조용히 삼키지 않는다.
- 알림 문구는 상수/메시지로 일관.

---

## 9. 빌드 · 실행
- Visual Studio 2026 / .NET 8 / Telerik 2026.3.1007. 패키지는 nuget.org 에서 받는다(2026 Q3 로컬 설치 폴더 없음 · `NuGet.Config` 의 로컬 소스는 2026 Q2).
  라이선스 키는 `%AppData%\Telerik\telerik-license.txt` — 빌드 때 `Telerik.Licensing` 이 읽는다(90_TECH_NOTES §1).
- 시작 프로젝트 `SDSM_Surveyor_App`. `SDSM_Models`는 `..\SDSM\SDSM_Models` 참조.
- 진행 현황·할 일은 `todo.md` 참조(계속 갱신).
