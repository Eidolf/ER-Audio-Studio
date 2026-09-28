@echo off
cd /d "%~dp0"
echo ============================================================
echo   ER Audio Studio - Starter
echo ============================================================
echo.

call .\build.bat
if %ERRORLEVEL% equ 0 (
    start "" "bin\ErAudioStudio.exe"
)

