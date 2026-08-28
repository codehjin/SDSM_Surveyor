# design.md — 조사자 앱 UI/UX 디자인 가이드 (SDSM_Surveyor)

> 관리자 앱(`SDSM_App`)의 디자인 시스템을 기준으로 계승하고, 공개된 UI 모범사례(대비·확대·색 의존 금지)를 반영한 **조사자 앱 단일 디자인 기준**.
> 아키텍처·계산 규칙은 `CLAUDE.md` 참조.
>
> **2026-08-23 갱신 — 6단계 디자인 개편(`docs/06_DESIGN_REBUILD.md`) 반영.**
> 토큰·3유형 골격·공용 UserControl 4종이 이때 확정됐다. 설계 근거와 폭 계산은 그 문서 §5 에 있다.

## 라이선스 원칙 (중요)
- **삼성 브랜드 자산(로고, 브랜드 남색 등)과 SamsungOne/One UI 서체는 라이선스 대상이므로 사용하지 않는다.**
- 삼성 느낌은 **브랜드 자산이 아닌 요소**(제품 UI 계열의 파랑 강조색, 절제된 여백, 카드형 정보 구조, 접근성 준수)로 구현한다.
- 추후 삼성 측에서 브랜드 자산·서체 사용을 **공식 승인**하면 그때 §3·§4를 갱신한다.

---

## 1. 기반 테마
- Telerik **Windows11 테마** 전역 적용: `StyleManager.ApplicationTheme = new Windows11Theme();`
  - **순서 주의**: 테마 설정 → `InitializeComponent()` (App.xaml의 `Windows11Resource`가 테마 로드 후 평가되도록).
- 색·코너·폰트는 원칙적으로 `telerik:Windows11Resource` 키 사용. **XAML에 `#RRGGBB` 리터럴 금지.**
- 도메인 색(분류군·차트·강조·등급)만 `Colors.xaml`에 **키로 정의**하고 키로만 참조 → 다크/라이트 대응 유지.

> ⚠ **Telerik 컨트롤에 `Style` 을 통째로 지정하지 않는다.** StyleManager 테마가 벗겨진다(`90_TECH_NOTES.md` §2).
> 높이·여백은 사용처에서 개별 속성으로 준다 — `Height="{StaticResource InputHeight}"`.
> 공통 동작을 묶어야 하면 **상속·Style 이 아니라 감싸는 UserControl** 로 만든다(§7 `ObservationGrid` 가 그 예).

### 관리자 팔레트 커스터마이징(계승)
| 항목 | 값 |
|------|-----|
| PrimaryBorderColor | `#20000000` |
| SecondaryForegroundColor | `#BB000000` |
| PrimarySolidBorderColor / SelectedColor | `#FFE5E5E5` |
| ReadOnlyOpacity | `1` |
| FocusVisualMargin | `0` |
| 다크모드 | MouseOverBackground `#25FFFFFF`, PrimarySolidBorder·Selected `#FF353535` |

---

## 2. 레이아웃 · 치수 토큰

### 2.1 간격 스케일 — 이 6단계만 쓴다

화면에 여백 숫자를 직접 적지 않는다. 개편 전에는 2·6·8·10·12·14·16·20·24가 뒤섞여 있었다.

| 토큰 | 값 | 용도 |
|------|---:|------|
| `SpaceXs` | 4 | 라벨↔입력, 아이콘↔글자 |
| `SpaceSm` | 8 | 폼 칸 사이, 버튼 사이 |
| `SpaceMd` | 12 | 카드 내부 그룹 사이 |
| `SpaceLg` | 16 | 카드 패딩(밀도 높은 곳) |
| `SpaceXl` | 20 | 카드 패딩(여유) · 카드 사이 |
| `Space2Xl` | 32 | 화면 상하 여백 |

두께 토큰: `CardPadding` 16 · `CardPaddingRoomy` 20 · `StatusBarPadding` 12 8 · `FieldMargin` 0 0 12 12
· `ActionButtonPadding` 12 4 · `ActionButtonMargin` 0 0 8 0

### 2.2 컨트롤 치수

| 토큰 | 값 | 비고 |
|------|---:|------|
| `DefaultHeight` / `InputHeight` | **36** | 40 → 36 (2026-08-23). 760 높이 노트북에서 폼 4칸당 16px를 번다 |
| `TitleBarHeight` | 50 | 캡션 바 |
| `TitleBarRowHeight` | 50 | ⚠ `Grid.RowDefinition.Height` 는 **`GridLength`** 를 요구한다. `sys:Double` 을 넣으면 XAML 로드가 터진다 |
| `TaxonTabHeight` | 44 | 분류군 세그먼트 탭 |
| `GridHeaderHeight` / `GridRowHeight` | 34 / 34 | |
| `GridRow2LineHeight` | 30 | 관찰형 2단 행의 아랫줄 |
| `RowDetailsCellWidth` | 44 | 2행 숫자 칸 |
| `ContentXSmallMaxWidth` | 1240 | 본문 최대폭 |

### 2.3 폼 입력 폭 — 4단 규격

| 토큰 | 값 | 쓰는 곳 |
|------|---:|------|
| `FieldXs` | 96 | 하상구성 8종, 비정상종 DE·EF·LE·TU |
| `FieldSm` | 128 | 채집도구, 흐름상태, 측정값 대부분 |
| `FieldMd` | 176 | 하천형태, 조사불가 사유 |
| `FieldLg` | 264 | 비고 |

`FormFieldXs/Sm/Md/LgStyle`(StackPanel)로 감싸 쓴다. 폭과 여백이 함께 붙는다.
구버전 이름(`FormControlSmallWidth`·`MiddleWidth`·`Width`)은 외부 참조 호환을 위해 남겨 뒀다.

### 2.4 결과 레일 폭 — 고정 아님

| 토큰 | 값 |
|------|---:|
| `RailMinWidth` / `RailMaxWidth` | **280 / 360** |

```xml
<ColumnDefinition Width="3*"/>
<ColumnDefinition Width="*" MinWidth="{StaticResource RailMinWidth}"
                            MaxWidth="{StaticResource RailMaxWidth}"/>
```

종전 320 고정은 1280 창에서 그리드를 과하게 누르고 넓은 화면에서는 낭비였다.

### 2.5 창 기준 폭 — 1280에서 성립해야 한다

| 창 | 창 폭 | 본문 가용폭 | 본문 세로 |
|---|---:|---:|---:|
| 최소 | 1280×760 | **1,224 px** | 534 px |
| 기준 | 1440×860 | 1,384 px | 634 px |

가용폭 = 창 − 탭 여백 24 − 테두리 2 − 컨트롤 여백 8 − 세로 스크롤바 14.
세로 = 창 − 226(캡션 50 · 여백 12 · 탭 44 · 조사개황 접힘 48 · 간격 16 · 상태바 48 · 여백 8).

**규칙**
- ✅ 1280에서 **선언한 열 폭이 유지**된다(§7 열 폭 규칙).
- ✅ 세로 스크롤은 허용한다(폼형·저서동물은 760에서 생긴다).
- 🚫 가로 스크롤을 전제로 설계하지 않는다.

---

## 3. 컬러

### 3.1 강조색 (앱 고유 · 브랜드 자산 아님)

| 키 | 값 | 용도 |
|----|-----|------|
| `AppAccentColor` | `#0381FE` | 주요 강조(지수 값, 활성 상태, 주요 버튼) |
| `AppAccentStrongColor` | `#0072DE` | 밝은 배경 위 텍스트·아이콘(대비 확보) |
| `AppAccentDarkColor` | `#3E91FF` | 다크 모드 강조 |

> 큰 면적(상단 바)에는 관리자와 동일한 **딥 네이비 `#1E232E`** 계열을 쓰고, 강조색은 포인트로만 절제 사용.

### 3.2 셸(상단바) — 관리자 계승

| 키 | 값 |
|----|-----|
| `ShellPrimaryColor` | `#FF1E232E` |
| `ShellMouseOverColor` | `#AA293140` |
| `ShellSelectedColor` | `#FF353E52` |
| `ShellForegroundColor` | `#FF869DCF` |
| `ShellTitleForegroundColor` | `#FFF2F4F8` |

### 3.3 분류군 식별색 — **관리자와 동일하게 유지**(두 앱 인지 일관성)

| 분류군 | 색 | | 분류군 | 색 |
|--------|-----|—|--------|-----|
| 어류 | `#00B8D4` | | 양서파충류 | `#CC33FF` |
| 저서동물 | `#651FFF` | | 서식수변 | `#37474F` |
| 조류 | `#9E9D24` | | 수질 | `#2979FF` |
| 포유류 | `#FE9900` | | 식물상/식생 | `#00B200` / `#007200` |

### 3.4 등급 뱃지 6색 (2026-08-23 신설)

| 키 | 값 | 등급 |
|----|-----|------|
| `GradeAColor` | `#2E7D32` | A ≥ 80 |
| `GradeBColor` | `#0381FE` | B ≥ 60 |
| `GradeCColor` | `#F9A825` | C ≥ 40 |
| `GradeDColor` | `#EF6C00` | D ≥ 20 |
| `GradeEColor` | `#C62828` | E |
| `GradeNoneColor` | `#9AA0A8` | `-`(미평가·조사불가) |

**등급은 언제나 글자(A~E)와 함께 나온다** — 색만으로 구분하지 않는다(§11).
`GradeBadgeBorderStyle` + `GradeBadgeTextStyle` 을 쓰고, 배경 브러시만 `DataTrigger` 로 고른다.

> ⚠ **`-`(미평가)와 `0 · E`는 다르다.** 서식수변에서 10개 항목을 전부 0점으로 골랐으면 그것은 평가한 것이다
> (`docs/_excel_formula_audit.md` §5-5). 화면에서 둘이 구분되지 않으면 그 수정의 의미가 사라진다.

### 3.5 표 · 상태색

| 키 | 값 | 용도 |
|----|-----|------|
| `RowAltColor` | `#FAFBFC` | 짝수행 |
| `Row2LineColor` | `#F5F6F8` | 관찰형 2단 행의 아랫줄(같은 행임을 묶는다) |
| `GridHeaderColor` | `#F1F2F4` | 표 머리글 |
| `ReadOnlyBackColor` | `#F7F8FA` | **읽기전용** — 값은 유효하고 편집만 안 된다 |
| `DisabledBackColor` / `DisabledForeColor` | `#F1F2F4` / `#9AA0A8` | **비활성** — 아직 쓸 수 없다 |

> **읽기전용과 비활성을 같은 회색으로 칠하지 않는다.** 지점 드롭다운은 대분류 미선택 시 *비활성*이고,
> `흔적계`·`구분` 열은 *읽기전용*이다. 같아 보이면 조사자가 "고장났나"로 읽는다.

### 3.6 차트·경고
- 최대 `#FFF25022` · 평균 `#FF8EC441` · 최소 `#FF5B9BD5`
- 경고/오류는 `telerik:Windows11Resource ValidationBrush` 사용(리터럴 금지)

---

## 4. 타이포그래피

- **서체는 현행 유지**(SamsungOne은 라이선스 대상이라 사용하지 않음). 본문은 `DefaultFontFamily`(맑은 고딕).

| 역할 | 토큰 | 크기 |
|------|------|------|
| 페이지 제목 | `PageTitleFontSize` | 26 |
| 섹션 제목 | `SectionTitleFontSize` | 19 |
| 본문 | `DefaultFontSize` | 14.5 |
| 폼 라벨 | `FormLabelFontSize` | 15 |
| 보조 설명 | `CaptionFontSize` | **12** (11.5 → 12 · 소수점 크기는 렌더링이 흐렸다) |
| 핵심 수치 | `ValueFontSize` | 30 Bold + 강조색 |
| 등급 | `GradeFontSize` | 24 SemiBold |

---

## 5. 화면 골격 — 3구획

7개 분류군 화면이 같은 골격을 쓴다.

```
[셸]   캡션바 50 → 분류군 탭 44 → 조사개황 카드(접이식)      ← 셸이 소유. 화면마다 그리지 않는다
[본문] 좌: 주작업 영역        |  우: 결과 레일 (유형에 따라)
[하단] TaxonStatusBar (총계 · 상태 · 액션)
```

### 5.1 조사개황은 **셸이 한 번만** 소유한다

종전에는 7개 컨트롤이 같은 카드를 각자 그렸다(껍데기 7벌). `SurveyMeta` 는 싱글턴이라 내용은 원래 하나였다.

- 얻는 것: 탭을 옮겨도 접힘 상태 유지 · 본문이 넓어짐 · **대분류 격리 UI 가 한 곳에만 존재**
- 접힌 상태에서도 요약줄로 `● 방류하천 │ 곡교천1 (St.1) │ 2026반기1차 │ 05-14` 를 보여준다
- **대분류를 고르기 전에는 접기 버튼이 잠긴다** — 격리 규칙을 눈에서 놓치지 않게 한다
- 분류군별 주석(예: "관리자 조류 자료에는 대권역·중권역이 없습니다")은 셸 헤더의 주석 슬롯에 둔다

### 5.2 유형별 본문

| 유형 | 분류군 | 좌 주작업 | 우 결과 레일 |
|------|--------|-----------|--------------|
| **A. 종 입력형** | 어류 · 저서동물 | 조사환경(접이식) + 빠른추가 + 종 그리드 | **있음** — FAI / BMI·다양도 4지수 |
| **B. 관찰형** | 조류 · 포유류 · 양서파충류 | 빠른추가 + `ObservationGrid`(2단 행) | **없음** — 총계는 상태바로 |
| **C. 폼형** | 수질 · 서식수변 | 항목 타일 / 평가항목 드롭다운 | **있음** — 등급 요약 / HRI |

**레일은 결과가 '값'인 유형에만 둔다.** 관찰형에 레일을 두면 그리드가 300px 좁아져 포유류 22열이 안 들어간다.

### 5.3 관찰형 2단 행

한 종 = 한 행이되 **행을 두 줄로** 쓴다. 종이 야장이 원래 그 모양이다.

- 1행 = 종 식별 + 합계(읽기전용) + 좌표 + 특징·특이사항·구분
- 2행 = 1행에 다 못 넣는 묶음 (`RowDetailsVisibilityMode="Visible"` — **상시 표시**)
- Tab 은 1행 열을 모두 지난 뒤 2행 칸으로 들어가고, 그다음 행으로 넘어간다

> 🚫 **접거나 감추지 않는다.** 표시 행이 줄어드는 것(1280×760에서 6행)은 승인된 대가다.
> 행이 적게 보인다는 이유로 열을 접는 구조로 되돌아가지 말 것.

| 화면 | 총 열 | 1행 | 2행 묶음 | 1행 고정폭 | 특징(*) |
|---|---:|---:|---|---:|---:|
| 조류 | 14 | 10 | 도래유형·대항목·세부항목·서식유형 | 992 | 232 |
| 포유류 | 22 | 11 (+흔적계) | 흔적 12칸 | 1,096 | 128 |
| 양서파충류 | 17 | 12 (+관찰계) | 성체·유생·알·울음소리·로드킬·기타 | 1,080 | 144 |

---

## 6. 공용 UserControl 4종 (2026-08-23 신설)

같은 것을 여러 화면이 각자 들고 있으면 한 곳만 고쳤을 때 화면끼리 어긋난다. 실제로 그랬다.

| 컨트롤 | 쓰는 화면 | 화면이 넘기는 것 | 컨트롤이 소유하는 것 |
|---|---:|---|---|
| `QuickAddBar` | 5 | `SearchWatermark` · `CountWatermark` · `Hint` | 콤보·수량칸·추가버튼, **포커스 연결**(종 선택 → 수량 → Enter → 복귀) |
| `TaxonStatusBar` | 7 | `Summary` · `Actions` 슬롯, `WarningText` | 상태문·경고 표시·버튼 계층 배치 |
| `ResultRail` | 4 | `Header` · `Body` 슬롯 | 카드 껍데기·스크롤·가변폭 |
| `ObservationGrid` | 3 | `Rows` · `Columns`(1행) · `RowDetails`(2행) | 그리드 설정 · Ctrl+V · 보호종 강조 · 2단 행 |

**VM 계약**
- `QuickAddBar`·`ObservationGrid` 는 DataContext 가 `IQuickAddHost` 임을 전제한다
  (`SpeciesEntryViewModelBase` 가 구현: `QuickAddCompleted`·`QuickSpeciesPicked`·`AddQuickCommand`·`PasteRows`).
- `TaxonStatusBar` 는 `StatusText`(모든 분류군 VM 의 기반 속성)를 직접 읽는다.
- ⚠ **VM 속성명을 바꾸지 않는다.** `SpeciesEntries`·`Entries` 는 5단계에서 파생 클래스가 기반의 `Rows` 를
  그 이름으로 노출하도록 맞춰 둔 것이다. 바꾸면 기준 엑셀 20개 대조가 깨진다.

**버튼 계층** — 위험(빈 행 정리)은 **가장 왼쪽**, 주요(보고서 엑셀)는 **가장 오른쪽**. 멀리 떼어 놓아야 잘못 누르지 않는다.

| 계층 | 표현 |
|---|---|
| 주요 | `PrimaryActionBackBrush` 채움 + 흰 글자 |
| 보조 | Telerik 기본 |
| 위험 | 기본 외곽선 + `DangerActionForeBrush` 글자 |

---

## 7. 컨트롤 규칙 (실전 검증됨)

- 표는 **`RadGridView`**, 입력은 `RadWatermarkTextBox`·`RadComboBox`·`RadDatePicker`·`RadAutoCompleteBox`·`RadButton`.
- **RadGridView 필수 설정**: `AutoGenerateColumns="False"`, `CanUserInsertRows="True"`, `NewRowPosition="Bottom"`,
  **`GroupRenderMode="Flat"`**(Bottom 새 행의 전제 조건), `SelectionUnit="Cell"`, `ClipboardCopyMode="Cells"`.
  관찰형은 이 설정을 직접 쓰지 말고 **`ObservationGrid`** 를 쓴다.
- 종명 입력: `RadAutoCompleteBox` + `ChosungFilteringBehavior`(초성 검색).
  ⚠ 빠른추가 바는 `RadAutoCompleteBox` 가 아니라 `RadComboBox` 다 — 자동완성+커스텀 필터 조합은 두 번 클릭해야 선택된다.
- **`RadComboBox`는 `SelectedValuePath`를 노출하지 않는다** → 점수 선택 등은 **객체를 `SelectedItem`으로 바인딩**(예: `HriOption`).
- 측정값·개체수 등 자유 수치는 **문자열 입력 + 파싱**(빠른 타이핑, 바인딩 안정).
- 미입력은 워터마크 `·`, 실측 0 은 `0` — **null 과 0 을 눈으로 구분한다**(`CLAUDE.md` §4).

### 열 폭 규칙 — 판정은 `선언 폭 == 실제 폭`

> ⚠ **`RadGridView` 는 열 폭 합이 뷰포트를 넘어도 가로 스크롤을 만들지 않는다. 열을 균등하게 뭉갠다.**
> 그래서 "가로 스크롤 없음"을 기준으로 삼으면 아무것도 걸러내지 못한다(`90_TECH_NOTES.md` §2).

- 1행 고정폭 합 ≤ **1,104 px** (= 가용 1,224 − 자유폭 열 최소 120)
- 넘치면 폭을 깎기 전에 **2행으로 내린다**. 열 삭제는 불가.
- 열 수가 아니라 **폭 합계**로 판단한다. 6칸을 내려도 새 열 하나가 붙으면 순감이 줄어든다.

---

## 8. 데이터 바인딩 규칙
- View↔VM: DataContext는 코드비하인드에서 DI 주입. `[ObservableProperty]` 생성 속성명 / `[RelayCommand]` 생성 명령명(`XxxCommand`)에 바인딩.
- **셀 템플릿에서 화면 VM 접근**은 `RelativeSource`:
  ```xml
  ItemsSource="{Binding DataContext.SpeciesListSource,
                RelativeSource={RelativeSource AncestorType=UserControl}}"
  ```
  콤보 컬럼은 `ItemsSourceBinding="{Binding DataContext.XXX, RelativeSource={RelativeSource AncestorType=telerik:RadGridView}}"`.
- 템플릿(별도 네임스코프) 밖 요소를 `ElementName`으로 참조하지 말 것 → `RelativeSource` 사용.
- 개발 중 **출력창 바인딩 오류 0** 유지 — 화면 스모크 하네스가 이것을 자동으로 센다(`06_DESIGN_REBUILD.md` §7).

---

## 9. 아이콘
- **MahApps.Metro.IconPacks** 사용. 신규 Kind는 **해당 팩 DLL에 존재하는지 확인 후** 사용(없으면 XAML 런타임 예외).
- 검증된 사용 아이콘(2026-08-23 DLL 대조):
  - `PackIconMaterial`: `ClipboardTextOutline`, `ClockOutline`, `ContentSaveOutline`, `PlusBoxOutline`,
    `DeleteSweepOutline`, `CloudSyncOutline`, `FolderMultipleOutline`, `AlertCircleOutline`, `CheckboxBlankOutline`
  - `PackIconModern`: `OfficeExcel`
  - `PackIconCodicons`: `ChromeMinimize`, `ChromeMaximize`, `ChromeClose`
- 아이콘 색은 `Windows11Resource` 브러시에 바인딩(테마 전환 대응).

---

## 10. 창(Window) · WindowChrome
- `WindowChrome`(ResizeBorderThickness 8, **CaptionHeight 50**, `UseAeroCaptionButtons="False"`) + 커스텀 최소화/최대화/닫기.
- 창 크기: `MinWidth 1280 / Width 1440`, `MinHeight 760 / Height 860`.
- 최대화 시 클리핑 보정: `WindowState=Maximized` DataTrigger → `Margin=8`.
- 캡션 버튼 스타일은 App.xaml 공용, 색은 `Windows11Resource`.
- 창 상단에는 프로그램명 + 버전 표기(관리자와 동일 톤).

---

## 11. 접근성 (완료 조건에 포함)
- **명도 대비**: 작은 텍스트 **4.5:1 이상**, 큰 텍스트(일반 18px↑/굵은 14px↑) **3:1 이상**.
- **텍스트 200% 확대**해도 내용·기능이 깨지지 않을 것.
- **색만으로 정보를 구분하지 말 것** — 색약·흑백 사용자 대응.
  - 등급 = 색 뱃지 + **글자(A~E)**
  - 이상치 경고 = 붉은 강조 + **아이콘** + 툴팁 + 상태바 건수
  - 보호종 = 강조색 + 굵기 · 생태계교란생물 = 경고색 + 굵기 (**의미가 반대이므로 색을 분리**)
  - 대분류 = 색점 + **글자**
- 키보드만으로 전 입력 흐름 완주 가능해야 한다.

### 키보드 흐름

| 키 | 동작 |
|---|---|
| 빠른추가 | 초성 검색 → (자동) 수량 → `Enter` → (자동) 검색창 복귀 |
| `Tab` / `Shift+Tab` | 다음/이전 칸. 관찰형은 1행 전체 → 2행 → 다음 행 |
| `Enter` | 편집 확정 + 아래 행 같은 칸(엑셀 습관) |
| `F2` / `Esc` | 편집 시작 / 편집 취소 |
| `Ctrl+V` | 엑셀 다중 행 붙여넣기 (VM 이 직접 파싱) |

> ⚠ `GridViewDataColumn` 에 **`IsTabStop` 이 없다.** 읽기전용 열도 Tab 순서에 남는다(멈추기만 하고 편집은 안 됨).

---

## 12. 신규 화면 체크리스트
1. VM은 DI 주입 + 마커 인터페이스. 색/폰트/코너는 `Windows11Resource`(도메인 색은 `Colors.xaml` 키).
2. **여백·폭·높이를 숫자로 적지 않는다** — §2 토큰만 쓴다. 입력 높이는 `InputHeight`.
3. 3구획 골격(§5)을 따르고 유형에 맞는 공용 UserControl(§6)을 쓴다. 새로 그리지 않는다.
4. 그리드는 §7 필수 설정. 관찰형이면 `ObservationGrid`.
5. **1280 폭에서 선언 열 폭이 유지되는지 확인**(§7). 넘치면 2행으로 내린다 — 열 삭제 불가.
6. 결측치(null)와 0 구분, 지수/등급은 `SDSM_Core/Ecology/*Calculator` 사용.
7. 아이콘 Kind 존재 확인(§9), 바인딩 오류 0 확인.
8. 접근성(§11) 확인 후 완료 처리.
