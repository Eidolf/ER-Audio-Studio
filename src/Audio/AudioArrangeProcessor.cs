using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ErAudioTool.Audio
{
    public class AudioArrangeOptions
    {
        public double BassGainDb { get; set; }
        public double MidGainDb { get; set; }
        public double TrebleGainDb { get; set; }
        public double MasterVolumeDb { get; set; }
        public bool Normalize { get; set; }
        public bool Compress { get; set; }
        public double CompressionRatio { get; set; }
        public bool Reverb { get; set; }
        public double ReverbAmount { get; set; }
    }

    public class AudioArrangeResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class AudioArrangeProcessor
    {
        public AudioArrangeResult ProcessAudio(string inputPath, string outputPath, AudioArrangeOptions options, Action<string> logCallback)
        {
            try
            {
                if (logCallback != null) logCallback("Lade Audiodatei...");

                // Read input audio file
                float[][] audioData;
                int sampleRate;
                int channels;

                if (!ReadAudioFile(inputPath, out audioData, out sampleRate, out channels, logCallback))
                {
                    return new AudioArrangeResult { Success = false, ErrorMessage = "Fehler beim Laden der Audiodatei" };
                }

                if (logCallback != null) logCallback(string.Format("Geladen: {0} Hz, {1} Kanäle, {2:0.1} Sekunden", sampleRate, channels, audioData[0].Length / (double)sampleRate));

                // Apply EQ
                if (Math.Abs(options.BassGainDb) > 0.01 || Math.Abs(options.MidGainDb) > 0.01 || Math.Abs(options.TrebleGainDb) > 0.01)
                {
                    if (logCallback != null) logCallback("Wende 3-Band-Equalizer an...");
                    ApplyEqualizer(audioData, sampleRate, options.BassGainDb, options.MidGainDb, options.TrebleGainDb);
                }

                // Apply compression
                if (options.Compress)
                {
                    if (logCallback != null) logCallback(string.Format("Wende Kompressor an (Ratio: {0:0.0}:1)...", options.CompressionRatio));
                    ApplyCompression(audioData, options.CompressionRatio);
                }

                // Apply reverb
                if (options.Reverb)
                {
                    if (logCallback != null) logCallback(string.Format("Wende Hall-Effekt an ({0:0}%)...", options.ReverbAmount * 100));
                    ApplyReverb(audioData, sampleRate, options.ReverbAmount);
                }

                // Apply master volume
                if (Math.Abs(options.MasterVolumeDb) > 0.01)
                {
                    if (logCallback != null) logCallback(string.Format("Wende Master-Lautstärke an ({0:+0.0;-0.0;0} dB)...", options.MasterVolumeDb));
                    ApplyGain(audioData, options.MasterVolumeDb);
                }

                // Normalize
                if (options.Normalize)
                {
                    if (logCallback != null) logCallback("Normalisiere Audio...");
                    NormalizeAudio(audioData);
                }

                // Write output file
                if (logCallback != null) logCallback("Schreibe Ausgabedatei...");
                if (!WriteAudioFile(outputPath, audioData, sampleRate, channels, logCallback))
                {
                    return new AudioArrangeResult { Success = false, ErrorMessage = "Fehler beim Schreiben der Ausgabedatei" };
                }

                if (logCallback != null) logCallback("Fertig!");

                return new AudioArrangeResult { Success = true };
            }
            catch (Exception ex)
            {
                return new AudioArrangeResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private bool ReadAudioFile(string path, out float[][] audioData, out int sampleRate, out int channels, Action<string> logCallback)
        {
            audioData = null;
            sampleRate = 44100;
            channels = 2;

            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();

                if (ext == ".wav")
                {
                    return ReadWavFile(path, out audioData, out sampleRate, out channels);
                }
                else
                {
                    // For non-WAV files, use FFmpeg if available
                    if (logCallback != null) logCallback("Konvertiere zu WAV mit FFmpeg...");
                    string tempWav = Path.GetTempFileName() + ".wav";

                    if (AudioConverterService.ConvertAudio(path, tempWav, "wav", 0, logCallback))
                    {
                        bool result = ReadWavFile(tempWav, out audioData, out sampleRate, out channels);
                        try { File.Delete(tempWav); } catch { }
                        return result;
                    }

                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool ReadWavFile(string path, out float[][] audioData, out int sampleRate, out int channels)
        {
            audioData = null;
            sampleRate = 44100;
            channels = 2;

            try
            {
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                {
                    // Read WAV header
                    var riff = new string(br.ReadChars(4));
                    if (riff != "RIFF") return false;

                    br.ReadInt32(); // file size
                    var wave = new string(br.ReadChars(4));
                    if (wave != "WAVE") return false;

                    // Find fmt chunk
                    while (fs.Position < fs.Length)
                    {
                        var chunkId = new string(br.ReadChars(4));
                        var chunkSize = br.ReadInt32();

                        if (chunkId == "fmt ")
                        {
                            var audioFormat = br.ReadInt16(); // 1=PCM, 3=IEEE float
                            channels = br.ReadInt16();
                            sampleRate = br.ReadInt32();
                            br.ReadInt32(); // byte rate
                            br.ReadInt16(); // block align
                            var bitsPerSample = br.ReadInt16();

                            // Skip rest of fmt chunk
                            if (chunkSize > 16)
                            {
                                br.ReadBytes(chunkSize - 16);
                            }

                            // Find data chunk
                            while (fs.Position < fs.Length)
                            {
                                var dataChunkId = new string(br.ReadChars(4));
                                var dataChunkSize = br.ReadInt32();

                                if (dataChunkId == "data")
                                {
                                    // Read audio data
                                    int sampleCount = dataChunkSize / (channels * (bitsPerSample / 8));
                                    audioData = new float[channels][];
                                    for (int i = 0; i < channels; i++)
                                    {
                                        audioData[i] = new float[sampleCount];
                                    }

                                    for (int i = 0; i < sampleCount; i++)
                                    {
                                        for (int ch = 0; ch < channels; ch++)
                                        {
                                            if (audioFormat == 3 && bitsPerSample == 32)
                                            {
                                                // 32-bit float
                                                audioData[ch][i] = br.ReadSingle();
                                            }
                                            else if (bitsPerSample == 16)
                                            {
                                                // 16-bit PCM
                                                short sample = br.ReadInt16();
                                                audioData[ch][i] = sample / 32768.0f;
                                            }
                                            else if (bitsPerSample == 24)
                                            {
                                                // 24-bit PCM
                                                byte b1 = br.ReadByte();
                                                byte b2 = br.ReadByte();
                                                byte b3 = br.ReadByte();
                                                int sample = (b3 << 16) | (b2 << 8) | b1;
                                                if ((sample & 0x800000) != 0) sample |= unchecked((int)0xFF000000);
                                                audioData[ch][i] = sample / 8388608.0f;
                                            }
                                        }
                                    }

                                    return true;
                                }
                                else
                                {
                                    br.ReadBytes(dataChunkSize);
                                }
                            }
                        }
                        else
                        {
                            br.ReadBytes(chunkSize);
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool WriteAudioFile(string path, float[][] audioData, int sampleRate, int channels, Action<string> logCallback)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();

                if (ext == ".wav")
                {
                    return WriteWavFile(path, audioData, sampleRate, channels);
                }
                else
                {
                    // Write temp WAV then convert with FFmpeg
                    string tempWav = Path.GetTempFileName() + ".wav";

                    if (!WriteWavFile(tempWav, audioData, sampleRate, channels))
                    {
                        return false;
                    }

                    string format = ext.TrimStart('.');
                    bool success = AudioConverterService.ConvertAudio(tempWav, path, format, 0, logCallback);

                    try { File.Delete(tempWav); } catch { }

                    return success;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool WriteWavFile(string path, float[][] audioData, int sampleRate, int channels)
        {
            try
            {
                using (var fs = File.Create(path))
                using (var bw = new BinaryWriter(fs))
                {
                    int sampleCount = audioData[0].Length;
                    int dataSize = sampleCount * channels * 2; // 16-bit
                    int fileSize = 36 + dataSize;

                    // RIFF header
                    bw.Write("RIFF".ToCharArray());
                    bw.Write(fileSize);
                    bw.Write("WAVE".ToCharArray());

                    // fmt chunk
                    bw.Write("fmt ".ToCharArray());
                    bw.Write(16); // chunk size
                    bw.Write((short)1); // PCM
                    bw.Write((short)channels);
                    bw.Write(sampleRate);
                    bw.Write(sampleRate * channels * 2); // byte rate
                    bw.Write((short)(channels * 2)); // block align
                    bw.Write((short)16); // bits per sample

                    // data chunk
                    bw.Write("data".ToCharArray());
                    bw.Write(dataSize);

                    // Write samples
                    for (int i = 0; i < sampleCount; i++)
                    {
                        for (int ch = 0; ch < channels; ch++)
                        {
                            float sample = Math.Max(-1.0f, Math.Min(1.0f, audioData[ch][i]));
                            short intSample = (short)(sample * 32767.0f);
                            bw.Write(intSample);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyEqualizer(float[][] audioData, int sampleRate, double bassGainDb, double midGainDb, double trebleGainDb)
        {
            // Simple 3-band EQ using biquad filters
            float bassGain = (float)Math.Pow(10, bassGainDb / 20.0);
            float midGain = (float)Math.Pow(10, midGainDb / 20.0);
            float trebleGain = (float)Math.Pow(10, trebleGainDb / 20.0);

            // Bass: low-shelf filter at 250 Hz
            // Mid: peaking filter at 1000 Hz
            // Treble: high-shelf filter at 4000 Hz

            for (int ch = 0; ch < audioData.Length; ch++)
            {
                float[] data = audioData[ch];

                // Simple approximation: split frequency bands and apply gain
                float[] lowPass = ApplySimpleLowPass(data, sampleRate, 250);
                float[] bandPass = ApplySimpleBandPass(data, sampleRate, 250, 4000);
                float[] highPass = ApplySimpleHighPass(data, sampleRate, 4000);

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = lowPass[i] * bassGain + bandPass[i] * midGain + highPass[i] * trebleGain;
                }
            }
        }

        private float[] ApplySimpleLowPass(float[] input, int sampleRate, double cutoffHz)
        {
            float[] output = new float[input.Length];
            float rc = (float)(1.0 / (2.0 * Math.PI * cutoffHz));
            float dt = 1.0f / sampleRate;
            float alpha = dt / (rc + dt);

            output[0] = input[0];
            for (int i = 1; i < input.Length; i++)
            {
                output[i] = output[i - 1] + alpha * (input[i] - output[i - 1]);
            }

            return output;
        }

        private float[] ApplySimpleHighPass(float[] input, int sampleRate, double cutoffHz)
        {
            float[] output = new float[input.Length];
            float rc = (float)(1.0 / (2.0 * Math.PI * cutoffHz));
            float dt = 1.0f / sampleRate;
            float alpha = rc / (rc + dt);

            output[0] = input[0];
            for (int i = 1; i < input.Length; i++)
            {
                output[i] = alpha * (output[i - 1] + input[i] - input[i - 1]);
            }

            return output;
        }

        private float[] ApplySimpleBandPass(float[] input, int sampleRate, double lowCutoffHz, double highCutoffHz)
        {
            float[] lowPassed = ApplySimpleLowPass(input, sampleRate, highCutoffHz);
            float[] bandPassed = ApplySimpleHighPass(lowPassed, sampleRate, lowCutoffHz);
            return bandPassed;
        }

        private void ApplyCompression(float[][] audioData, double ratio)
        {
            float threshold = 0.5f; // -6 dB threshold
            float ratioInv = (float)(1.0 / ratio);

            for (int ch = 0; ch < audioData.Length; ch++)
            {
                for (int i = 0; i < audioData[ch].Length; i++)
                {
                    float sample = audioData[ch][i];
                    float abs = Math.Abs(sample);

                    if (abs > threshold)
                    {
                        float over = abs - threshold;
                        float compressed = threshold + over * ratioInv;
                        audioData[ch][i] = Math.Sign(sample) * compressed;
                    }
                }
            }
        }

        private void ApplyReverb(float[][] audioData, int sampleRate, double amount)
        {
            // Simple reverb using comb filters
            int[] delays = new int[]
            {
                (int)(0.037 * sampleRate), // 37ms
                (int)(0.041 * sampleRate), // 41ms
                (int)(0.043 * sampleRate), // 43ms
                (int)(0.047 * sampleRate)  // 47ms
            };

            float feedback = 0.5f;
            float wet = (float)amount;
            float dry = 1.0f - wet;

            for (int ch = 0; ch < audioData.Length; ch++)
            {
                float[] data = audioData[ch];
                float[] output = new float[data.Length];

                float[][] delayBuffers = new float[delays.Length][];
                for (int d = 0; d < delays.Length; d++)
                {
                    delayBuffers[d] = new float[delays[d]];
                }

                for (int i = 0; i < data.Length; i++)
                {
                    float reverbSum = 0;

                    for (int d = 0; d < delays.Length; d++)
                    {
                        int delayPos = i % delays[d];
                        float delayed = delayBuffers[d][delayPos];
                        reverbSum += delayed;

                        delayBuffers[d][delayPos] = data[i] + delayed * feedback;
                    }

                    output[i] = data[i] * dry + (reverbSum / delays.Length) * wet;
                }

                audioData[ch] = output;
            }
        }

        private void ApplyGain(float[][] audioData, double gainDb)
        {
            float gain = (float)Math.Pow(10, gainDb / 20.0);

            for (int ch = 0; ch < audioData.Length; ch++)
            {
                for (int i = 0; i < audioData[ch].Length; i++)
                {
                    audioData[ch][i] *= gain;
                }
            }
        }

        private void NormalizeAudio(float[][] audioData)
        {
            float maxAbs = 0;

            for (int ch = 0; ch < audioData.Length; ch++)
            {
                for (int i = 0; i < audioData[ch].Length; i++)
                {
                    float abs = Math.Abs(audioData[ch][i]);
                    if (abs > maxAbs) maxAbs = abs;
                }
            }

            if (maxAbs > 0.001f)
            {
                float gain = 0.95f / maxAbs; // Normalize to 95% to avoid clipping

                for (int ch = 0; ch < audioData.Length; ch++)
                {
                    for (int i = 0; i < audioData[ch].Length; i++)
                    {
                        audioData[ch][i] *= gain;
                    }
                }
            }
        }
    }
}
