@echo off
setlocal
cd /d "%~dp0"

echo ==========================================================
echo   ER Audio Loopback Tool - Batch Compilation
echo ==========================================================

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "WPF=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

if not exist "%CSC%" (
    echo [FEHLER] C#-Compiler nicht gefunden unter: %CSC%
    pause
    exit /b 1
)

if not exist "bin" mkdir "bin"
if not exist "recordings" mkdir "recordings"

set "REFS=/r:"%WPF%\WindowsBase.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xaml.dll"
set "SOURCES=src\AssemblyInfo.cs src\App.cs src\Audio\WasapiInterop.cs src\Audio\AudioDevice.cs src\Audio\WavWriter.cs src\Audio\WasapiLoopbackEngine.cs src\Audio\SimpleAudioPlayer.cs src\Audio\AudioAnalyzer.cs src\Audio\AudioConverterService.cs src\Audio\MidiWriter.cs src\Audio\AudioToMidiConverter.cs src\UI\Localization.cs src\UI\VuMeterControl.cs src\UI\MainWindow.cs src\CLI\CommandLineRunner.cs"

echo 1. Kompiliere GUI-Anwendung (ErAudioTool.exe)...
"%CSC%" /target:winexe /optimize+ /nologo /out:"bin\ErAudioTool.exe" %REFS% %SOURCES%
if %ERRORLEVEL% neq 0 (
    echo [FEHLER] Kompilierung von ErAudioTool.exe fehlgeschlagen!
    pause
    exit /b 1
)

echo 2. Kompiliere CLI-Anwendung (ErAudioCli.exe)...
"%CSC%" /target:exe /optimize+ /nologo /out:"bin\ErAudioCli.exe" %REFS% %SOURCES%
if %ERRORLEVEL% neq 0 (
    echo [FEHLER] Kompilierung von ErAudioCli.exe fehlgeschlagen!
    pause
    exit /b 1
)

echo.
echo ==========================================================
echo   Erfolgreich kompiliert!
echo   GUI-App: bin\ErAudioTool.exe
echo   CLI-App: bin\ErAudioCli.exe
echo ==========================================================
echo.
pause
