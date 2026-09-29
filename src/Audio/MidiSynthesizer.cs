using System;
using System.IO;

namespace ErAudioTool.Audio
{
    // Simple MIDI to Audio Synthesizer
    // Converts MIDI notes to WAV audio using basic sine wave synthesis
    public static class MidiSynthesizer
    {
        public static bool ConvertMidiToWav(string midiFile, string wavFile, Action<string> onLog)
        {
            if (!File.Exists(midiFile))
            {
                if (onLog != null) onLog("MIDI-Datei nicht gefunden: " + midiFile);
                return false;
            }

            try
            {
                if (onLog != null) onLog("Lese MIDI-Datei: " + Path.GetFileName(midiFile));

                // Parse MIDI file
                var midiData = ParseMidiFile(midiFile);
                if (midiData == null || midiData.Notes.Count == 0)
                {
                    if (onLog != null) onLog("Keine MIDI-Noten gefunden oder Datei beschädigt.");
                    return false;
                }

                if (onLog != null) onLog(string.Format("Gefunden: {0} Noten, Tempo: {1} BPM", midiData.Notes.Count, midiData.TempoBpm));

                // Synthesize audio
                if (onLog != null) onLog("Synthese läuft... (Einfacher Sinuswellen-Synthesizer)");
                var audioData = SynthesizeAudio(midiData, onLog);

                // Write WAV file
                if (onLog != null) onLog("Schreibe WAV-Datei: " + Path.GetFileName(wavFile));
                WavWriter.WriteWav(wavFile, audioData, 44100, 2, WavOutputFormat.Pcm16);

                if (onLog != null) onLog("✓ MIDI zu WAV Konvertierung erfolgreich!");
                return true;
            }
            catch (Exception ex)
            {
                if (onLog != null) onLog("Fehler bei MIDI-Synthese: " + ex.Message);
                return false;
            }
        }

        private static MidiData ParseMidiFile(string midiFile)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(midiFile);
                var midi = new MidiData();

                // Simple MIDI parser - reads basic format
                int pos = 0;

                // Check header "MThd"
                if (bytes.Length < 14 || bytes[0] != 0x4D || bytes[1] != 0x54 || bytes[2] != 0x68 || bytes[3] != 0x64)
                {
                    return null; // Not a valid MIDI file
                }

                pos = 8; // Skip header chunk
                int format = (bytes[pos] << 8) | bytes[pos + 1];
                int tracks = (bytes[pos + 2] << 8) | bytes[pos + 3];
                int division = (bytes[pos + 4] << 8) | bytes[pos + 5];
                pos += 6;

                midi.TicksPerQuarterNote = division;

                // Parse tracks
                while (pos < bytes.Length - 8)
                {
                    // Check for track header "MTrk"
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
                // Read delta time (variable length)
                int deltaTime = ReadVarLen(bytes, ref pos);
                currentTick += deltaTime;

                if (pos >= end) break;

                byte statusByte = bytes[pos];

                // Running status
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
                        // Velocity 0 = Note Off
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
                else if (eventType == 0xB0) // Control Change
                {
                    pos += 2;
                }
                else if (eventType == 0xC0) // Program Change
                {
                    pos += 1;
                }
                else if (eventType == 0xD0) // Channel Pressure
                {
                    pos += 1;
                }
                else if (eventType == 0xE0) // Pitch Bend
                {
                    pos += 2;
                }
                else if (eventType == 0xA0) // Polyphonic Aftertouch
                {
                    pos += 2;
                }
                else if (statusByte == 0xFF) // Meta Event
                {
                    if (pos >= end) break;
                    int metaType = bytes[pos++];
                    int length = ReadVarLen(bytes, ref pos);

                    if (metaType == 0x51 && length == 3) // Set Tempo
                    {
                        int microsecondsPerQuarter = (bytes[pos] << 16) | (bytes[pos + 1] << 8) | bytes[pos + 2];
                        midi.TempoBpm = (int)(60000000.0 / microsecondsPerQuarter);
                    }

                    pos += length;
                }
                else if (statusByte == 0xF0 || statusByte == 0xF7) // SysEx
                {
                    int length = ReadVarLen(bytes, ref pos);
                    pos += length;
                }
                else
                {
                    // Unknown event, try to skip
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

            // Calculate total duration
            int maxTick = 0;
            foreach (var note in midi.Notes)
            {
                if (note.Tick > maxTick) maxTick = note.Tick;
            }

            double totalSeconds = maxTick * secondsPerTick + 2.0; // +2 sec for reverb tail
            int totalSamples = (int)(totalSeconds * sampleRate) * 2; // Stereo

            float[] audioData = new float[totalSamples];

            // Track active notes per channel
            var activeNotes = new System.Collections.Generic.Dictionary<int, MidiNote>();

            // Sort notes by tick
            midi.Notes.Sort((a, b) => a.Tick.CompareTo(b.Tick));

            int lastReportedPercent = 0;

            // Render each note
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
                    // Note Off - render the note
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

                // Progress reporting
                int percent = (i * 100) / midi.Notes.Count;
                if (percent > lastReportedPercent && percent % 20 == 0)
                {
                    if (onLog != null) onLog(string.Format("Synthese-Fortschritt: {0}%", percent));
                    lastReportedPercent = percent;
                }
            }

            // Render any remaining active notes (no explicit note-off)
            foreach (var kvp in activeNotes)
            {
                var noteOn = kvp.Value;
                double startTime = noteOn.Tick * secondsPerTick;
                double duration = 0.5; // Default duration
                RenderNote(audioData, sampleRate, noteOn.NoteNumber, noteOn.Velocity, startTime, duration);
            }

            return audioData;
        }

        private static void RenderNote(float[] audioData, int sampleRate, int midiNote, int velocity, double startTime, double duration)
        {
            // MIDI note to frequency: f = 440 * 2^((n-69)/12)
            double frequency = 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);

            int startSample = (int)(startTime * sampleRate);
            int durationSamples = (int)(duration * sampleRate);

            double amplitude = (velocity / 127.0) * 0.15; // Scale down to prevent clipping

            // ADSR envelope
            int attackSamples = (int)(0.01 * sampleRate);  // 10ms attack
            int decaySamples = (int)(0.05 * sampleRate);   // 50ms decay
            double sustainLevel = 0.7;
            int releaseSamples = (int)(0.1 * sampleRate);  // 100ms release

            for (int i = 0; i < durationSamples + releaseSamples; i++)
            {
                int sampleIndex = (startSample + i) * 2; // Stereo
                if (sampleIndex >= audioData.Length - 1) break;

                double t = i / (double)sampleRate;
                double phase = 2.0 * Math.PI * frequency * t;

                // Simple sine wave with harmonics
                double sample = Math.Sin(phase);
                sample += 0.3 * Math.Sin(2 * phase); // 2nd harmonic
                sample += 0.1 * Math.Sin(3 * phase); // 3rd harmonic
                sample /= 1.4; // Normalize

                // Apply ADSR envelope
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
                    // Release
                    int releasePos = i - durationSamples;
                    envelope = sustainLevel * (1.0 - (double)releasePos / releaseSamples);
                }

                float value = (float)(sample * amplitude * envelope);

                // Stereo - slightly different pan for richness
                audioData[sampleIndex] += value * 0.9f;     // Left
                audioData[sampleIndex + 1] += value * 1.1f; // Right (slightly louder)
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
