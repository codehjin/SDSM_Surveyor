# 2단계 — 남은 5개 화면 전체 필드 확장

> 선행: `01_GITHUB_SPLIT.md` 완료
> 필독: `90_TECH_NOTES.md` (특히 §2 Telerik 함정)

**목표**: 조류·포유류·양서파충류·수질·서식수변 화면을 **어류·저서동물과 동일한 수준**으로 끌어올린다.
필드 목록의 근거는 관리자 모델(`SDSM\SDSM_Models\*.cs`)이다. **자동계산 필드만 빼고 전부 입력**한다.

---

## 0-0. 착수 전 — 빌드 복구 (먼저 확인)

이 단계 시작 시점에 **빌드가 깨져 있을 수 있다.**
`Export\FishReportExporter.cs` 의 AutoFilter 적용부에서 `IWorksheetFilter`가 internal이라 CS0122가 발생했다.

1. 먼저 빌드해서 상태를 확인한다.
2. 오류가 있으면 `90_TECH_NOTES.md` §3의 AutoFilter 항목대로 처리한다.
   - `ws.Filter.FilterRange = new CellRange(...)` 대입이 되면 그대로 사용
   - **그것도 접근 제한이면 자동 필터를 제거한다.** 표(헤더+데이터) 구조만 유지하면 사용자가 엑셀에서 `데이터 > 필터`를 한 번 누르면 동일하다.
   - 이 사소한 기능 때문에 진행을 막지 말 것. 처리 결과를 `90_TECH_NOTES.md` §3에 한 줄로 기록한다.
3. **빌드가 통과한 뒤** 아래 작업을 시작한다.

---

## 0. 기준 패턴 (어류·저서동물이 이미 이 구조다 — 그대로 복제)

참고 파일: `Views\UserControls\FishEntryControl.xaml(.cs)`, `ViewModels\FishEntryViewModel.cs`

1. **공통 조사개황**: VM에 `public SurveyMeta Meta { get; } = new();`
   XAML에서 `<ctrl:SurveyOverviewControl DataContext="{Binding Meta}"/>`
   → 분류군 VM에 연도차수·조사일·권역·하천·지점·좌표·날씨·조사기관·조사자를 **개별 속성으로 두지 않는다.**
2. **접이식**: 상단 카드에 `RadToggleButton`(x:Name=MetaToggle, IsChecked=True) + 상세부 `Visibility="{Binding IsChecked, ElementName=MetaToggle, Converter={StaticResource BoolToVisibility}}"`
3. **그룹 소제목**: `Foreground="{StaticResource AppAccentStrongBrush}"` + FontWeight SemiBold
4. **종 빠른추가 바**(그리드 위): `RadComboBox`(편집형) + 개체수 + [추가], Enter 연속입력, 종 선택 시 개체수 칸으로 포커스 이동, 추가 후 검색창 복귀
5. **엑셀 붙여넣기**: 그리드 `PreviewKeyDown`에서 Ctrl+V 가로채 `VM.PasteRows(clipboardText)` 호출
6. **빈 행 정리** 버튼 + 초기 빈 행을 만들지 않음
7. 그리드 옵션: `GroupRenderMode="Flat"` `ShowGroupPanel="False"` `ClipboardCopyMode="Cells"` `RowIndicatorVisibility="Collapsed"`

---

## 1. 조류 (Bird)

근거: `SDSM\SDSM_Models\Bird.cs` · **자동계산 없음(전 필드 입력)**

**조사개황**: `SurveyMeta` 공통 사용 (연도·연도차수·조사일·날씨·하천명·지점명·조사자 등)
> 참고: 관리자 Bird 테이블은 대권역/중권역/하천유형/조사기관 컬럼이 없다. 공통 컨트롤을 쓰되 **내보내기 시 해당 열만 비운다.**

**그리드(1행 = 1출현기록)** — 현재 누락된 것 굵게
| 컬럼 | 속성 | 비고 |
|---|---|---|
| 일반명(국명) | SpeciesKo | 초성 검색 |
| 학명 | SpeciesEn | 종 선택 시 자동 |
| 개체수 | IndividualCount | int? |
| 도래유형 | MigratoryType | 드롭다운(RES/SV/WV/PM 등) |
| 대항목 | Category | |
| 세부항목 | CategoryDetail | |
| 서식유형 | HabitatType | |
| **위도** | **Lat** | **기록별 좌표(신규)** |
| **경도** | **Lng** | **기록별 좌표(신규)** |
| 특징 | Feature | |
| **특이사항** | **Note** | **신규** |

---

## 2. 포유류 (Mammal)

근거: `Mammal.cs` · **자동계산 없음**

**그리드**
| 컬럼 | 속성 | 비고 |
|---|---|---|
| 일반명 | SpeciesKo | 초성 검색 |
| 학명 | SpeciesEn | 자동 |
| 관찰지유형 | ObservationSite | |
| **포획·관찰·울음·사체·족적·털·식흔·굴·번식지·배설물·카메라·기타** | **Trace1~12** | **⚠ 현재 bool 체크박스 → `int?`(개체수/횟수) 입력으로 교체** |
| **위도 / 경도** | **Lat / Lng** | **신규** |
| 특징 | Feature | |
| **특이사항** | **Note** | **신규** |

> Trace 컬럼이 12개라 가로가 길다. 헤더 글자가 잘리지 않도록 폭을 충분히 주고 `IsFilterable="False"` 로 필터 아이콘을 제거한다.

---

## 3. 양서파충류 (AmphibianReptile)

근거: `AmphibianReptile.cs` · **자동계산 없음**

**그리드**
| 컬럼 | 속성 | 비고 |
|---|---|---|
| 일반명 | SpeciesKo | 초성 검색 |
| 학명 | SpeciesEn | 자동 |
| **성체·유생·알·울음소리·로드킬·기타** | **Trace1~6** | **⚠ bool → `int?` 로 교체** |
| **대분류 / 중분류** | **MajorCategory / MiddleCategory** | 서식지 유형 |
| **위도 / 경도** | **Lat / Lng** | **신규** |
| 특징 | Feature | |
| **특이사항** | **Note** | **신규** |

---

## 4. 수질 (WaterQuality)

근거: `WaterQuality.cs` · **자동계산: 등급 8종**(PH/BOD/COD/TOC/SS/DO/TP/EColi) — 입력하지 않고 앱이 산출

**측정항목(현재 있음)**: PH, BOD, COD, TOC, SS, DO, TP, 생태독성(Ecotoxicity), 대장균군(EColi)

**추가항목 14개 (전부 신규)**
`TN`, `EC(전기전도도)`, `Cl(염소이온)`, `SO42(황이온)`, `Cu`, `Zn`, `Cr`, `Turbidity(탁도)`, `Chla(클로로필a)`, `WaterTemperature(수온)`, `WaterDepth(수심)`, `FlowVelocity(유속)`, `FlowSec(초당유량)`, `FlowDay(일당유량)`

**배치**: 조사개황(공통·접이식) → [측정항목] 그룹(등급 자동 표시) → [추가항목] 그룹.
현재 화면은 항목이 한 줄에 몰려 아래가 비어 있다. **2~3열 그룹 배치**로 정리한다.

---

## 5. 서식수변 (HabitatWaterEdge)

근거: `HabitatWaterEdge.cs` · **자동계산: 평가점수·평가등급**

- 평가항목 1~10 드롭다운([별표5] 점수) + 좌/우안 평균 — **이미 구현되어 있음. 유지.**
- 조사개황을 **공통 `SurveyMeta`로 교체**(현재 개별 속성 사용 중)
- **신규**: `Note`(비고) 입력 추가
- 우측 HRI 패널 유지

---

## 6. 공통 작업

1. **5개 화면 모두** 조사개황을 `SurveyMeta` + `SurveyOverviewControl`로 교체
   → 기존 개별 속성(YearChsu·SurveyDate·River·Site·Surveyor·Weather 등)은 VM에서 제거하고 `Meta.X`로 대체.
   임시저장 Draft 생성부도 `Meta.X`를 읽도록 수정.
2. **관찰형 3종**에 종 빠른추가 바 + Ctrl+V 붙여넣기 + 빈 행 정리 적용
3. **Trace bool → int?** 변경에 따라 모델(`Models\MammalEntry.cs`, `AmphibianReptileEntry.cs`)·그리드 컬럼(`GridViewCheckBoxColumn` → `GridViewDataColumn`)·통계 집계 로직 수정
4. 종목록은 `ISpeciesListProvider.GetBirdSpecies()/GetMammalSpecies()/GetAmphibianSpecies()` 사용 (이미 species.json에서 로드)

---

## 7. 완료 기준

- [x] 5개 화면 모두 관리자 모델의 **자동계산 외 전 필드**를 입력 가능
- [x] 5개 화면 모두 공통 조사개황(`SurveyMeta`) 사용 — 개별 중복 속성 없음
- [x] 관찰형 3종에 빠른추가·붙여넣기·빈행정리 동작
- [x] Trace가 정수 입력으로 동작하고 통계(총 개체수/건수)가 맞음
- [x] 빌드 통과 + 7개 탭 모두 실행 확인

---

## 8. 완료 기록 (2026-08-20)

**0-0 빌드 복구**: 착수 시점에 이미 빌드가 통과했다(오류 0). `FishReportExporter.AutoFilter()`가
`ws.Filter.FilterRange = new CellRange(...)` **속성 대입**으로 해결되어 있었다(`SetFilterRange`는 여전히 호출 불가).
→ `90_TECH_NOTES.md` §3에 "해결됨"으로 기록.

**붙여넣기 열 순서** (Ctrl+V, 열 순서 = 그리드 열 순서. 뒤쪽 열은 생략 가능)
| 분류군 | 순서 |
|---|---|
| 조류 | 국명 · 개체수 · 학명 · 목 · 과 · 도래유형 · 대항목 · 세부항목 · 서식유형 · 위도 · 경도 · 특징 · 특이사항 |
| 포유류 | 국명 · 학명 · 목 · 과 · 관찰지유형 · 흔적12(포획~기타) · 위도 · 경도 · 특징 · 특이사항 |
| 양서파충류 | 국명 · 학명 · 목 · 과 · 대분류 · 중분류 · 흔적6(성체~기타) · 위도 · 경도 · 특징 · 특이사항 |
> 목·과 열은 `10_SPECIES_MASTER.md`(공식 종목록 반영) 작업에서 추가되었다.
> 조류는 어류와 같이 **[국명, 개체수] 2열만 붙여넣어도** 되도록 개체수를 국명 바로 옆에 두었다.
> 빠른추가 바의 개체수는 포유류=`관찰`(Trace2), 양서파충류=`성체`(Trace1)로 들어간다.

**✅ 해소됨 — 관찰형 3종의 학명 자동 채움**
작업 당시에는 `species.json`의 `Bird`/`Mammal`/`Amphibian`이 국명 문자열 목록이라 학명을 채울 근거가 없어
직접 입력하도록 열어 두었다. 이후 **`10_SPECIES_MASTER.md`(국가생물종목록 반영)에서 해소**되어
지금은 종 선택·붙여넣기 시 **학명·목·과가 자동으로 채워진다**(수정 가능).

**검증**: 빌드 오류 0(잔여 경고 14건은 모두 이번 작업과 무관한 어류 파일의 기존 경고).
UI Automation으로 7개 탭을 실제로 전환해 렌더링 확인, ViewModel 로직 42건 점검 통과
(흔적 정수 파싱·null과 0 구분·통계 합계·빠른추가·빈행정리·수질 등급·서식수변 조사불가 `-`).
