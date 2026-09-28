using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ErAudioTool.Audio
{
    public class ConversionProgressEventArgs : EventArgs
    {
        public double Percentage { get; private set; }
        public string StatusText { get; private set; }

        public ConversionProgressEventArgs(double percentage, string statusText)
        {
            Percentage = percentage;
            StatusText = statusText;
        }
    }

    public enum AudioFormat
    {
        MP3,
        WAV,
        FLAC,
        OGG,
        AAC,
        M4A,
        OPUS,
        WMA,
        AIFF,
        ALAC
    }

    public static class AudioConverterService
    {
        public static string FindFfmpeg()
        {
            // 1. Check CodecManager path first
            string codecPath = CodecManager.GetFfmpegPath();
            if (!string.IsNullOrEmpty(codecPath)) return codecPath;

            // 2. In project folder or tools folder
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFfmpeg = Path.Combine(baseDir, "ffmpeg.exe");
            if (File.Exists(localFfmpeg)) return localFfmpeg;

            string assetsFfmpeg = Path.Combine(baseDir, "..", "assets", "ffmpeg.exe");
            if (File.Exists(assetsFfmpeg)) return Path.GetFullPath(assetsFfmpeg);

            // 3. In PATH
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "ffmpeg",
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

            return null;
        }

        public static bool ConvertAudio(string inputFile, string outputFile, string targetFormat, int bitrateKbps, Action<string> onLog)
        {
            string ffmpeg = FindFfmpeg();
            if (string.IsNullOrEmpty(ffmpeg))
            {
                if (onLog != null) onLog("FFmpeg wurde nicht gefunden. Für MP3/FLAC Konvertierung bitte ffmpeg.exe im Programmordner ablegen.");
                return false;
            }

            string args = "";
            string fmt = targetFormat.ToLowerInvariant();
            if (fmt == "mp3")
            {
                args = string.Format("-i \"{0}\" -vn -b:a {1}k -y \"{2}\"", inputFile, bitrateKbps, outputFile);
            }
            else if (fmt == "flac")
            {
                args = string.Format("-i \"{0}\" -vn -c:a flac -y \"{2}\"", inputFile, bitrateKbps, outputFile);
            }
            else if (fmt == "ogg")
            {
                args = string.Format("-i \"{0}\" -vn -c:a libvorbis -b:a {1}k -y \"{2}\"", inputFile, bitrateKbps, outputFile);
            }
            else if (fmt == "aac" || fmt == "m4a")
            {
                args = string.Format("-i \"{0}\" -vn -c:a aac -b:a {1}k -y \"{2}\"", inputFile, bitrateKbps, outputFile);
            }
            else // wav
            {
                args = string.Format("-i \"{0}\" -vn -c:a pcm_s16le -y \"{2}\"", inputFile, bitrateKbps, outputFile);
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var process = Process.Start(psi))
                {
                    string err = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && File.Exists(outputFile))
                    {
                        if (onLog != null) onLog("Konvertierung erfolgreich abgeschlossen: " + Path.GetFileName(outputFile));
                        return true;
                    }
                    else
                    {
                        if (onLog != null) onLog("Fehler bei FFmpeg: " + err);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                if (onLog != null) onLog("Ausnahmefehler: " + ex.Message);
                return false;
            }
        }

        public static bool ConvertAudioAdvanced(string inputFile, string outputFile, AudioFormat targetFormat,
            int bitrateKbps, int sampleRate, Action<string> onLog, Action<int> onProgress)
        {
            string ffmpeg = FindFfmpeg();
            if (string.IsNullOrEmpty(ffmpeg))
            {
                if (onLog != null) onLog("FFmpeg wurde nicht gefunden. Bitte über 'Codecs verwalten' installieren.");
                return false;
            }

            string args = BuildConversionArgs(inputFile, outputFile, targetFormat, bitrateKbps, sampleRate);

            try
            {
                if (onLog != null) onLog("Starte Konvertierung: " + Path.GetFileName(inputFile) + " → " + Path.GetFileName(outputFile));

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var process = Process.Start(psi))
                {
                    string line;
                    double duration = 0;
                    bool durationFound = false;

                    while ((line = process.StandardError.ReadLine()) != null)
                    {
                        // Parse duration from FFmpeg output
                        if (!durationFound && line.Contains("Duration:"))
                        {
                            Match m = Regex.Match(line, @"Duration: (\d{2}):(\d{2}):(\d{2})\.(\d{2})");
                            if (m.Success)
                            {
                                int hours = int.Parse(m.Groups[1].Value);
                                int minutes = int.Parse(m.Groups[2].Value);
                                int seconds = int.Parse(m.Groups[3].Value);
                                duration = hours * 3600 + minutes * 60 + seconds;
                                durationFound = true;
                            }
                        }

                        // Parse progress
                        if (durationFound && line.Contains("time="))
                        {
                            Match m = Regex.Match(line, @"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})");
                            if (m.Success && duration > 0)
                            {
                                int hours = int.Parse(m.Groups[1].Value);
                                int minutes = int.Parse(m.Groups[2].Value);
                                int seconds = int.Parse(m.Groups[3].Value);
                                double currentTime = hours * 3600 + minutes * 60 + seconds;
                                int progress = (int)((currentTime / duration) * 100);
                                if (onProgress != null) onProgress(Math.Min(progress, 99));
                            }
                        }
                    }

                    process.WaitForExit();

                    if (process.ExitCode == 0 && File.Exists(outputFile))
                    {
                        if (onProgress != null) onProgress(100);
                        if (onLog != null) onLog("✓ Konvertierung erfolgreich abgeschlossen!");
                        return true;
                    }
                    else
                    {
                        if (onLog != null) onLog("✗ Fehler: Konvertierung fehlgeschlagen (Exit Code: " + process.ExitCode + ")");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                if (onLog != null) onLog("✗ Ausnahmefehler: " + ex.Message);
                return false;
            }
        }

        private static string BuildConversionArgs(string inputFile, string outputFile, AudioFormat format, int bitrateKbps, int sampleRate)
        {
            string baseArgs = string.Format("-i \"{0}\" -vn", inputFile);
            string sampleRateArg = sampleRate > 0 ? string.Format(" -ar {0}", sampleRate) : "";
            string outputArgs = "";

            switch (format)
            {
                case AudioFormat.MP3:
                    outputArgs = string.Format(" -c:a libmp3lame -b:a {0}k -q:a 2", bitrateKbps);
                    break;

                case AudioFormat.WAV:
                    outputArgs = " -c:a pcm_s16le";
                    break;

                case AudioFormat.FLAC:
                    outputArgs = " -c:a flac -compression_level 8";
                    break;

                case AudioFormat.OGG:
                    outputArgs = string.Format(" -c:a libvorbis -b:a {0}k -q:a 6", bitrateKbps);
                    break;

                case AudioFormat.AAC:
                case AudioFormat.M4A:
                    outputArgs = string.Format(" -c:a aac -b:a {0}k -movflags +faststart", bitrateKbps);
                    break;

                case AudioFormat.OPUS:
                    outputArgs = string.Format(" -c:a libopus -b:a {0}k -vbr on", bitrateKbps);
                    break;

                case AudioFormat.WMA:
                    outputArgs = string.Format(" -c:a wmav2 -b:a {0}k", bitrateKbps);
                    break;

                case AudioFormat.AIFF:
                    outputArgs = " -c:a pcm_s16be";
                    break;

                case AudioFormat.ALAC:
                    outputArgs = " -c:a alac";
                    break;

                default:
                    outputArgs = string.Format(" -b:a {0}k", bitrateKbps);
                    break;
            }

            return string.Format("{0}{1}{2} -y \"{3}\"", baseArgs, sampleRateArg, outputArgs, outputFile);
        }

        public static string GetFormatExtension(AudioFormat format)
        {
            switch (format)
            {
                case AudioFormat.MP3: return ".mp3";
                case AudioFormat.WAV: return ".wav";
                case AudioFormat.FLAC: return ".flac";
                case AudioFormat.OGG: return ".ogg";
                case AudioFormat.AAC: return ".aac";
                case AudioFormat.M4A: return ".m4a";
                case AudioFormat.OPUS: return ".opus";
                case AudioFormat.WMA: return ".wma";
                case AudioFormat.AIFF: return ".aiff";
                case AudioFormat.ALAC: return ".m4a";
                default: return ".mp3";
            }
        }

        public static string GetFormatDescription(AudioFormat format)
        {
            switch (format)
            {
                case AudioFormat.MP3: return "MP3 (MPEG-1 Audio Layer 3) - Universell kompatibel";
                case AudioFormat.WAV: return "WAV (Waveform Audio) - Unkomprimiert, verlustfrei";
                case AudioFormat.FLAC: return "FLAC (Free Lossless Audio Codec) - Verlustfreie Kompression";
                case AudioFormat.OGG: return "OGG Vorbis - Open-Source, gute Qualität";
                case AudioFormat.AAC: return "AAC (Advanced Audio Coding) - Modern, effizient";
                case AudioFormat.M4A: return "M4A/AAC - Apple/iTunes Format";
                case AudioFormat.OPUS: return "Opus - Modernster Codec, beste Effizienz";
                case AudioFormat.WMA: return "WMA (Windows Media Audio)";
                case AudioFormat.AIFF: return "AIFF - Apple unkomprimiert";
                case AudioFormat.ALAC: return "ALAC (Apple Lossless) - Verlustfrei für Apple";
                default: return format.ToString();
            }
        }
    }
}
