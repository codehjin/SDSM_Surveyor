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
| UI | Telerik UI for WPF `AllControls.Xaml` 2026.2.520 (Windows11 테마) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection + Scrutor |
| 아이콘 | MahApps.Metro.IconPacks |
| 저장 | 로컬 JSON(AppData) — `System.Text.Json` |
| 공유 | `SDSM_Models`(관리자와 동일 POCO) 참조 |

**폴더 구조 (`SDSM_Surveyor_App/`)**
```
App.xaml(.cs)          진입점(Windows11 테마 + DI)
InjectableServices/    DI 마커 인터페이스
Helpers/               ChosungHelper (초성 검색)
Behaviors/             ChosungFilteringBehavior
Ecology/               EcologyCalculator(FAI) · BenthosCalculator(DI/H/R1/J/BMI)
                       · WaterQualityCalculator(등급) · HabitatEvaluator(평가점수)
Models/                *Entry / *SpeciesEntry (화면 행 모델)
Data/                  ISpeciesListProvider · SpeciesListProvider(시드)
                       · ILocalDraftStore · LocalDraftStore(임시저장)
Messengers/            NotifyMessage (토스트)
ViewModels/            분류군별 EntryViewModel
Views/Windows/         MainSurveyWindow
Views/UserControls/    분류군별 EntryControl
```
> 향후 `Helpers`/`Behaviors`/`Ecology`는 관리자와 공유하는 **SDSM_Core**로 이관 예정.

---

## 2. MVVM 규칙 (관리자와 동일)
- ViewModel: `ObservableObject` 상속 + `partial class`. 상태는 `[ObservableProperty]`, 명령은 `[RelayCommand]`(+ `CanExecute = nameof(CanXxx)`).
- View는 코드비하인드에서 **DI로 VM 주입**: `DataContext = App.Current.Services.GetRequiredService<XxxViewModel>();`
- DI 등록은 **마커 인터페이스 자동 스캔**(Scrutor): 화면/VM/Provider에 `ITransientService` 또는 `ISingletonService` 부여.
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

- 계산기 위치(`Ecology/`)와 관리자 원본:
  - `EcologyCalculator.CalculateFai` ← `GB.GetFAI` (M1~M8, 등급 A~E, 조사불가 "-")
  - `BenthosCalculator.GetDI/GetH/GetR1/GetJ/GetBMI/GetRankScorer` ← `GB.*`
  - `WaterQualityCalculator.*Grade` ← `GB.Get*Grade` (pH/BOD/COD/TOC/SS/DO/TP/대장균)
  - `HabitatEvaluator.Evaluate` ← `GetGrade`(10항목 합÷2, A~E, 접근불가 "-")
- 규칙(반드시 준수):
  - **결측치(null) vs 0 엄격 구분.** 개체수/측정값 미입력은 `null`, 실측 부재만 `0`. 집계는 `?? 0` 후 `> 0` 필터.
  - 반올림 `MidpointRounding.AwayFromZero`, NaN/Infinity·분모 0 가드, 조사불가 사유(`접근불가/건천화/준설/공사중`) 시 등급 `"-"`.
  - 지수 입력에 쓰는 **종속성(길드·오탁치·지표가중치)은 종목록에서 자동 상속**한다. → 지수가 관리자와 일치하려면 **종목록(기준자료) 버전이 관리자와 동일**해야 한다.
- 계산식 변경 금지. 관리자 로직이 바뀌면 여기도 함께 바꾼다(장기적으로 SDSM_Core로 단일화).

---

## 5. 종명 · 초성 검색
- 종명 입력은 `RadAutoCompleteBox` + `ChosungFilteringBehavior`(초성 `ㅋㅈㄴ` + 부분일치). Telerik `IFilteringBehavior.FindMatchingItems` 구현(검증됨).
- **종명은 원문 그대로**(`Trim`만, 대소문자·띄어쓰기 보존; CLAUDE 규칙). 길드/보호종 코드성 값만 정규화.
- 종목록형은 모델 기반 목록(`FishSpeciesList`/`BenthosSpeciesList`), 관찰형은 종명 문자열 목록.
- 현재 `SpeciesListProvider`는 **데모 시드**. 실제 배포 시 관리자 종목록 로딩(+버전/체크섬)으로 교체.

---

## 6. 입력 범위 · 오프라인 저장 · 내보내기 (정책)
- **입력 범위:** 관리자 그리드에서 **자동계산 항목을 제외한 모든 항목**을 조사자가 입력(기존 분류군별 엑셀 양식 기준 아님).
- **오프라인 임시저장:** `ILocalDraftStore` → `%AppData%\SDSM_Surveyor\drafts\{taxon}.json`. 실행폴더가 아닌 AppData(업데이트·재설치에도 데이터 보존).
- **엑셀 내보내기(2종, 구현 예정):**
  - ① 일괄입력용 — 관리자 Import에 필요한 **필수자료만**.
  - ② 열람·보관용 — **모든 수식 포함**.
  - 생물종별 개별 엑셀 지원. 내보내기 엑셀에 구분 컬럼: 프로젝트명·사업장·과업대표하천·지점명(= `SiteDivision`).

---

## 7. 조사지점 (구현 예정 · 핵심)
- 대분류 **방류하천 / 생태현황** 구분 입력폼.
- 조사자는 **관리자가 등록한 지점 번호만 선택**(자유 입력 제한). `ST1/ST2` 입력 → DB 저장 시 실제 지점명(`곡교천1`)으로 변환.
- 연도별 지점 이력(엑셀 `조사지점` 시트) 기준. 좌표 클릭 시 외부 지도 연동.
- 관리자만 지점 수정·병합·분야 선택(설정 창, 평상시 비활성화).
- 데이터 기준: `SiteDivision`(Project=프로젝트명, Workplace=사업장, WorkplaceRiver=과업대표하천, WorkplaceSite=지점명).

---

## 8. 예외·로깅 관례
- UI 흐름 예외는 사용자에게 일반화된 메시지, 파일 I/O·DB는 별도 안내. 예외를 조용히 삼키지 않는다.
- 알림 문구는 상수/메시지로 일관.

---

## 9. 빌드 · 실행
- Visual Studio 2026 / .NET 8 / Telerik 2026.2.520. `NuGet.Config`에 로컬 Telerik 소스 등록됨.
- 시작 프로젝트 `SDSM_Surveyor_App`. `SDSM_Models`는 `..\SDSM\SDSM_Models` 참조.
- 진행 현황·할 일은 `todo.md` 참조(계속 갱신).
