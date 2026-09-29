@echo off
REM ER Audio Studio - Build Script
REM Kompiliert die Anwendung (GUI und CLI)

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

REM Run build script without automatically opening the GUI
echo Starte Kompilierung...
echo.
powershell.exe -ExecutionPolicy Bypass -File "%~dp0build.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo FEHLER: Build fehlgeschlagen!
    pause
    exit /b 1
)

echo.
echo Build erfolgreich abgeschlossen!
echo.
