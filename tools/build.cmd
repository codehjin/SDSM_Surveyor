@echo off
rem ---------------------------------------------------------------------------
rem  SDSM_Surveyor 빌드 게이트
rem    1) xaml_audit.py     : 전방참조 / 미정의 키 / hex 리터럴
rem    2) contrast_audit.py : 명도대비 (design.md §3.7)
rem    3) dotnet build
rem  결과는 tools\_build.log 에 모두 남는다.
rem  ⚠ echo 문에서 변수 뒤에 바로 >> 를 붙이면 안 된다 —
rem     값이 숫자면 cmd 가 "1>>" 를 스트림 리다이렉트로 읽는다. (echo ...) >> 로 감싼다.
rem ---------------------------------------------------------------------------
chcp 65001 > nul
setlocal
set PYTHONUTF8=1
set PYTHONIOENCODING=utf-8
set LOG=%~dp0_build.log
cd /d "%~dp0.."

rem -- 앱이 떠 있으면 exe 가 잠겨 빌드가 실패한다. 먼저 알려 준다. --
tasklist /FI "IMAGENAME eq SDSM_Surveyor_App.exe" 2>nul | find /I "SDSM_Surveyor_App.exe" > nul
if not errorlevel 1 (
    echo.
    echo [!] SDSM_Surveyor_App 이 실행 중입니다.
    echo     실행 파일이 잠겨 있어 빌드가 실패합니다. 앱을 닫고 다시 실행하세요.
    echo.
    (echo [!] 앱 실행 중 - 빌드하지 않았습니다.) > "%LOG%"
    pause
    exit /b 9
)

(echo === xaml_audit.py ===) > "%LOG%"
python "%~dp0xaml_audit.py" >> "%LOG%" 2>&1
set AUDIT=%ERRORLEVEL%

(echo.) >> "%LOG%"
(echo === contrast_audit.py ===) >> "%LOG%"
python "%~dp0contrast_audit.py" >> "%LOG%" 2>&1
set CONTRAST=%ERRORLEVEL%

(echo.) >> "%LOG%"
(echo === dotnet build ===) >> "%LOG%"
echo === dotnet build SDSM_Surveyor.sln -c Debug ===
dotnet build SDSM_Surveyor.sln -c Debug -v minimal -nologo >> "%LOG%" 2>&1
set RC=%ERRORLEVEL%

(echo.) >> "%LOG%"
(echo === 요약 ===) >> "%LOG%"
(echo XAML_AUDIT=%AUDIT%) >> "%LOG%"
(echo CONTRAST_AUDIT=%CONTRAST%) >> "%LOG%"
(echo BUILD=%RC%) >> "%LOG%"

type "%LOG%"
echo.
echo -- 로그: %LOG%   xaml=%AUDIT% contrast=%CONTRAST% build=%RC%
pause
