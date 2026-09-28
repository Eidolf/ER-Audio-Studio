using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ErAudioTool.Audio;

namespace ErAudioTool.CLI
{
    public static class CommandLineRunner
    {
        public static int Run(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            var parsed = ParseArgs(args);

            if (parsed.ContainsKey("help") || parsed.ContainsKey("h") || parsed.ContainsKey("?"))
            {
                PrintHelp();
                return 0;
            }

            if (parsed.ContainsKey("list-devices") || parsed.ContainsKey("l"))
            {
                PrintDevices();
                return 0;
            }

            if (parsed.ContainsKey("record") || parsed.ContainsKey("r"))
            {
                return RunRecord(parsed);
            }

            if (parsed.ContainsKey("midi") || parsed.ContainsKey("audio-to-midi") || parsed.ContainsKey("m"))
            {
                return RunAudioToMidi(parsed);
            }

            // Default help if unknown arguments
            PrintHelp();
            return 1;
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("--"))
                {
                    string key = arg.Substring(2);
                    string val = "true";
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        val = args[i + 1];
                        i++;
                    }
                    dict[key] = val;
                }
                else if (arg.StartsWith("-") || arg.StartsWith("/"))
                {
                    string key = arg.Substring(1);
                    string val = "true";
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        val = args[i + 1];
                        i++;
                    }
                    dict[key] = val;
                }
            }
            return dict;
        }

        private static void PrintHelp()
        {
            Console.WriteLine();
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  ER AUDIO LOOPBACK RECORDER (CLI)");
            Console.WriteLine("  Natives Windows WASAPI Loopback Audio Capture Tool");
            Console.WriteLine("===============================================================================");
            Console.WriteLine();
            Console.WriteLine("VERWENDUNG:");
            Console.WriteLine("  ErAudioTool.exe [Befehle] [Optionen]");
            Console.WriteLine("  (Ohne Argumente gestartet, öffnet sich die grafische Benutzeroberfläche)");
            Console.WriteLine();
            Console.WriteLine("BEFEHLE:");
            Console.WriteLine("  -r, --record              Startet eine Audioaufnahme über Loopback");
            Console.WriteLine("  -m, --midi <audiofile>    Konvertiert eine Audiodatei in eine MIDI-Datei (.mid)");
            Console.WriteLine("  -l, --list-devices        Listet alle aktiven Wiedergabegeräte auf");
            Console.WriteLine("  -h, --help                Zeigt diesen Hilfetext an");
            Console.WriteLine();
            Console.WriteLine("OPTIONEN FÜR --midi:");
            Console.WriteLine("  -i, --in <dateipfad>      Eingangs-Audiodatei (WAV, MP3, FLAC, OGG, M4A)");
            Console.WriteLine("  -o, --out <dateipfad>     Zieldateipfad (.mid)");
            Console.WriteLine("  -s, --threshold <dB>      Lautstärke-Schwellenwert (Standard: -42 dB)");
            Console.WriteLine("  --min-dur <sekunden>      Minimale Notenlänge (Standard: 0.08 s)");
            Console.WriteLine("  --no-cli                  Erzwingt native C# Tonhöhenerkennung");
            Console.WriteLine();
            Console.WriteLine("OPTIONEN FÜR --record:");
            Console.WriteLine("  -d, --device <idx|id|name> Gerät per Index, ID oder Name wählen (Standard: Standardgerät)");
            Console.WriteLine("  -o, --out <dateipfad>     Zieldateipfad (.wav) angeben");
            Console.WriteLine("  -t, --duration <sekunden> Automatisch nach N Sekunden stoppen");
            Console.WriteLine("  -f, --format <pcm16|float32> Audioformat: pcm16 (Standard) oder float32");
            Console.WriteLine("  -q, --quiet               Fortschrittsbalken und Pegelanzeige ausblenden");
            Console.WriteLine();
            Console.WriteLine("BEISPIELE:");
            Console.WriteLine("  # Alle Wiedergabegeräte anzeigen");
            Console.WriteLine("  ErAudioTool.exe --list-devices");
            Console.WriteLine();
            Console.WriteLine("  # 10 Sekunden vom Standard-Audiogerät aufnehmen:");
            Console.WriteLine("  ErAudioTool.exe --record --duration 10");
            Console.WriteLine();
            Console.WriteLine("  # Audiodatei in MIDI umwandeln:");
            Console.WriteLine("  ErAudioTool.exe --midi \"aufnahme.wav\" --out \"melodie.mid\"");
            Console.WriteLine("===============================================================================");
            Console.WriteLine();
        }

        private static void PrintDevices()
        {
            Console.WriteLine();
            Console.WriteLine("Verfügbare Wiedergabegeräte (Loopback-Quellen):");
            Console.WriteLine("-------------------------------------------------------------------------------");
            var devices = AudioDeviceEnumerator.GetRenderDevices();
            if (devices.Count == 0)
            {
                Console.WriteLine("  [!] Keine aktiven Audiogeräte gefunden.");
                return;
            }

            for (int i = 0; i < devices.Count; i++)
            {
                var d = devices[i];
                string def = d.IsDefault ? " [STANDARD]" : "";
                Console.WriteLine(string.Format("  [{0}] {1}{2}", i, d.Name, def));
                Console.WriteLine(string.Format("      Format: {0} | ID: {1}", d.FormatDescription, d.Id));
            }
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine();
        }

        private static int RunRecord(Dictionary<string, string> args)
        {
            var devices = AudioDeviceEnumerator.GetRenderDevices();
            if (devices.Count == 0)
            {
                Console.Error.WriteLine("[FEHLER] Kein aktives Audiowiedergabegerät gefunden.");
                return 1;
            }

            AudioDeviceInfo targetDevice = null;
            string devArg = null;
            if (args.TryGetValue("device", out devArg) || args.TryGetValue("d", out devArg))
            {
                int idx;
                if (int.TryParse(devArg, out idx) && idx >= 0 && idx < devices.Count)
                {
                    targetDevice = devices[idx];
                }
                else
                {
                    foreach (var d in devices)
                    {
                        if (d.Id.IndexOf(devArg, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            d.Name.IndexOf(devArg, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            targetDevice = d;
                            break;
                        }
                    }
                }
            }

            if (targetDevice == null)
            {
                targetDevice = AudioDeviceEnumerator.GetDefaultRenderDevice();
            }

            // Output path
            string outPath = null;
            if (args.TryGetValue("out", out outPath) || args.TryGetValue("o", out outPath))
            {
                if (!outPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    outPath += ".wav";
                }
                outPath = Path.GetFullPath(outPath);
            }
            else
            {
                string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recordings");
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                string ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                outPath = Path.Combine(baseDir, string.Format("Loopback_{0}.wav", ts));
            }

            // Duration
            int durationSec = 0;
            string durStr = null;
            if (args.TryGetValue("duration", out durStr) || args.TryGetValue("t", out durStr))
            {
                int.TryParse(durStr, out durationSec);
            }

            // Format
            WavOutputFormat format = WavOutputFormat.Pcm16;
            string fmtStr = null;
            if (args.TryGetValue("format", out fmtStr) || args.TryGetValue("f", out fmtStr))
            {
                if (string.Equals(fmtStr, "float32", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fmtStr, "float", StringComparison.OrdinalIgnoreCase))
                {
                    format = WavOutputFormat.Float32;
                }
            }

            bool quiet = args.ContainsKey("quiet") || args.ContainsKey("q");

            Console.WriteLine();
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  ER AUDIO LOOPBACK RECORDER - AUFNAHME GESTARTET");
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  Gerät         : " + targetDevice.Name);
            Console.WriteLine("  Format        : " + (format == WavOutputFormat.Pcm16 ? "16-Bit PCM WAV" : "32-Bit Float WAV") + " (" + targetDevice.SampleRate + " Hz, Stereo)");
            Console.WriteLine("  Ausgabe-Datei : " + outPath);
            if (durationSec > 0)
            {
                Console.WriteLine("  Dauer         : " + durationSec + " Sekunden (stoppt automatisch)");
            }
            else
            {
                Console.WriteLine("  Dauer         : Manuell (Drücken Sie [ENTER] oder [STRG+C] zum Beenden)");
            }
            Console.WriteLine("===============================================================================");
            Console.WriteLine();

            using (var engine = new WasapiLoopbackEngine())
            {
                var stopEvent = new ManualResetEvent(false);

                ConsoleCancelEventHandler cancelHandler = (s, e) =>
                {
                    e.Cancel = true;
                    stopEvent.Set();
                };
                Console.CancelKeyPress += cancelHandler;

                float currentMasterPeak = 0;
                engine.PeakUpdated += (s, e) =>
                {
                    currentMasterPeak = e.Master;
                };

                engine.ErrorOccurred += (s, ex) =>
                {
                    Console.Error.WriteLine("\n[FEHLER BEI AUFNAHME] " + ex.Message);
                    stopEvent.Set();
                };

                TimeSpan elapsed = TimeSpan.Zero;
                long bytesWritten = 0;
                engine.StatsUpdated += (s, e) =>
                {
                    elapsed = e.Elapsed;
                    bytesWritten = e.BytesWritten;
                };

                engine.StartRecording(targetDevice, outPath, format);

                DateTime startTime = DateTime.UtcNow;
                DateTime nextUiUpdate = DateTime.UtcNow;

                // Dedicated thread to listen for Enter keypress
                Thread keyListener = null;
                if (durationSec <= 0)
                {
                    keyListener = new Thread(() =>
                    {
                        try
                        {
                            Console.ReadLine();
                            stopEvent.Set();
                        }
                        catch { }
                    }) { IsBackground = true };
                    keyListener.Start();
                }

                while (!stopEvent.WaitOne(50))
                {
                    if (durationSec > 0 && (DateTime.UtcNow - startTime).TotalSeconds >= durationSec)
                    {
                        break;
                    }

                    if (!quiet && DateTime.UtcNow >= nextUiUpdate)
                    {
                        nextUiUpdate = DateTime.UtcNow.AddMilliseconds(100);

                        double mb = (double)bytesWritten / (1024 * 1024);
                        double db = currentMasterPeak > 0.0001f ? Math.Max(-60.0, 20.0 * Math.Log10(currentMasterPeak)) : -60.0;
                        string dbStr = db <= -59.5 ? "-∞ dB" : string.Format("{0,5:0.0} dB", db);

                        int barWidth = 16;
                        int filled = (int)(Math.Sqrt(Math.Max(0f, Math.Min(1f, currentMasterPeak))) * barWidth);
                        string meterBar = new string('█', filled) + new string('░', Math.Max(0, barWidth - filled));

                        Console.Write("\r  [REC] {0:00}:{1:00}:{2:00}.{3:0} | {4,5:0.0} MB | Pegel: [{5}] {6}  ",
                            (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds, elapsed.Milliseconds / 100,
                            mb, meterBar, dbStr);
                    }
                }

                Console.WriteLine();
                Console.WriteLine("\nAufnahme wird beendet und Datei finalisiert...");
                engine.StopRecording();

                Console.CancelKeyPress -= cancelHandler;
            }

            var fi = new FileInfo(outPath);
            if (fi.Exists)
            {
                double finalMb = (double)fi.Length / (1024 * 1024);
                Console.WriteLine();
                Console.WriteLine("===============================================================================");
                Console.WriteLine("  AUFNAHME ERFOLGREICH GESPEICHERT!");
                Console.WriteLine("===============================================================================");
                Console.WriteLine("  Datei      : " + fi.FullName);
                Console.WriteLine("  Dateigröße : " + string.Format("{0:0.00} MB ({1:N0} Bytes)", finalMb, fi.Length));
                Console.WriteLine("===============================================================================");
                Console.WriteLine();
                return 0;
            }
            else
            {
                Console.Error.WriteLine("[FEHLER] Zieldatei wurde nicht erstellt.");
                return 1;
            }
        }

        private static int RunAudioToMidi(Dictionary<string, string> args)
        {
            string inPath = null;
            if (!args.TryGetValue("midi", out inPath) || inPath == "true")
            {
                if (!args.TryGetValue("in", out inPath) && !args.TryGetValue("i", out inPath))
                {
                    Console.Error.WriteLine("[FEHLER] Keine Eingangs-Audiodatei angegeben. Nutzen Sie --midi <datei> oder -i <datei>.");
                    return 1;
                }
            }

            if (!File.Exists(inPath))
            {
                Console.Error.WriteLine("[FEHLER] Datei nicht gefunden: " + inPath);
                return 1;
            }

            string outPath = null;
            if (!args.TryGetValue("out", out outPath) && !args.TryGetValue("o", out outPath))
            {
                string dir = Path.GetDirectoryName(inPath);
                string baseName = Path.GetFileNameWithoutExtension(inPath);
                outPath = Path.Combine(dir, baseName + ".mid");
            }
            else if (!outPath.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
            {
                outPath += ".mid";
            }

            var opts = new AudioToMidiOptions();

            string threshStr = null;
            if (args.TryGetValue("threshold", out threshStr) || args.TryGetValue("s", out threshStr))
            {
                double thresh;
                if (double.TryParse(threshStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out thresh))
                {
                    opts.EnergyThresholdDb = thresh;
                }
            }

            string minDurStr = null;
            if (args.TryGetValue("min-dur", out minDurStr))
            {
                double dur;
                if (double.TryParse(minDurStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dur))
                {
                    opts.MinNoteDurationSec = dur;
                }
            }

            if (args.ContainsKey("no-cli"))
            {
                opts.PreferAiCliIfAvailable = false;
            }

            Console.WriteLine();
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  ER AUDIO - AUDIO ZU MIDI KONVERTER");
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  Eingabe-Audio : " + inPath);
            Console.WriteLine("  Ausgabe-MIDI  : " + outPath);
            Console.WriteLine("  Schwellenwert : " + opts.EnergyThresholdDb + " dB");
            Console.WriteLine("  Min. Länge    : " + (opts.MinNoteDurationSec * 1000.0) + " ms");
            Console.WriteLine("===============================================================================");
            Console.WriteLine();

            var result = AudioToMidiConverter.Convert(inPath, outPath, opts, msg =>
            {
                Console.WriteLine("  " + msg);
            });

            if (result.Success)
            {
                Console.WriteLine();
                Console.WriteLine("===============================================================================");
                Console.WriteLine("  KONVERTIERUNG ERFOLGREICH ABGESCHLOSSEN!");
                Console.WriteLine("===============================================================================");
                Console.WriteLine("  Methode       : " + result.MethodUsed);
                Console.WriteLine("  Notenanzahl   : " + result.NoteCount);
                Console.WriteLine("  MIDI-Datei    : " + result.OutputFilePath);
                Console.WriteLine("===============================================================================");
                Console.WriteLine();
                return 0;
            }
            else
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("[FEHLER] Audio-zu-MIDI fehlgeschlagen: " + result.ErrorMessage);
                return 1;
            }
        }
    }
}
