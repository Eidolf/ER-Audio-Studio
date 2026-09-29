# MIDI zu Audio Synthesizer - Dokumentation

## 🎵 Übersicht

Der integrierte **MIDI-Synthesizer** ermöglicht die direkte Konvertierung von MIDI-Dateien (.mid, .midi) in Audio-Formate ohne externe Software.

## ✨ Features

### Automatische MIDI-Erkennung
- Erkennt MIDI-Dateien automatisch beim Auswählen
- Zeigt Bestätigungsdialog vor der Konvertierung
- Zwei-Schritt-Prozess: MIDI → WAV → Zielformat

### Eingebauter Synthesizer
- **Native C# Implementierung** - keine externen Abhängigkeiten
- **MIDI-Parser**: Liest Standard MIDI Files (Format 0 & 1)
- **Sinuswellen-Synthese** mit Obertönen für natürlicheren Klang
- **ADSR-Hüllkurve**: Attack, Decay, Sustain, Release
- **Stereo-Output**: Leichte Panorama-Variation für räumlichen Klang
- **Tempo-Erkennung**: Liest BPM aus MIDI-Datei

### Unterstützte MIDI-Events
- ✅ Note On/Off (0x90/0x80)
- ✅ Tempo-Änderungen (Meta Event 0x51)
- ✅ Multiple Tracks
- ✅ Variable Length Encoding
- ✅ Running Status
- ✅ Velocity-Dynamik

## 🎹 Technische Details

### Synthese-Algorithmus

**Frequenz-Berechnung**:
```
f = 440 Hz × 2^((n-69)/12)
```
- MIDI Note 69 = A4 = 440 Hz
- 12 Halbtöne pro Oktave

**Wellenform**:
- Grundton: `sin(2πft)`
- 2. Oberton: `0.3 × sin(4πft)`
- 3. Oberton: `0.1 × sin(6πft)`
- Normalisierung: Division durch 1.4

**ADSR-Envelope**:
- **Attack**: 10ms (0.01s) - Anstieg von 0 auf 1
- **Decay**: 50ms (0.05s) - Abfall von 1 auf 0.7
- **Sustain**: 0.7 (70%) - Haltepegel
- **Release**: 100ms (0.1s) - Abfall auf 0

**Audio-Specs**:
- Sample Rate: 44100 Hz
- Bit Depth: 16-bit PCM
- Channels: 2 (Stereo)
- Amplitude: 15% Maximum (Clipping-Prevention)

### MIDI-Parser

**Unterstützte Chunks**:
- `MThd` - Header Chunk (Format, Tracks, Division)
- `MTrk` - Track Chunk (Events)

**Event-Typen**:
| Event | Hex | Beschreibung |
|-------|-----|--------------|
| Note On | 0x90 | Note beginnt |
| Note Off | 0x80 | Note endet |
| Control Change | 0xB0 | Controller |
| Program Change | 0xC0 | Instrument |
| Pitch Bend | 0xE0 | Tonhöhen-Biegung |
| Set Tempo | 0xFF 0x51 | BPM-Änderung |

### Konvertierungs-Pipeline

```
MIDI-Datei (.mid)
    ↓
[MIDI Parser]
    ↓
Note-Events + Timing
    ↓
[Synthesizer Engine]
    ↓
PCM Audio Buffer (Float32)
    ↓
[WAV Writer]
    ↓
WAV-Datei (44.1kHz, 16-bit, Stereo)
    ↓
[FFmpeg Converter] (optional)
    ↓
Zielformat (MP3, FLAC, OGG, etc.)
```

## 🚀 Nutzung

### Im Audio-Converter Tab

1. Klicke auf "📁 Durchsuchen..." bei Eingangsdatei
2. Wähle eine MIDI-Datei (.mid oder .midi)
3. Wähle das gewünschte Ausgabeformat (MP3, WAV, FLAC, etc.)
4. Bestätige den Konvertierungs-Dialog
5. Warte auf die Synthese (Fortschrittsanzeige)
6. Bei Nicht-WAV-Formaten: Automatische Konvertierung folgt

### Protokoll-Ausgabe

```
[01:47:14] Schritt 1/2: MIDI zu WAV Synthese...
[01:47:14] Lese MIDI-Datei: song.mid
[01:47:14] Gefunden: 523 Noten, Tempo: 120 BPM
[01:47:14] Synthese läuft... (Einfacher Sinuswellen-Synthesizer)
[01:47:15] Synthese-Fortschritt: 20%
[01:47:15] Synthese-Fortschritt: 40%
[01:47:16] Synthese-Fortschritt: 60%
[01:47:16] Synthese-Fortschritt: 80%
[01:47:17] Schreibe WAV-Datei: song.wav
[01:47:17] ✓ MIDI zu WAV Konvertierung erfolgreich!
[01:47:17] Schritt 2/2: WAV zu MP3 Konvertierung...
[01:47:18] ✓ Konvertierung erfolgreich abgeschlossen!
[01:47:18] ✓ MIDI-Konvertierung erfolgreich abgeschlossen!
```

## ⚠️ Einschränkungen

### Audio-Qualität
- **Einfache Synthese**: Verwendet Sinuswellen, keine komplexen Samples
- **Keine Instrumente**: Alle Noten klingen gleich (keine GM-Instrument-Unterstützung)
- **Keine Effekte**: Kein Reverb, Delay, Chorus, etc.
- **Mono-Timbre**: Nur eine Wellenform für alle Kanäle

### MIDI-Features nicht unterstützt
- ❌ Pitch Bend (wird ignoriert)
- ❌ Modulation Wheel
- ❌ Sustain Pedal (CC 64)
- ❌ Expression (CC 11)
- ❌ SysEx Messages
- ❌ Aftertouch
- ❌ Channel-spezifische Instrumente

### Empfohlene Alternative für bessere Qualität

Für professionelle Ergebnisse:
1. **FluidSynth** + SoundFont (.sf2)
2. **VirtualMIDISynth** (Windows)
3. **DAW**: FL Studio, Ableton, Cubase, etc.
4. **Online**: MIDI → MP3 Web-Services

Der integrierte Synthesizer ist für **schnelle Vorschau** und **einfache MIDI-Dateien** gedacht.

## 📊 Performance

**Benchmark** (typische MIDI-Datei):
- 500 Noten, 2 Minuten Länge, 120 BPM
- Synthese-Zeit: ~3-5 Sekunden
- WAV-Größe: ~21 MB (44.1kHz, 16-bit, Stereo)
- CPU-Auslastung: Mittel (Single-Thread)

**Memory**:
- MIDI-Datei: < 1 MB
- Audio-Buffer: ~21 MB für 2 Minuten
- Peak Memory: ~50 MB

## 🔧 Code-Struktur

### MidiSynthesizer.cs

**Hauptmethode**:
```csharp
public static bool ConvertMidiToWav(
    string midiFile, 
    string wavFile, 
    Action<string> onLog
)
```

**Interne Klassen**:
- `MidiData` - Container für geparste MIDI-Daten
- `MidiNote` - Repräsentiert einzelne Note
- `ParseMidiFile()` - MIDI-Datei-Parser
- `ParseTrack()` - Track-Event-Parser
- `SynthesizeAudio()` - Audio-Rendering-Engine
- `RenderNote()` - Einzelne Note synthetisieren

## 🎓 Verwendete Algorithmen

### 1. Variable Length Quantity (VLQ)
MIDI verwendet VLQ für Delta-Times:
```
Byte: 1xxxxxxx (continue) oder 0xxxxxxx (end)
Value: 7 Bits pro Byte, akkumuliert
```

### 2. MIDI Note zu Frequenz
Equal Temperament Tuning:
```
A4 = 440 Hz (MIDI 69)
Ratio = 2^(1/12) ≈ 1.059463
```

### 3. ADSR Envelope Generator
```
Level = Attack → Decay → Sustain → Release
Linearer Übergang zwischen Phasen
```

### 4. Additive Synthese
Mehrere Sinuswellen (Harmonics):
```
Output = sin(f) + 0.3*sin(2f) + 0.1*sin(3f)
```

## 🐛 Troubleshooting

**Problem**: "Keine MIDI-Noten gefunden"
- **Lösung**: MIDI-Datei beschädigt oder ungewöhnliches Format

**Problem**: Audio zu leise
- **Lösung**: Amplitude ist auf 15% begrenzt (Code-Änderung möglich)

**Problem**: Knackser/Clicks im Audio
- **Lösung**: ADSR-Envelope sollte diese verhindern - ggf. Bug-Report

**Problem**: Tempo falsch
- **Lösung**: Manche MIDI-Dateien haben Tempo-Änderungen die nicht unterstützt werden

## 📝 Lizenz & Credits

- **Synthesizer**: Eigenentwicklung für ER Audio Studio
- **MIDI-Spezifikation**: MIDI Manufacturers Association
- **Algorithmen**: Standard Digital Audio Processing

---

**Version**: 1.0  
**Hinzugefügt**: 2024  
**Kompatibilität**: .NET 4.0 / C# 5.0  
**Abhängigkeiten**: Keine (Pure C#)
