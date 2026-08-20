# 기술 노트 — 검증된 사실과 함정 (작업 전 필독)

> 이 문서의 내용은 **실제 DLL·DB·엑셀 파일을 열어 검증한 것**이다. 추측이 아니다.
> 새 API를 쓰기 전에는 반드시 §0의 검증 절차를 거칠 것. Telerik은 버전마다 멤버 이름이 다르다.

---

## 0. API 검증 절차 (필수 습관)

Telerik 멤버명을 추측해서 쓰면 반드시 실패한다. 사용 전 DLL에서 존재를 확인한다.

```bash
# 예: enum 멤버 확인
DLL="F:/desktopSW/samsung/1_program/SDSM/SDSM_App/bin/Debug/net8.0-windows/Telerik.Windows.Controls.GridView.dll"
strings -n 3 "$DLL" | grep -xE "None|InsertNewRows|Repeat"
```
```python
# 정확한 타입 전체이름 확인 (권장)
import re
d = open(dll_path,'rb').read()
bool(re.search(b'(?<=\x00)' + b'ThemableColor' + b'\x00', d))   # 표준 토큰 존재
b'Telerik.Documents.Common.Model.ThemableColor' in d            # 정규화된 전체 이름
```

**아이콘 Kind도 동일**하게 검증한다. (`MahApps.Metro.IconPacks.Material.dll`)
- 확인됨: `ClockOutline`, `ContentSaveOutline`, `PlusBoxOutline`, `DeleteSweepOutline`, `CloudSyncOutline`, `CloudDownloadOutline`, `FolderOpenOutline`, `Reload`
- **없음**: `Refresh`, `ContentSaveClockOutline`, `BroomOutline`, `Sync`, `Import`

---

## 1. 환경

- .NET 8 WPF, C# nullable enable, ImplicitUsings enable
- **Telerik UI for WPF 2026.2.520**, 패키지명 `Telerik.UI.for.Wpf.AllControls.Xaml`
  (⚠ `Telerik.UI.for.Wpf.80.Xaml`은 온라인 피드 전용. 로컬 폴더엔 AllControls만 존재)
- 로컬 NuGet: `C:\Program Files (x86)\Progress\Telerik UI for WPF 2026 Q2\Binaries\WPF80\NuGet`
- MVVM: CommunityToolkit.Mvvm / DI: Scrutor (마커 인터페이스 `ITransientService`/`IScopedService`/`ISingletonService`)
- 아이콘: MahApps.Metro.IconPacks
- **Telerik 문서처리(엑셀) DLL이 조사자 앱 bin에 이미 포함**되어 있다(AllControls 패키지). 별도 패키지 추가 불필요.

### 파일 경로
| 용도 | 경로 |
|---|---|
| 종목록 | `%AppData%\SDSM_Surveyor\species.json` → 없으면 실행폴더 번들 |
| 이상치 기준 | `%AppData%\SDSM_Surveyor\reference.json` → 없으면 번들 |
| 앱 설정 | `%AppData%\SDSM_Surveyor\settings.json` |
| 임시저장 | `%AppData%\SDSM_Surveyor\drafts\{분류군}.json` |

---

## 2. Telerik WPF 컨트롤 함정

| 함정 | 해결 |
|---|---|
| `RadGridView.NewRowPosition="Bottom"` 사용 시 예외 | `GroupRenderMode="Flat"` 필수 |
| `RadComboBox`에 `SelectedValuePath` 없음 | `SelectedItem`으로 객체 바인딩 |
| **StyleManager 테마에서 `RadTabItem`에 Style 통째 지정 금지** | 테마 템플릿이 벗겨짐. HeaderTemplate·MinWidth·Padding만 지정. (현재는 RadioButton 세그먼트 탭 사용) |
| `RadAutoCompleteBox`에 `SearchText` 양방향 바인딩 + 커스텀 필터 → **선택하려면 두 번 클릭** | 빠른추가는 `RadComboBox`(IsEditable=True, IsTextSearchEnabled=False, IsFilteringEnabled=False, OpenDropDownOnFocus=True, StaysOpenOnEdit=True) + **VM에서 직접 필터**로 해결 |
| `GridViewClipboardPasteMode` 멤버 | **`None` / `InsertNewRows` / `OverwriteWithEmptyValues` / `Repeat`** 만 존재. `ExpandRowsOnPaste`·`AllSelectedCells` **없음** |
| `ClipboardCopyMode="All"` 은 **헤더까지 복사** → 붙여넣기 시 "국명"이 데이터 행으로 들어감 | `ClipboardCopyMode="Cells"` |
| 커스텀 CellEditTemplate 컬럼이 있으면 **기본 붙여넣기가 탭 분리를 못 함**(한 칸에 뭉침) | 그리드 `PreviewKeyDown`에서 Ctrl+V를 가로채 **클립보드를 직접 파싱**(`VM.PasteRows`) |
| 붙여넣기 직후 **그리드가 즉시 안 그려짐**(인서트 행 편집 상태) | `grid.CancelEdit()` 후 `Dispatcher.BeginInvoke(..., DispatcherPriority.Background)`로 행 추가 |
| Telerik 기본 UI 문구가 영문 | `LocalizationManager.Manager = new KoreanLocalizationManager()` (App 생성자, `Helpers/KoreanLocalizationManager.cs`) |

### 팔레트/테마
`App.xaml.cs` 생성자 순서 중요: 테마 설정 → 팔레트 → DI → `InitializeComponent()` → 폰트/팔레트 색 적용.
팔레트 `AccentColor`를 바꾸면 기존 화면의 `AccentBrush`가 일괄 반영된다.

---

## 3. Telerik 문서처리 (엑셀 읽기/쓰기)

관리자와 **동일 라이브러리**를 쓴다 → 100% 호환.

```csharp
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx; // XlsxFormatProvider
using Telerik.Windows.Documents.Spreadsheet.Model;      // Workbook, Worksheet, CellSelection, CellRange, PatternFill, PatternType
using Telerik.Windows.Documents.Spreadsheet.Formatting; // ColumnWidth, RadHorizontalAlignment
using ThemableColor = Telerik.Documents.Common.Model.ThemableColor;           // ⚠ 네임스페이스 주의
using ThemableFontFamily = Telerik.Documents.Common.Model.ThemableFontFamily; // ⚠ 네임스페이스 주의
```

- **읽기**: `new XlsxFormatProvider().Import(stream, null)`
- **쓰기**: `new XlsxFormatProvider().Export(workbook, stream, null)` ← **3번째 인자 `TimeSpan?` 필수**
- 셀 인덱스는 **0-based** (`Cells[0,0]` = A1)
- 서식: `SetIsBold` `SetFontSize` `SetForeColor(new ThemableColor(color))` `SetFill(new PatternFill(PatternType.Solid, fore, back))` `SetHorizontalAlignment(RadHorizontalAlignment.Center)` `SetFontFamily(new ThemableFontFamily("맑은 고딕"))`
- 컬럼 폭: `ws.Columns[i].SetWidth(new ColumnWidth(px, true))`

### ✅ AutoFilter (해결됨)
`worksheet.Filter`의 타입은 `AutoFilter`이며 `SetFilterRange`는 **internal 인터페이스 `IWorksheetFilter`의 명시적 구현**이라 외부에서 호출 불가(CS0122).
→ **`ws.Filter.FilterRange = new CellRange(headerRow, 0, lastRow, lastCol)` 속성 대입은 public이라 정상 동작한다.**
`FishReportExporter.AutoFilter()`가 이 방식을 쓰며 빌드·실행 모두 통과(2026-08-20 확인). `SetFilterRange`는 호출하지 말 것.

---

## 4. 관리자 일괄입력 엑셀 셀 매핑 (검증 완료)

관리자 `SDSM_App\ViewModels\BulkRegisters\Bulk*ControlViewModel.cs` 기준. 인덱스는 **0-based `Cells[row,col]`**.

**공통**: 파일명에 `방류하천` 또는 `생태현황` 키워드가 있어야 import가 시작된다.

### 배치 유형
| 분류군 | 시트명 | 배치 | 레코드 열 시작 | 종목록 시작 행 |
|---|---|---|---|---|
| 어류 | `어류_입력` | 전치 | **16(Q)** | **78(행79)** |
| 저서동물 | `입력` | 전치 | **12(M)** | **46(행47)** |
| 수질 | `수질` | 전치 | **5(F)** | 없음 |
| 서식및수변 | `서식 및 수변환경` | 전치 | **5(F)** | 없음 |
| 조류 | `조류` | 일반(1행=1기록) | 열1(B), **0-based 행4**(엑셀 5행) | — |
| 포유류 | `포유류` | 일반 | 열1(B), **0-based 행4**(엑셀 5행) | — |
| 양서파충류 | `양서파충류` | 일반 | 열1(B), **0-based 행4**(엑셀 5행) | — |

> 근거: 관찰형 3종 `FirstRowNumber = 5`, `FirstColumnNumber = 2` → 읽기 시작 `[4, 1]`.
> 수질·서식수변은 `FirstRowNumber = 2`, `FirstColumnNumber = 6` → 앵커 `[1, 5]`(F열).

> **전치**: 필드=행 고정, 조사레코드=열(오른쪽으로 확장). 각 레코드 열의 **행1(엑셀 행2)=연도차수**가 비면 그 레코드는 무시된다.
> **일반**: 1행=1출현기록, 각 행의 **B열=연도차수**가 비면 그 행은 무시된다.

### 어류 행 매핑 (0-based row → 값)
`1`연도차수 `2`지점명 `3`조사일 `4`대권역 `5`중권역 `6`하천명 `7`하천유형 `8`위도 `9`경도 `10`날씨 `11`조사기관 `12`조사자 `13`채집소요시간 `14`채집도구 `15`흐름상태 `16`**하천차수** `17~24`하상구성(암반·콘크리트·진흙·모래·잔자갈·자갈·작은돌·큰돌) `26`하천형태 `27`흐름상태 `28`채집불가특이사항 `29`비고 `43~46`**DE·EF·LE·TU**

종목록(행78부터): 열 `0`FishTrait `1`ToleranceGuild `2`FeedingGuild `3`HabitatGuild `4`Exotic `5`Endemic `6`Endangered1 `7`Endangered2 `8`NaturalMonument `9`LineageOrder `10`강 `11`목 `12`과 `13`**국명** `14`**학명**, 개체수=레코드 열(16+), **정수**

### 저서동물 행 매핑
`1`연도차수 `2`지점명 `3`조사일 `4`대권역 `5`중권역 `6`하천명 `7`하천유형 `8`위도 `9`경도 `10`날씨 `11`조사기관 `12`조사자 `13`Surber30 `14`Surber50 `15`드렛지 `16`에크만 `17`유역이용 `18`오염원 `19`식생수피도 `20`범람원 `21`제방좌안 `22`제방우안 `23~30`하상구성8 `32`하천형태 `33`하폭 `34`수폭 `35`평균수심 `36`평균유속 `37`기온 `38`수온 `39`흐름상태 `40`투명도 `41`냄새 `42`비고 `43`채집불가사유

종목록(행46부터): 열 `0`오탁치 `1`지표가중치 `2`Endangered1 `3`Endangered2 `4`Endemic `5`문 `6`강 `7`목 `8`과 `9`**학명** `10`**국명**, 개체수=레코드 열(12+), **실수(double)**
> ⚠ 어류와 학명/국명 열 순서가 **반대**다.

### 수질 행 매핑
`1`연도차수 `2`지점명 `3`조사일 `4`대권역 `5`중권역 `6`하천명 `7`하천유형 `8`위도 `9`경도 `10`날씨 `11`조사기관 `12`조사자 `13`PH `14`BOD `15`COD `16`TOC `17`SS `18`DO `19`TP `20`생태독성 `21`대장균군
**확장항목(TN·EC·Cl·SO4·Cu·Zn·Cr·탁도·Chla·수온·수심·유속·초당유량·일당유량)은 고정 행이 아니다.** 관리자가 행22부터 **C열(및 D열)의 라벨 텍스트**를 검색해 찾는다 → 내보낼 때 C열에 라벨을 반드시 기입.

> ⚠ **라벨에 단위를 붙이지 말 것.** 검색은 `C열+D열`을 공백제거·소문자화한 뒤 `Contains`로 비교하고 **첫 일치 행**을 채택한다.
> 예를 들어 `초당유량(m3/sec)`은 `ec`를 포함해 **EC(전기전도도)로 오인식**된다.
> → C열에는 단위 없는 라벨(`T-N`·`전기전도도`·`염소이온`·`황이온`·`구리`·`아연`·`크롬`·`탁도`·`클로로필a`·`수온`·`수심`·`유속`·`초당유량`·`일당유량`)만 넣고,
> 단위는 검색 대상이 아닌 **E열**에 둔다(`WaterQualityExcelExporter` 참고).

### 서식및수변 행 매핑
`1`연도차수 `2`지점명 `3`조사일 `4`대권역 `5`중권역 `6`하천명 `7`하천유형 `8`위도 `9`경도 `10`날씨 `11`조사기관 `12`조사자 `13`조사불가사유 `14`비고 `16~25`**평가항목1~10**

### 조류/포유류/양서파충류 (열 매핑, 행4부터)
- **조류**: `1`연도차수 `2`조사일 `3`날씨 `4`하천명 `5`지점명 `6`국명 `7`학명 `8`특징 `9~11`위도도분초 `12~14`경도도분초 `15`위도 `16`경도 `18`개체수 `19`도래유형 `20`대항목 `21`세부항목 `22`서식유형 `23`조사자 `24`비고
- **포유류**: `1`연도차수 `2`조사일 `3`날씨 `4`하천명 `5`지점명 `6`국명 `7`학명 `8`특징 `9~11`위도도분초 `12~14`경도도분초 `15`위도 `16`경도 `17`관찰지유형 `19~30`**Trace1~12** `31`조사자 `32`비고
- **양서파충류**: `1`연도차수 `2`조사일 `3`날씨 `4`하천명 `5`지점명 `6`국명 `7`학명 `8`특징 `9~14`**Trace1~6** `15~17`위도도분초 `18~20`경도도분초 `21`위도 `22`경도 `24`대분류 `25`중분류 `26`조사자 `27`비고

### 자동계산 필드 (엑셀에 넣지 않는다 — 관리자가 재계산)
- 어류: 총종수·총개체수·M1~M7·FAIScore·FAIGrade·종별 RankScorer
- 저서: DI·H'·R1·J'·BMIScore·BMIGrade·RankScorer
- 수질: PH/BOD/COD/TOC/SS/DO/TP/EColi **등급 8종**
- 서식수변: EvaluationScore·EvaluationGrade
- 조류·포유류·양서파충류: **자동계산 없음(전 필드 입력)**

> 단, **재계산의 입력값**은 반드시 채워야 한다: 하천차수, DE/EF/LE/TU, 종별 개체수, 수질 원측정값, 평가항목1~10, 채집불가사유.

---

## 5. 생태 계산식 (지침 대조 완료 — 관리자 `GB.cs`와 동일)

- **FAI** = M1~M8 합(각 0/6.25/12.5). 등급 A≥80·B≥60·C≥40·D≥20·E.
  M1 국내종수 / M2 여울성저서종수 / M3 민감종수(모두 하천차수별 기준) / M4 내성종비율 / M5 잡식종비율 / M6 충식종비율 / M7 국내종개체수(차수별) / M8 비정상종비율
  ### ⚠ M8 비정상종 비율 — 정수 나눗셈은 **결함**이다 (2026-08-20 결정)

  기존 구현(관리자 `GB.cs`, 이를 이식한 조사자 `EcologyCalculator`)은 다음과 같았다.
  ```csharp
  double abnormalRatio = abnormalCount != 0 ? (abnormalCount / totalIndiv) * 100 : 0;  // ← 둘 다 int
  ```
  `abnormalCount`·`totalIndiv`가 모두 `int`라 **정수 나눗셈**이 되어, 비정상 개체가 총개체수보다 적으면 결과가 **항상 0%**다.
  → M8이 사실상 언제나 만점(12.5)이 되어 **비정상종 지표가 작동하지 않는다.**

  예: 총 250개체 중 비정상 3개체 → 정수 `3/250 = 0` → 0% → **12.5점**
  (지침대로면 1.2% → 1% 초과 → **0점**. 100점 만점에서 12.5점 차이로 등급이 바뀔 수 있다.)

  **결정: 수생태계 건강성 평가 지침에 따라 실수 나눗셈으로 수정한다.**
  ```csharp
  double abnormalRatio = totalIndiv > 0 ? (double)abnormalCount / totalIndiv * 100.0 : 0;
  ```
  - **관리자 `GB.cs`와 조사자 `Ecology\` 양쪽을 함께** 고쳐야 값이 일치한다.
  - 과거 데이터 재산출 여부는 별도 판단 필요 → `..\..\SDSM\docs\ADMIN_TASKS.md` §6 참조.
  - 이 항목은 "관리자 동작 보존" 원칙의 **예외**다. 기존 동작이 관행이 아니라 결함이기 때문이다.
  ⚠ **FAI 대하천(표12) 미구현** — 삼성 소하천은 영향 적음. 필요 시 추가.
- **BMI** = `(4 − Σ(s·g·h)/Σ(g·h)) × 25`, 등급 A≥80·B≥65·C≥50·D≥35·E
- **DI** = (1위+2위 개체수)/총개체수, **H'** Shannon, **R1** = (S−1)/ln(N), **J'** = H'/log2(S)
- **HRI(서식수변)** = 평가항목 10개 합 ÷ 2, 등급 A80·B60·C40·D20·E
- 조사불가 사유(접근불가·건천화·준설·공사중)가 있으면 등급 `"-"`
- 유효 개체가 0이면 지수를 계산하지 않고 `-` 표시(0건인데 FAI 37.5 같은 오해 방지)

### 계산식 일치 검증 결과 (2026-08-20) — 상세는 `_calc_parity_20260820.md`

관리자 `GB.cs` ↔ 조사자 `Ecology\` 를 **1,379건 대조**했다(경계값 중심, DB·관리자 앱 미실행).
**일치 1,367 · 불일치 12.** 수질 등급 8종과 저서 지수(DI·H'·R1·J'·순위점수)는 완전 일치한다.

| 남은 차이 | 관리자 | 조사자 | 성격 |
|---|---|---|---|
| **어류 0종 자료** | 37.5 · D | (null) | ⚠ 관리자에 0 가드가 없어 M4·M5·M8이 만점 → D등급. **사용자 결정 대기** |
| BMI 음수 등급 | `Check` | `E` | 오탁치 s>4 에서만 발생 — 정상 종목록에선 없음 |
| HRI 빈 자료·`"접근 불가"`(공백) | 편집화면만 다름 | — | 조사자는 **관리자 일괄입력(DB 저장) 경로와 100% 일치**. 관리자 편집화면이 공백을 제거하지 않는 결함 |

> ⚠ **0으로 나누기**: 총개체수 0 + 비정상 개체수 > 0 이면 위 정수 나눗셈에서
> **양쪽 모두 `DivideByZeroException`** 이 난다. M8을 실수 나눗셈으로 고칠 때 `총개체수 > 0` 가드를 함께 넣을 것.

> 조사불가 사유 비교는 **공백 제거 후**가 표준이다(관리자 일괄입력·조사자 동일).
> 관리자 편집화면(`HabitatWaterEdgeEditorControlViewModel.GetGrade`)만 공백을 제거하지 않아 `"접근 불가"`를 놓친다.

---

## 6. 데이터·모델 참고

### ✅ 관찰형 3종 종목록 = 국가생물종목록 (2026-08-20)
`species.json`의 관찰형 3종은 과거 조사기록에서 뽑은 국명 문자열 목록이었으나(조류 169·포유류 17·양서파충류 22, 학명 없음),
**국립생물자원관 국가생물종목록으로 교체**했다 → `Bird` 609 · `Mammal` 125 · `Amphibian` 65(양서28+파충37).

- 타입: `SpeciesCatalog.Bird/Mammal/Amphibian` = **`List<ObservedSpecies>`**
  (`SpeciesKo`·`SpeciesEn`·`OrderKo`·`FamilyKo`·`Endangered1/2`·`NaturalMonument`·`Invasive`·`Ktsn`)
- **구형 파일(국명 문자열 배열)도 그대로 읽힌다** — `ObservedSpeciesListConverter`가 문자열이면 `SpeciesKo`만 채운 객체로 변환.
  이 하위 호환을 제거하면 구형 `species.json`을 쓰는 배포본이 종목록을 통째로 잃는다.
- 생성: `tools\build_species_json.py` (입력 `docs\_input\생물종 일괄입력.xlsx`). **어류·저서동물 부분은 건드리지 않고 보존**한다.
  원본 목록이 갱신되면 이 스크립트를 다시 실행한다. 열 위치는 헤더 텍스트로 검증하므로 구조가 바뀌면 오류로 멈춘다.
- 조류 617행 중 **국명 중복 8건은 첫 항목만 채택**(아종 차이로 학명이 다름) → 609종.
- ⚠ **보호종·교란종 목록에는 속 단위 등재가 있다.** 학명이 `spp.`/`sp.` 로 끝나면
  **속 이름만 비교해 그 속의 모든 종에 플래그**를 줘야 한다. 정확 일치만 하면
  `붉은귀거북`(`Trachemys scripta`)이 `Trachemys spp.` 등재와 매칭되지 않아 교란종 표시가 누락된다.

- 공유 모델은 `SDSM\SDSM_Models\` (관리자·조사자 공용, ProjectReference로 참조)
  - `FishSpeciesList`(길드·보호종), `BenthosSpeciesList`(오탁치·가중치), `ImportFishSpecies`, `ImportBenthosSpecies`
  - `SpeciesCatalog`(종목록 카탈로그), `ReferenceData`/`SpeciesRange`(이상치 기준)
- 관리자 DB: `SDSM\SDSM_App\bin\Debug\net8.0-windows\SDSMDB.sqlite`
  주요 테이블: `FishSpeciesList`(253) `BenthosSpeciesList`(821) `Fish` `Benthos` `Bird` `Mammal` `AmphibianReptile` `WaterQuality` `HabitatWaterEdge` `SiteDivision`
  ⚠ **이 DB는 사용자의 실데이터다. `bin` 폴더를 지우거나 옮기지 말 것.**
- 실제 제출 엑셀 샘플: `F:\17_한국환경지리연구소\2_삼성DS\2026년\5_보낸파일\20260305\일괄입력자료\`
  → 템플릿에 **`조사지점` 시트**가 있고 여기에 조사장소 번호·좌표가 들어있다(지점 체계 구현 시 원천 자료).

---

## 7. 개발 환경 주의

- F: 드라이브 마운트에서 **bash로 방금 쓴 파일을 읽으면 내용이 오래된 경우가 있다.** 파일 도구(Read/Edit/Write)를 신뢰할 것. 특히 `.git/config`를 bash로 판단하지 말 것.
- CS8826 경고(부분 메서드 시그니처 차이)는 `On○○Changed(T v)` 매개변수명을 소스 생성기 규약인 **`value`** 로 바꾸면 사라진다. 동작에는 영향 없음.
