<#
  SDSM_Surveyor 게시 스크립트
    .\publish.ps1 -Test      테스트 배포본(자료함 예시 자료 포함)
    .\publish.ps1            실제 배포본(예시 자료 없음)
    .\publish.ps1 -Test -CheckOnly   1~3단계 검사만(압축하지 않음)

  ⚠ 압축(4단계)까지 가는 실행은 커밋하지 않은 변경이 있으면 시작하자마자 멈춘다(2026-10-19).
     제품 버전에 박히는 커밋 해시가 압축 내용과 같아야 하기 때문이다. -CheckOnly 는 경고만 하고 계속한다.

  ⚠ 한글이 들어가는 일은 전부 여기(PowerShell)에서 한다.
     .cmd 는 UTF-8 로 저장하면 cmd.exe 가 멀티바이트 글자에서 읽는 위치를 잃고
     명령이 잘려 나간다("SM_Surveyor_App.csproj 은(는) ... 아닙니다").
     그래서 publish_test.cmd / publish_release.cmd 는 ASCII 만 남기고 이 파일을 부른다.
  ⚠ 이 파일은 UTF-8 with BOM 으로 저장한다. BOM 이 없으면 Windows PowerShell 5.1 이
     한글을 CP949 로 읽어 깨진다.
#>
param([switch]$Test, [switch]$CheckOnly)

$ErrorActionPreference = 'Stop'
# 콘솔을 UTF-8 로. 없거나 실패해도 진행한다(출력 글자만 영향).
try { chcp 65001 > $null } catch { }

function Fail($msg, $code) {
    Write-Host ''
    Write-Host "[!] $msg" -ForegroundColor Red
    Read-Host '계속하려면 Enter'
    exit $code
}

trap {
    Write-Host ''
    Write-Host "[!] 예기치 못한 오류 : $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
    Read-Host '계속하려면 Enter'
    exit 99
}

$tools = $PSScriptRoot
$root  = Split-Path -Parent $tools            # ...\SDSM_Surveyor
Set-Location $root

$kind  = if ($Test) { '테스트' } else { '실배포' }
$stage = Join-Path $tools ('_publish_' + $(if ($Test) { 'test' } else { 'release' }))

Write-Host "=== SDSM_Surveyor $kind 배포본 만들기 ===" -ForegroundColor Cyan

# ── 0 커밋 확인 ────────────────────────────────────────────────────────────
# 2026-10-19 : 커밋하기 전 작업 폴더로 압축을 만들어, 제품 버전의 커밋 해시(b942e46)가 압축 내용(8250bc1)과 달랐다.
#   압축까지 가는 실행은 커밋하지 않은 변경이 있으면 멈춘다. -CheckOnly(1~3단계 검사만)는 경고만 하고 계속한다.
#   .gitignore 로 빠지는 파일은 git status 에 나오지 않는다(게시 임시 폴더 · 빌드 로그 등).
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }   # 한글 파일 이름이 깨지지 않게
$eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'   # git 이 stderr 에 쓰는 안내로 멈추지 않게
$dirty = @(& git -C $root -c core.quotepath=off status --porcelain 2>$null)
$gitOk = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $eap
if (-not $gitOk) { Fail 'git 상태를 읽지 못했습니다(git 이 없거나 저장소가 아닙니다).' 5 }
if ($dirty.Count -gt 0) {
    Write-Host ''
    Write-Host '[!] 커밋하지 않은 변경이 있습니다 — 먼저 커밋하세요' -ForegroundColor Yellow
    foreach ($line in $dirty) { Write-Host "      $line" }
    if ($CheckOnly) {
        Write-Host '    -CheckOnly : 1~3단계 검사만 하므로 계속합니다(압축은 만들지 않습니다).' -ForegroundColor Yellow
    } else {
        Fail "커밋하지 않은 변경 $($dirty.Count)건 — 배포본을 만들지 않았습니다. 커밋한 뒤 다시 실행하세요." 5
    }
} else {
    Write-Host '    작업 폴더 깨끗함(커밋하지 않은 변경 없음)'
}

# 앱이 떠 있으면 exe 가 잠겨 게시가 실패한다.
if (Get-Process -Name 'SDSM_Surveyor_App' -ErrorAction SilentlyContinue) {
    Fail 'SDSM_Surveyor_App 이 실행 중입니다. 앱을 닫고 다시 실행하세요.' 9
}

# ── 1/4 정적 검사 ──────────────────────────────────────────────────────────
Write-Host ''
Write-Host '=== 1/4  정적 검사 ==='
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'
foreach ($py in 'xaml_audit.py', 'contrast_audit.py') {
    & python (Join-Path $tools $py)
    if ($LASTEXITCODE -ne 0) { Fail "$py 실패" 1 }
}

# ── 2/4 게시 ───────────────────────────────────────────────────────────────
Write-Host ''
Write-Host "=== 2/4  게시 (단독 실행형 · $kind) ==="
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }

$dotnetArgs = @(
    'publish', 'SDSM_Surveyor_App\SDSM_Surveyor_App.csproj',
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-p:DebugType=none', '-p:DebugSymbols=false',
    '-o', $stage, '-v', 'minimal', '-nologo'
)
if ($Test) { $dotnetArgs += '-p:IncludeTestData=true' }

& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { Fail 'dotnet publish 실패' 2 }

if ($Test) {
    Copy-Item (Join-Path $tools '읽어주세요.txt') (Join-Path $stage '읽어주세요.txt') -Force
}

# ── 3/4 내용 확인 ──────────────────────────────────────────────────────────
Write-Host ''
Write-Host '=== 3/4  내용 확인 ==='
$must = @(
    'SDSM_Surveyor_App.exe',
    'species.json',
    'sites.json',
    'maps\maps.json',
    'Assets\Fonts\OFL.txt',     # Pretendard 재배포 의무(SIL OFL 1.1)
    'guideline\guideline_2026-30.pdf',   # 지침 원본 — 프로그램 정보 창 [지침 원본 보기](v0.5.0)
    # 일괄입력용 양식 내장본 6개 + 어류 라벨 글자(B-7). 없으면 일괄입력용 내보내기가 멈춘다.
    'templates\bulk\benthos.xlsx',
    'templates\bulk\bird.xlsx',
    'templates\bulk\mammal.xlsx',
    'templates\bulk\amphrep.xlsx',
    'templates\bulk\water.xlsx',
    'templates\bulk\habitat.xlsx',
    'templates\bulk\fish_labels.json',
    # 서식수변 평가내용(화면 표시용 · 10-17). 없어도 입력은 되지만 '평가 내용' 열·툴팁이 빈다.
    'templates\bulk\habitat_criteria.json'
)
foreach ($f in $must) {
    if (-not (Test-Path (Join-Path $stage $f))) { Fail "필수 파일 누락 : $f" 3 }
}

$nTpl = (Get-ChildItem (Join-Path $stage 'templates\bulk') -Filter *.xlsx).Count
if ($nTpl -ne 6) { Fail "일괄입력용 내장본이 6개가 아닙니다 — 현재 $nTpl 개" 3 }
Write-Host "    일괄입력용 내장본 $nTpl 개 확인"

$nMap = (Get-ChildItem (Join-Path $stage 'maps') -Filter *.png).Count
if ($nMap -ne 29) { Fail "지도가 29장이 아닙니다 — 현재 $nMap 장" 3 }

$seedDir = Join-Path $stage 'sessions_seed'
if ($Test) {
    if (-not (Test-Path (Join-Path $seedDir 'index.json'))) { Fail '자료함 예시 자료가 없습니다' 3 }
    $nSeed = (Get-ChildItem $seedDir -Filter *.json).Count
    if ($nSeed -ne 4) { Fail "예시 자료가 4개(세션 3 + 목록 1)가 아닙니다 — 현재 $nSeed 개" 3 }
    Write-Host "    지도 $nMap 장 · 자료함 예시 $($nSeed - 1) 건 확인"
} else {
    if (Test-Path $seedDir) { Fail '실배포본에 예시 자료가 섞였습니다' 3 }
    Write-Host "    지도 $nMap 장 확인 · 예시 자료 없음 확인"
}

# 판 — 게시한 exe 의 파일 버전 앞 세 자리(v0.4.1 · G2 2026-10-21 · 종전 두 자리).
#   압축 안 맨 위 폴더 SDSM_Surveyor_v<판>\ (2026-10-19 결정 H2)와 압축 파일 이름에 쓴다. -CheckOnly 에서도 미리 보인다.
$exeVer = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stage 'SDSM_Surveyor_App.exe')).FileVersion
if (-not $exeVer) { Fail 'exe 파일 버전을 읽지 못했습니다' 3 }
$ver  = ([version]$exeVer).ToString(3)
$top  = 'SDSM_Surveyor_v' + $ver
$name = $(if ($Test) { 'SDSM_Surveyor_테스트_' } else { 'SDSM_Surveyor_' }) + 'v' + $ver + '_'
$zipName = $name + (Get-Date -Format 'yyyyMMdd') + '.zip'
Write-Host "    판 v$ver (exe 파일 버전 $exeVer) · 압축 파일 $zipName · 맨 위 폴더 $top\"

if ($CheckOnly) {
    Write-Host ''
    Write-Host '=== 1~3단계 통과 (-CheckOnly : 4단계 압축은 하지 않음) ===' -ForegroundColor Green
    exit 0
}

# ── 4/4 압축 ───────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '=== 4/4  압축 ==='
$outRoot = Join-Path $root '..\..\7_보낸파일'
if (-not (Test-Path $outRoot)) { Fail "보낼 폴더를 찾지 못했습니다 : $outRoot" 4 }
$outRoot = (Resolve-Path $outRoot).Path

$dest = Join-Path $outRoot (Get-Date -Format 'yyyy-MM-dd')
if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest | Out-Null }

$zip  = Join-Path $dest $zipName
if (Test-Path $zip) { Remove-Item $zip -Force }

# 압축 안 맨 위 폴더 $top(위에서 정함) — 압축을 풀면 이 폴더 하나가 생기고 그 안에 실행 파일이 있다
#   (맨 위 폴더가 없으면 '여기에 풀기'로 파일 400여 개가 흩어진다).

# 게시 폴더를 '맨 위 폴더 이름'으로 잠시 옮겨 그 폴더째 압축한다(includeBaseDirectory = $true).
$wrap = Join-Path $tools '_publish_zip'
if (Test-Path $wrap) { Remove-Item $wrap -Recurse -Force }
New-Item -ItemType Directory -Path $wrap | Out-Null
$topDir = Join-Path $wrap $top
Move-Item $stage $topDir

# ⚠ Compress-Archive 는 300MB 급에서 느리고 한글 파일명 플래그를 안 세운다.
#    CreateFromDirectory 는 entryNameEncoding 을 UTF-8 로 못박을 수 있다.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $topDir, $zip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true,
    [System.Text.Encoding]::UTF8)
if (-not (Test-Path $zip)) { Fail '압축 실패' 4 }

Remove-Item $wrap -Recurse -Force
Write-Host "    압축 안 맨 위 폴더 : $top\"

$mb = '{0:N1} MB' -f ((Get-Item $zip).Length / 1MB)
Write-Host ''
Write-Host "완료 : $zip  ($mb)" -ForegroundColor Green
Start-Process explorer.exe -ArgumentList "/select,`"$zip`""
Read-Host '끝났습니다. Enter 를 누르면 창이 닫힙니다'
