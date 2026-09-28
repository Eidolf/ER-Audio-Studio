# Audio Converter Feature - Dokumentation

## 🎯 Übersicht

Der **Any Audio zu Any Audio Converter** ist eine neue Hauptfunktion von ER Audio Studio, die umfassende Audio-Konvertierung zwischen allen gängigen Formaten ermöglicht.

## ✨ Hauptfunktionen

### 1. **Universelle Format-Unterstützung**
Unterstützte Formate:
- **MP3** - MPEG-1 Audio Layer 3 (universell kompatibel)
- **WAV** - Waveform Audio (unkomprimiert, verlustfrei)
- **FLAC** - Free Lossless Audio Codec (verlustfreie Kompression)
- **OGG Vorbis** - Open-Source, gute Qualität
- **AAC** - Advanced Audio Coding (modern, effizient)
- **M4A** - Apple/iTunes Format
- **Opus** - Modernster Codec, beste Effizienz
- **WMA** - Windows Media Audio
- **AIFF** - Apple unkomprimiert
- **ALAC** - Apple Lossless (verlustfrei für Apple)

### 2. **Integriertes Codec-Management**
- ✅ **Automatischer FFmpeg-Download**: Ein-Klick-Installation (~50 MB)
- 🔄 **Status-Anzeige**: Zeigt, ob FFmpeg installiert ist
- 🗑️ **Codec-Verwaltung**: Einfaches Löschen nicht benötigter Codecs
- 📊 **Größenanzeige**: Transparente Anzeige des Speicherbedarfs

### 3. **Einzeldatei-Konvertierung**
- 📁 Einfache Dateiauswahl mit Browser-Dialog
- 🎚️ Konfigurierbare **Bitrate** (128, 192, 256, 320 kbps)
- 🎵 Einstellbare **Abtastrate** (Original, 44.1, 48, 96 kHz)
- 📊 **Echtzeit-Fortschrittsanzeige** während der Konvertierung
- 📝 **Detailliertes Protokoll** mit Zeitstempeln

### 4. **Batch-Konvertierung**
- ➕ **Mehrere Dateien gleichzeitig** hinzufügen
- 🔄 **Stapelverarbeitung** aller Dateien mit einem Klick
- 📋 **Übersichtliche Liste** mit Eingabe- und Ausgabedateien
- ✅ **Erfolgs-/Fehler-Statistik** nach Abschluss

### 5. **Benutzerfreundliche UI**
- 🌑 **Dark Mode Design** passend zum Rest der Anwendung
- 🎨 **Moderne Card-basierte Oberfläche**
- 📱 **Responsive Layout** mit Scroll-Unterstützung
- ⚡ **Schneller Tab-Wechsel** zwischen Aufnahme, Konverter und MIDI

## 🏗️ Architektur

### Neue Dateien

#### 1. `src/Audio/CodecManager.cs`
Verwaltet FFmpeg-Installation und -Verwaltung:
- `GetFfmpegPath()` - Findet FFmpeg auf dem System
- `IsFfmpegInstalled()` - Prüft FFmpeg-Status
- `DownloadFfmpeg()` - Lädt FFmpeg automatisch herunter
- `DeleteCodecs()` - Entfernt installierte Codecs
- `GetCodecSize()` - Ermittelt Speicherbedarf

**Features**:
- ✅ .NET 4.0 kompatibel
- ✅ Verwendet System.IO.Compression oder PowerShell-Fallback
- ✅ Fortschritts-Callbacks während Download
- ✅ Automatische Extraktion aus ZIP-Archiv

#### 2. `src/Audio/AudioConverterService.cs` (erweitert)
Neue Features:
- `AudioFormat` Enum für alle unterstützten Formate
- `ConvertAudioAdvanced()` - Erweiterte Konvertierung mit Fortschritt
- `BuildConversionArgs()` - Format-spezifische FFmpeg-Parameter
- `GetFormatExtension()` - Dateiendungen pro Format
- `GetFormatDescription()` - Benutzerfreundliche Beschreibungen

**Technische Details**:
- ✅ Regex-basiertes Fortschritts-Parsing aus FFmpeg-Ausgabe
- ✅ Unterstützung für Sample-Rate-Konvertierung
- ✅ Format-optimierte Codec-Einstellungen
- ✅ Fehlerbehandlung und Logging

#### 3. `src/UI/AudioConverterTab.cs`
Vollständige UI-Implementierung:
- Tab-Container mit 4 Haupt-Cards:
  1. **Codec-Status & Verwaltung**
  2. **Einzel-Konvertierung**
  3. **Batch-Konvertierung**
  4. **Konvertierungs-Protokoll**

- Codec-Management-Dialog mit:
  - Download-Fortschrittsanzeige
  - Installationsgröße
  - Installations-Pfad
  - Lösch-Funktion

**UI-Komponenten**:
- ✅ Format-ComboBox mit allen 10 Formaten
- ✅ Bitrate-Auswahl (128-320 kbps)
- ✅ Sample-Rate-Auswahl (Original, 44.1k, 48k, 96k)
- ✅ ProgressBar mit Prozent-Anzeige
- ✅ Multi-File-Browser für Batch
- ✅ Echtzeit-Log mit Scroll

#### 4. `src/UI/MainWindow.cs` (erweitert)
Integration des Converter-Tabs:
- ➕ Neuer Tab-Button "🔄 Konverter"
- 🔀 Tab-Wechsel-Logik für 3 Tabs (Record, Convert, MIDI)
- 📦 Instanziierung von `AudioConverterTab`

#### 5. `src/UI/Localization.cs` (erweitert)
Neue Lokalisierungs-Einträge:
- Deutsch und Englisch für alle Converter-UI-Elemente
- Codec-Management-Texte
- Batch-Konvertierung-Labels

## 🚀 Nutzung

### Erste Schritte

1. **FFmpeg installieren** (nur beim ersten Mal):
   - Klicke auf den Tab "🔄 Konverter"
   - Klicke auf "⚙️ Codecs verwalten"
   - Klicke auf "📥 FFmpeg herunterladen (~50 MB)"
   - Warte auf die Installation (Fortschrittsanzeige)

2. **Einzelne Datei konvertieren**:
   - Wähle eine Eingangsdatei über "📁 Durchsuchen..."
   - Wähle das Zielformat (z.B. MP3)
   - Wähle Bitrate und Abtastrate
   - Klicke auf "🔄 Jetzt konvertieren"
   - Verfolge den Fortschritt in Echtzeit

3. **Mehrere Dateien konvertieren** (Batch):
   - Klicke auf "➕ Dateien hinzufügen"
   - Wähle mehrere Dateien aus (Mehrfachauswahl)
   - Stelle Format und Qualität ein
   - Klicke auf "▶️ Alle konvertieren"
   - Warte auf Abschluss aller Konvertierungen

### Codec-Verwaltung

- **Status prüfen**: Die Status-Card zeigt ob FFmpeg installiert ist
- **Codecs löschen**: Über "⚙️ Codecs verwalten" → "🗑️ Codecs löschen"
- **Speicherplatz**: Anzeige der aktuellen Installationsgröße
- **Pfad**: Codecs werden im Unterordner `codecs/` installiert

## 🔧 Technische Details

### FFmpeg-Integration

**Download-Quelle**: 
- gyan.dev/ffmpeg (offizieller Windows-Build)
- FFmpeg Essentials (~50 MB statt ~120 MB Full Build)

**Installations-Pfad**:
```
<Anwendungsverzeichnis>/codecs/ffmpeg.exe
<Anwendungsverzeichnis>/codecs/ffprobe.exe
```

**Fallback-Logik**:
1. Prüfe `codecs/ffmpeg.exe` (Codec-Manager)
2. Prüfe `<Basis>/ffmpeg.exe` (Legacy)
3. Prüfe `assets/ffmpeg.exe` (Assets-Ordner)
4. Prüfe System-PATH (wo.exe/where.exe)

### Format-Spezifikationen

| Format | Codec | Extension | Kompression | Qualität |
|--------|-------|-----------|-------------|----------|
| MP3 | libmp3lame | .mp3 | Verlustbehaftet | Sehr gut |
| WAV | pcm_s16le | .wav | Unkomprimiert | Perfekt |
| FLAC | flac | .flac | Verlustfrei | Perfekt |
| OGG | libvorbis | .ogg | Verlustbehaftet | Sehr gut |
| AAC | aac | .aac | Verlustbehaftet | Exzellent |
| M4A | aac | .m4a | Verlustbehaftet | Exzellent |
| Opus | libopus | .opus | Verlustbehaftet | Beste Effizienz |
| WMA | wmav2 | .wma | Verlustbehaftet | Gut |
| AIFF | pcm_s16be | .aiff | Unkomprimiert | Perfekt |
| ALAC | alac | .m4a | Verlustfrei | Perfekt |

### .NET 4.0 Kompatibilität

✅ **Alle Features sind .NET 4.0 / C# 5.0 kompatibel**:
- Keine async/await (ThreadPool stattdessen)
- Keine LINQ-Erweiterungen die .NET 4.5+ brauchen
- System.IO.Compression mit Fallback zu PowerShell
- WebClient statt HttpClient

### Fehlerbehandlung

- ✅ Prüfung auf FFmpeg-Installation vor Konvertierung
- ✅ Validierung von Eingabe- und Ausgabedateien
- ✅ Exit-Code-Prüfung von FFmpeg
- ✅ Benutzerfreundliche Fehlermeldungen
- ✅ Detailliertes Logging im Protokoll-Fenster

## 📋 Checkliste für Entwickler

### Erfolgreich implementiert:
- ✅ CodecManager mit Download-Funktion
- ✅ Erweiterte AudioConverterService
- ✅ Vollständige UI in AudioConverterTab
- ✅ Integration in MainWindow
- ✅ Lokalisierung (Deutsch/Englisch)
- ✅ Batch-Konvertierung
- ✅ Echtzeit-Fortschrittsanzeige
- ✅ Codec-Management-Dialog
- ✅ .NET 4.0 Kompatibilität
- ✅ Dark Mode Design

### Build-Anweisungen:

```powershell
# Standard-Build
.\build.ps1

# Mit GUI starten
.\build.ps1 -RunGui

# Oder über Batch-Datei
Start-Studio.bat
```

## 🎨 Design-Prinzipien

Das Feature folgt den Projekt-Vorgaben:

1. **Moderne UI**: Card-basiertes Layout mit Dark Mode
2. **Selbständig**: FFmpeg wird in der Anwendung verwaltet
3. **Optional**: Codecs können gelöscht werden wenn nicht benötigt
4. **Benutzerfreundlich**: Ein-Klick-Installation, klare Status-Anzeigen
5. **Konsistent**: Gleicher Design-Stil wie Recorder und MIDI-Tabs
6. **Performant**: Asynchrone Konvertierung, UI bleibt responsive

## 🔮 Zukünftige Erweiterungen (optional)

Mögliche Verbesserungen:
- [ ] Unterstützung für Video-Audio-Extraktion
- [ ] Preset-Verwaltung (Lieblings-Einstellungen speichern)
- [ ] Drag & Drop für Dateien
- [ ] Automatische Codec-Updates
- [ ] Erweiterte Audio-Filter (Equalizer, Normalisierung)
- [ ] Cloud-Upload nach Konvertierung

## 📞 Support

Bei Fragen oder Problemen:
1. Prüfe ob FFmpeg installiert ist (Status-Card)
2. Schaue ins Konvertierungs-Protokoll für Details
3. Versuche FFmpeg neu zu installieren über Codec-Manager
4. Prüfe ob genug Speicherplatz verfügbar ist

---

**Version**: 1.0  
**Hinzugefügt**: 2024  
**Lizenz**: Folgt der Projekt-Lizenz  
**FFmpeg**: GPL/LGPL (siehe ffmpeg.org)
