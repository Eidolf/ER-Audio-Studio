@echo off
REM ER Audio Studio - Build & Start Script
REM Kompiliert die Anwendung und startet die GUI

echo ========================================
echo ER Audio Studio - Build System
echo ========================================
echo.

REM Check for PowerShell
where powershell >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo FEHLER: PowerShell wurde nicht gefunden!
    echo Bitte installieren Sie PowerShell oder fuehren Sie build.ps1 manuell aus.
    pause
    exit /b 1
)

REM Run build script
echo Starte Kompilierung...
echo.
powershell.exe -ExecutionPolicy Bypass -File "%~dp0build.ps1" -RunGui

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo FEHLER: Build fehlgeschlagen!
    pause
    exit /b 1
)

echo.
echo Build erfolgreich!
echo.
