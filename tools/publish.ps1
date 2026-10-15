<#
  SDSM_Surveyor 게시 스크립트
    .\publish.ps1 -Test      테스트 배포본(자료함 예시 자료 포함)
    .\publish.ps1            실제 배포본(예시 자료 없음)

  ⚠ 한글이 들어가는 일은 전부 여기(PowerShell)에서 한다.
     .cmd 는 UTF-8 로 저장하면 cmd.exe 가 멀티바이트 글자에서 읽는 위치를 잃고
     명령이 잘려 나간다("SM_Surveyor_App.csproj 은(는) ... 아닙니다").
     그래서 publish_test.cmd / publish_release.cmd 는 ASCII 만 남기고 이 파일을 부른다.
  ⚠ 이 파일은 UTF-8 with BOM 으로 저장한다. BOM 이 없으면 Windows PowerShell 5.1 이
     한글을 CP949 로 읽어 깨진다.
#>
param([switch]$Test)

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
    'Assets\Fonts\OFL.txt'      # Pretendard 재배포 의무(SIL OFL 1.1)
)
foreach ($f in $must) {
    if (-not (Test-Path (Join-Path $stage $f))) { Fail "필수 파일 누락 : $f" 3 }
}

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

# ── 4/4 압축 ───────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '=== 4/4  압축 ==='
$outRoot = Join-Path $root '..\..\7_보낸파일'
if (-not (Test-Path $outRoot)) { Fail "보낼 폴더를 찾지 못했습니다 : $outRoot" 4 }
$outRoot = (Resolve-Path $outRoot).Path

$dest = Join-Path $outRoot (Get-Date -Format 'yyyy-MM-dd')
if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest | Out-Null }

$name = if ($Test) { 'SDSM_Surveyor_테스트_' } else { 'SDSM_Surveyor_' }
$zip  = Join-Path $dest ($name + (Get-Date -Format 'yyyyMMdd') + '.zip')
if (Test-Path $zip) { Remove-Item $zip -Force }

# ⚠ Compress-Archive 는 300MB 급에서 느리고 한글 파일명 플래그를 안 세운다.
#    CreateFromDirectory 는 entryNameEncoding 을 UTF-8 로 못박을 수 있다.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $stage, $zip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false,
    [System.Text.Encoding]::UTF8)
if (-not (Test-Path $zip)) { Fail '압축 실패' 4 }

Remove-Item $stage -Recurse -Force

$mb = '{0:N1} MB' -f ((Get-Item $zip).Length / 1MB)
Write-Host ''
Write-Host "완료 : $zip  ($mb)" -ForegroundColor Green
Start-Process explorer.exe -ArgumentList "/select,`"$zip`""
Read-Host '끝났습니다. Enter 를 누르면 창이 닫힙니다'
