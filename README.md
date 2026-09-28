# ER-Audio-Studio

<p align=center>
  <img src=assets/logo.png alt=ER Audio Studio Logo width=160 style=border-radius: 14px; />
</p>

<p align=center>
  <b>Moderne, lokale Desktop-Audio-Suite</b><br/>
  Aufnahme (Loopback & Mikrofon), Konvertierung, technische Audio-Inspektion & Kennzahlen.
</p>

---

## 🌟 Hauptfunktionen & Architektur

### 1. 🎙 Professioneller Loopback & Mikrofon Recorder
- **Windows (nativ):** Direkte Windows CoreAudio (**WASAPI**) Schnittstelle. Nimmt System-Audio, Browser, Games oder Spotify verlustfrei ohne Stereo Mix oder Virtual-Cable-Treiber auf.
- **Stereo VU-Meter:** Echte Dezibel (dBFS)-Pegelanzeige in Echtzeit für L/R-Kanäle.
- **Silence-Keeper Technologie:** Verhindert Knackser oder Abreißen der Aufnahme bei leisen Passagen.
- **Integrierter Player & Historie:** Aufnahmen direkt anhören oder im Dateimanager öffnen.

### 2. 🎹 Audio zu MIDI & Multi-Instrument Arrangement (src/Audio/AudioToMidiConverter.cs & MidiWriter.cs)
- **Multi-Instrument Auto-Arrangement für Suno & DAWs:**
  - **Drums & Takt (Kanal 10):** Vollwertiger Beat mit Kick (Bassdrum auf 1 & 3), Snare (auf 2 & 4) und Closed Hi-Hat (Achtel-Groove) synchronisiert auf das Wunsch-Tempo.
  - **Bassline (Kanal 2):** Automatisches Fundament (Electric Bass), das den Grundtönen der Melodie eine Oktave tiefer folgt.
  - **Akkord-Harmonien (Kanal 3):** Begleitende Terz- und Quint-Harmonien (Acoustic Guitar/Pad).
  - **Melodiespur (Kanal 1):** Aus der Audioaufnahme isolierte Lead-Melodie (Acoustic Grand Piano).
- **Native Pitch-Tracking-Engine (C#):** YIN-Autokorrelations-Algorithmus zur präzisen monophonen Noten- & Tonhöhenerkennung aus WAV/MP3/FLAC/OGG/M4A (.NET 4.0 & C# 5 kompatibel).
- **Integrierter Standard-MIDI-Writer (SMF Format 0 Multi-Channel):** Generiert eigenständige `.mid`-Dateien inklusive variabler Delays, Note-On/Off Events, Dynamik/Velocity, GM-Program-Changes und Tempo-Metadaten ohne externe Abhängigkeiten.
- **Optionale KI-/CLI-Erweiterung:** Automatische Einbindung fortschrittlicher Transkriptions-Engines (`basic-pitch`, `aubionotes`), wenn auf dem System verfügbar, mit unterbrechungsfreiem Fallback auf die native C#-Engine.
- **Interaktiver Studio-Tab & Ein-Klick-Workflow:** Aufnahmen direkt aus der Recorder-Historie mit einem Klick in MIDI transformieren, Tempo (BPM) und Begleitinstrumente anpassen.

### 3. 🔄 Audio-Konverter (src/Audio/AudioConverterService.cs)
- Plattformunabhängige Konvertierung zwischen gängigen Formaten: **MP3, WAV, FLAC, OGG, AAC/M4A**.
- Bitraten von 128 kbps bis 320 kbps (High Quality) via FFmpeg.
- Batch-Verarbeitung und saubere Fortschritts-Callbacks.

### 4. 📊 Technische Audio-Analyse & Inspektion (src/Audio/AudioAnalyzer.cs)
- 100% plattformunabhängige C#-PCM-Inspektions-Engine.
- Ermittlung von Peak-Pegeln (dBFS) pro Kanal.
- RMS-Lautheit und Berechnung des Dynamikumfangs.
- Erkennung von Clipping-Verzerrungen und Stille-Anteilen (< -60 dBFS).

### 5. 🌐 Mehrsprachigkeit (src/UI/Localization.cs)
- Zweisprachiges Dictionary-System (Deutsch / Englisch).
- Dynamisch umschaltbar ohne Neustart.

---

## 🐧 Hinweise für die Weiterentwicklung unter Linux

Dieses Repository ist so strukturiert, dass die Kern-Logik und die Assets direkt auf Linux weiterverwendet werden können:

| Komponente | Windows-Status | Linux-Implementierung |
| :--- | :--- | :--- |
| **Audio-Analyse** (AudioAnalyzer.cs) | ✅ Fertig & Getestet | 1:1 portabel (.NET 8 / Mono / Python) |
| **Audio-Konverter** (AudioConverterService.cs) | ✅ Fertig | 1:1 portabel (nutzt System-fmpeg) |
| **Lokalisierung & Assets** | ✅ Fertig | 1:1 portabel (PNGs/ICOs in ssets/) |
| **Desktop-UI** | WPF (MainWindow.cs) | Empfehlung: **[Avalonia UI](https://avaloniaui.net/)** (C# cross-platform XAML) oder Python |
| **Loopback-Aufnahme** | WASAPI (WasapiInterop.cs) | **PipeWire** oder **PulseAudio** Monitor-Sink (parec / FFmpeg pulse) |

---

## 🚀 Schnellstart (Windows)

Einfach doppelt auf die Starter-Datei klicken:
`cmd
Start-Studio.bat
`

Oder über PowerShell kompilieren:
`powershell
.\build.ps1 -RunGui
`
