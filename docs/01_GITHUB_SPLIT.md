# 1단계 — GitHub 저장소 분리 + 문서 업로드

> 선행: 없음. **가장 먼저 수행한다.**
> 참고: `90_TECH_NOTES.md` §7 (F: 드라이브에서 bash로 git 상태를 판단하지 말 것)

---

## 1. 현재 상태 (확인된 사실)

| 폴더 | git | 원격 |
|---|---|---|
| `F:\desktopSW\samsung\1_program\SDSM\` (관리자) | 저장소 **있음** | `https://github.com/codehjin/SDSM.git` (branch `master`) |
| `F:\desktopSW\samsung\1_program\SDSM_Surveyor\` (조사자) | **없음** | — |

→ 조사자 앱이 아직 버전관리 밖에 있다. **별도 저장소로 독립시킨다.**

---

## 2. 사용자가 먼저 할 일 (수동)

Claude Code는 GitHub에 새 저장소를 만들 권한이 없다. 사용자가 직접 생성한다.

1. GitHub에서 **빈 저장소** 생성: `codehjin/SDSM_Surveyor`
   - README·.gitignore·라이선스 **모두 체크 해제**(빈 저장소여야 충돌이 없다)
2. 생성된 주소를 확인: `https://github.com/codehjin/SDSM_Surveyor.git`

---

## 3. 작업 내용

### 3-1. 조사자 앱 저장소 생성

작업 폴더: `F:\desktopSW\samsung\1_program\SDSM_Surveyor`

1. `.gitignore` 생성 (아래 내용)
2. `git init` → `git add .` → 최초 커밋 (`chore: 조사자 앱 최초 커밋`)
3. 원격 추가: `https://github.com/codehjin/SDSM_Surveyor.git`
4. 브랜치명을 관리자 저장소와 맞춰 `master`로 통일한 뒤 push

**`.gitignore` 내용**
```gitignore
# 빌드 산출물
[Bb]in/
[Oo]bj/
.vs/
*.user
*.suo

# 로컬 설정·데이터
*.db-shm
*.db-wal

# 대용량 참고자료
*.xlsx
!docs/**/*.xlsx
```

> ⚠ **주의**: `bin/`을 무시해도 **기존 파일은 지워지지 않는다.** 절대 `git clean -fdx` 를 실행하지 말 것.
> 관리자 실데이터 `SDSM\SDSM_App\bin\Debug\net8.0-windows\SDSMDB.sqlite` 가 bin 안에 있다.
>
> ⚠ `species.json`(400KB)은 **번들 필수 파일이므로 커밋한다.** 위 `*.xlsx` 무시 규칙과 무관.
> `reference.json`도 커밋한다.

### 3-2. 관리자 앱 저장소 정리

작업 폴더: `F:\desktopSW\samsung\1_program\SDSM`

1. 원격이 `https://github.com/codehjin/SDSM.git` 인지 확인 (변경 불필요)
2. `.gitignore`가 없거나 부실하면 위와 동일한 내용으로 생성
3. 미커밋 변경사항 커밋 후 push
   - 이번 작업으로 추가된 파일: `SDSM_Models\SpeciesCatalog.cs`, `SDSM_Models\ReferenceData.cs`, `SDSM_App\Commons\ReferenceDataExporter.cs`, 설정 화면의 기준자료 내보내기 관련 수정

### 3-3. 문서 업로드

- 조사자 저장소: `docs/*.md` 전체 + `CLAUDE.md` + `design.md` + `todo.md`
- 관리자 저장소: `CLAUDE.md` + `design.md` + `docs/ADMIN_TASKS.md`

---

## 4. 완료 기준

- [ ] `SDSM_Surveyor`가 독립 저장소로 GitHub에 올라감
- [ ] `SDSM`은 기존 주소 유지, 두 저장소의 원격 주소가 **서로 다름**
- [ ] 두 저장소 모두 `bin/`·`obj/`가 추적되지 않음
- [ ] 모든 md 문서가 각 저장소에 올라감
- [ ] **작업 폴더의 파일이 하나도 삭제되지 않음** (특히 SQLite DB)

---

## 5. 이후 규칙

- 각 단계 문서를 끝낼 때마다 커밋한다. 커밋 메시지는 한글로 명확하게.
  예: `feat(조사자): 조류·포유류·양서파충류 전체 필드 확장`
- 기능 단위로 커밋하고, 빌드가 깨진 상태로 push 하지 않는다.
