# Changelog - ER Audio Studio

## [Unreleased] - Audio Converter Feature

### ✨ Neue Features

#### 🔄 Universal Audio-Konverter
- **Any Audio zu Any Audio Konvertierung** zwischen 10 Formaten:
  - MP3, WAV, FLAC, OGG, AAC, M4A, Opus, WMA, AIFF, ALAC
- **Integriertes Codec-Management**:
  - Ein-Klick FFmpeg-Download & Installation (~50 MB)
  - Status-Anzeige: Installiert/Nicht installiert
  - Codec-Löschfunktion für Speicherplatz-Verwaltung
  - Automatische Extraktion und Einrichtung
- **Einzeldatei-Konvertierung**:
  - Datei-Browser für Ein- und Ausgabe
  - Format-Auswahl mit Beschreibungen
  - Bitrate-Einstellung (128-320 kbps)
  - Abtastrate-Konfiguration (Original, 44.1k, 48k, 96k)
  - Echtzeit-Fortschrittsbalken
  - Detailliertes Konvertierungs-Protokoll
- **Batch-Konvertierung**:
  - Mehrere Dateien gleichzeitig hinzufügen
  - Alle Dateien mit einem Klick konvertieren
  - Fortschrittsanzeige pro Datei
  - Erfolgs-/Fehler-Statistik
- **Benutzerfreundliche UI**:
  - Neuer Tab "🔄 Konverter" in der Hauptanwendung
  - Card-basiertes Dark-Mode-Design
  - Codec-Management-Dialog
  - Explorer-Integration (Ausgabedatei öffnen)

### 🏗️ Architektur

#### Neue Dateien
- `src/Audio/CodecManager.cs` - FFmpeg Download & Verwaltung
- `src/UI/AudioConverterTab.cs` - Vollständige Converter-UI

#### Erweiterte Dateien
- `src/Audio/AudioConverterService.cs`:
  - `AudioFormat` Enum für 10 Formate
  - `ConvertAudioAdvanced()` mit Fortschritts-Callbacks
  - Format-spezifische FFmpeg-Parameter
  - Fortschritts-Parsing aus FFmpeg-Ausgabe
- `src/UI/MainWindow.cs`:
  - Integration des Converter-Tabs
  - 3-Tab-Wechsel-Logik (Aufnahme, Konverter, MIDI)
- `src/UI/Localization.cs`:
  - Deutsch/Englisch-Texte für Converter-Features
  - Codec-Management-Strings
  - Batch-Konvertierungs-Labels
- `build.ps1`:
  - Neue Quelldateien im Build-Prozess

### 🔧 Technische Details

#### FFmpeg-Integration
- **Download-Quelle**: gyan.dev/ffmpeg (offizieller Windows-Build)
- **Installations-Pfad**: `<AppDir>/codecs/ffmpeg.exe`
- **Fallback-Logik**: Codecs-Ordner → Basis-Ordner → Assets → System-PATH
- **Extraktion**: System.IO.Compression mit PowerShell-Fallback

#### .NET 4.0 Kompatibilität
- ✅ Keine async/await (ThreadPool-basiert)
- ✅ WebClient statt HttpClient
- ✅ Kompatible Regex-Muster
- ✅ Kompatible ZIP-Extraktion

#### Format-Codec-Mapping
| Format | FFmpeg Codec | Qualität | Kompression |
|--------|-------------|----------|-------------|
| MP3 | libmp3lame | Sehr gut | Verlustbehaftet |
| WAV | pcm_s16le | Perfekt | Keine |
| FLAC | flac | Perfekt | Verlustfrei |
| OGG | libvorbis | Sehr gut | Verlustbehaftet |
| AAC | aac | Exzellent | Verlustbehaftet |
| M4A | aac | Exzellent | Verlustbehaftet |
| Opus | libopus | Beste Effizienz | Verlustbehaftet |
| WMA | wmav2 | Gut | Verlustbehaftet |
| AIFF | pcm_s16be | Perfekt | Keine |
| ALAC | alac | Perfekt | Verlustfrei |

### 📚 Dokumentation
- **AUDIO_CONVERTER.md**: Vollständige Feature-Dokumentation
- **README.md**: Aktualisiert mit Converter-Beschreibung
- **CHANGELOG.md**: Diese Datei

### ✅ Qualitätssicherung
- [x] .NET 4.0 / C# 5.0 Kompatibilität
- [x] Dark Mode Design konsistent mit bestehendem UI
- [x] Fehlerbehandlung und Validierung
- [x] Benutzerfreundliche Fehlermeldungen
- [x] Lokalisierung (Deutsch/Englisch)
- [x] Memory-Management (Dispose-Pattern)
- [x] Thread-sichere UI-Updates

### 🎯 Design-Prinzipien erfüllt
1. ✅ **Selbständig**: FFmpeg wird in der App verwaltet
2. ✅ **Optional**: Codecs können gelöscht werden
3. ✅ **Download-Button**: Ein-Klick-Installation
4. ✅ **Projektkonform**: Folgt bestehendem Code-Stil
5. ✅ **Modern**: Card-basiertes Dark-Mode-UI

---

## [1.0.0] - Vorherige Features

### Features
- 🎙 WASAPI Loopback & Mikrofon Recording
- 🎹 Audio zu MIDI mit Multi-Instrument Arrangement
- 📊 Technische Audio-Analyse
- 🌐 Zweisprachige Oberfläche (DE/EN)
- 🎨 Modern Dark Mode UI
