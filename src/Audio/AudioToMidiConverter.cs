using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ErAudioTool.Audio
{
    public class AudioToMidiOptions
    {
        public double MinPitchHz { get; set; }
        public double MaxPitchHz { get; set; }
        public double EnergyThresholdDb { get; set; }
        public double MinNoteDurationSec { get; set; }
        public int TempoBpm { get; set; }
        public bool PreferAiCliIfAvailable { get; set; }
        public bool AddDrums { get; set; }
        public bool AddBass { get; set; }
        public bool AddChords { get; set; }
        public double YinThreshold { get; set; }
        public int WindowSize { get; set; }
        public int HopSize { get; set; }
        public int MedianFilterSize { get; set; }

        public AudioToMidiOptions()
        {
            MinPitchHz = 55.0;
            MaxPitchHz = 1760.0;
            EnergyThresholdDb = -42.0;
            MinNoteDurationSec = 0.08;
            TempoBpm = 120;
            PreferAiCliIfAvailable = true;
            AddDrums = true;
            AddBass = true;
            AddChords = true;
            YinThreshold = 0.18;
            WindowSize = 2048;
            HopSize = 512;
            MedianFilterSize = 5;
        }
    }

    public class AudioPreAnalysis
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double PeakDbFs { get; set; }
        public double RmsDbFs { get; set; }
        public double NoiseFloorDb { get; set; }
        public double DynamicRangeDb { get; set; }
        public double SilencePercent { get; set; }
        public int ClippingSamples { get; set; }
        public double DurationSec { get; set; }
        public int SampleRate { get; set; }
        public double EstimatedOnsetDensity { get; set; }
        public double SpectralCentroidHz { get; set; }
        public double EstimatedMinPitchHz { get; set; }
        public double EstimatedMaxPitchHz { get; set; }
        public bool IsLikelyPolyphonic { get; set; }
        public int EstimatedTempoBpm { get; set; }
        public AudioToMidiOptions RecommendedOptions { get; set; }
        public string AnalysisSummary { get; set; }
    }

    public class AudioToMidiResult
    {
        public bool Success { get; set; }
        public string OutputFilePath { get; set; }
        public int NoteCount { get; set; }
        public string MethodUsed { get; set; }
        public string ErrorMessage { get; set; }
        public List<MidiNote> Notes { get; set; }
        public AudioToMidiResult() { Notes = new List<MidiNote>(); }
    }

    public static class AudioToMidiConverter
    {
        public static AudioPreAnalysis PreAnalyze(string audioFilePath, Action<string> onLog = null)
        {
            var analysis = new AudioPreAnalysis();
            if (!File.Exists(audioFilePath))
            {
                analysis.Success = false;
                analysis.ErrorMessage = "Datei nicht gefunden: " + audioFilePath;
                return analysis;
            }
            try
            {
                if (onLog != null) onLog("Starte automatische Audio-Voranalyse...");
                string wavToRead = audioFilePath;
                bool tempWavCreated = false;
                if (!audioFilePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    string tempWav = Path.Combine(Path.GetTempPath(), "er_preanalyze_" + Guid.NewGuid().ToString("N") + ".wav");
                    if (AudioConverterService.ConvertAudio(audioFilePath, tempWav, "wav", 0, null))
                    { wavToRead = tempWav; tempWavCreated = true; }
                    else { analysis.Success = false; analysis.ErrorMessage = "Konnte Datei nicht als WAV dekodieren."; return analysis; }
                }
                int sampleRate;
                float[] samples;
                try { samples = ReadWavMonoFloat(wavToRead, out sampleRate); }
                finally { if (tempWavCreated && File.Exists(wavToRead)) { try { File.Delete(wavToRead); } catch { } } }
                if (samples == null || samples.Length == 0) { analysis.Success = false; analysis.ErrorMessage = "Keine Audiodaten gefunden."; return analysis; }
                analysis.SampleRate = sampleRate;
                analysis.DurationSec = (double)samples.Length / sampleRate;
                if (onLog != null) onLog(string.Format("  Audio geladen: {0:N1}s, {1} Hz, {2:N0} Samples", analysis.DurationSec, sampleRate, samples.Length));

                AnalyzeLevels(samples, analysis);
                if (onLog != null) onLog(string.Format("  Peak: {0:N1} dBFS | RMS: {1:N1} dBFS | Stille: {2:N1}%", analysis.PeakDbFs, analysis.RmsDbFs, analysis.SilencePercent));

                AnalyzeNoiseFloor(samples, sampleRate, analysis);
                if (onLog != null) onLog(string.Format("  Rauschboden: {0:N1} dB | Dynamikbereich: {1:N1} dB", analysis.NoiseFloorDb, analysis.DynamicRangeDb));

                AnalyzeSpectralContent(samples, sampleRate, analysis);
                if (onLog != null) onLog(string.Format("  Spektraler Schwerpunkt: {0:N0} Hz | Tonbereich: {1:N0} - {2:N0} Hz", analysis.SpectralCentroidHz, analysis.EstimatedMinPitchHz, analysis.EstimatedMaxPitchHz));

                AnalyzeOnsetDensity(samples, sampleRate, analysis);
                if (onLog != null) onLog(string.Format("  Onset-Dichte: {0:N1} Anschlaege/Sek. | Polyphon: {1}", analysis.EstimatedOnsetDensity, analysis.IsLikelyPolyphonic ? "Ja" : "Nein"));

                EstimateTempo(samples, sampleRate, analysis);
                if (onLog != null) onLog(string.Format("  Geschaetztes Tempo: {0} BPM", analysis.EstimatedTempoBpm));

                var opts = ComputeRecommendedOptions(analysis);
                analysis.RecommendedOptions = opts;
                analysis.AnalysisSummary = string.Format(
                    "Dauer: {0:N1}s | Peak: {1:N1} dBFS | RMS: {2:N1} dBFS | Rauschboden: {3:N1} dB\n" +
                    "Tonbereich: {4:N0}-{5:N0} Hz | Tempo: ~{6} BPM | Polyphon: {7}\n" +
                    "Empfohlene Parameter: Threshold={8:N1} dB | YIN={9:N2} | MinDauer={10:N0} ms | Fenster={11}",
                    analysis.DurationSec, analysis.PeakDbFs, analysis.RmsDbFs, analysis.NoiseFloorDb,
                    analysis.EstimatedMinPitchHz, analysis.EstimatedMaxPitchHz,
                    analysis.EstimatedTempoBpm, analysis.IsLikelyPolyphonic ? "Ja" : "Nein",
                    opts.EnergyThresholdDb, opts.YinThreshold, opts.MinNoteDurationSec * 1000, opts.WindowSize);

                if (onLog != null)
                {
                    onLog("=== Voranalyse abgeschlossen ===");
                    onLog(string.Format("  -> Energie-Schwelle: {0:N1} dB (auto)", opts.EnergyThresholdDb));
                    onLog(string.Format("  -> YIN-Empfindlichkeit: {0:N2} ({1})", opts.YinThreshold,
                        opts.YinThreshold >= 0.22 ? "tolerant/polyphon" : opts.YinThreshold <= 0.14 ? "streng/monophon" : "ausgewogen"));
                    onLog(string.Format("  -> Minimale Notendauer: {0:N0} ms", opts.MinNoteDurationSec * 1000));
                    onLog(string.Format("  -> Tempo: {0} BPM", opts.TempoBpm));
                    onLog(string.Format("  -> Tonbereich: {0:N0} - {1:N0} Hz", opts.MinPitchHz, opts.MaxPitchHz));
                    onLog(string.Format("  -> Fenstergroesse: {0} | Hop: {1} | Medianfilter: {2}", opts.WindowSize, opts.HopSize, opts.MedianFilterSize));
                }
                analysis.Success = true;
                return analysis;
            }
            catch (Exception ex) { analysis.Success = false; analysis.ErrorMessage = "Fehler bei Voranalyse: " + ex.Message; return analysis; }
        }

        private static void AnalyzeLevels(float[] samples, AudioPreAnalysis analysis)
        {
            double peak = 0; double sumSq = 0; int silentCount = 0; int clipCount = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double abs = Math.Abs(samples[i]);
                if (abs > peak) peak = abs;
                sumSq += samples[i] * (double)samples[i];
                if (abs < 0.001) silentCount++;
                if (abs >= 0.999) clipCount++;
            }
            double rms = Math.Sqrt(sumSq / Math.Max(1, samples.Length));
            analysis.PeakDbFs = peak > 0 ? 20.0 * Math.Log10(peak) : -100.0;
            analysis.RmsDbFs = rms > 0 ? 20.0 * Math.Log10(rms) : -100.0;
            analysis.SilencePercent = (double)silentCount / Math.Max(1, samples.Length) * 100.0;
            analysis.ClippingSamples = clipCount;
        }

        private static void AnalyzeNoiseFloor(float[] samples, int sampleRate, AudioPreAnalysis analysis)
        {
            int frameSize = sampleRate / 20;
            int numFrames = samples.Length / frameSize;
            if (numFrames < 2) { analysis.NoiseFloorDb = -60; analysis.DynamicRangeDb = analysis.PeakDbFs - analysis.NoiseFloorDb; return; }
            double[] frameRmsDb = new double[numFrames];
            for (int f = 0; f < numFrames; f++)
            {
                int offset = f * frameSize; double sumSq = 0;
                for (int i = 0; i < frameSize && offset + i < samples.Length; i++) sumSq += samples[offset + i] * (double)samples[offset + i];
                double rms = Math.Sqrt(sumSq / frameSize);
                frameRmsDb[f] = rms > 1e-8 ? 20.0 * Math.Log10(rms) : -120.0;
            }
            Array.Sort(frameRmsDb);
            int nonSilentStart = 0;
            for (int i = 0; i < frameRmsDb.Length; i++) { if (frameRmsDb[i] > -80.0) { nonSilentStart = i; break; } }
            int nonSilentCount = frameRmsDb.Length - nonSilentStart;
            if (nonSilentCount < 3) { analysis.NoiseFloorDb = -60; }
            else { int p10Index = nonSilentStart + (int)(nonSilentCount * 0.10); analysis.NoiseFloorDb = frameRmsDb[Math.Min(p10Index, frameRmsDb.Length - 1)]; }
            int p90Index = nonSilentStart + (int)(nonSilentCount * 0.90);
            double loudDb = frameRmsDb[Math.Min(p90Index, frameRmsDb.Length - 1)];
            analysis.DynamicRangeDb = loudDb - analysis.NoiseFloorDb;
        }

        private static void AnalyzeSpectralContent(float[] samples, int sampleRate, AudioPreAnalysis analysis)
        {
            int segmentSize = 4096;
            int numSegments = Math.Min(50, samples.Length / segmentSize);
            if (numSegments < 1) numSegments = 1;
            double minDetectedHz = 10000; double maxDetectedHz = 20; double sumCentroid = 0; int centroidCount = 0;
            int step = Math.Max(1, (samples.Length - segmentSize) / numSegments);
            int minPeriodGlobal = Math.Max(2, sampleRate / 4000);
            int maxPeriodGlobal = Math.Min(segmentSize / 2, sampleRate / 40);
            for (int seg = 0; seg < numSegments; seg++)
            {
                int offset = seg * step;
                if (offset + segmentSize > samples.Length) break;
                double segSumSq = 0;
                for (int i = 0; i < segmentSize; i++) segSumSq += samples[offset + i] * (double)samples[offset + i];
                double segRms = Math.Sqrt(segSumSq / segmentSize);
                double segDb = segRms > 1e-8 ? 20.0 * Math.Log10(segRms) : -120;
                if (segDb < -50) continue;
                double hz = DetectPitchYin(samples, offset, segmentSize, sampleRate, minPeriodGlobal, maxPeriodGlobal, 0.25);
                if (hz > 30 && hz < 5000) { if (hz < minDetectedHz) minDetectedHz = hz; if (hz > maxDetectedHz) maxDetectedHz = hz; }
                int zeroCrossings = 0;
                for (int i = 1; i < segmentSize; i++) { if ((samples[offset + i] >= 0) != (samples[offset + i - 1] >= 0)) zeroCrossings++; }
                double zcRate = (double)zeroCrossings / (segmentSize / (double)sampleRate) / 2.0;
                sumCentroid += zcRate; centroidCount++;
            }
            analysis.SpectralCentroidHz = centroidCount > 0 ? sumCentroid / centroidCount : 500;
            if (minDetectedHz < 10000 && maxDetectedHz > 20) { analysis.EstimatedMinPitchHz = Math.Max(30, minDetectedHz * 0.7); analysis.EstimatedMaxPitchHz = Math.Min(4200, maxDetectedHz * 1.5); }
            else { analysis.EstimatedMinPitchHz = 65; analysis.EstimatedMaxPitchHz = 2100; }
        }

        private static void AnalyzeOnsetDensity(float[] samples, int sampleRate, AudioPreAnalysis analysis)
        {
            int frameSize = sampleRate / 20;
            int numFrames = samples.Length / frameSize;
            if (numFrames < 3) { analysis.EstimatedOnsetDensity = 2.0; analysis.IsLikelyPolyphonic = false; return; }
            double[] frameEnergy = new double[numFrames];
            for (int f = 0; f < numFrames; f++)
            {
                int offset = f * frameSize; double sum = 0;
                for (int i = 0; i < frameSize && offset + i < samples.Length; i++) sum += samples[offset + i] * (double)samples[offset + i];
                frameEnergy[f] = sum / frameSize;
            }
            int onsetCount = 0; double avgEnergy = 0;
            for (int f = 0; f < numFrames; f++) avgEnergy += frameEnergy[f];
            avgEnergy /= numFrames;
            double onsetThreshold = avgEnergy * 1.5; int cooldown = 0;
            for (int f = 1; f < numFrames; f++)
            {
                if (cooldown > 0) { cooldown--; continue; }
                double flux = frameEnergy[f] - frameEnergy[f - 1];
                if (flux > onsetThreshold && frameEnergy[f] > avgEnergy * 0.3) { onsetCount++; cooldown = 2; }
            }
            analysis.EstimatedOnsetDensity = (double)onsetCount / Math.Max(0.1, analysis.DurationSec);
            analysis.IsLikelyPolyphonic = analysis.EstimatedOnsetDensity > 4.0 || analysis.DynamicRangeDb > 25.0 || (analysis.SpectralCentroidHz > 800 && analysis.EstimatedOnsetDensity > 2.5);
        }

        private static void EstimateTempo(float[] samples, int sampleRate, AudioPreAnalysis analysis)
        {
            int frameSize = sampleRate / 20;
            int numFrames = samples.Length / frameSize;
            if (numFrames < 20) { analysis.EstimatedTempoBpm = 120; return; }
            double[] onsetStrength = new double[numFrames];
            double prevEnergy = 0;
            for (int f = 0; f < numFrames; f++)
            {
                int offset = f * frameSize; double sum = 0;
                for (int i = 0; i < frameSize && offset + i < samples.Length; i++) sum += samples[offset + i] * (double)samples[offset + i];
                double energy = sum / frameSize;
                onsetStrength[f] = Math.Max(0, energy - prevEnergy); prevEnergy = energy;
            }
            int minLag = Math.Max(2, (int)(60.0 * 20 / 200.0));
            int maxLag = Math.Min(numFrames / 2, (int)(60.0 * 20 / 50.0));
            double bestCorr = 0; int bestLag = 10;
            int corrLen = Math.Min(numFrames - maxLag, numFrames / 2);
            if (corrLen < 1) corrLen = 1;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                double corr = 0;
                for (int i = 0; i < corrLen; i++) corr += onsetStrength[i] * onsetStrength[i + lag];
                if (corr > bestCorr) { bestCorr = corr; bestLag = lag; }
            }
            int estimatedBpm = (int)Math.Round(60.0 * 20.0 / bestLag);
            if (estimatedBpm < 50) estimatedBpm = 50;
            if (estimatedBpm > 200) estimatedBpm = 200;
            analysis.EstimatedTempoBpm = estimatedBpm;
        }

        private static AudioToMidiOptions ComputeRecommendedOptions(AudioPreAnalysis analysis)
        {
            var opts = new AudioToMidiOptions();
            opts.EnergyThresholdDb = Math.Max(-55, Math.Min(-25, analysis.NoiseFloorDb + 8));
            if (analysis.IsLikelyPolyphonic) opts.YinThreshold = 0.25;
            else if (analysis.DynamicRangeDb < 15) opts.YinThreshold = 0.15;
            else opts.YinThreshold = 0.20;
            if (analysis.ClippingSamples > 100) opts.YinThreshold = Math.Min(0.30, opts.YinThreshold + 0.03);
            opts.MinPitchHz = analysis.EstimatedMinPitchHz;
            opts.MaxPitchHz = analysis.EstimatedMaxPitchHz;
            if (analysis.EstimatedMinPitchHz < 80) opts.WindowSize = 4096;
            else if (analysis.EstimatedOnsetDensity > 8) opts.WindowSize = 1024;
            else opts.WindowSize = 2048;
            opts.HopSize = opts.WindowSize / 4;
            if (analysis.EstimatedOnsetDensity > 8) opts.MinNoteDurationSec = 0.04;
            else if (analysis.EstimatedOnsetDensity > 4) opts.MinNoteDurationSec = 0.06;
            else if (analysis.EstimatedOnsetDensity < 1.5) opts.MinNoteDurationSec = 0.15;
            else opts.MinNoteDurationSec = 0.08;
            opts.MedianFilterSize = (analysis.IsLikelyPolyphonic || analysis.DynamicRangeDb > 30) ? 7 : 5;
            opts.TempoBpm = analysis.EstimatedTempoBpm;
            opts.AddDrums = true; opts.AddBass = true; opts.AddChords = true; opts.PreferAiCliIfAvailable = true;
            return opts;
        }

        public static AudioToMidiResult Convert(string inputAudioFile, string outputMidiFile, AudioToMidiOptions options = null, Action<string> onLog = null)
        {
            if (options == null) options = new AudioToMidiOptions();
            var result = new AudioToMidiResult { OutputFilePath = outputMidiFile };
            if (!File.Exists(inputAudioFile)) { result.Success = false; result.ErrorMessage = "Eingabedatei existiert nicht: " + inputAudioFile; if (onLog != null) onLog("[Fehler] " + result.ErrorMessage); return result; }

            if (options.PreferAiCliIfAvailable)
            {
                string cliTool = FindExternalMidiCli();
                if (!string.IsNullOrEmpty(cliTool))
                {
                    if (onLog != null) onLog("Verwende erweitertes Tool: " + Path.GetFileName(cliTool));
                    if (RunExternalMidiConversion(cliTool, inputAudioFile, outputMidiFile, onLog)) { result.Success = true; result.MethodUsed = Path.GetFileNameWithoutExtension(cliTool); return result; }
                    if (onLog != null) onLog("Rueckfall auf native C# Tonhoehenerkennung...");
                }
            }

            try
            {
                if (onLog != null) onLog("Starte native C# Audio-zu-MIDI Konvertierung...");
                if (onLog != null) onLog(string.Format("  Parameter: YIN={0:N2} | Threshold={1:N1}dB | MinDauer={2:N0}ms | Fenster={3} | Hop={4}",
                    options.YinThreshold, options.EnergyThresholdDb, options.MinNoteDurationSec * 1000, options.WindowSize, options.HopSize));
                float[] samples = null; int sampleRate = 44100;
                string wavToRead = inputAudioFile; bool tempWavCreated = false;
                if (!inputAudioFile.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    string tempWav = Path.Combine(Path.GetTempPath(), "er_midi_temp_" + Guid.NewGuid().ToString("N") + ".wav");
                    if (onLog != null) onLog("Konvertiere zu WAV via FFmpeg...");
                    if (AudioConverterService.ConvertAudio(inputAudioFile, tempWav, "wav", 0, null)) { wavToRead = tempWav; tempWavCreated = true; }
                    else { result.Success = false; result.ErrorMessage = "Eingangsdatei konnte nicht als WAV dekodiert werden."; return result; }
                }
                try { samples = ReadWavMonoFloat(wavToRead, out sampleRate); }
                finally { if (tempWavCreated && File.Exists(wavToRead)) { try { File.Delete(wavToRead); } catch { } } }
                if (samples == null || samples.Length == 0) { result.Success = false; result.ErrorMessage = "Audiodaten konnten nicht geladen werden."; return result; }

                var notes = DetectNotes(samples, sampleRate, options, onLog);
                if (onLog != null) onLog(string.Format("Tonhoehenerkennung abgeschlossen: {0} Melodie-Noten erkannt.", notes.Count));

                var arrangedNotes = new List<MidiNote>(notes);
                if (notes.Count > 0)
                {
                    double totalDuration = 0;
                    foreach (var n in notes) { double end = n.StartTimeSec + n.DurationSec; if (end > totalDuration) totalDuration = end; }
                    if (options.AddBass) { var bassNotes = GenerateBassline(notes, options.TempoBpm); arrangedNotes.AddRange(bassNotes); if (onLog != null) onLog(string.Format("Auto-Arrangement: {0} Bass-Noten (Kanal 2).", bassNotes.Count)); }
                    if (options.AddChords) { var chordNotes = GenerateHarmonies(notes, options.TempoBpm); arrangedNotes.AddRange(chordNotes); if (onLog != null) onLog(string.Format("Auto-Arrangement: {0} Harmonie-Noten (Kanal 3).", chordNotes.Count)); }
                    if (options.AddDrums && totalDuration > 0.5) { var drumNotes = GenerateDrums(totalDuration, options.TempoBpm); arrangedNotes.AddRange(drumNotes); if (onLog != null) onLog(string.Format("Auto-Arrangement: {0} Drum-Events (Kanal 10).", drumNotes.Count)); }
                }
                result.Notes = arrangedNotes; result.NoteCount = arrangedNotes.Count;
                MidiWriter.SaveMidiFile(outputMidiFile, arrangedNotes, options.TempoBpm);
                result.Success = true; result.MethodUsed = "Native C# YIN (optimiert) + Auto-Arrangement";
                if (onLog != null) onLog(string.Format("Konvertierung abgeschlossen: {0} Gesamtevents -> {1}", arrangedNotes.Count, Path.GetFileName(outputMidiFile)));
                return result;
            }
            catch (Exception ex) { result.Success = false; result.ErrorMessage = "Fehler: " + ex.Message; if (onLog != null) onLog("[Fehler] " + result.ErrorMessage); return result; }
        }

        private static List<MidiNote> DetectNotes(float[] samples, int sampleRate, AudioToMidiOptions options, Action<string> onLog)
        {
            int windowSize = options.WindowSize > 0 ? options.WindowSize : 2048;
            int hopSize = options.HopSize > 0 ? options.HopSize : windowSize / 4;
            double hopDurationSec = (double)hopSize / sampleRate;
            double yinThreshold = options.YinThreshold > 0 ? options.YinThreshold : 0.18;
            int minPeriod = Math.Max(2, (int)(sampleRate / options.MaxPitchHz));
            int maxPeriod = Math.Min(windowSize / 2, (int)(sampleRate / options.MinPitchHz));
            int totalFrames = (samples.Length - windowSize) / hopSize;
            if (totalFrames <= 0) return new List<MidiNote>();

            // Pass 1: Pitch detection per frame
            int[] rawPitchTrack = new int[totalFrames];
            double[] frameRms = new double[totalFrames];
            for (int f = 0; f < totalFrames; f++)
            {
                int offset = f * hopSize;
                double sumSq = 0;
                for (int i = 0; i < windowSize; i++) { float s = samples[offset + i]; sumSq += s * s; }
                double rms = Math.Sqrt(sumSq / windowSize);
                double db = rms > 1e-6 ? 20.0 * Math.Log10(rms) : -120.0;
                frameRms[f] = rms;
                int detectedMidi = -1;
                if (db >= options.EnergyThresholdDb)
                {
                    double pitchHz = DetectPitchYin(samples, offset, windowSize, sampleRate, minPeriod, maxPeriod, yinThreshold);
                    if (pitchHz > 0)
                    {
                        detectedMidi = HzToMidi(pitchHz);
                        // Octave error correction
                        int periodForDetected = (int)(sampleRate / pitchHz);
                        int octaveDownPeriod = periodForDetected * 2;
                        if (octaveDownPeriod <= maxPeriod)
                        {
                            double octaveDownHz = DetectPitchYin(samples, offset, windowSize, sampleRate,
                                Math.Max(minPeriod, octaveDownPeriod - 2), Math.Min(maxPeriod, octaveDownPeriod + 2), yinThreshold * 1.3);
                            if (octaveDownHz > 0) { double ratio = pitchHz / octaveDownHz; if (ratio > 1.9 && ratio < 2.1) detectedMidi = HzToMidi(octaveDownHz); }
                        }
                    }
                }
                rawPitchTrack[f] = detectedMidi;
            }

            // Pass 2: Median filter
            int medianSize = options.MedianFilterSize > 0 ? options.MedianFilterSize : 5;
            int[] smoothedPitch = MedianFilterPitch(rawPitchTrack, medianSize);

            // Pass 3: Note extraction
            int currentMidi = -1; double currentNoteStart = 0; double currentNoteDuration = 0; float maxRmsInNote = 0;
            var resultNotes = new List<MidiNote>();
            for (int f = 0; f < totalFrames; f++)
            {
                double time = f * hopDurationSec;
                int detMidi = smoothedPitch[f];
                if (detMidi == currentMidi && detMidi != -1) { currentNoteDuration += hopDurationSec; if ((float)frameRms[f] > maxRmsInNote) maxRmsInNote = (float)frameRms[f]; }
                else
                {
                    if (currentMidi != -1 && currentNoteDuration >= options.MinNoteDurationSec)
                    {
                        int vel = (int)Math.Max(30, Math.Min(127, 20 + maxRmsInNote * 250));
                        resultNotes.Add(new MidiNote { NoteNumber = currentMidi, StartTimeSec = currentNoteStart, DurationSec = currentNoteDuration, Velocity = vel });
                    }
                    if (detMidi != -1) { currentMidi = detMidi; currentNoteStart = time; currentNoteDuration = hopDurationSec; maxRmsInNote = (float)frameRms[f]; }
                    else { currentMidi = -1; }
                }
            }
            if (currentMidi != -1 && currentNoteDuration >= options.MinNoteDurationSec)
            {
                int vel = (int)Math.Max(30, Math.Min(127, 20 + maxRmsInNote * 250));
                resultNotes.Add(new MidiNote { NoteNumber = currentMidi, StartTimeSec = currentNoteStart, DurationSec = currentNoteDuration, Velocity = vel });
            }

            if (onLog != null && resultNotes.Count > 0)
            {
                int minNote = 127, maxNote = 0;
                foreach (var n in resultNotes) { if (n.NoteNumber < minNote) minNote = n.NoteNumber; if (n.NoteNumber > maxNote) maxNote = n.NoteNumber; }
                string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
                string minName = noteNames[minNote % 12] + ((minNote / 12) - 1);
                string maxName = noteNames[maxNote % 12] + ((maxNote / 12) - 1);
                onLog(string.Format("  Erkannter Tonbereich: {0} (MIDI {1}) bis {2} (MIDI {3}) = {4} Halbtoene", minName, minNote, maxName, maxNote, maxNote - minNote));
            }
            return resultNotes;
        }

        private static int[] MedianFilterPitch(int[] pitchTrack, int filterSize)
        {
            int[] result = new int[pitchTrack.Length];
            int halfSize = filterSize / 2;
            for (int i = 0; i < pitchTrack.Length; i++)
            {
                if (pitchTrack[i] == -1) { result[i] = -1; continue; }
                var neighbors = new List<int>();
                for (int j = Math.Max(0, i - halfSize); j <= Math.Min(pitchTrack.Length - 1, i + halfSize); j++)
                {
                    if (pitchTrack[j] != -1) neighbors.Add(pitchTrack[j]);
                }
                if (neighbors.Count == 0) result[i] = -1;
                else { neighbors.Sort(); result[i] = neighbors[neighbors.Count / 2]; }
            }
            return result;
        }

        private static double DetectPitchYin(float[] samples, int offset, int windowSize, int sampleRate, int minPeriod, int maxPeriod, double yinThreshold = 0.18)
        {
            if (offset + windowSize > samples.Length) return 0;
            if (maxPeriod <= minPeriod || maxPeriod <= 0) return 0;
            int halfWindow = windowSize / 2;
            if (maxPeriod >= halfWindow) maxPeriod = halfWindow - 1;
            if (minPeriod < 1) minPeriod = 1;

            double[] d = new double[maxPeriod + 1];
            for (int tau = 1; tau <= maxPeriod; tau++)
            {
                double sum = 0;
                for (int j = 0; j < halfWindow; j++) { double diff = samples[offset + j] - samples[offset + j + tau]; sum += diff * diff; }
                d[tau] = sum;
            }
            double[] dPrime = new double[maxPeriod + 1];
            dPrime[0] = 1; double runningSum = 0;
            for (int tau = 1; tau <= maxPeriod; tau++) { runningSum += d[tau]; dPrime[tau] = runningSum > 0 ? (d[tau] * tau) / runningSum : 1; }

            int tauEstimate = -1;
            for (int tau = minPeriod; tau <= maxPeriod; tau++)
            {
                if (dPrime[tau] < yinThreshold) { while (tau + 1 <= maxPeriod && dPrime[tau + 1] < dPrime[tau]) tau++; tauEstimate = tau; break; }
            }
            if (tauEstimate == -1)
            {
                double minVal = yinThreshold * 2.0; if (minVal > 0.45) minVal = 0.45;
                for (int tau = minPeriod; tau <= maxPeriod; tau++) { if (dPrime[tau] < minVal) { minVal = dPrime[tau]; tauEstimate = tau; } }
            }
            if (tauEstimate <= 0) return 0;

            double s0 = tauEstimate > 0 ? dPrime[tauEstimate - 1] : dPrime[tauEstimate];
            double s1 = dPrime[tauEstimate];
            double s2 = tauEstimate + 1 <= maxPeriod ? dPrime[tauEstimate + 1] : dPrime[tauEstimate];
            double denom = (2 * s1 - s0 - s2); double delta = 0;
            if (Math.Abs(denom) > 1e-9) delta = (s0 - s2) / (2 * denom);
            double refinedPeriod = tauEstimate + delta;
            if (refinedPeriod <= 0) return 0;
            return sampleRate / refinedPeriod;
        }

        private static int HzToMidi(double hz) { if (hz <= 0) return 0; return (int)Math.Round(69.0 + 12.0 * Math.Log(hz / 440.0, 2.0)); }

        public static float[] ReadWavMonoFloat(string filePath, out int sampleRate)
        {
            sampleRate = 44100;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                string riff = new string(br.ReadChars(4));
                if (riff != "RIFF") throw new InvalidOperationException("Keine gueltige RIFF Datei");
                br.ReadUInt32(); string wave = new string(br.ReadChars(4));
                if (wave != "WAVE") throw new InvalidOperationException("Keine WAVE Datei");
                short audioFormat = 1; short channels = 1; int bitsPerSample = 16; long dataPos = -1; uint dataLength = 0;
                while (fs.Position < fs.Length - 8)
                {
                    string chunkId = new string(br.ReadChars(4)); uint chunkSize = br.ReadUInt32();
                    if (chunkId == "fmt ") { audioFormat = br.ReadInt16(); channels = br.ReadInt16(); sampleRate = br.ReadInt32(); br.ReadInt32(); br.ReadInt16(); bitsPerSample = br.ReadInt16(); long remaining = chunkSize - 16; if (remaining > 0) fs.Seek(remaining, SeekOrigin.Current); }
                    else if (chunkId == "data") { dataPos = fs.Position; dataLength = chunkSize; break; }
                    else { fs.Seek(chunkSize, SeekOrigin.Current); }
                }
                if (dataPos < 0) throw new InvalidOperationException("WAV 'data' Chunk nicht gefunden");
                fs.Seek(dataPos, SeekOrigin.Begin);
                int bytesPerSample = bitsPerSample / 8;
                int totalSamples = (int)(dataLength / (bytesPerSample * channels));
                float[] mono = new float[totalSamples];
                byte[] raw = br.ReadBytes((int)dataLength); int rawIdx = 0;
                if (audioFormat == 1 && bitsPerSample == 16)
                { for (int i = 0; i < totalSamples; i++) { float sum = 0; for (int c = 0; c < channels; c++) { short val = (short)(raw[rawIdx] | (raw[rawIdx + 1] << 8)); sum += val / 32768.0f; rawIdx += 2; } mono[i] = sum / channels; } }
                else if (audioFormat == 3 && bitsPerSample == 32)
                { for (int i = 0; i < totalSamples; i++) { float sum = 0; for (int c = 0; c < channels; c++) { float val = BitConverter.ToSingle(raw, rawIdx); sum += val; rawIdx += 4; } mono[i] = sum / channels; } }
                else { throw new NotSupportedException(string.Format("WAV Format {0} mit {1} Bits nicht unterstuetzt.", audioFormat, bitsPerSample)); }
                return mono;
            }
        }

        private static string FindExternalMidiCli()
        {
            string[] toolNames = { "basic-pitch", "basic-pitch.exe", "aubionotes", "aubionotes.exe" };
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var tool in toolNames)
            {
                string local = Path.Combine(baseDir, tool); if (File.Exists(local)) return local;
                string assets = Path.Combine(baseDir, "..", "assets", tool); if (File.Exists(assets)) return Path.GetFullPath(assets);
                try { var p = Process.Start(new ProcessStartInfo { FileName = "where.exe", Arguments = tool, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }); string output = p.StandardOutput.ReadLine(); p.WaitForExit(); if (!string.IsNullOrEmpty(output) && File.Exists(output.Trim())) return output.Trim(); } catch { }
            }
            return null;
        }

        private static bool RunExternalMidiConversion(string toolExe, string inputAudio, string outputMidi, Action<string> onLog)
        {
            try
            {
                string outDir = Path.GetDirectoryName(outputMidi);
                var psi = new ProcessStartInfo { FileName = toolExe, Arguments = string.Format("\"{0}\" \"{1}\"", outDir, inputAudio), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var p = Process.Start(psi)) { p.WaitForExit(); return p.ExitCode == 0; }
            }
            catch (Exception ex) { if (onLog != null) onLog("[Warnung] Externes Tool Fehler: " + ex.Message); return false; }
        }

        private static List<MidiNote> GenerateDrums(double totalDurationSec, int tempoBpm)
        {
            var drums = new List<MidiNote>();
            double beatSec = 60.0 / Math.Max(40, tempoBpm); double halfBeatSec = beatSec / 2.0;
            int totalBeats = (int)Math.Ceiling(totalDurationSec / beatSec);
            for (int b = 0; b < totalBeats; b++)
            {
                double time = b * beatSec; if (time >= totalDurationSec) break;
                int measureBeat = b % 4;
                if (measureBeat == 0 || measureBeat == 2) drums.Add(new MidiNote { NoteNumber = 36, StartTimeSec = time, DurationSec = 0.12, Velocity = measureBeat == 0 ? 110 : 95, Channel = 9 });
                if (measureBeat == 1 || measureBeat == 3) drums.Add(new MidiNote { NoteNumber = 38, StartTimeSec = time, DurationSec = 0.12, Velocity = 100, Channel = 9 });
                drums.Add(new MidiNote { NoteNumber = 42, StartTimeSec = time, DurationSec = 0.06, Velocity = 80, Channel = 9 });
                double offBeatTime = time + halfBeatSec;
                if (offBeatTime < totalDurationSec) drums.Add(new MidiNote { NoteNumber = 42, StartTimeSec = offBeatTime, DurationSec = 0.06, Velocity = 65, Channel = 9 });
            }
            return drums;
        }

        private static List<MidiNote> GenerateBassline(List<MidiNote> melodyNotes, int tempoBpm)
        {
            var bass = new List<MidiNote>(); double beatSec = 60.0 / Math.Max(40, tempoBpm); double barSec = beatSec * 4.0;
            if (melodyNotes.Count == 0) return bass;
            double maxTime = 0; foreach (var n in melodyNotes) { double end = n.StartTimeSec + n.DurationSec; if (end > maxTime) maxTime = end; }
            int totalBars = (int)Math.Ceiling(maxTime / barSec);
            for (int bar = 0; bar < totalBars; bar++)
            {
                double barStart = bar * barSec; double barEnd = barStart + barSec;
                int primaryNote = -1;
                foreach (var n in melodyNotes) { if (n.StartTimeSec >= barStart && n.StartTimeSec < barEnd) { primaryNote = n.NoteNumber; break; } }
                if (primaryNote == -1) primaryNote = 60;
                int bassNote = primaryNote; while (bassNote > 48) bassNote -= 12; while (bassNote < 33) bassNote += 12;
                bass.Add(new MidiNote { NoteNumber = bassNote, StartTimeSec = barStart, DurationSec = beatSec * 1.8, Velocity = 90, Channel = 1 });
                if (barStart + beatSec * 2.0 < maxTime) bass.Add(new MidiNote { NoteNumber = bassNote, StartTimeSec = barStart + beatSec * 2.0, DurationSec = beatSec * 1.8, Velocity = 85, Channel = 1 });
            }
            return bass;
        }

        private static List<MidiNote> GenerateHarmonies(List<MidiNote> melodyNotes, int tempoBpm)
        {
            var harmony = new List<MidiNote>();
            foreach (var n in melodyNotes)
            {
                if (n.Channel != 0) continue; if (n.DurationSec < 0.1) continue;
                harmony.Add(new MidiNote { NoteNumber = Math.Max(36, Math.Min(110, n.NoteNumber - 12 + 4)), StartTimeSec = n.StartTimeSec, DurationSec = n.DurationSec, Velocity = Math.Max(40, n.Velocity - 20), Channel = 2 });
                harmony.Add(new MidiNote { NoteNumber = Math.Max(36, Math.Min(110, n.NoteNumber - 12 + 7)), StartTimeSec = n.StartTimeSec, DurationSec = n.DurationSec, Velocity = Math.Max(35, n.Velocity - 25), Channel = 2 });
            }
            return harmony;
        }
    }
}
