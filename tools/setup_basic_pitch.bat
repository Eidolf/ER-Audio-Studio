@echo off
setlocal enabledelayedexpansion
title ER Audio Studio - Basic-Pitch Setup

echo ==========================================================
echo   ER Audio Studio - Basic-Pitch KI Setup
echo ==========================================================
echo.

set "SCRIPT_DIR=%~dp0"
if exist "%SCRIPT_DIR%..\bin\codecs" (
    set "CODEC_DIR=%SCRIPT_DIR%..\bin\codecs"
) else if exist "%SCRIPT_DIR%codecs" (
    set "CODEC_DIR=%SCRIPT_DIR%codecs"
) else (
    set "CODEC_DIR=%SCRIPT_DIR%"
)

for %%i in ("%CODEC_DIR%") do set "CODEC_DIR=%%~fi"
if not exist "%CODEC_DIR%" mkdir "%CODEC_DIR%"

set "PY_DIR=%CODEC_DIR%\python_env"
set "BASIC_PITCH_CMD=%CODEC_DIR%\basic-pitch.bat"

echo 1. Pruefe Python Installation...
where pip >nul 2>nul
if %ERRORLEVEL% equ 0 (
    echo    -^> Systemweites pip gefunden!
    echo.
    echo 2. Installiere / Aktualisiere basic-pitch via pip...
    pip install basic-pitch
    if %ERRORLEVEL% equ 0 (
        echo.
        echo ==========================================================
        echo [ERFOLG] Spotifys Basic-Pitch KI wurde erfolgreich installiert!
        echo Das Studio erkennt das KI-Modul jetzt automatisch.
        echo ==========================================================
        pause
        exit /b 0
    )
)

echo    -^> Kein systemweites pip verfuegbar.
echo.
echo 2. Richte eigenstaendige portable Python-Umgebung ein...
echo    Zielordner: %CODEC_DIR%
echo    Lade Python 3.11 Embedded herunter (~10 MB)...

set "PY_ZIP=%CODEC_DIR%\python_embed.zip"
powershell -Command "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12; (New-Object System.Net.WebClient).DownloadFile('https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip', '%PY_ZIP%')"
if not exist "%PY_ZIP%" (
    echo [FEHLER] Download von Python fehlgeschlagen.
    pause
    exit /b 1
)

echo    Entpacke Python in: %PY_DIR% ...
if not exist "%PY_DIR%" mkdir "%PY_DIR%"
powershell -Command "Expand-Archive -Path '%PY_ZIP%' -DestinationPath '%PY_DIR%' -Force"
del "%PY_ZIP%"

echo    Aktiviere pip und Modul-Unterstuetzung in Python...
powershell -Command "$pth = Get-ChildItem '%PY_DIR%' -Filter '*._pth' | Select-Object -First 1; if ($pth) { (Get-Content $pth.FullName) -replace '#import site', 'import site' | Set-Content $pth.FullName }"

echo    Lade pip-Installer...
set "GET_PIP=%PY_DIR%\get-pip.py"
powershell -Command "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12; (New-Object System.Net.WebClient).DownloadFile('https://bootstrap.pypa.io/get-pip.py', '%GET_PIP%')"

"%PY_DIR%\python.exe" "%GET_PIP%" --no-warn-script-location
if exist "%GET_PIP%" del "%GET_PIP%"

echo.
echo 3. Installiere Spotifys Basic-Pitch KI-Engine...
"%PY_DIR%\Scripts\pip.exe" install basic-pitch --no-warn-script-location

echo @echo off > "%BASIC_PITCH_CMD%"
echo "%PY_DIR%\Scripts\basic-pitch.exe" %%%%* >> "%BASIC_PITCH_CMD%"

echo.
echo ==========================================================
echo [ERFOLG] Spotifys Basic-Pitch KI ist nun vollstaendig eingerichtet!
echo Pfad: %BASIC_PITCH_CMD%
echo Das Studio erkennt das KI-Modul jetzt automatisch.
echo ==========================================================
echo.
pause
