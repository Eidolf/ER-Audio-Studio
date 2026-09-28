@echo off
cd /d "%~dp0"
echo ============================================================
echo   ER Audio Studio - Starter
echo ============================================================
echo.

if not exist "bin\ErAudioStudio.exe" (
    echo Erstelle Anwendungs-Binaries...
    powershell -ExecutionPolicy Bypass -File .\build.ps1
)

start "" "bin\ErAudioStudio.exe"
