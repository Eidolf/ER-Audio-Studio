using System;
using System.Collections.Generic;

namespace ErAudioTool.UI
{
    public enum Language
    {
        Deutsch,
        English
    }

    public static class Loc
    {
        public static Language Current = Language.Deutsch;

        private static readonly Dictionary<string, string> De = new Dictionary<string, string>
        {
            {"AppTitle", "ER AUDIO STUDIO"},
            {"AppSubtitle", "Professionelle Audioaufnahme, Konvertierung & Analyse"},
            {"StatusReady", "● BEREIT"},
            {"StatusRecording", "● AUFNAHME LÄUFT"},
            {"StatusPaused", "❚❚ PAUSIERT"},
            {"StatusConverting", "⏳ KONVERTIERUNG..."},

            // Tabs
            {"TabRecord", "🎙 Aufnahme"},
            {"TabConvert", "🔄 Konverter"},
            {"TabMidi", "🎹 Audio zu MIDI"},
            {"TabAnalyze", "📊 Audio-Analyse"},
            {"TabSettings", "⚙️ Einstellungen"},

            // Tab 1: Record
            {"DeviceSection", "AUDIO-QUELLE / GERÄTEWAHL"},
            {"DeviceTypeRender", "Lautsprecher / System-Audio (Loopback)"},
            {"DeviceTypeCapture", "Mikrofon / Eingang"},
            {"RefreshDevices", "🔄 Aktualisieren"},
            {"LiveMeter", "LIVE AUDIO-PEGEL (STEREO VU-METER)"},
            {"AudioActive", "● Audio aktiv"},
            {"AudioSilent", "○ Stille"},
            {"StartRecord", "● Aufnahme starten"},
            {"StopRecord", "⏹ Stopp & Speichern"},
            {"PauseRecord", "❚❚ Pause"},
            {"ResumeRecord", "▶ Fortsetzen"},
            {"HistorySection", "LETZTE AUFNAHMEN"},
            {"Play", "▶ Abspielen"},
            {"StopPlay", "⏹ Stopp"},
            {"Explorer", "📂 Explorer"},
            {"AnalyzeFile", "📊 Analysieren"},
            {"ConvertToMidi", "🎹 In MIDI umwandeln"},

            // Tab 2: Convert
            {"ConvertSection", "AUDIO-KONVERTER"},
            {"SelectFile", "Datei auswählen..."},
            {"TargetFormat", "Zielformat:"},
            {"Bitrate", "Bitrate:"},
            {"StartConvert", "🔄 Jetzt konvertieren"},
            {"ConvertLog", "Konvertierungs-Protokoll:"},

            // Tab MIDI
            {"MidiSection", "AUDIO ZU MIDI KONVERTER (PITCH-TRACKING)"},
            {"MidiInputAudio", "Eingangs-Audiodatei:"},
            {"MidiOutputMidi", "Ziel-MIDI-Datei (.mid):"},
            {"MidiSensitivity", "Empfindlichkeit / Min. Lautstärke:"},
            {"MidiMinNoteDur", "Minimale Notenlänge:"},
            {"MidiStartConvert", "🎹 Jetzt in MIDI konvertieren"},
            {"MidiStatusLog", "MIDI-Protokoll & Noten:"},

            // Tab 3: Analyze
            {"AnalyzeSection", "TECHNISCHE AUDIO-INSPEKTION"},
            {"DropHint", "WAV-Datei wählen oder hier ablegen"},
            {"PeakLoudness", "Peak-Pegel (dBFS):"},
            {"RmsLoudness", "RMS Lautheit (dBFS):"},
            {"ClippedSamples", "Clipping-Verzerrungen:"},
            {"SilencePart", "Stille-Anteil:"},
            {"DynamicRange", "Dynamikumfang:"},

            // Tab 4: Settings
            {"SettingsSection", "STUDIO EINSTELLUNGEN & PFADE"},
            {"OutputDir", "Aufnahmeverzeichnis:"},
            {"Browse", "Durchsuchen..."},
            {"FilePrefix", "Dateinamen-Präfix:"},
            {"Language", "Sprache:"}
        };

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            {"AppTitle", "ER AUDIO STUDIO"},
            {"AppSubtitle", "Professional Audio Capture, Conversion & Inspection"},
            {"StatusReady", "● READY"},
            {"StatusRecording", "● RECORDING"},
            {"StatusPaused", "❚❚ PAUSED"},
            {"StatusConverting", "⏳ CONVERTING..."},

            // Tabs
            {"TabRecord", "🎙 Recorder"},
            {"TabConvert", "🔄 Converter"},
            {"TabMidi", "🎹 Audio to MIDI"},
            {"TabAnalyze", "📊 Audio Inspector"},
            {"TabSettings", "⚙️ Settings"},

            // Tab 1: Record
            {"DeviceSection", "AUDIO SOURCE / DEVICE SELECTION"},
            {"DeviceTypeRender", "Speakers / System Audio (Loopback)"},
            {"DeviceTypeCapture", "Microphone / Input"},
            {"RefreshDevices", "🔄 Refresh"},
            {"LiveMeter", "LIVE AUDIO LEVEL (STEREO VU-METER)"},
            {"AudioActive", "● Audio Active"},
            {"AudioSilent", "○ Silent"},
            {"StartRecord", "● Start Recording"},
            {"StopRecord", "⏹ Stop & Save"},
            {"PauseRecord", "❚❚ Pause"},
            {"ResumeRecord", "▶ Resume"},
            {"HistorySection", "RECENT RECORDINGS"},
            {"Play", "▶ Play"},
            {"StopPlay", "⏹ Stop"},
            {"Explorer", "📂 Explorer"},
            {"AnalyzeFile", "📊 Analyze"},
            {"ConvertToMidi", "🎹 Convert to MIDI"},

            // Tab 2: Convert
            {"ConvertSection", "AUDIO CONVERTER"},
            {"SelectFile", "Select Audio File..."},
            {"TargetFormat", "Target Format:"},
            {"Bitrate", "Bitrate:"},
            {"StartConvert", "🔄 Start Conversion"},
            {"ConvertLog", "Conversion Log:"},

            // Tab MIDI
            {"MidiSection", "AUDIO TO MIDI CONVERTER (PITCH-TRACKING)"},
            {"MidiInputAudio", "Input Audio File:"},
            {"MidiOutputMidi", "Target MIDI File (.mid):"},
            {"MidiSensitivity", "Threshold / Min. Volume:"},
            {"MidiMinNoteDur", "Minimum Note Duration:"},
            {"MidiStartConvert", "🎹 Convert to MIDI Now"},
            {"MidiStatusLog", "MIDI Log & Detected Notes:"},

            // Tab 3: Analyze
            {"AnalyzeSection", "TECHNICAL AUDIO INSPECTION"},
            {"DropHint", "Select or drop WAV file here"},
            {"PeakLoudness", "Peak Level (dBFS):"},
            {"RmsLoudness", "RMS Loudness (dBFS):"},
            {"ClippedSamples", "Clipped Samples:"},
            {"SilencePart", "Silence Ratio:"},
            {"DynamicRange", "Dynamic Range:"},

            // Tab 4: Settings
            {"SettingsSection", "STUDIO SETTINGS & PATHS"},
            {"OutputDir", "Recording Output Directory:"},
            {"Browse", "Browse..."},
            {"FilePrefix", "Filename Prefix:"},
            {"Language", "Language:"}
        };

        public static string Get(string key)
        {
            var dict = Current == Language.English ? En : De;
            if (dict.ContainsKey(key)) return dict[key];
            if (De.ContainsKey(key)) return De[key];
            return key;
        }
    }
}
