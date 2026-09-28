using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ErAudioTool.Audio
{
    public class AudioToMidiOptions
    {
        public double MinPitchHz { get; set; } = 55.0; // A1
        public double MaxPitchHz { get; set; } = 1760.0; // A6
        public double EnergyThresholdDb { get; set; } = -42.0;
        public double MinNoteDurationSec { get; set; } = 0.08;
        public int TempoBpm { get; set; } = 120;
        public bool PreferAiCliIfAvailable { get; set; } = true;
    }

    public class AudioToMidiResult
    {
        public bool Success { get; set; }
        public string OutputFilePath { get; set; }
        public int NoteCount { get; set; }
        public string MethodUsed { get; set; }
        public string ErrorMessage { get; set; }
        public List<MidiNote> Notes { get; set; } = new List<MidiNote>();
    }

    public static class AudioToMidiConverter
    {
        public static AudioToMidiResult Convert(string inputAudioFile, string outputMidiFile, AudioToMidiOptions options = null, Action<string> onLog = null)
        {
            if (options == null) options = new AudioToMidiOptions();
            var result = new AudioToMidiResult { OutputFilePath = outputMidiFile };

            if (!File.Exists(inputAudioFile))
            {
                result.Success = false;
                result.ErrorMessage = "Eingabedatei existiert nicht: " + inputAudioFile;
                if (onLog != null) onLog("[Fehler] " + result.ErrorMessage);
                return result;
            }

            // Option 2 check: Test if external CLI tool (basic-pitch / aubio) is available and preferred
            if (options.PreferAiCliIfAvailable)
            {
                string cliTool = FindExternalMidiCli();
                if (!string.IsNullOrEmpty(cliTool))
                {
                    if (onLog != null) onLog("Verwende erweitertes Tool für Audio-zu-MIDI: " + Path.GetFileName(cliTool));
                    if (RunExternalMidiConversion(cliTool, inputAudioFile, outputMidiFile, onLog))
                    {
                        result.Success = true;
                        result.MethodUsed = Path.GetFileNameWithoutExtension(cliTool);
                        if (onLog != null) onLog("Audio-zu-MIDI Konvertierung erfolgreich abgeschlossen via " + result.MethodUsed);
                        return result;
                    }
                    if (onLog != null) onLog("Rückfall auf native C# Tonhöhenerkennung...");
                }
            }

            // Native C# Pitch Tracking & Note Extraction
            try
            {
                if (onLog != null) onLog("Starte native C# Audio-zu-MIDI Konvertierung...");
                float[] samples = null;
                int sampleRate = 44100;

                string wavToRead = inputAudioFile;
                bool tempWavCreated = false;

                // If input is not WAV, convert to temporary WAV first using FFmpeg if possible
                if (!inputAudioFile.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    string tempWav = Path.Combine(Path.GetTempPath(), "er_midi_temp_" + Guid.NewGuid().ToString("N") + ".wav");
                    if (onLog != null) onLog("Konvertiere Eingangsdatei temporär zu WAV via FFmpeg...");
                    if (AudioConverterService.ConvertAudio(inputAudioFile, tempWav, "wav", 0, null))
                    {
                        wavToRead = tempWav;
                        tempWavCreated = true;
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = "Eingangsdatei konnte nicht als WAV dekodiert werden.";
                        return result;
                    }
                }

                try
                {
                    samples = ReadWavMonoFloat(wavToRead, out sampleRate);
                }
                finally
                {
                    if (tempWavCreated && File.Exists(wavToRead))
                    {
                        try { File.Delete(wavToRead); } catch { }
                    }
                }

                if (samples == null || samples.Length == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Audiodaten konnten nicht geladen werden.";
                    return result;
                }

                var notes = DetectNotes(samples, sampleRate, options, onLog);
                result.Notes = notes;
                result.NoteCount = notes.Count;

                MidiWriter.SaveMidiFile(outputMidiFile, notes, options.TempoBpm);

                result.Success = true;
                result.MethodUsed = "Native C# YIN/Autokorrelation";
                if (onLog != null) onLog(string.Format("Native Konvertierung abgeschlossen: {0} Noten erkannt -> {1}", notes.Count, Path.GetFileName(outputMidiFile)));
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Fehler bei nativer Konvertierung: " + ex.Message;
                if (onLog != null) onLog("[Fehler] " + result.ErrorMessage);
                return result;
            }
        }

        private static List<MidiNote> DetectNotes(float[] samples, int sampleRate, AudioToMidiOptions options, Action<string> onLog)
        {
            int windowSize = 2048;
            int hopSize = 512;
            double hopDurationSec = (double)hopSize / sampleRate;

            int minPeriod = (int)(sampleRate / options.MaxPitchHz);
            int maxPeriod = (int)(sampleRate / options.MinPitchHz);

            int totalFrames = (samples.Length - windowSize) / hopSize;
            if (totalFrames <= 0) return new List<MidiNote>();

            int currentMidi = -1;
            double currentNoteStart = 0;
            double currentNoteDuration = 0;
            float maxRmsInNote = 0;

            var resultNotes = new List<MidiNote>();

            for (int f = 0; f < totalFrames; f++)
            {
                int offset = f * hopSize;
                double time = f * hopDurationSec;

                // Frame Energy / RMS
                double sumSq = 0;
                for (int i = 0; i < windowSize; i++)
                {
                    float s = samples[offset + i];
                    sumSq += s * s;
                }
                double rms = Math.Sqrt(sumSq / windowSize);
                double db = rms > 1e-6 ? 20.0 * Math.Log10(rms) : -120.0;

                int detectedMidi = -1;
                if (db >= options.EnergyThresholdDb)
                {
                    double pitchHz = DetectPitchYin(samples, offset, windowSize, sampleRate, minPeriod, maxPeriod);
                    if (pitchHz > 0)
                    {
                        detectedMidi = HzToMidi(pitchHz);
                    }
                }

                if (detectedMidi == currentMidi && detectedMidi != -1)
                {
                    // Note continuing
                    currentNoteDuration += hopDurationSec;
                    if ((float)rms > maxRmsInNote) maxRmsInNote = (float)rms;
                }
                else
                {
                    // Note ended or changed
                    if (currentMidi != -1 && currentNoteDuration >= options.MinNoteDurationSec)
                    {
                        int vel = (int)Math.Max(30, Math.Min(127, 20 + maxRmsInNote * 250));
                        resultNotes.Add(new MidiNote
                        {
                            NoteNumber = currentMidi,
                            StartTimeSec = currentNoteStart,
                            DurationSec = currentNoteDuration,
                            Velocity = vel
                        });
                    }

                    if (detectedMidi != -1)
                    {
                        currentMidi = detectedMidi;
                        currentNoteStart = time;
                        currentNoteDuration = hopDurationSec;
                        maxRmsInNote = (float)rms;
                    }
                    else
                    {
                        currentMidi = -1;
                        currentNoteDuration = 0;
                        maxRmsInNote = 0;
                    }
                }
            }

            // End last note
            if (currentMidi != -1 && currentNoteDuration >= options.MinNoteDurationSec)
            {
                int vel = (int)Math.Max(30, Math.Min(127, 20 + maxRmsInNote * 250));
                resultNotes.Add(new MidiNote
                {
                    NoteNumber = currentMidi,
                    StartTimeSec = currentNoteStart,
                    DurationSec = currentNoteDuration,
                    Velocity = vel
                });
            }

            return resultNotes;
        }

        private static double DetectPitchYin(float[] samples, int offset, int windowSize, int sampleRate, int minPeriod, int maxPeriod)
        {
            int halfWindow = windowSize / 2;
            if (maxPeriod >= halfWindow) maxPeriod = halfWindow - 1;
            if (minPeriod < 2) minPeriod = 2;

            double[] d = new double[maxPeriod + 1];

            // 1. Difference function
            for (int tau = 0; tau <= maxPeriod; tau++)
            {
                double sum = 0;
                for (int j = 0; j < halfWindow; j++)
                {
                    double diff = samples[offset + j] - samples[offset + j + tau];
                    sum += diff * diff;
                }
                d[tau] = sum;
            }

            // 2. Cumulative mean normalized difference function
            double[] dPrime = new double[maxPeriod + 1];
            dPrime[0] = 1;
            double runningSum = 0;
            for (int tau = 1; tau <= maxPeriod; tau++)
            {
                runningSum += d[tau];
                dPrime[tau] = runningSum > 0 ? (d[tau] * tau) / runningSum : 1;
            }

            // 3. Absolute thresholding (YIN threshold typically 0.15 .. 0.20)
            double threshold = 0.18;
            int tauEstimate = -1;
            for (int tau = minPeriod; tau <= maxPeriod; tau++)
            {
                if (dPrime[tau] < threshold)
                {
                    while (tau + 1 <= maxPeriod && dPrime[tau + 1] < dPrime[tau])
                    {
                        tau++;
                    }
                    tauEstimate = tau;
                    break;
                }
            }

            // Fallback: global minimum within range if below 0.35
            if (tauEstimate == -1)
            {
                double minVal = 0.35;
                for (int tau = minPeriod; tau <= maxPeriod; tau++)
                {
                    if (dPrime[tau] < minVal)
                    {
                        minVal = dPrime[tau];
                        tauEstimate = tau;
                    }
                }
            }

            if (tauEstimate <= 0) return 0;

            // Parabolic interpolation around tauEstimate
            double s0 = tauEstimate > 0 ? dPrime[tauEstimate - 1] : dPrime[tauEstimate];
            double s1 = dPrime[tauEstimate];
            double s2 = tauEstimate + 1 <= maxPeriod ? dPrime[tauEstimate + 1] : dPrime[tauEstimate];

            double denom = (2 * s1 - s0 - s2);
            double delta = 0;
            if (Math.Abs(denom) > 1e-9)
            {
                delta = (s0 - s2) / (2 * denom);
            }

            double refinedPeriod = tauEstimate + delta;
            if (refinedPeriod <= 0) return 0;

            return sampleRate / refinedPeriod;
        }

        private static int HzToMidi(double hz)
        {
            if (hz <= 0) return 0;
            double midi = 69.0 + 12.0 * Math.Log(hz / 440.0, 2.0);
            return (int)Math.Round(midi);
        }

        public static float[] ReadWavMonoFloat(string filePath, out int sampleRate)
        {
            sampleRate = 44100;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                string riff = new string(br.ReadChars(4));
                if (riff != "RIFF") throw new InvalidOperationException("Keine gültige RIFF Datei");
                br.ReadUInt32(); // Size
                string wave = new string(br.ReadChars(4));
                if (wave != "WAVE") throw new InvalidOperationException("Keine WAVE Datei");

                short audioFormat = 1;
                short channels = 1;
                int bitsPerSample = 16;
                long dataPos = -1;
                uint dataLength = 0;

                while (fs.Position < fs.Length - 8)
                {
                    string chunkId = new string(br.ReadChars(4));
                    uint chunkSize = br.ReadUInt32();

                    if (chunkId == "fmt ")
                    {
                        audioFormat = br.ReadInt16();
                        channels = br.ReadInt16();
                        sampleRate = br.ReadInt32();
                        br.ReadInt32(); // byte rate
                        br.ReadInt16(); // block align
                        bitsPerSample = br.ReadInt16();

                        long remaining = chunkSize - 16;
                        if (remaining > 0) fs.Seek(remaining, SeekOrigin.Current);
                    }
                    else if (chunkId == "data")
                    {
                        dataPos = fs.Position;
                        dataLength = chunkSize;
                        break;
                    }
                    else
                    {
                        fs.Seek(chunkSize, SeekOrigin.Current);
                    }
                }

                if (dataPos < 0) throw new InvalidOperationException("WAV 'data' Chunk nicht gefunden");

                fs.Seek(dataPos, SeekOrigin.Begin);
                int bytesPerSample = bitsPerSample / 8;
                int totalSamples = (int)(dataLength / (bytesPerSample * channels));
                float[] mono = new float[totalSamples];

                byte[] raw = br.ReadBytes((int)dataLength);
                int rawIdx = 0;

                if (audioFormat == 1 && bitsPerSample == 16) // PCM 16-bit
                {
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float sum = 0;
                        for (int c = 0; c < channels; c++)
                        {
                            short val = (short)(raw[rawIdx] | (raw[rawIdx + 1] << 8));
                            sum += val / 32768.0f;
                            rawIdx += 2;
                        }
                        mono[i] = sum / channels;
                    }
                }
                else if (audioFormat == 3 && bitsPerSample == 32) // IEEE Float 32-bit
                {
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float sum = 0;
                        for (int c = 0; c < channels; c++)
                        {
                            float val = BitConverter.ToSingle(raw, rawIdx);
                            sum += val;
                            rawIdx += 4;
                        }
                        mono[i] = sum / channels;
                    }
                }
                else
                {
                    throw new NotSupportedException(string.Format("WAV Format {0} mit {1} Bits wird für Audio-zu-MIDI noch nicht direkt unterstützt.", audioFormat, bitsPerSample));
                }

                return mono;
            }
        }

        private static string FindExternalMidiCli()
        {
            string[] toolNames = { "basic-pitch", "basic-pitch.exe", "aubionotes", "aubionotes.exe" };
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            foreach (var tool in toolNames)
            {
                string local = Path.Combine(baseDir, tool);
                if (File.Exists(local)) return local;

                string assets = Path.Combine(baseDir, "..", "assets", tool);
                if (File.Exists(assets)) return Path.GetFullPath(assets);

                // PATH check
                try
                {
                    var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "where.exe",
                        Arguments = tool,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    });
                    string output = p.StandardOutput.ReadLine();
                    p.WaitForExit();
                    if (!string.IsNullOrEmpty(output) && File.Exists(output.Trim()))
                    {
                        return output.Trim();
                    }
                }
                catch { }
            }
            return null;
        }

        private static bool RunExternalMidiConversion(string toolExe, string inputAudio, string outputMidi, Action<string> onLog)
        {
            try
            {
                string outDir = Path.GetDirectoryName(outputMidi);
                string args = string.Format("\"{0}\" \"{1}\"", outDir, inputAudio);

                var psi = new ProcessStartInfo
                {
                    FileName = toolExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var p = Process.Start(psi))
                {
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                if (onLog != null) onLog("[Warnung] Externes Tool Fehler: " + ex.Message);
                return false;
            }
        }
    }
}
