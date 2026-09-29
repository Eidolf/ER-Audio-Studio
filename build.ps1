# ER Audio Studio - Build Script
param(
    [switch]$RunGui,
    [switch]$RunCli
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  ER Audio Studio - Compilation" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$binDir = Join-Path $PSScriptRoot "bin"
$guiExe = Join-Path $binDir "ErAudioStudio.exe"
$cliExe = Join-Path $binDir "ErAudioCli.exe"

if (-not (Test-Path $csc)) {
    Write-Error "C# Compiler (csc.exe) nicht gefunden unter: $csc"
    exit 1
}

if (-not (Test-Path $binDir)) {
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
}

$recDir = Join-Path $PSScriptRoot "recordings"
if (-not (Test-Path $recDir)) {
    New-Item -ItemType Directory -Path $recDir -Force | Out-Null
}

$srcFiles = @(
    (Join-Path $PSScriptRoot "src\AssemblyInfo.cs"),
    (Join-Path $PSScriptRoot "src\App.cs"),
    (Join-Path $PSScriptRoot "src\Audio\WasapiInterop.cs"),
    (Join-Path $PSScriptRoot "src\Audio\AudioDevice.cs"),
    (Join-Path $PSScriptRoot "src\Audio\WavWriter.cs"),
    (Join-Path $PSScriptRoot "src\Audio\WasapiLoopbackEngine.cs"),
    (Join-Path $PSScriptRoot "src\Audio\SimpleAudioPlayer.cs"),
    (Join-Path $PSScriptRoot "src\Audio\AudioAnalyzer.cs"),
    (Join-Path $PSScriptRoot "src\Audio\AudioConverterService.cs"),
    (Join-Path $PSScriptRoot "src\Audio\AudioArrangeProcessor.cs"),
    (Join-Path $PSScriptRoot "src\Audio\CodecManager.cs"),
    (Join-Path $PSScriptRoot "src\Audio\MidiWriter.cs"),
    (Join-Path $PSScriptRoot "src\Audio\MidiSynthesizer.cs"),
    (Join-Path $PSScriptRoot "src\Audio\AudioToMidiConverter.cs"),
    (Join-Path $PSScriptRoot "src\UI\Localization.cs"),
    (Join-Path $PSScriptRoot "src\UI\VuMeterControl.cs"),
    (Join-Path $PSScriptRoot "src\UI\DarkThemeStyles.cs"),
    (Join-Path $PSScriptRoot "src\UI\AudioConverterTab.cs"),
    (Join-Path $PSScriptRoot "src\UI\MainWindow.cs"),
    (Join-Path $PSScriptRoot "src\CLI\CommandLineRunner.cs")
)

$references = @(
    "/r:`"$wpf\WindowsBase.dll`"",
    "/r:`"$wpf\PresentationCore.dll`"",
    "/r:`"$wpf\PresentationFramework.dll`"",
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Xaml.dll"
)

$iconArg = ""
$iconPath = Join-Path $PSScriptRoot "assets\app_icon.ico"
if (Test-Path $iconPath) {
    $iconArg = "/win32icon:`"$iconPath`""
}

Write-Host "1. Kompiliere GUI-Anwendung: ErAudioStudio.exe..." -ForegroundColor Yellow
if ($iconArg -ne "") {
    & $csc /target:winexe /optimize+ /nologo /out:$guiExe $iconArg $references $srcFiles
} else {
    & $csc /target:winexe /optimize+ /nologo /out:$guiExe $references $srcFiles
}
if ($LASTEXITCODE -ne 0) {
    Write-Error "GUI-Kompilierung fehlgeschlagen!"
    exit 1
}
Write-Host "   -> GUI erfolgreich erstellt: $guiExe" -ForegroundColor Green

Write-Host "2. Kompiliere CLI-Anwendung: ErAudioCli.exe..." -ForegroundColor Yellow
& $csc /target:exe /optimize+ /nologo /out:$cliExe $references $srcFiles
if ($LASTEXITCODE -ne 0) {
    Write-Error "CLI-Kompilierung fehlgeschlagen!"
    exit 1
}
Write-Host "   -> CLI erfolgreich erstellt: $cliExe" -ForegroundColor Green

Write-Host "`nBuild abgeschlossen! Alle Dateien bereit." -ForegroundColor Cyan

if ($RunGui) {
    Start-Process $guiExe
} elseif ($RunCli) {
    & $cliExe --list-devices
}
