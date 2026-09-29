using System;
using System.IO;
using System.Net;
using System.ComponentModel;

namespace ErAudioTool.Audio
{
    public class DownloadProgressEventArgs : EventArgs
    {
        public int ProgressPercentage { get; set; }
        public long BytesReceived { get; set; }
        public long TotalBytesToReceive { get; set; }
    }

    public static class CodecManager
    {
        private const string FFMPEG_ESSENTIALS_URL = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        private const string FLUIDSYNTH_WIN64_URL = "https://github.com/FluidSynth/fluidsynth/releases/download/v2.6.1/fluidsynth-v2.6.1-win10-x64-cpp11.zip";
        private const string SOUNDFONT_URL = "https://raw.githubusercontent.com/urish/cinto/master/media/FluidR3%20GM.sf2";

        public static string GetCodecDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string codecDir = Path.Combine(baseDir, "codecs");
            if (!Directory.Exists(codecDir))
            {
                try { Directory.CreateDirectory(codecDir); } catch { }
            }
            return codecDir;
        }

        public static string GetFfmpegPath()
        {
            string codecDir = GetCodecDirectory();
            string ffmpegExe = Path.Combine(codecDir, "ffmpeg.exe");
            if (File.Exists(ffmpegExe)) return ffmpegExe;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFfmpeg = Path.Combine(baseDir, "ffmpeg.exe");
            if (File.Exists(localFfmpeg)) return localFfmpeg;

            return null;
        }

        public static bool IsFfmpegInstalled()
        {
            return !string.IsNullOrEmpty(GetFfmpegPath());
        }

        public static string GetFluidSynthPath()
        {
            string codecDir = GetCodecDirectory();
            string fsExe = Path.Combine(codecDir, "fluidsynth.exe");
            if (File.Exists(fsExe)) return fsExe;

            // Also check subfolder if extracted there
            string nested = Path.Combine(codecDir, "fluidsynth", "bin", "fluidsynth.exe");
            if (File.Exists(nested)) return nested;

            return null;
        }

        public static bool IsFluidSynthInstalled()
        {
            return !string.IsNullOrEmpty(GetFluidSynthPath());
        }

        public static string GetSoundFontPath()
        {
            string codecDir = GetCodecDirectory();

            // 1. High-Quality SoundFont (FluidR3 GM)
            string hqSf = Path.Combine(codecDir, "FluidR3_GM.sf2");
            if (File.Exists(hqSf)) return hqSf;

            // 2. Any .sf2 file in codecs directory
            try
            {
                if (Directory.Exists(codecDir))
                {
                    string[] sfFiles = Directory.GetFiles(codecDir, "*.sf2", SearchOption.AllDirectories);
                    if (sfFiles.Length > 0) return sfFiles[0];
                }
            }
            catch { }

            // 3. Fallback: Windows default GM.DLS soundbank (always present on Windows)
            string winDls = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\gm.dls");
            if (File.Exists(winDls)) return winDls;

            return null;
        }

        public static bool HasSoundFont()
        {
            return !string.IsNullOrEmpty(GetSoundFontPath());
        }

        public static bool HasHighQualitySoundFont()
        {
            string sf = GetSoundFontPath();
            return !string.IsNullOrEmpty(sf) && sf.EndsWith(".sf2", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsHighQualityMidiAvailable()
        {
            return IsFluidSynthInstalled() && HasSoundFont();
        }

        public static long GetCodecSize()
        {
            string codecDir = GetCodecDirectory();
            if (!Directory.Exists(codecDir)) return 0;

            long totalSize = 0;
            try
            {
                foreach (string file in Directory.GetFiles(codecDir, "*.*", SearchOption.AllDirectories))
                {
                    FileInfo fi = new FileInfo(file);
                    totalSize += fi.Length;
                }
            }
            catch { }
            return totalSize;
        }

        public static bool DeleteCodecs()
        {
            try
            {
                string codecDir = GetCodecDirectory();
                if (Directory.Exists(codecDir))
                {
                    Directory.Delete(codecDir, true);
                    Directory.CreateDirectory(codecDir);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureTlsSecurity()
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    (SecurityProtocolType)3072 | // TLS 1.2
                    (SecurityProtocolType)768 |  // TLS 1.1
                    SecurityProtocolType.Tls;
            }
            catch { }
        }

        public static void DownloadFfmpeg(Action<int> onProgress, Action<bool, string> onComplete)
        {
            string codecDir = GetCodecDirectory();
            string zipPath = Path.Combine(codecDir, "ffmpeg-temp.zip");
            string url = FFMPEG_ESSENTIALS_URL;

            EnsureTlsSecurity();

            WebClient client = new WebClient();
            client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            client.DownloadProgressChanged += (s, e) =>
            {
                if (onProgress != null) onProgress(e.ProgressPercentage);
            };

            client.DownloadFileCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    if (onComplete != null) onComplete(false, "Download fehlgeschlagen: " + e.Error.Message);
                    try { client.Dispose(); } catch { }
                    return;
                }

                try
                {
                    ExtractFfmpegFromZip(zipPath, codecDir);
                    if (File.Exists(zipPath)) File.Delete(zipPath);

                    if (IsFfmpegInstalled())
                    {
                        if (onComplete != null) onComplete(true, "FFmpeg erfolgreich installiert!");
                    }
                    else
                    {
                        if (onComplete != null) onComplete(false, "FFmpeg konnte nicht extrahiert werden.");
                    }
                }
                catch (Exception ex)
                {
                    if (onComplete != null) onComplete(false, "Fehler beim Entpacken: " + ex.Message);
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                }
            };

            try
            {
                client.DownloadFileAsync(new Uri(url), zipPath);
            }
            catch (Exception ex)
            {
                if (onComplete != null) onComplete(false, "Download konnte nicht gestartet werden: " + ex.Message);
                try { client.Dispose(); } catch { }
            }
        }

        public static void DownloadFluidSynth(Action<int> onProgress, Action<bool, string> onComplete)
        {
            string codecDir = GetCodecDirectory();
            string zipPath = Path.Combine(codecDir, "fluidsynth-temp.zip");
            string url = FLUIDSYNTH_WIN64_URL;

            EnsureTlsSecurity();

            WebClient client = new WebClient();
            client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            client.DownloadProgressChanged += (s, e) =>
            {
                if (onProgress != null) onProgress(e.ProgressPercentage);
            };

            client.DownloadFileCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    if (onComplete != null) onComplete(false, "FluidSynth Download fehlgeschlagen: " + e.Error.Message);
                    try { client.Dispose(); } catch { }
                    return;
                }

                try
                {
                    ExtractFluidSynthFromZip(zipPath, codecDir);
                    if (File.Exists(zipPath)) File.Delete(zipPath);

                    if (IsFluidSynthInstalled())
                    {
                        if (onComplete != null) onComplete(true, "FluidSynth Synthesizer erfolgreich installiert!");
                    }
                    else
                    {
                        if (onComplete != null) onComplete(false, "FluidSynth konnte nicht extrahiert werden.");
                    }
                }
                catch (Exception ex)
                {
                    if (onComplete != null) onComplete(false, "Fehler beim Entpacken: " + ex.Message);
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                }
            };

            try
            {
                client.DownloadFileAsync(new Uri(url), zipPath);
            }
            catch (Exception ex)
            {
                if (onComplete != null) onComplete(false, "Download konnte nicht gestartet werden: " + ex.Message);
                try { client.Dispose(); } catch { }
            }
        }

        public static void DownloadSoundFont(Action<int> onProgress, Action<bool, string> onComplete)
        {
            string codecDir = GetCodecDirectory();
            string sfPath = Path.Combine(codecDir, "FluidR3_GM.sf2");
            string tempPath = Path.Combine(codecDir, "FluidR3_GM.sf2.tmp");
            string url = SOUNDFONT_URL;

            EnsureTlsSecurity();

            WebClient client = new WebClient();
            client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            client.DownloadProgressChanged += (s, e) =>
            {
                if (onProgress != null) onProgress(e.ProgressPercentage);
            };

            client.DownloadFileCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    if (File.Exists(tempPath)) try { File.Delete(tempPath); } catch { }
                    if (onComplete != null) onComplete(false, "SoundFont Download fehlgeschlagen: " + e.Error.Message);
                    try { client.Dispose(); } catch { }
                    return;
                }

                try
                {
                    if (File.Exists(sfPath)) File.Delete(sfPath);
                    File.Move(tempPath, sfPath);

                    if (onComplete != null) onComplete(true, "FluidR3 GM SoundFont erfolgreich installiert!");
                }
                catch (Exception ex)
                {
                    if (onComplete != null) onComplete(false, "Fehler beim Speichern: " + ex.Message);
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                }
            };

            try
            {
                client.DownloadFileAsync(new Uri(url), tempPath);
            }
            catch (Exception ex)
            {
                if (onComplete != null) onComplete(false, "Download konnte nicht gestartet werden: " + ex.Message);
                try { client.Dispose(); } catch { }
            }
        }

        private static void ExtractFfmpegFromZip(string zipPath, string targetDir)
        {
            try
            {
                var zipFileType = Type.GetType("System.IO.Compression.ZipFile, System.IO.Compression.FileSystem");
                if (zipFileType != null)
                {
                    var extractMethod = zipFileType.GetMethod("ExtractToDirectory", new[] { typeof(string), typeof(string) });
                    if (extractMethod != null)
                    {
                        string tempExtract = Path.Combine(targetDir, "temp_extract_ffmpeg");
                        if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                        Directory.CreateDirectory(tempExtract);

                        extractMethod.Invoke(null, new object[] { zipPath, tempExtract });

                        string[] ffmpegFiles = Directory.GetFiles(tempExtract, "ffmpeg.exe", SearchOption.AllDirectories);
                        if (ffmpegFiles.Length > 0)
                        {
                            string ffmpegExe = ffmpegFiles[0];
                            string targetPath = Path.Combine(targetDir, "ffmpeg.exe");
                            File.Copy(ffmpegExe, targetPath, true);

                            string ffprobeSource = Path.Combine(Path.GetDirectoryName(ffmpegExe), "ffprobe.exe");
                            if (File.Exists(ffprobeSource))
                            {
                                File.Copy(ffprobeSource, Path.Combine(targetDir, "ffprobe.exe"), true);
                            }
                        }

                        try { Directory.Delete(tempExtract, true); } catch { }
                        return;
                    }
                }

                // Fallback via PowerShell
                string cmd = string.Format("-Command \"Expand-Archive -Path '{0}' -DestinationPath '{1}\\temp_extract_ffmpeg' -Force\"", zipPath, targetDir);
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", cmd)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = System.Diagnostics.Process.Start(psi)) { p.WaitForExit(); }

                string tempDir = Path.Combine(targetDir, "temp_extract_ffmpeg");
                if (Directory.Exists(tempDir))
                {
                    string[] files = Directory.GetFiles(tempDir, "ffmpeg.exe", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        File.Copy(files[0], Path.Combine(targetDir, "ffmpeg.exe"), true);
                    }
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Entpacken von FFmpeg: " + ex.Message);
            }
        }

        private static void ExtractFluidSynthFromZip(string zipPath, string targetDir)
        {
            try
            {
                string tempDir = Path.Combine(targetDir, "temp_extract_fs");
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                var zipFileType = Type.GetType("System.IO.Compression.ZipFile, System.IO.Compression.FileSystem");
                if (zipFileType != null)
                {
                    var extractMethod = zipFileType.GetMethod("ExtractToDirectory", new[] { typeof(string), typeof(string) });
                    if (extractMethod != null)
                    {
                        extractMethod.Invoke(null, new object[] { zipPath, tempDir });
                    }
                }
                else
                {
                    string cmd = string.Format("-Command \"Expand-Archive -Path '{0}' -DestinationPath '{1}' -Force\"", zipPath, tempDir);
                    var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", cmd)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var p = System.Diagnostics.Process.Start(psi)) { p.WaitForExit(); }
                }

                // Copy all files from the extracted 'bin' folder (fluidsynth.exe and its companion DLLs)
                string[] binDirs = Directory.GetDirectories(tempDir, "bin", SearchOption.AllDirectories);
                if (binDirs.Length > 0)
                {
                    string binFolder = binDirs[0];
                    foreach (string file in Directory.GetFiles(binFolder))
                    {
                        string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                }
                else
                {
                    string[] exes = Directory.GetFiles(tempDir, "fluidsynth.exe", SearchOption.AllDirectories);
                    if (exes.Length > 0)
                    {
                        string dir = Path.GetDirectoryName(exes[0]);
                        foreach (string file in Directory.GetFiles(dir))
                        {
                            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
                        }
                    }
                }

                try { Directory.Delete(tempDir, true); } catch { }
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Entpacken von FluidSynth: " + ex.Message);
            }
        }
    }
}
