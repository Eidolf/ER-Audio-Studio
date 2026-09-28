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
        private const string FFMPEG_DOWNLOAD_URL = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        private const string FFMPEG_ESSENTIALS_URL = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

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

            // Check in base directory (legacy location)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFfmpeg = Path.Combine(baseDir, "ffmpeg.exe");
            if (File.Exists(localFfmpeg)) return localFfmpeg;

            return null;
        }

        public static bool IsFfmpegInstalled()
        {
            return !string.IsNullOrEmpty(GetFfmpegPath());
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

        public static void DownloadFfmpeg(Action<int> onProgress, Action<bool, string> onComplete)
        {
            string codecDir = GetCodecDirectory();
            string zipPath = Path.Combine(codecDir, "ffmpeg-temp.zip");

            // Use simpler essentials build (smaller download)
            string url = FFMPEG_ESSENTIALS_URL;

            // Enable TLS 1.2 for modern HTTPS connections (required for .NET 4.0)
            try
            {
                System.Net.ServicePointManager.SecurityProtocol =
                    (System.Net.SecurityProtocolType)3072; // TLS 1.2
            }
            catch
            {
                // Fallback: try setting multiple protocols
                try
                {
                    System.Net.ServicePointManager.SecurityProtocol =
                        System.Net.SecurityProtocolType.Ssl3 |
                        System.Net.SecurityProtocolType.Tls |
                        (System.Net.SecurityProtocolType)768 |  // Tls11
                        (System.Net.SecurityProtocolType)3072;  // Tls12
                }
                catch { }
            }

            WebClient client = new WebClient();

            // Add headers to appear as a regular browser
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

                // Extract ZIP
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

        private static void ExtractFfmpegFromZip(string zipPath, string targetDir)
        {
            // Simple extraction using System.IO.Compression (available in .NET 4.5+)
            // For .NET 4.0 compatibility, we use a simple shell-based approach
            try
            {
                // Try using System.IO.Compression.ZipFile if available
                var zipFileType = Type.GetType("System.IO.Compression.ZipFile, System.IO.Compression.FileSystem");
                if (zipFileType != null)
                {
                    var extractMethod = zipFileType.GetMethod("ExtractToDirectory", new[] { typeof(string), typeof(string) });
                    if (extractMethod != null)
                    {
                        string tempExtract = Path.Combine(targetDir, "temp_extract");
                        if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                        Directory.CreateDirectory(tempExtract);

                        extractMethod.Invoke(null, new object[] { zipPath, tempExtract });

                        // Find ffmpeg.exe in extracted folders
                        string[] ffmpegFiles = Directory.GetFiles(tempExtract, "ffmpeg.exe", SearchOption.AllDirectories);
                        if (ffmpegFiles.Length > 0)
                        {
                            string ffmpegExe = ffmpegFiles[0];
                            string targetPath = Path.Combine(targetDir, "ffmpeg.exe");
                            File.Copy(ffmpegExe, targetPath, true);

                            // Also copy ffprobe if exists
                            string ffprobeSource = Path.Combine(Path.GetDirectoryName(ffmpegExe), "ffprobe.exe");
                            if (File.Exists(ffprobeSource))
                            {
                                File.Copy(ffprobeSource, Path.Combine(targetDir, "ffprobe.exe"), true);
                            }
                        }

                        // Cleanup temp
                        try { Directory.Delete(tempExtract, true); } catch { }
                        return;
                    }
                }

                // Fallback: Manual extraction using shell
                ExtractUsingShell(zipPath, targetDir);
            }
            catch
            {
                // Last resort
                ExtractUsingShell(zipPath, targetDir);
            }
        }

        private static void ExtractUsingShell(string zipPath, string targetDir)
        {
            try
            {
                // Use PowerShell to extract (available on Windows 7+)
                string tempExtract = Path.Combine(targetDir, "temp_extract");
                if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                Directory.CreateDirectory(tempExtract);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = string.Format("-NoProfile -Command \"Expand-Archive -Path '{0}' -DestinationPath '{1}' -Force\"", zipPath, tempExtract),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    process.WaitForExit(30000); // 30 sec timeout
                }

                // Find and copy ffmpeg.exe
                string[] ffmpegFiles = Directory.GetFiles(tempExtract, "ffmpeg.exe", SearchOption.AllDirectories);
                if (ffmpegFiles.Length > 0)
                {
                    string ffmpegExe = ffmpegFiles[0];
                    File.Copy(ffmpegExe, Path.Combine(targetDir, "ffmpeg.exe"), true);

                    string ffprobeSource = Path.Combine(Path.GetDirectoryName(ffmpegExe), "ffprobe.exe");
                    if (File.Exists(ffprobeSource))
                    {
                        File.Copy(ffprobeSource, Path.Combine(targetDir, "ffprobe.exe"), true);
                    }
                }

                // Cleanup
                try { Directory.Delete(tempExtract, true); } catch { }
            }
            catch (Exception ex)
            {
                throw new Exception("Entpacken fehlgeschlagen: " + ex.Message);
            }
        }
    }
}
