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

> ⚠ **널 선행 정규식은 일부 이름을 놓친다.** 2026-08-23 에 `PlusBoxOutline` 이 이 방식으로
> "없음"으로 나왔지만 **평문 검색으로는 있었고 실제로도 동작한다.**
> 아이콘 Kind 처럼 존재 여부만 알면 될 때는 **평문 부분문자열 검색**(`b'PlusBoxOutline' in d`)을 쓴다.
> 널 선행은 "토큰 경계까지 정확히" 봐야 할 때만.

**아이콘 Kind도 동일**하게 검증한다. (`MahApps.Metro.IconPacks.Material.dll`)
- 확인됨: `ClockOutline`, `ContentSaveOutline`, `PlusBoxOutline`, `DeleteSweepOutline`, `CloudSyncOutline`,
  `CloudDownloadOutline`, `FolderOpenOutline`, `Reload`, `ClipboardTextOutline`, `FolderMultipleOutline`,
  `AlertCircleOutline`, `CheckboxBlankOutline` (뒤 4개는 2026-08-23 추가 대조)
- **없음**: `Refresh`, `ContentSaveClockOutline`, `BroomOutline`, `Sync`, `Import`

---

## 1. 환경

- .NET 8 WPF, C# nullable enable, ImplicitUsings enable
- **Telerik UI for WPF 2026.3.1007**(2026-10-16 업그레이드 마법사로 2026.2.520 → 올림), 패키지명 `Telerik.UI.for.Wpf.AllControls.Xaml`
  (⚠ `Telerik.UI.for.Wpf.80.Xaml`은 온라인 피드 전용. 로컬 폴더엔 AllControls만 존재)
  - 문서처리(엑셀) DLL 은 **2026.3.1006** 이 따라 들어온다(AllControls 의 의존).
  - 2026.3.1007 은 **nuget.org** 에서 받는다. 2026 Q3 로컬 설치 폴더는 이 PC 에 없다
    (`NuGet.Config` 의 로컬 소스는 2026 Q2 그대로 — 거기엔 2026.2.520 만 있다).
- 로컬 NuGet(2026 Q2): `C:\Program Files (x86)\Progress\Telerik UI for WPF 2026 Q2\Binaries\WPF80\NuGet`
- **라이선스**: `Telerik.Licensing` 1.9.3 을 앱 프로젝트에 명시 참조. 빌드 때 `%AppData%\Telerik\telerik-license.txt` 를 찾아 앱에 넣는다.
  확인: `dotnet build -p:TelerikLicensingVerbosity=diagnostic` → `License OK for "Telerik UI for WPF" … valid until …`.
  키 파일은 저장소에 넣지 않는다.
- ⚠ **2026 Q3 문서처리 네임스페이스 변경** — `Telerik.Windows.Documents.Spreadsheet.*` → **`Telerik.Documents.Spreadsheet.*`**,
  `ThemableColor`·`ThemableFontFamily` 는 **`Telerik.Documents.Common.Model`** 로 옮겨졌다. DLL 파일 이름은 그대로다.
  올린 직후 `Workbook`·`Worksheet` 를 못 찾는 컴파일 오류 74개가 이것이었다(using 만 바꿔 해결).
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


### ⚠ RadGridView 는 열이 넘쳐도 가로 스크롤을 만들지 않는다 — **뭉갠다** (2026-08-23 실측)

열 폭 합이 뷰포트를 넘으면 스크롤바가 생길 것 같지만, `RadGridView` 는 **모든 열을 균등하게 눌러** 폭을 맞춘다.
선언한 `Width` 는 무시되고 `ActualWidth` 만 줄어든다.

```
조류(재배치 전) 1280 폭 :  일반명 선언 190 → 실제  86 px   ← 종명이 안 보인다
양서파충류      1280 폭 :  일반명 선언 180 → 실제  70 px
포유류(재배치 후) 1280 폭 : 일반명 선언 168 → 실제 168 px  ✅
```

**세 경우 모두 "가로 스크롤 없음" 검사는 통과한다.** 그래서 그 검사는 아무것도 걸러내지 못한다.

**판정은 `선언 폭 == 실제 폭` 으로 한다.**

```csharp
// 뭉개진 열이 하나라도 있으면 실패
var squashed = grid.Columns.Cast<GridViewColumn>()
    .Where(c => c.Width.IsAbsolute && c.ActualWidth < c.Width.Value - 1)
    .ToList();
```

- `ComputedHorizontalScrollBarVisibility` 로 판정하지 말 것. 언제나 `Collapsed` 다.
- `ScrollViewer` 를 찾을 때는 `ExtentWidth` 가 가장 큰 것을 고른다 — 첫 번째를 잡으면 머리글용 뷰어라 오판한다.
- 실무 상한(1280 창) : **1행 고정폭 합 ≤ 1,104 px** (= 가용 1,224 − 자유폭 열 최소 120).
  넘치면 폭을 깎지 말고 `RowDetails` 2행으로 내린다(`06_DESIGN_REBUILD.md` §5-2-2).
- **열 수가 아니라 폭 합계로 판단한다.** 6칸을 내려도 새 열 하나가 붙으면 순감이 줄어든다.

### ⚠ Telerik 키 동작은 `MoveFocus()` 로 확인하면 안 된다 (2026-08-23 실측)

`MoveFocus(new TraversalRequest(...))` 는 WPF 논리 포커스만 옮긴다.
**Telerik 컨트롤의 `OnKeyDown` 을 건너뛰므로** "된다"는 잘못된 결론이 난다
(RadGridView 는 Tab 을 가로채 셀 이동으로 처리한다).

**진짜 키를 `InputManager` 로 흘려야 한다.**

```csharp
static void SendTab(Window win)
{
    // ProcessInput 한 번이 Preview → Bubble → PostProcess 전체를 돈다.
    // 두 번 부르면 KeyboardNavigation 이 두 번 처리해 Tab 이 2칸씩 뛴다.
    var src = PresentationSource.FromVisual(win)!;
    InputManager.Current.ProcessInput(
        new KeyEventArgs(Keyboard.PrimaryDevice, src, 0, Key.Tab) { RoutedEvent = Keyboard.KeyDownEvent });
}
```

- `PreviewKeyDownEvent` 와 `KeyDownEvent` 를 **따로 두 번 보내지 말 것.** 한 번이면 된다.
- 창은 `Show()` 로 실제로 띄워야 한다(`Left = -6000` 으로 화면 밖에 두면 사용자를 방해하지 않는다).
  `ContentRendered` 이후 `DispatcherPriority.ApplicationIdle` 로 한 박자 미룬 뒤 검사한다.
- 확인 결과: `RowDetailsVisibilityMode="Visible"` + `KeyboardNavigation.TabNavigation="Local"` 이면
  Tab 이 **1행 열을 모두 지난 뒤** `RowDetails` 안으로 들어가고 다음 행으로 넘어간다.
- `GridViewDataColumn` 에는 **`IsTabStop` 이 없다.** 읽기전용 열을 Tab 순서에서 뺄 수 없다.

### `DataTemplate` 을 코드로 만들 때

`FrameworkElementFactory` 로 조립하면 `"봉인된 템플릿에 있어야 합니다"` 예외가 난다.
검증 하네스에서는 **`XamlReader.Parse`** 로 만드는 것이 실제 XAML 경로와도 같아 안전하다.

```csharp
return (DataTemplate)XamlReader.Parse(
    "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>…</DataTemplate>");
```

### `Grid.RowDefinition.Height` 는 `GridLength` 를 요구한다

`<sys:Double x:Key="TitleBarHeight">50</sys:Double>` 을 `Height="{StaticResource TitleBarHeight}"` 로 쓰면
**빌드는 통과하고 실행 시 XAML 로드가 터진다**(`'50'은 'Height' 속성의 유효한 값이 아닙니다`).
`<GridLength x:Key="TitleBarRowHeight">50</GridLength>` 를 따로 둔다.

### 팔레트/테마
`App.xaml.cs` 생성자 순서 중요: 테마 설정 → 팔레트 → DI → `InitializeComponent()` → 폰트/팔레트 색 적용.
팔레트 `AccentColor`를 바꾸면 기존 화면의 `AccentBrush`가 일괄 반영된다.

### ⚠ 바깥 스크롤 안의 그리드 — `FillPanel` (C-3 · 2026-10-16 실측)

본문 전체를 `ScrollViewer` 로 감싸면 안의 패널은 **높이 무한**으로 재진다. `Grid` 의 별(*) 행은 그때 Auto 처럼 굴어
`RadGridView` 가 모든 행을 만든다(가상화 꺼짐 · 수백 행에서 느림). 그래서 `Views\UserControls\FillPanel` 을 쓴다 — 함정 두 개를 겪었다.

1. **WPF 는 받은 높이보다 크게 원해도 `DesiredSize` 를 받은 높이로 잘라 알린다**(넘친 부분은 잘려 그려진다).
   그래서 안쪽의 최소 높이(`MinHeight`)가 바깥 패널로 올라오지 않는다 — 첫 시도에서 그리드는 최소 높이를 지켰는데 그 아래(합계·상태바)가 잘렸고 바깥 스크롤도 안 생겼다.
   → `FillPanel` 이 채움 칸 안을 따라 내려가 **최소 필요 높이를 직접 센다**(감싸는 요소만 · 안쪽 `FillPanel` 이면 그 고정 칸 합 + 채움 칸 최소).
2. 안쪽 패널은 받은 높이를 늘 다 채우므로 블록을 **접어도 `DesiredSize` 가 그대로** → 바깥이 다시 재지 않아 스크롤이 남는다.
   → 최소 필요 높이가 바뀌면 바깥 `FillPanel` 에 직접 `InvalidateMeasure`.

- 맨 바깥 `FillPanel` 은 바깥 스크롤의 `ViewportHeight` 를 기준으로 삼고, `ScrollChanged`(보이는 높이 변화)에 다시 잰다. 여백은 패널 `Margin` 이 아니라 스크롤 `Padding` 으로 준다.
- 휠: WPF `ScrollViewer` 는 끝에 닿아도 휠 이벤트를 처리됨으로 표시한다(바깥으로 안 넘김). Telerik `GridViewScrollViewer` 도 `ScrollViewer` 를 상속한다(메타데이터 확인).
  → `Behaviors\NestedWheelScroll` 이 바깥 스크롤의 `PreviewMouseWheel` 에서 끝에 닿은 안쪽만 대신 굴린다. 안쪽 스크롤이 없는 곳은 손대지 않는다.

### ⚠ XAML 주석 안에 `-->` 를 쓰지 말 것 (2026-10-16)

기존 주석에 줄을 덧붙이며 `… 후보 이름). -->` 처럼 닫는 표시를 넣으면 **기존 주석이 그 자리에서 닫히고** 남은 주석 글자가 요소 내용이 된다.
빌드와 `xaml_audit` 는 통과하고 **실행할 때** `XamlParseException`(UIElementCollection 에 String 추가)으로 터진다. 화면 하네스가 잡았다.

### ⚠ `CollapsibleSummary` 슬롯 안의 요소에는 `x:Name` 을 줄 수 없다 (2026-10-17)

`SummarySlot`·`ExpandedSlot` 내용은 `CollapsibleSummary` 의 이름 범위에 들어가 빌드가 `MC3093`(이미 다른 범위에 등록)으로 막힌다.
자동 검증이 찾아야 하면 `AutomationProperties.Name` 을 준다(수질 미입력 경고 `"미입력 경고"`).

### 가로 StackPanel 안의 글은 줄바꿈되지 않는다 (2026-10-17 · 수질 미입력 경고)

가로 `StackPanel` 은 자식에게 폭 무한을 준다 — `TextWrapping="Wrap"` 이어도 한 줄로 재고, 바깥 `MaxWidth` 에서 잘린다.
아이콘 + 글은 `Grid`(Auto · *)로 둔다.

### 드롭다운이 열린 채 휠 — 목록창은 별도 창인데 휠은 바깥까지 올라간다 (2026-10-18 · `DropDownWheelGuard`)

- 목록창(`Popup`)은 별도 창이지만 휠 이벤트는 논리 트리를 따라 콤보 → 바깥 `ScrollViewer` 까지 간다.
  바깥이 스크롤되면 목록창은 제자리에 남아 칸에서 떨어져 보인다. C-3 `NestedWheelScroll` 은 목록 끝에서 바깥을 굴렸다.
- `RadComboBox.IsMouseWheelEnabled`(기본 True) — 포커스가 있는 **닫힌** 콤보 위의 휠이 **선택값을 바꾼다**(시험에서 서식수변 점수 · → 30).
  그런데 False 로 해도 콤보가 휠을 처리됨으로 표시해 바깥 화면이 멈춘다.
- 처리: 앱 전체 클래스 처리기 3개(`App` 생성자에서 `DropDownWheelGuard.Register()`) —
  `Window.PreviewMouseWheel`(목록이 열렸고 휠이 목록창 밖이면 닫기 · 닫힌 콤보 위면 가장 가까운 스크롤을 대신 굴림) ·
  `Popup.MouseWheel`(handledEventsToo · 목록창 안 휠이 밖으로 못 나가게) · `RadComboBox.Loaded`(`IsMouseWheelEnabled=False`).
  `NestedWheelScroll` 은 바깥까지 시각 트리로 이어지지 않는 휠(= 목록창에서 온 것)은 손대지 않는다.
- 하네스에서 휠은 `PreviewMouseWheel` → (처리 안 됐으면) 같은 인자로 `MouseWheel` 을 `RaiseEvent` 로 흉내 낸다.
  목록이 열리면 콤보가 마우스를 캡처하므로 **목록 밖 휠은 콤보 자체가 대상**이다.

### `RadAutoCompleteBox` Enter 가 고르는 항목 = `HighlightBehavior` (2026-10-18 · D1)

- 그리드 국명 칸에서 Enter 를 누르면 Telerik 은 **강조된 후보**를 고른다. 기본 강조 규칙(`HighlightBehavior`)은 친 글자를 포함하는 **첫 후보**라
  종목록에 있는 `방울새` 를 정확히 쳐도 앞쪽 `홍방울새` 가 들어갔다(5개 그리드 · 192종).
- `Behaviors\ExactNameHighlightBehavior`(`IHighlightBehavior.FindHighlightedIndex`) — **친 글자와 이름이 똑같은 후보만** 강조, 없으면 -1.
  -1 이면 Enter 가 아무것도 고르지 않아 `GridSpeciesCommit` 이 친 글자를 그대로 남긴다(목록 밖). 방향키·마우스로 고른 후보는 Telerik 기본 그대로.
- 후보 순서는 `ChosungFilteringBehavior` 가 정한다 — 같은 이름 → 앞부분 일치 → 포함(`Helpers\SpeciesNameRank` · 같은 순위는 종목록 순). 빠른추가 목록도 같은 함수.
- 시그니처는 메타데이터로 확인: `int FindHighlightedIndex(string searchText, IList filteredItems, IEnumerable<object> escapedItems, string textSearchPath, TextSearchMode textSearchMode)`.
- 하네스 `d1_grid`(192종 + 분류군별 무작위 20 · ↓ 고르기 · 목록 밖 · 초성) · `diag_d1_all`(5개 분류군 종목록 1,855종 전부 · 약 10분) 통과.

### Telerik 확인한 멤버 (2026.3.1007 · 2026-10-16)

| 용도 | 멤버 |
|---|---|
| 콤보 비우기 단추 | `RadComboBox.ClearSelectionButtonVisibility` · `ClearSelectionButtonContent` |
| 그리드 여러 행 고르기 | `RadGridView.SelectionMode="Extended"` · `SelectedItems` · 이벤트 `SelectionChanged`(`Telerik.Windows.Controls.SelectionChangeEventArgs`) |
| `RadButton` | `System.Windows.Controls.Button` 을 상속한다(`IsCancel`·`IsDefault` 그대로 쓴다) |
| 아이콘(Material) | `FolderZipOutline` · `FolderDownloadOutline` 있음(평문 검색) |
| 그리드 편집기 준비(10-17) | `RadGridView.PreparedCellForEdit`(`GridViewPreparingCellForEditEventArgs` · `Column` · `EditingElement`) |
| 콤보 글자 찾기(10-17) | `RadComboBox.IsTextSearchEnabled` · `TextSearchMode` — 점수 숫자 입력은 `PreviewTextInput` 에서 먼저 처리하고 끝낸다(`ScoreKeySelect`) |
| `PlacementMode` | `Telerik.Windows.Controls` 에도 같은 이름이 있다 — `System.Windows.Controls.Primitives.PlacementMode` 로 적는다 |

- .NET 8 WPF 에는 폴더 고르기 `Microsoft.Win32.OpenFolderDialog` 가 있다(새 패키지 불필요 · C-8).

---

## 3. Telerik 문서처리 (엑셀 읽기/쓰기)

관리자와 **동일 라이브러리**를 쓴다 → 100% 호환.

```csharp
// ⚠ 2026 Q3(2026.3.1006)부터 Telerik.Windows.Documents.* → Telerik.Documents.* (§1)
using Telerik.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx; // XlsxFormatProvider
using Telerik.Documents.Spreadsheet.Model;      // Workbook, Worksheet, CellSelection, CellRange, PatternFill, PatternType
using Telerik.Documents.Spreadsheet.Formatting; // ⚠ 2026 Q3 에서 ColumnWidth·RadHorizontalAlignment 는 Model 네임스페이스에 있다
using ThemableColor = Telerik.Documents.Common.Model.ThemableColor;           // ⚠ 네임스페이스 주의
using ThemableFontFamily = Telerik.Documents.Common.Model.ThemableFontFamily; // ⚠ 네임스페이스 주의
```

- **읽기**: `new XlsxFormatProvider().Import(stream, null)`
- **쓰기**: `new XlsxFormatProvider().Export(workbook, stream, null)` ← **3번째 인자 `TimeSpan?` 필수**
- 셀 인덱스는 **0-based** (`Cells[0,0]` = A1)
- 서식: `SetIsBold` `SetFontSize` `SetForeColor(new ThemableColor(color))` `SetFill(new PatternFill(PatternType.Solid, fore, back))` `SetHorizontalAlignment(RadHorizontalAlignment.Center)` `SetFontFamily(new ThemableFontFamily("맑은 고딕"))`
- 컬럼 폭: `ws.Columns[i].SetWidth(new ColumnWidth(px, true))`

### ⚠ License 시트 — 라이선스 증빙은 **진입 실행 파일**에서 읽힌다 (2026-10-16 실측)

- `Telerik.Licensing` 이 빌드 때 증빙을 그 프로젝트의 어셈블리에 넣는다. 런타임은 **진입 실행 파일**의 증빙을 본다.
- 앱(`SDSM_Surveyor_App.exe`)이 만든 엑셀에는 `License` 시트가 붙지 않는다. 같은 exporter 를 **다른 실행 파일**(BaselineGen·검증 하네스)에서
  부르면 그 실행 파일에 증빙이 없는 한 계속 붙는다 → 시험 도구로 License 시트를 확인하려면 그 도구에도 `Telerik.Licensing` 을 참조한다.

### ⚠ 엑셀 왕복의 알려진 결함 (2026.3.1006 기준)

| 결함 | 증상 | 범위 |
|---|---|---|
| 빈 인덱스의 `CHOOSE` | `CHOOSE(X$17,…)` 의 인덱스 셀이 비면 Export 가 `InvalidCastException(Double → RadExpression)` 으로 **파일을 못 만든다**(Export 는 셀 형식을 정하려고 모든 수식을 평가한다) | 2026 Q2·Q3 둘 다. 방류하천 어류 양식 69·70·74행 |
| `SUMPRODUCT` 에 `@` 삽입 | `SUMPRODUCT(($E$79:$E$331="O")*(CQ$79:CQ$331<>""))` 를 Import→Export 하면 파일 속 수식이 `SUMPRODUCT((@$E$79:…="O")*(@CQ$79:…<>""))` 로 바뀌고 Excel 에서 **`#VALUE!`** | **2026 Q3 에서 새로 생김**(Q2 왕복본은 정상). 어류 양식 36·37·38·41행. 끄는 설정 없음(`XlsxExportSettings` = `SkipShapes` 뿐) |

→ 양식 파일을 Telerik 으로 열어 다시 저장하는 기능(B-7 양식 내장)은 **어류 양식에 쓰지 않는다**. 저서·조류·포유류·양서파충류·수질·서식수변 양식은 왕복이 깨끗하다.

### 통합문서 사이 셀 옮기기 (C-8 보고서 일괄 · 2026-10-16 실측)

`using var frag = src.Cells[r0, c0, r1, c1].Copy();` → `dst.Cells[r, c].Paste(frag, PasteOptions.All)` — **다른 통합문서로도 서식째 옮겨진다**.
열 너비는 따라오지 않으므로 `dst.Columns[c].SetWidth(src.Columns[c].GetWidth().Value)` 로 따로 옮긴다.
보고서 일괄(`Export\CombinedReport`)은 지점별 보고서 exporter 의 `Build(vm)` 통합문서를 이렇게 합친다(계산을 다시 하지 않는다).

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
**확장항목(TN·EC·Cl·SO4·Cu·Zn·Cr·탁도·Chla·수온·수심·유속·초당유량·일당유량)은 고정 행이 아니다.** 관리자가 행22부터 **C열(및 D열)의 라벨 텍스트**를 검색해 찾는다.

**행 검색 규칙(관리자 `BulkWaterQualityControlViewModel` · 앱 `WaterQualityExcelExporter.FindRow` 가 같은 규칙):**
- 0-based 행22부터 아래로, **C열이 빈 행은 건너뛴다**.
- `C열 글자 + D열 글자` 를 이어 붙여 **공백 제거·소문자화** 한 뒤, 항목별 검색어 중 하나라도 `Contains` 이면 **첫 일치 행**을 채택한다.
- 검색어: T-N=`TN`·`T-N` / 전기전도도=`EC`·`전기전도도`·`ElectricalConductivity`·`Electrical` / 염소이온=`Cl`·`Cl-`·`염소이온` /
  황이온=`SO42`·`SO42-`·`황이온` / 구리=`구리`·`Cu` / 아연=`아연`·`Zn` / 크롬=`크롬`·`Cr` / 탁도=`탁도` / 클로로필a=`클로로필a`·`클로로필`·`Chla`·`Chl-a` /
  수온=`수온`·`온도` / 수심=`수심` / 유속=`유속` / 초당유량=`초당유량`·`m3/sec`·`㎥/sec` / 일당유량=`일당유량`·`m3/day`·`㎥/day`.

**앱 내보내기(2026-10-16 (3) · B-7):**
- 내장 양식(`water.xlsx`)을 열고, 위 규칙으로 **양식 행을 찾아 F열에만** 쓴다. 양식의 라벨(B~D)·예시(E)는 건드리지 않는다 — **단위를 E열에 쓰지 않는다**.
- 2026-03 최종본 양식에 **없는 항목은 `수심` 하나**다. 값이 있으면 양식의 `지속 삽입 가능` 행(엑셀 36행) 아래 **B~F 가 빈 첫 행**(엑셀 37행)에
  C열 라벨 `수심`(관리자 검색어 그대로)과 F열 값을 덧붙인다. 값이 없으면 덧붙이지 않는다. 덧붙인 항목은 내보내기 완료 안내에 목록으로 나온다.
- `지속 삽입 가능` 행을 못 찾으면(양식이 바뀜) 멈춘다(`BulkTemplateException`).

> ⚠ **라벨에 단위를 붙이지 말 것.** 예를 들어 `초당유량(m3/sec)`은 `ec`를 포함해 **EC(전기전도도)로 오인식**된다(첫 일치 행 채택).
> 덧붙이는 라벨도 단위 없는 검색어 그대로 쓴다.

> ⚠ **양식 숫자 서식으로 읽힌다.** 관리자 Import 는 `GetResultValueAsString(GetFormat())`, 곧 **서식이 적용된 글자**를 읽는다.
> 양식 F열 서식(14~29행 `0.00`, T-P `0.000` 등)보다 자릿수가 많은 값은 그 자리에서 반올림되어 들어간다
> (예: pH 7.856 → `7.86`, T-P 0.0523 → `0.052`). 공식 양식으로 제출한 실자료와 같은 동작이다(작업로그_261015 「2026-10-16 (3)」).

### 서식및수변 행 매핑
`1`연도차수 `2`지점명 `3`조사일 `4`대권역 `5`중권역 `6`하천명 `7`하천유형 `8`위도 `9`경도 `10`날씨 `11`조사기관 `12`조사자 `13`조사불가사유 `14`비고 `16~25`**평가항목1~10**

### 조류/포유류/양서파충류 (열 매핑, 행4부터)
- **조류**: `1`연도차수 `2`조사일 `3`날씨 `4`하천명 `5`지점명 `6`국명 `7`학명 `8`특징 `9~11`위도도분초 `12~14`경도도분초 `15`위도 `16`경도 `18`개체수 `19`도래유형 `20`대항목 `21`세부항목 `22`서식유형 `23`조사자 `24`비고
- **포유류**: `1`연도차수 `2`조사일 `3`날씨 `4`하천명 `5`지점명 `6`국명 `7`학명 `8`특징 `9~11`위도도분초 `12~14`경도도분초 `15`위도 `16`경도 `17`관찰지유형 `19~30`**Trace1~12** `31`조사자 `32`비고
  - ⚠ **관찰지유형 열이 관리자와 양식에서 다르다.** 관리자 Import(현재)는 `17`(R열)을 읽는다.
    **2026-03 최종본 양식의 머리글 `관찰지 유형`과 실자료는 `18`(S열)** 이다(2026-10-16 확인).
  - **앱은 양식대로 `18`(S열)에 쓰고 R열에는 쓰지 않는다**(2026-10-16 (2) 결정 · `MammalExcelExporter.ObservationSiteCol`).
    관리자 Import 를 S열로 고치는 일은 관리자 저장소에서 따로 한다 — 그 전까지 관리자로 들여오면 관찰지유형이 비어 보인다.
- **양서파충류**: `1`연도차수 `2`조사일 `3`날씨 `4`하천명 `5`지점명 `6`국명 `7`학명 `8`특징 `9~14`**Trace1~6** `15~17`위도도분초 `18~20`경도도분초 `21`위도 `22`경도 `24`대분류 `25`중분류 `26`조사자 `27`비고
  - ⚠ 양식의 `2`조사일(C열) 칸은 날짜 서식(`mm-dd-yy`)이다. 숫자 `20250923` 을 넣으면 날짜 일련번호로 읽혀 깨진다 → **글자 `yyyyMMdd`** 로 쓴다
    (관리자 Import 는 `DateTime.TryParse` 실패 시 8자리 숫자 글자를 날짜로 바꾼다).
  - 최종본 자료 행(5~824행)의 학명(H)·특징(I) VLOOKUP 수식은 내장본에서 지웠다(2026-10-16 (2) 결정) — 앱이 그 칸에 값을 쓴다.

### 자동계산 필드 (엑셀에 넣지 않는다 — 관리자가 재계산)
- 어류: 총종수·총개체수·M1~M7·FAIScore·FAIGrade·종별 RankScorer
- 저서: DI·H'·R1·J'·BMIScore·BMIGrade·RankScorer
- 수질: PH/BOD/COD/TOC/SS/DO/TP/EColi **등급 8종**
- 서식수변: EvaluationScore·EvaluationGrade
- 조류·포유류·양서파충류: **자동계산 없음(전 필드 입력)**

> 단, **재계산의 입력값**은 반드시 채워야 한다: 하천차수, DE/EF/LE/TU, 종별 개체수, 수질 원측정값, 평가항목1~10, 채집불가사유.

---

### 일괄입력용 양식 내장본 (B-7 · 2026-10-16)

- `SDSM_Surveyor_App\templates\bulk\` — `benthos·bird·mammal·amphrep·water·habitat.xlsx`(최종본에서 실자료만 지운 것) + `fish_labels.json`(어류 라벨·필독 글자).
  `tools\TemplateGen` 이 ③ 최종본을 읽어 만든다(손으로 고치지 않는다 · README.md 에 원본·처리 내용).
- exporter 는 `Export\BulkTemplate` 으로 내장본을 열어 **위 표의 좌표 그대로** 쓴다. 쓰려는 칸이 양식에서 수식이면 쓰지 않고 멈추고,
  내장본이 없으면 빈 통합문서로 대신하지 않고 멈춘다.
- 지금 내장본을 쓰는 것: **저서동물·조류·포유류·양서파충류·수질·서식수변(6종 전부)**. 어류는 양식을 쓰지 않고 글자만 덧붙인다(Telerik 왕복 결함 · §3).
  포유류(관찰지유형 S열)·수질(행 검색·수심 덧붙임)·양서파충류(조사일 글자·H/I 수식 지움)의 세부는 위 §4 각 항목.
- 내장본에 글자로 꼭 넣어야 하는 칸은 `BulkTemplate.PutText`(수식 칸 보호는 `Put` 과 같다).
- Telerik 은 지운 시트의 그림을 참조 없는 채로 패키지에 남긴다 — TemplateGen 이 참조 없는 `xl/media` 를 걷어낸다.
- 검사: `python tools\bulk_check.py templates <내장본> <③>` (실자료 잔존 0 등) · `python tools\bulk_check.py files <세션.json> <일괄입력용 폴더>` (세션 값 대조).
  예시 7개 생성: `dotnet tools\BaselineGen\bin\Debug\net8.0-windows\BaselineGen.dll --seed <세션.json> --out <폴더>`.

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

| 항목 | 처리 결과 (2026-08-20 수정 완료 · `12_CALC_FIX.md`) |
|---|---|
| **M8 정수 나눗셈** | ✅ 양쪽 실수 나눗셈 + `총개체수 > 0` 가드. **0으로 나누기 예외도 함께 해소** |
| **어류·저서 0종 자료** | ✅ 점수 0 · 등급 **E**(관리자·조사자 동일). 우선순위 **조사불가 > 0종 선언 > 미입력** |
| BMI 음수 등급 | ✅ 조사자도 `Check` 로 통일 |
| HRI `"접근 불가"`(공백) | ✅ 관리자 편집화면도 공백 제거 후 비교 |
| HRI 평가항목 전부 빈 값 | ✅ 편집화면도 `null`(`-`)로 통일(`ADMIN_TASKS.md` §7-1). 세 경로 값 동일 |

**수정 후 재대조: 1,388건 전부 일치(불일치 0).** FAI·저서·수질·HRI(편집화면·일괄입력) 모두 동일하다.

> ⚠ **0종 선언은 조사자 화면의 명시적 입력이다.** 종 목록이 비었다는 사실만으로는
> "아직 미입력"과 "조사했는데 0종"을 구분할 수 없다(CLAUDE.md §7.2). 화면의 `출현종 없음(0종)` 토글로 선언받는다.
> 관리자에는 "미입력" 상태가 없다(연도차수가 있어야 레코드가 생긴다) → 0종이면 항상 조사 수행으로 본다.

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

### 지점 — 권역 · 정렬 · St.번호 (C-6·C-7 · 2026-10-16)

- **대권역·중권역**: `Data\RegionTable`(하천 단위 고정 · 키 = `sites.json` 의 `River`). 지점을 고르면 채우고, **세션 복원(대량 적용 구간)에서는 채우지 않는다**(저장된 값 보존).
- **정렬·표시**: `Data\SiteOrdering` — 하천(가나다) → 그 하천 St.번호가 결측·중복 없이 다 있으면 번호 순, 아니면 지점명 자연 정렬. St.번호 표시(`곡교천1 (St.1)`)도 같은 판정.
  하천 이름을 코드에 박지 않는다(자료로 판정). 공유 `SurveySite.Display` 는 늘 번호를 붙이므로 조사자 앱 콤보는 `SurveyMeta.SiteChoice` 를 쓴다.
- **St.번호 입력**: 공유 `SiteResolver.Resolve` 는 대분류 안 첫 일치를 고른다(`St.4` → 늘 곡교천4). 조사자 앱 `SiteListProvider.Resolve` 가 감싸 후보(같은 St.번호·지도 번호)가 **둘 이상이면 고르지 않고** 콤보에 그 후보만 보여 준다.
  2026-10-15 자료 기준 후보 예: `St.4` → 곡교천4·오산천4·오산천5, `St.11` → 성성공원·오산천3_1·오산천6.

### 생태현황 지점 · 연도 구간 · 목록 밖 지점 (D4 · 2026-10-18)

- `sites.json` 생태현황 149곳(어류 118 · 조류 28 · 포유류 3 · 항목 155) — `tools/build_sites_json.py --ecostatus <추출 보고서>`.
  **방류하천 항목은 기존 파일에서 그대로 옮긴다**(엑셀에서 다시 만들지 않는다 · 전후 대조로 43곳 글자 그대로 확인).
  원천은 `..\SDSM\docs\_ecostatus_sites_extracted.md` §3 표(+ §4(가) 기록별 좌표). 관리자 DB(게시본) 어류 127건과 대조해 불일치 0.
- 판정 규칙(생성기에 코드로): 같은 이름의 기록이 **다른 해에 300m 넘게** 떨어지면 해마다 별개 지점(`ValidFrom` = 그 해, `ValidTo` = 다음 조사 연도 − 1) —
  §8-3 표와 같다(진위천1~5 · 오산천2). 300m 이내면 하나로 두고 **가장 최근 기록**의 좌표·차수(오산천1 — 차수 2023년 4 · 2025년 2 를 보완 필요로).
  같은 해에 두 곳(진위천1·2 의 2025 송전천)은 **하천명 = 조사권역(지점명 접두어)** 인 기록을 고르고 나머지를 `Desc` 에 적는다(§8-4 · 개명 금지).
  조류·포유류는 좌표를 넣지 않는다(기록 좌표가 정점이 아님 · §7-5·§7-6 · §9 (가)) → `보완 필요: 좌표`. 하천명은 기록 수가 가장 많은 값, 여러 개면 `하천명 여러 개(…)`.
  §4(라) 하천이 아닌 값(0 · 호소·공원 · 현장 메모 · 권역명)은 값은 그대로 두고 `하천명(원본 값 '…')`. 황구지천·황구지천1 은 `같은 지점인지 미판정`.
  `Workplace` 는 비운다(관리자 DB `SiteDivision` 생태현황 행의 사업장이 비어 있다) · `SiteGroup` = 지점명 접두어(조사권역).
- 조사자 앱: `ISiteListProvider.ByProject/Resolve` 에 `year`(선택) — `SurveySite.IsValidFor` 로 거른 뒤 공유 `SiteResolver` 에 넘긴다(공유 코드는 그대로).
  `SurveyMeta.SiteYear` = 연도 칸, 없으면 연도차수 앞 네 자리. 연도가 바뀌면 같은 이름의 그 해 지점으로 바꾼다(좌표가 그 구간 값으로).
  지점 콤보 항목은 구간 `[2023~2024]` · 생태현황은 분류군(` · 조류`)을 붙인다.
- 지점을 고르면 좌표·사업장을 **마스터 값으로 덮어쓴다(없으면 비운다)** — 전에는 마스터에 좌표가 없으면 앞 지점 좌표가 남았다.
  세션 복원(`SurveyMetaSnapshot.ApplyTo`)은 마스터에 빈 칸(하천·사업장·좌표)만 저장된 값으로 되살린다(조류 지점에 손으로 적은 좌표).
- 목록 밖 지점(생태현황만): 지점명을 직접 치거나 지점명 없이 하천명만 — `SurveyMeta.IsOffListSite` · 안내 '목록 밖 지점'.
  저장 키의 지점 자리 = `SiteKeys.Of` — 지점명, 없으면 `하천명(지점명 없음)`(그냥 하천명이면 포유류 지점 `평택` 과 세션 파일이 겹친다).
  일괄입력용·보고서 파일명도 같은 값. 저장 필수 칸 = 대분류 · 연도차수 · 지점명 또는 하천명(생태현황) / 지점명(방류하천).
  하천명을 직접 적으면 `RegionTable` 에 있는 하천만 권역을 채우고(비어 있을 때), 대권역·중권역 칸은 편집 가능한 콤보(관리자 DB 게시본 값 한강·금강 / 안성천·삽교천·경안천·남한강하류).
- 관리자 Import(`ImportCheck`)는 생태현황 파일을 지점 목록과 대조하지 않는다(생태현황은 `SiteDivision` 한 행) — 목록 밖·지점명 없음 파일도 오류 0.

### 하천유형 두 칸 · 지점 칸 Enter·Tab · 생태현황 목록 분류군별 (E1~E4 · 2026-10-18 (2))

- **E1 하천유형 두 칸** — `SurveyMeta.RiverType`(어류·저서) · `RiverTypeWater`(수질·서식수변) · 세션 `SurveyMetaSnapshot.RiverTypeWater`(추가한 칸 하나).
  `ApplyTaxonForm(kind)` 는 보일 칸만 정한다(`IsRiverTypeFishVisible`/`IsRiverTypeWaterVisible` · 값은 비우지 않음 — D7 의 '비우기'는 걷어냄).
  출력: 어류·저서 `m.RiverType` · 수질·서식수변 `m.RiverTypeWater`(일괄입력용 8행 · 보고서 `WriteSurveyMeta(m, riverType)`). 관찰형 3종 보고서 조사개황 '하천유형' 칸은 종전대로 `RiverType`.
  복원(`ApplyTo`)에서 `RiverTypeWater` 가 없고 `RiverType` 이 본류/지류/기타면 옮긴다. 알림은 칸별(`RiverTypeNotice` · `RiverTypeWaterNotice`).
  `tools\bulk_check.py` 도 같은 규칙(`river_types`) · BaselineGen 픽스처에 `RiverTypeWater = "지류"`.
- **E2 지점 칸** — ⚠ Telerik `RadComboBox`(편집형)는 **닫힌 채로 글자를 치는 동안에도** 그 글자로 시작하는 첫 항목을 `SelectedItem` 으로 고른다
  (`곡교` → 곡교천1 · `CanAutocompleteSelectItems=False` 로도 막히지 않음 · 하네스 `diag_site_trace` 실측). 포커스가 떠날 때도 고른다(`UpdateSelectionOnLostFocus` 기본 True → False 로).
  그래서 치는 동안(`PreviewTextInput`·Back·Delete)은 `SurveyMeta.HoldSiteSelection` 을 켜 콤보의 선택을 조사개황이 받지 않고, 방향키·마우스 누름에서 끈다.
  Enter·Tab 은 `SurveyOverviewControl.Decide` — ① `ResolveSiteText` 성공 ② 방향키로 움직였으면 콤보 기본 동작 ③ 고르지 않음(방류하천 `SiteEntryNotice` · 생태현황 `ChoiceDialog` [예]=`CommitOffListSite`).
  ③에서는 콤보가 스스로 고른 항목을 걷어 내고 친 글자를 되돌린다. 포커스 이탈도 같은 규칙(확인 창이 떠 있는 동안은 건너뜀).
  함께 고친 것: 전에는 목록에 없는 지점명을 쳐도 **조사개황 `Site` 로 들어가는 길이 없었다**(콤보 글자만 남고 저장 안 됨 — D4 ② 직접 입력은 프로그램으로만 시험됐었다).
- **E3** — `SurveyMeta.CurrentTaxon`(`ApplyTaxonForm` 이 넣음) · 생태현황 `RefreshSiteOptions` 는 `Taxa` 에 지금 분류군이 있는 지점만(Taxa 가 비면 모두).
  친 글자 해석도 그 목록 안에서(세션 복원은 대분류 전체 — 다른 탭에서 불러와도 지점을 되찾게). 고른 지점이 목록 밖이면 콤보 항목에 덧붙여 `(조류 지점)` 표시.
- **E4** — [새 지점](`SessionService.StartNewSite`)은 압정 상태를 보지 않는다(설계 · CLAUDE.md §7).
- **G1 지점 칸 ↓ (2026-10-18 (3))** — ↑↓·PgUp/PgDn 은 콤보 기본 동작(닫힌 채 전체 목록에서 바로 고름 — `진위` ↓↓ → `삽교천`) 대신
  `SurveyOverviewControl.MoveCandidate` 가 처리한다: 처음 ↓ 에서 `SurveyMeta.SiteCandidates(친 글자)`(지금 목록을 같음 → 앞부분 → 포함 순 ·
  `SpeciesNameRank` · 지점명과 표기 중 잘 맞는 쪽 · 글자가 없으면 전부)로 콤보 항목을 바꿔 펼치고, 강조만 옮긴다(선택 보류 그대로 — 조사개황은 안 바뀜).
  Enter·Tab = 강조된 후보로 확정(E2 ②) · Esc = 목록만 닫고 친 글자로 · 글자를 더 치면 친 글자로 돌아가 잇는다 · 목록이 닫히면 항목을 지금 목록 전부로 되돌린다.
  ⚠ 콤보 자체 거르기(`IsFilteringEnabled`)를 끄면 Telerik 이 **치는 글자를 첫 항목 표기로 자동 완성**한다(`진위천1` → `진위천15` · 하네스에서 확인) —
  `IsTextSearchEnabled=False` 를 함께 꺼야 한다. 두 가지를 끄고 거르기·강조는 앱이 한다.
  ⚠ 강조는 콤보 `SelectedItem` 을 코드로 바꾸면 안 된다 — 양방향 바인딩이 (선택 보류 중이라 그대로인) `SelectedSiteChoice` 를 다시 읽어 곧바로 되돌린다.
  `SurveyMeta.PreviewSiteChoice` 가 보류 중에만 쓰는 '강조 후보'를 두고 `SelectedSiteChoice` 가 그것을 돌려준다(조사개황 `SelectedSite` 는 그대로 · 보류가 풀리면 지움).
- **G2** — 관찰형 3종 보고서 조사개황은 하천유형 행이 없다(`WriteSurveyMeta(m, null, withRiverType: false)` · 14행). 각주에서도 '하천유형'을 뺌. 일괄입력용·다른 보고서는 그대로.

## 7. 개발 환경 주의

- F: 드라이브 마운트에서 **bash로 방금 쓴 파일을 읽으면 내용이 오래된 경우가 있다.** 파일 도구(Read/Edit/Write)를 신뢰할 것. 특히 `.git/config`를 bash로 판단하지 말 것.
- CS8826 경고(부분 메서드 시그니처 차이)는 `On○○Changed(T v)` 매개변수명을 소스 생성기 규약인 **`value`** 로 바꾸면 사라진다. 동작에는 영향 없음.

- WPF `App.xaml`의 리소스 사전은 **절대 pack URI**(`pack://application:,,,/SDSM_Surveyor_App;component/Styles/Colors.xaml`)로 쓴다.
  상대 URI(`/Styles/Colors.xaml`)는 **진입 어셈블리** 기준으로 풀려서, 앱을 다른 exe(테스트 하네스 등)에서 호스팅하면 `'styles/colors.xaml' 리소스가 없습니다`로 실패한다.
- **캡션바(WindowChrome) 버튼은 UI Automation으로 누를 수 없다.** `InvokePattern.Invoke()`도 합성 마우스 클릭도 핸들러에 닿지 않는다
  (기존 [동기화] 버튼도 동일). 창 자체를 검증할 때는 `Application.ResourceAssembly` 대신 **실제 `App`을 생성해 창을 직접 `Show()`** 하는 하네스를 쓴다.
- **UI 스레드에서 `Task.GetAwaiter().GetResult()` 금지.** WPF 디스패처가 잡혀 교착한다. `DispatcherTimer.Tick`은 `async (_, _) =>` 로 만들고 `await` 할 것.
  `[RelayCommand]`가 만든 **비동기 명령은 `Execute(null)`로 완료를 기다릴 수 없다** — `await cmd.ExecuteAsync(null)` 을 쓴다.

## 8. AppData 실자료 취급 (사고 재발 방지)

`%AppData%\SDSM_Surveyor\` 는 **사용자의 실제 조사 자료**다. 검증 하네스에서 절대 하지 말 것:

- ❌ 폴더를 다른 이름으로 **옮겼다가 되돌리는** 기법. 복구 루틴이 두 번 돌면(명시 호출 + `ProcessExit`) 되돌린 폴더를 그대로 지운다.
- ❌ `Directory.Delete(appRoot, true)` — 되돌리기 실패 시 원본이 사라진다. 휴지통으로도 가지 않는다.
- ✅ 하네스는 **자기가 만든 파일만 이름으로 지운다**. 실폴더 자체는 건드리지 않는다.
- ✅ `Environment.GetFolderPath(ApplicationData)` 는 **`APPDATA` 환경변수로 못 바꾼다**(Windows는 레지스트리를 본다). 샌드박스로 돌릴 수 있다고 가정하지 말 것.
