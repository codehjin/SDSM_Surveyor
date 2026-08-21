# SDSM_Surveyor — 조사자 입력 앱

현장 조사자가 **오프라인으로** 생태 조사 자료를 입력·검증하고, 관리자 표준 엑셀로 내보내 제출하는
.NET 8 WPF 데스크톱 애플리케이션. 7개 분류군(어류·저서동물·조류·포유류·양서파충류·서식수변·수질)을 다룹니다.

## ⚠ 관리자 저장소를 **같은 상위 폴더에 나란히** clone 해야 합니다

이 앱은 관리자 저장소(`SDSM`)의 공용 프로젝트를 **상대 경로로 참조**합니다.
폴더 배치가 아래와 다르면 **빌드되지 않습니다.**

```
<상위폴더>\
  SDSM\            ← 관리자 (공용 모델 SDSM_Models · 공용 계산 SDSM_Core)
  SDSM_Surveyor\   ← 이 저장소
```

```bash
cd <상위폴더>
git clone https://github.com/codehjin/SDSM.git
git clone https://github.com/codehjin/SDSM_Surveyor.git
```

배치가 틀리면 빌드가 이유를 알려 줍니다:

```
관리자 저장소(SDSM)를 찾지 못했습니다. SDSM 과 SDSM_Surveyor 를
같은 상위 폴더에 나란히 clone하세요.
```

> NuGet 패키지·git submodule 은 쓰지 않습니다(`docs\05_REFACTORING.md` §2-0의 결정).

## 빌드·실행

- Visual Studio 2026 / .NET 8 / Telerik UI for WPF 2026.2.520
- 시작 프로젝트: `SDSM_Surveyor_App`

## 저장 위치

| 대상 | 위치 |
|---|---|
| 조사 세션(작업본) | `%AppData%\SDSM_Surveyor\sessions\` |
| 종목록·기준자료·지점 마스터 | 실행 폴더 번들, `%AppData%\SDSM_Surveyor\` 로 교체 가능 |

> ⚠ `%AppData%\SDSM_Surveyor\` 는 **사용자의 실제 조사 자료**입니다.
> 스크립트로 이 폴더를 옮기거나 지우지 마세요(`docs\90_TECH_NOTES.md` §8).

## 회귀 기준

리팩토링·수정 후 결과가 달라지지 않았는지 확인하는 기준 엑셀이 `docs\_baseline\` 에 있습니다.

```bash
dotnet run --project tools\BaselineGen\BaselineGen.csproj -- --out C:\Temp\after
python tools\baseline_diff.py docs\_baseline C:\Temp\after
```

자세한 내용은 `docs\_baseline\README.md`.

## 문서

| 문서 | 내용 |
|---|---|
| `CLAUDE.md` | 개발·아키텍처 표준 (필독) |
| `design.md` | UI·화면 규칙 |
| `todo.md` | 진행 현황 |
| `docs\00_MASTER_PLAN.md` | 단계별 로드맵 |
| `docs\90_TECH_NOTES.md` | 검증된 기술 함정 모음 (필독) |
