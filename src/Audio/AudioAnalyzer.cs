using System;
using System.IO;

namespace ErAudioTool.Audio
{
    public class AudioAnalysisResult
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public TimeSpan Duration { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }
        public float PeakLevelLeft { get; set; }
        public float PeakLevelRight { get; set; }
        public float PeakDbfs { get; set; }
        public float RmsDbfs { get; set; }
        public int ClippedSamples { get; set; }
        public double SilencePercentage { get; set; }
        public string DynamicRangeDescription { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    public static class AudioAnalyzer
    {
        public static AudioAnalysisResult AnalyzeWavFile(string filePath)
        {
            var res = new AudioAnalysisResult
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                IsValid = false
            };

            if (!File.Exists(filePath))
            {
                res.ErrorMessage = "Datei nicht gefunden.";
                return res;
            }

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var br = new BinaryReader(fs))
                {
                    // Check RIFF
                    string riff = new string(br.ReadChars(4));
                    if (riff != "RIFF")
                    {
                        res.ErrorMessage = "Ungültiges Format (Kein RIFF-Header).";
                        return res;
                    }

                    br.ReadUInt32(); // riff size
                    string wave = new string(br.ReadChars(4));
                    if (wave != "WAVE")
                    {
                        res.ErrorMessage = "Keine gültige WAVE-Datei.";
                        return res;
                    }

                    ushort audioFormat = 1;
                    ushort channels = 2;
                    uint sampleRate = 48000;
                    ushort bitsPerSample = 16;
                    long dataOffset = 0;
                    uint dataSize = 0;

                    // Scan chunks
                    while (fs.Position < fs.Length - 8)
                    {
                        string chunkId = new string(br.ReadChars(4));
                        uint chunkSize = br.ReadUInt32();

                        if (chunkId == "fmt ")
                        {
                            long startPos = fs.Position;
                            audioFormat = br.ReadUInt16();
                            channels = br.ReadUInt16();
                            sampleRate = br.ReadUInt32();
                            br.ReadUInt32(); // avgBytesPerSec
                            br.ReadUInt16(); // blockAlign
                            bitsPerSample = br.ReadUInt16();
                            fs.Position = startPos + chunkSize;
                        }
                        else if (chunkId == "data")
                        {
                            dataOffset = fs.Position;
                            dataSize = chunkSize;
                            break;
                        }
                        else
                        {
                            fs.Position += chunkSize;
                        }
                    }

                    if (dataOffset == 0 || dataSize == 0)
                    {
                        res.ErrorMessage = "Keine Audiodaten (data-Chunk) gefunden.";
                        return res;
                    }

                    res.SampleRate = (int)sampleRate;
                    res.Channels = channels;
                    res.BitsPerSample = bitsPerSample;

                    int bytesPerSample = bitsPerSample / 8;
                    int blockAlign = channels * bytesPerSample;
                    long totalFrames = blockAlign > 0 ? (dataSize / blockAlign) : 0;
                    double totalSec = sampleRate > 0 ? ((double)totalFrames / sampleRate) : 0;
                    res.Duration = TimeSpan.FromSeconds(totalSec);

                    // Scan samples for Peak, RMS, Clipping
                    fs.Position = dataOffset;
                    byte[] buffer = new byte[65536];
                    int bytesRead;

                    double sumSquares = 0;
                    long sampleCountTotal = 0;
                    float peakLeft = 0f;
                    float peakRight = 0f;
                    int clippedCount = 0;
                    long silentSamples = 0;
                    float silenceThreshold = 0.001f; // ~ -60 dBFS

                    if (audioFormat == 1 && bitsPerSample == 16) // PCM 16-bit
                    {
                        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0 && (fs.Position - dataOffset <= dataSize))
                        {
                            int samplePairs = bytesRead / 2;
                            for (int i = 0; i < samplePairs; i++)
                            {
                                short raw = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
                                float s = raw / 32768.0f;
                                float absVal = Math.Abs(s);

                                int ch = i % channels;
                                if (ch == 0)
                                {
                                    if (absVal > peakLeft) peakLeft = absVal;
                                }
                                else
                                {
                                    if (absVal > peakRight) peakRight = absVal;
                                }

                                if (absVal >= 0.999f) clippedCount++;
                                if (absVal < silenceThreshold) silentSamples++;

                                sumSquares += (s * s);
                                sampleCountTotal++;
                            }
                        }
                    }
                    else if (audioFormat == 3 && bitsPerSample == 32) // IEEE Float 32-bit
                    {
                        float[] floatBuf = new float[16384];
                        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            int floatCount = bytesRead / 4;
                            Buffer.BlockCopy(buffer, 0, floatBuf, 0, bytesRead);

                            for (int i = 0; i < floatCount; i++)
                            {
                                float s = floatBuf[i];
                                float absVal = Math.Abs(s);

                                int ch = i % channels;
                                if (ch == 0)
                                {
                                    if (absVal > peakLeft) peakLeft = absVal;
                                }
                                else
                                {
                                    if (absVal > peakRight) peakRight = absVal;
                                }

                                if (absVal >= 0.999f) clippedCount++;
                                if (absVal < silenceThreshold) silentSamples++;

                                sumSquares += (s * s);
                                sampleCountTotal++;
                            }
                        }
                    }
                    else
                    {
                        res.ErrorMessage = "Audioformat wird für Detail-Messung noch nicht unterstützt (Format: " + audioFormat + ", Bits: " + bitsPerSample + ")";
                        res.IsValid = true;
                        return res;
                    }

                    res.PeakLevelLeft = peakLeft;
                    res.PeakLevelRight = channels > 1 ? peakRight : peakLeft;

                    float maxPeak = Math.Max(res.PeakLevelLeft, res.PeakLevelRight);
                    res.PeakDbfs = maxPeak > 0.00001f ? (float)(20.0 * Math.Log10(maxPeak)) : -96.0f;

                    if (sampleCountTotal > 0)
                    {
                        double meanSquare = sumSquares / sampleCountTotal;
                        double rms = Math.Sqrt(meanSquare);
                        res.RmsDbfs = rms > 0.00001 ? (float)(20.0 * Math.Log10(rms)) : -96.0f;
                        res.SilencePercentage = Math.Round(((double)silentSamples / sampleCountTotal) * 100.0, 1);
                    }
                    else
                    {
                        res.RmsDbfs = -96.0f;
                    }

                    res.ClippedSamples = clippedCount;

                    float dynamicRange = res.PeakDbfs - res.RmsDbfs;
                    if (dynamicRange > 18) res.DynamicRangeDescription = "Sehr hoch / Unkomprimiert (" + dynamicRange.ToString("0.0") + " dB)";
                    else if (dynamicRange > 10) res.DynamicRangeDescription = "Ausgewogen (" + dynamicRange.ToString("0.0") + " dB)";
                    else res.DynamicRangeDescription = "Stark komprimiert / Laut (" + dynamicRange.ToString("0.0") + " dB)";

                    res.IsValid = true;
                }
            }
            catch (Exception ex)
            {
                res.ErrorMessage = ex.Message;
            }

            return res;
        }
    }
}
