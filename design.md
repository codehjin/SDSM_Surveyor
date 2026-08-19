# design.md — 조사자 앱 UI/UX 디자인 가이드 (SDSM_Surveyor)

> 관리자 앱(`SDSM_App`)의 디자인 시스템을 기준으로 계승하고, 공개된 UI 모범사례(대비·확대·색 의존 금지)를 반영한 **조사자 앱 단일 디자인 기준**.
> 아키텍처·계산 규칙은 `CLAUDE.md` 참조.

## 라이선스 원칙 (중요)
- **삼성 브랜드 자산(로고, 브랜드 남색 등)과 SamsungOne/One UI 서체는 라이선스 대상이므로 사용하지 않는다.**
- 삼성 느낌은 **브랜드 자산이 아닌 요소**(제품 UI 계열의 파랑 강조색, 절제된 여백, 카드형 정보 구조, 접근성 준수)로 구현한다.
- 추후 삼성 측에서 브랜드 자산·서체 사용을 **공식 승인**하면 그때 §3·§4를 갱신한다.

---

## 1. 기반 테마
- Telerik **Windows11 테마** 전역 적용: `StyleManager.ApplicationTheme = new Windows11Theme();`
  - **순서 주의**: 테마 설정 → `InitializeComponent()` (App.xaml의 `Windows11Resource`가 테마 로드 후 평가되도록).
- 색·코너·폰트는 원칙적으로 `telerik:Windows11Resource` 키 사용. **XAML에 `#RRGGBB` 리터럴 금지.**
- 도메인 색(분류군·차트·강조)만 `Colors.xaml`에 **키로 정의**하고 키로만 참조 → 다크/라이트 대응 유지.

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

## 2. 레이아웃 · 치수 토큰 (관리자 계승)

| 토큰 | 값 | 용도 |
|------|-----|------|
| `DefaultHeight` / `FormControlHeight` | **40** | 모든 입력 컨트롤 높이 통일 |
| `FormControlLargeHeight` | 60 | 큰 입력 |
| `TitleBarHeight` | 50 | 상단 바 |
| `ContentXSmallMaxWidth` | **1240** | 본문 최대폭(중앙 정렬) |
| `ContentSmallMaxWidth` / `ContentMaxWidth` | 1980 / 3120 | 넓은 화면용 |
| `FormControlSmallWidth` / `MiddleWidth` / `Width` / `BigWidth` / `LargeWidth` | 120 / 149.5 / **240** / 480 / 650 | 폼 입력 폭 규격 |

**구조 규칙**
- 제목 영역: Horizontal StackPanel, Margin `30,20,30,0`, Height 40.
- 본문: Margin `30`, 카드 반복. 그리드 중심 화면은 전체폭 사용 가능.
- **카드(ContentBox)**: Background `PrimaryBackgroundBrush`, Border `PrimaryBorderBrush` 1px, **Padding 20**, CornerRadius `CornerRadius`(또는 `OverlayCornerRadius`).
- 정보 밀도가 높은 야장 특성상 여백·구분선·헤더 위계를 명확히(가독성 우선).

---

## 3. 컬러

### 3.1 강조색 (앱 고유 · 브랜드 자산 아님)
제품 UI 계열의 밝은 파랑을 강조색으로 사용한다. `Colors.xaml`에 키로 정의하고 XAML은 키만 참조.

| 키 | 값 | 용도 |
|----|-----|------|
| `AppAccentColor` | `#0381FE` | 주요 강조(지수 값, 활성 상태) |
| `AppAccentStrongColor` | `#0072DE` | 밝은 배경 위 텍스트·아이콘(대비 확보) |
| `AppAccentDarkColor` | `#3E91FF` | 다크 모드 강조 |

> 큰 면적(상단 바·사이드바)에는 관리자와 동일한 **딥 네이비 `#1E232E`** 계열을 쓰고, 강조색은 포인트로만 절제 사용.

### 3.2 셸(상단바/사이드바) — 관리자 계승
| 키 | 값 |
|----|-----|
| `MainPrimaryColor` | `#FF1E232E` |
| `MainMouseOverColor` | `#AA293140` |
| `MainSelectedColor` | `#FF353E52` |
| `MenuForegroundColor` | `#FF869DCF` |

### 3.3 분류군 식별색 — **관리자와 동일하게 유지**(두 앱 인지 일관성)
| 분류군 | 색 | | 분류군 | 색 |
|--------|-----|—|--------|-----|
| 어류 | `#00B8D4` | | 양서파충류 | `#CC33FF` |
| 저서동물 | `#651FFF` | | 서식수변 | `#37474F` |
| 조류 | `#9E9D24` | | 수질 | `#2979FF` |
| 포유류 | `#FE9900` | | 식물상/식생 | `#00B200` / `#007200` |

### 3.4 차트·상태
- 최대 `#FFF25022` · 평균 `#FF8EC441` · 최소 `#FF5B9BD5`
- 통계 보조 `#FF0099BC`, `#FFADB827`
- 경고/오류는 `telerik:Windows11Resource ValidationBrush` 사용(리터럴 금지)

---

## 4. 타이포그래피
- **서체는 현행 유지**(SamsungOne은 라이선스 대상이라 사용하지 않음). 본문은 관리자와 동일 계열의 한글 고딕(현재 `DefaultFontFamily`).
- **타입 스케일은 관리자와 통일**:

| 역할 | 크기 | 비고 |
|------|------|------|
| 페이지 제목 | **26** | 화면 최상단 |
| 섹션 제목 | **19** | 카드/영역 제목 |
| 소제목·강조 | 14.5 | SemiBold 병행 |
| 본문 | **14.5** | 기본(`DefaultFontSize`) |
| 폼 라벨 | **15** | `SecondaryForegroundBrush` |
| 보조 설명 | 11~12 | `SecondaryForegroundBrush` |

- 지수/등급 등 핵심 수치는 **28~34 Bold + 강조색**으로 시각적 우선순위 부여.

---

## 5. 화면 패턴 3종
| 패턴 | 분류군 | 구성 |
|------|--------|------|
| **A. 종목록형** | 어류, 저서동물 | 기본정보(Top) + 종 그리드 + **지수 패널(우 320)** |
| **B. 관찰나열형** | 조류, 포유류, 양서파충류 | 기본정보 + 전체폭 그리드 + 총계(상태바) |
| **C. 항목측정형** | 수질, 서식수변 | 기본정보 + 항목 폼(카드/드롭다운) + **결과 패널** |

공통 골격: `Grid` 3행 = 상단 공통 기본정보 / 본문 / 하단 상태바+액션.

---

## 6. 컨트롤 규칙 (실전 검증됨)
- 표는 **`RadGridView`**, 입력은 `RadWatermarkTextBox`·`RadComboBox`·`RadDatePicker`·`RadAutoCompleteBox`·`RadButton`.
- **RadGridView 필수 설정**: `AutoGenerateColumns="False"`, `CanUserInsertRows="True"`, `NewRowPosition="Bottom"`, **`GroupRenderMode="Flat"`**(Bottom 새 행의 전제 조건), `SelectionUnit="Cell"`.
- 종명 입력: `RadAutoCompleteBox` + `ChosungFilteringBehavior`(초성 검색). 모델 목록은 `DisplayMemberPath`, 문자열 목록은 그대로.
- **`RadComboBox`는 `SelectedValuePath`를 노출하지 않는다** → 점수 선택 등은 **선택 항목 객체를 `SelectedItem`으로 바인딩**(예: `HriOption`).
- 흔적 등 불리언은 `GridViewCheckBoxColumn`, 선택형은 `GridViewComboBoxColumn`(`ItemsSourceBinding`).
- 측정값·개체수 등 자유 수치는 **문자열 입력 + 파싱**(빠른 타이핑, 바인딩 안정).

---

## 7. 데이터 바인딩 규칙
- View↔VM: DataContext는 코드비하인드에서 DI 주입. `[ObservableProperty]` 생성 속성명 / `[RelayCommand]` 생성 명령명(`XxxCommand`)에 바인딩.
- **셀 템플릿에서 화면 VM 접근**은 `RelativeSource`:
  ```xml
  ItemsSource="{Binding DataContext.SpeciesListSource,
                RelativeSource={RelativeSource AncestorType=UserControl}}"
  ```
  콤보 컬럼은 `ItemsSourceBinding="{Binding DataContext.XXX, RelativeSource={RelativeSource AncestorType=telerik:RadGridView}}"`.
- 템플릿(별도 네임스코프) 밖 요소를 `ElementName`으로 참조하지 말 것 → `RelativeSource` 사용. 첨부속성은 `Path=(ns:Type.Prop)` 괄호 표기.
- 개발 중 **출력창 바인딩 오류 0** 유지.

---

## 8. 아이콘
- **MahApps.Metro.IconPacks** 사용. 신규 Kind는 **해당 팩 DLL에 존재하는지 확인 후** 사용(없으면 XAML 런타임 예외).
- 현재 검증된 사용 아이콘:
  - `PackIconMaterial`: `ClipboardTextOutline`, `ClockOutline`, `ContentSaveOutline`, `PlusBoxOutline`
  - `PackIconModern`: `OfficeExcel`
  - `PackIconCodicons`: `ChromeMinimize`, `ChromeMaximize`, `ChromeClose`
- 아이콘 색은 `Windows11Resource` 브러시에 바인딩(테마 전환 대응).

---

## 9. 창(Window) · WindowChrome
- `WindowChrome`(ResizeBorderThickness 8, CaptionHeight 44, `UseAeroCaptionButtons="False"`) + 커스텀 최소화/최대화/닫기(코드비하인드).
- 최대화 시 클리핑 보정: `WindowState=Maximized` DataTrigger → `Margin=8`.
- 캡션 버튼 스타일은 App.xaml 공용, 색은 `Windows11Resource`.
- 창 상단에는 프로그램명 + 버전 표기(관리자와 동일 톤).

---

## 10. 접근성 (완료 조건에 포함)
- **명도 대비**: 작은 텍스트 **4.5:1 이상**, 큰 텍스트(일반 18px↑/굵은 14px↑) **3:1 이상**.
- **텍스트 200% 확대**해도 내용·기능이 깨지지 않을 것.
- **색만으로 정보를 구분하지 말 것** — 색약·흑백 사용자 대응.
  - 예) 이상치 경고 = **붉은 강조 + 툴팁 + 상태바 건수**(+아이콘 병행 권장), 보호종 = 강조색 + 굵기.
- 키보드만으로 전 입력 흐름(Tab/Enter/방향키) 완주 가능해야 한다(현장 초고속 입력 목표와 동일).

---

## 11. 신규 화면 체크리스트
1. VM은 DI 주입 + 마커 인터페이스, 색/폰트/코너는 `Windows11Resource`(도메인 색은 `Colors.xaml` 키).
2. 컨트롤 높이 40, 본문 최대폭 1240, 카드 padding 20, 타입 스케일 26/19/14.5/15 준수.
3. 그리드는 §6 필수 설정, 종명은 초성 자동완성 재사용.
4. 결측치(null)와 0 구분, 지수/등급은 `Ecology/*Calculator` 사용.
5. 아이콘 Kind 존재 확인, 바인딩 오류 0 확인.
6. 접근성(§10) 확인 후 완료 처리.
