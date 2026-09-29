using System;
using System.Diagnostics;
using System.IO;

namespace ErAudioTool.Audio
{
    // MIDI to Audio Synthesizer
    // Supports High-Quality SoundFont synthesis via FluidSynth (with .sf2 or Windows gm.dls)
    // and falls back to built-in algorithmic sine/harmonic synthesis.
    public static class MidiSynthesizer
    {
        public static bool ConvertMidiToWav(string midiFile, string wavFile, Action<string> onLog)
        {
            if (!File.Exists(midiFile))
            {
                if (onLog != null) onLog("MIDI-Datei nicht gefunden: " + midiFile);
                return false;
            }

            // 1. Try High-Quality FluidSynth if available
            if (CodecManager.IsFluidSynthInstalled() && CodecManager.HasSoundFont())
            {
                string fsExe = CodecManager.GetFluidSynthPath();
                string soundFont = CodecManager.GetSoundFontPath();

                if (onLog != null)
                {
                    string sfName = Path.GetFileName(soundFont);
                    onLog("Studio-Synthesizer aktiviert: FluidSynth + " + sfName);
                }

                if (ConvertWithFluidSynth(fsExe, soundFont, midiFile, wavFile, onLog))
                {
                    if (onLog != null) onLog("✓ High-Quality MIDI-Synthese erfolgreich abgeschlossen!");
                    return true;
                }

                if (onLog != null) onLog("FluidSynth-Synthese fehlgeschlagen, wechsle zu internem Synthesizer...");
            }

            // 2. Built-in algorithmic fallback synthesis
            try
            {
                if (onLog != null) onLog("Lese MIDI-Datei: " + Path.GetFileName(midiFile));

                var midiData = ParseMidiFile(midiFile);
                if (midiData == null || midiData.Notes.Count == 0)
                {
                    if (onLog != null) onLog("Keine MIDI-Noten gefunden oder Datei beschädigt.");
                    return false;
                }

                if (onLog != null) onLog(string.Format("Gefunden: {0} Noten, Tempo: {1} BPM", midiData.Notes.Count, midiData.TempoBpm));
                if (onLog != null) onLog("Synthese läuft... (Integrierter Standard-Synthesizer)");

                var audioData = SynthesizeAudio(midiData, onLog);

                if (onLog != null) onLog("Schreibe WAV-Datei: " + Path.GetFileName(wavFile));
                WavWriter.WriteWav(wavFile, audioData, 44100, 2, WavOutputFormat.Pcm16);

                if (onLog != null) onLog("✓ Standard MIDI zu WAV Konvertierung erfolgreich!");
                return true;
            }
            catch (Exception ex)
            {
                if (onLog != null) onLog("Fehler bei MIDI-Synthese: " + ex.Message);
                return false;
            }
        }

        private static bool ConvertWithFluidSynth(string fsExe, string soundFont, string midiFile, string wavFile, Action<string> onLog)
        {
            try
            {
                if (File.Exists(wavFile))
                {
                    try { File.Delete(wavFile); } catch { }
                }

                // fluidsynth -F "out.wav" -r 44100 "soundfont.sf2" "input.mid"
                var psi = new ProcessStartInfo
                {
                    FileName = fsExe,
                    Arguments = string.Format("-ni -F \"{0}\" -r 44100 \"{1}\" \"{2}\"", wavFile, soundFont, midiFile),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = new Process())
                {
                    process.StartInfo = psi;
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data) && onLog != null)
                        {
                            if (!e.Data.Contains("Rendering audio") && !e.Data.Contains("Copyright"))
                            {
                                onLog("FluidSynth: " + e.Data);
                            }
                        }
                    };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data) && onLog != null)
                        {
                            // Filter out harmless non-fatal warnings
                            if (!e.Data.Contains("warning: Ignoring unknown"))
                            {
                                onLog("FluidSynth: " + e.Data);
                            }
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit(120000); // 2 minute timeout

                    if (File.Exists(wavFile) && new FileInfo(wavFile).Length > 1000)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (onLog != null) onLog("FluidSynth Ausführungsfehler: " + ex.Message);
            }

            return false;
        }

        private static MidiData ParseMidiFile(string midiFile)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(midiFile);
                var midi = new MidiData();

                int pos = 0;
                if (bytes.Length < 14 || bytes[0] != 0x4D || bytes[1] != 0x54 || bytes[2] != 0x68 || bytes[3] != 0x64)
                {
                    return null;
                }

                pos = 8;
                int format = (bytes[pos] << 8) | bytes[pos + 1];
                int tracks = (bytes[pos + 2] << 8) | bytes[pos + 3];
                int division = (bytes[pos + 4] << 8) | bytes[pos + 5];
                pos += 6;

                midi.TicksPerQuarterNote = division;

                while (pos < bytes.Length - 8)
                {
                    if (bytes[pos] == 0x4D && bytes[pos + 1] == 0x54 && bytes[pos + 2] == 0x72 && bytes[pos + 3] == 0x6B)
                    {
                        pos += 4;
                        int trackLength = (bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                        pos += 4;

                        int trackEnd = pos + trackLength;
                        ParseTrack(bytes, pos, trackEnd, midi);
                        pos = trackEnd;
                    }
                    else
                    {
                        pos++;
                    }
                }

                return midi;
            }
            catch
            {
                return null;
            }
        }

        private static void ParseTrack(byte[] bytes, int start, int end, MidiData midi)
        {
            int pos = start;
            int currentTick = 0;
            byte lastStatus = 0;

            while (pos < end)
            {
                int deltaTime = ReadVarLen(bytes, ref pos);
                currentTick += deltaTime;

                if (pos >= end) break;

                byte statusByte = bytes[pos];
                if ((statusByte & 0x80) == 0)
                {
                    statusByte = lastStatus;
                }
                else
                {
                    lastStatus = statusByte;
                    pos++;
                }

                int eventType = statusByte & 0xF0;
                int channel = statusByte & 0x0F;

                if (eventType == 0x90) // Note On
                {
                    if (pos + 1 >= end) break;
                    int note = bytes[pos];
                    int velocity = bytes[pos + 1];
                    pos += 2;

                    if (velocity > 0)
                    {
                        midi.Notes.Add(new MidiNote
                        {
                            Tick = currentTick,
                            NoteNumber = note,
                            Velocity = velocity,
                            Channel = channel,
                            IsNoteOn = true
                        });
                    }
                    else
                    {
                        midi.Notes.Add(new MidiNote
                        {
                            Tick = currentTick,
                            NoteNumber = note,
                            Velocity = 0,
                            Channel = channel,
                            IsNoteOn = false
                        });
                    }
                }
                else if (eventType == 0x80) // Note Off
                {
                    if (pos + 1 >= end) break;
                    int note = bytes[pos];
                    int velocity = bytes[pos + 1];
                    pos += 2;

                    midi.Notes.Add(new MidiNote
                    {
                        Tick = currentTick,
                        NoteNumber = note,
                        Velocity = 0,
                        Channel = channel,
                        IsNoteOn = false
                    });
                }
                else if (eventType == 0xB0) { pos += 2; }
                else if (eventType == 0xC0) { pos += 1; }
                else if (eventType == 0xD0) { pos += 1; }
                else if (eventType == 0xE0) { pos += 2; }
                else if (eventType == 0xA0) { pos += 2; }
                else if (statusByte == 0xFF) // Meta Event
                {
                    if (pos >= end) break;
                    int metaType = bytes[pos++];
                    int length = ReadVarLen(bytes, ref pos);

                    if (metaType == 0x51 && length == 3)
                    {
                        int microsecondsPerQuarter = (bytes[pos] << 16) | (bytes[pos + 1] << 8) | bytes[pos + 2];
                        midi.TempoBpm = (int)(60000000.0 / microsecondsPerQuarter);
                    }

                    pos += length;
                }
                else if (statusByte == 0xF0 || statusByte == 0xF7)
                {
                    int length = ReadVarLen(bytes, ref pos);
                    pos += length;
                }
                else
                {
                    break;
                }
            }
        }

        private static int ReadVarLen(byte[] bytes, ref int pos)
        {
            int value = 0;
            byte b;
            do
            {
                if (pos >= bytes.Length) return value;
                b = bytes[pos++];
                value = (value << 7) | (b & 0x7F);
            } while ((b & 0x80) != 0);
            return value;
        }

        private static float[] SynthesizeAudio(MidiData midi, Action<string> onLog)
        {
            const int sampleRate = 44100;
            double secondsPerTick = (60.0 / midi.TempoBpm) / midi.TicksPerQuarterNote;

            int maxTick = 0;
            foreach (var note in midi.Notes)
            {
                if (note.Tick > maxTick) maxTick = note.Tick;
            }

            double totalSeconds = maxTick * secondsPerTick + 2.0;
            int totalSamples = (int)(totalSeconds * sampleRate) * 2;

            float[] audioData = new float[totalSamples];
            var activeNotes = new System.Collections.Generic.Dictionary<int, MidiNote>();
            midi.Notes.Sort((a, b) => a.Tick.CompareTo(b.Tick));

            int lastReportedPercent = 0;

            for (int i = 0; i < midi.Notes.Count; i++)
            {
                var note = midi.Notes[i];
                int noteKey = note.NoteNumber * 16 + note.Channel;

                if (note.IsNoteOn && note.Velocity > 0)
                {
                    activeNotes[noteKey] = note;
                }
                else
                {
                    if (activeNotes.ContainsKey(noteKey))
                    {
                        var noteOn = activeNotes[noteKey];
                        int startTick = noteOn.Tick;
                        int endTick = note.Tick;
                        double startTime = startTick * secondsPerTick;
                        double duration = (endTick - startTick) * secondsPerTick;

                        RenderNote(audioData, sampleRate, noteOn.NoteNumber, noteOn.Velocity, startTime, duration);
                        activeNotes.Remove(noteKey);
                    }
                }

                int percent = (i * 100) / midi.Notes.Count;
                if (percent > lastReportedPercent && percent % 20 == 0)
                {
                    if (onLog != null) onLog(string.Format("Synthese-Fortschritt: {0}%", percent));
                    lastReportedPercent = percent;
                }
            }

            foreach (var kvp in activeNotes)
            {
                var noteOn = kvp.Value;
                double startTime = noteOn.Tick * secondsPerTick;
                double duration = 0.5;
                RenderNote(audioData, sampleRate, noteOn.NoteNumber, noteOn.Velocity, startTime, duration);
            }

            return audioData;
        }

        private static void RenderNote(float[] audioData, int sampleRate, int midiNote, int velocity, double startTime, double duration)
        {
            double frequency = 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);

            int startSample = (int)(startTime * sampleRate);
            int durationSamples = (int)(duration * sampleRate);

            double amplitude = (velocity / 127.0) * 0.15;

            int attackSamples = (int)(0.01 * sampleRate);
            int decaySamples = (int)(0.05 * sampleRate);
            double sustainLevel = 0.7;
            int releaseSamples = (int)(0.1 * sampleRate);

            for (int i = 0; i < durationSamples + releaseSamples; i++)
            {
                int sampleIndex = (startSample + i) * 2;
                if (sampleIndex >= audioData.Length - 1) break;

                double t = i / (double)sampleRate;
                double phase = 2.0 * Math.PI * frequency * t;

                double sample = Math.Sin(phase);
                sample += 0.3 * Math.Sin(2 * phase);
                sample += 0.1 * Math.Sin(3 * phase);
                sample /= 1.4;

                double envelope = 1.0;
                if (i < attackSamples)
                {
                    envelope = (double)i / attackSamples;
                }
                else if (i < attackSamples + decaySamples)
                {
                    double decayProgress = (double)(i - attackSamples) / decaySamples;
                    envelope = 1.0 - (1.0 - sustainLevel) * decayProgress;
                }
                else if (i < durationSamples)
                {
                    envelope = sustainLevel;
                }
                else
                {
                    int releasePos = i - durationSamples;
                    envelope = sustainLevel * (1.0 - (double)releasePos / releaseSamples);
                }

                float value = (float)(sample * amplitude * envelope);
                audioData[sampleIndex] += value * 0.9f;
                audioData[sampleIndex + 1] += value * 1.1f;
            }
        }

        private class MidiData
        {
            public int TempoBpm = 120;
            public int TicksPerQuarterNote = 480;
            public System.Collections.Generic.List<MidiNote> Notes = new System.Collections.Generic.List<MidiNote>();
        }

        private class MidiNote
        {
            public int Tick;
            public int NoteNumber;
            public int Velocity;
            public int Channel;
            public bool IsNoteOn;
        }
    }
}
