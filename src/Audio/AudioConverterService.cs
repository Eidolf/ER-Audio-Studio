using System;
using System.Diagnostics;
using System.IO;

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

    public static class AudioConverterService
    {
        public static string FindFfmpeg()
        {
            // 1. In project folder or tools folder
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFfmpeg = Path.Combine(baseDir, "ffmpeg.exe");
            if (File.Exists(localFfmpeg)) return localFfmpeg;

            string assetsFfmpeg = Path.Combine(baseDir, "..", "assets", "ffmpeg.exe");
            if (File.Exists(assetsFfmpeg)) return Path.GetFullPath(assetsFfmpeg);

            // 2. In PATH
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
    }
}
