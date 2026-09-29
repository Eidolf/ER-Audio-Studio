using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ErAudioTool.Audio
{
    public class AudioSegment
    {
        public string Id { get; set; }
        public float[][] AudioData { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public double StartTime { get; set; }
        public double Duration { get; set; }
        public string Name { get; set; }

        public AudioSegment()
        {
            Id = Guid.NewGuid().ToString();
        }

        public AudioSegment Clone()
        {
            var clone = new AudioSegment
            {
                Id = Guid.NewGuid().ToString(),
                SampleRate = this.SampleRate,
                Channels = this.Channels,
                StartTime = this.StartTime,
                Duration = this.Duration,
                Name = this.Name
            };

            // Deep copy audio data
            clone.AudioData = new float[this.AudioData.Length][];
            for (int i = 0; i < this.AudioData.Length; i++)
            {
                clone.AudioData[i] = new float[this.AudioData[i].Length];
                Array.Copy(this.AudioData[i], clone.AudioData[i], this.AudioData[i].Length);
            }

            return clone;
        }
    }

    public class AudioEditorEngine
    {
        private List<AudioSegment> _segments = new List<AudioSegment>();
        private AudioSegment _clipboard = null;
        private int _sampleRate = 44100;
        private int _channels = 2;

        public List<AudioSegment> Segments
        {
            get { return _segments; }
        }

        public int SampleRate
        {
            get { return _sampleRate; }
        }

        public int Channels
        {
            get { return _channels; }
        }

        public bool HasClipboard
        {
            get { return _clipboard != null; }
        }

        public AudioSegment LoadAudioFile(string filePath, Action<string> onLog)
        {
            try
            {
                if (onLog != null) onLog("Lade Audiodatei: " + Path.GetFileName(filePath));

                float[][] audioData;
                int sampleRate;
                int channels;

                if (!ReadAudioFile(filePath, out audioData, out sampleRate, out channels, onLog))
                {
                    if (onLog != null) onLog("Fehler beim Laden der Datei");
                    return null;
                }

                _sampleRate = sampleRate;
                _channels = channels;

                double duration = audioData[0].Length / (double)sampleRate;

                var segment = new AudioSegment
                {
                    AudioData = audioData,
                    SampleRate = sampleRate,
                    Channels = channels,
                    StartTime = 0,
                    Duration = duration,
                    Name = Path.GetFileNameWithoutExtension(filePath)
                };

                _segments.Clear();
                _segments.Add(segment);

                if (onLog != null) onLog(string.Format("Geladen: {0} Kanäle, {1} Hz, {2:0.2}s", channels, sampleRate, duration));

                return segment;
            }
            catch (Exception ex)
            {
                if (onLog != null) onLog("Fehler: " + ex.Message);
                return null;
            }
        }

        public AudioSegment CutSegment(string segmentId, double startTime, double endTime, Action<string> onLog)
        {
            var segment = _segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment == null) return null;

            if (onLog != null) onLog(string.Format("Schneide Segment von {0:0.2}s bis {1:0.2}s", startTime, endTime));

            int startSample = (int)(startTime * segment.SampleRate);
            int endSample = (int)(endTime * segment.SampleRate);
            int length = endSample - startSample;

            if (startSample < 0 || endSample > segment.AudioData[0].Length || length <= 0)
            {
                if (onLog != null) onLog("Ungültige Zeitbereich");
                return null;
            }

            // Create new segment with cut portion
            var cutSegment = new AudioSegment
            {
                SampleRate = segment.SampleRate,
                Channels = segment.Channels,
                StartTime = startTime,
                Duration = (endTime - startTime),
                Name = segment.Name + "_cut"
            };

            cutSegment.AudioData = new float[segment.Channels][];
            for (int ch = 0; ch < segment.Channels; ch++)
            {
                cutSegment.AudioData[ch] = new float[length];
                Array.Copy(segment.AudioData[ch], startSample, cutSegment.AudioData[ch], 0, length);
            }

            return cutSegment;
        }

        public void SplitSegment(string segmentId, double splitTime, Action<string> onLog)
        {
            var segment = _segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment == null) return;

            if (onLog != null) onLog(string.Format("Teile Segment bei {0:0.2}s", splitTime));

            int splitSample = (int)(splitTime * segment.SampleRate);

            if (splitSample <= 0 || splitSample >= segment.AudioData[0].Length)
            {
                if (onLog != null) onLog("Ungültige Teilungsposition");
                return;
            }

            // Create first part
            var part1 = new AudioSegment
            {
                SampleRate = segment.SampleRate,
                Channels = segment.Channels,
                StartTime = segment.StartTime,
                Duration = splitTime,
                Name = segment.Name + "_part1"
            };

            part1.AudioData = new float[segment.Channels][];
            for (int ch = 0; ch < segment.Channels; ch++)
            {
                part1.AudioData[ch] = new float[splitSample];
                Array.Copy(segment.AudioData[ch], 0, part1.AudioData[ch], 0, splitSample);
            }

            // Create second part
            int part2Length = segment.AudioData[0].Length - splitSample;
            var part2 = new AudioSegment
            {
                SampleRate = segment.SampleRate,
                Channels = segment.Channels,
                StartTime = segment.StartTime + splitTime,
                Duration = segment.Duration - splitTime,
                Name = segment.Name + "_part2"
            };

            part2.AudioData = new float[segment.Channels][];
            for (int ch = 0; ch < segment.Channels; ch++)
            {
                part2.AudioData[ch] = new float[part2Length];
                Array.Copy(segment.AudioData[ch], splitSample, part2.AudioData[ch], 0, part2Length);
            }

            // Replace original with two parts
            int index = _segments.IndexOf(segment);
            _segments.RemoveAt(index);
            _segments.Insert(index, part1);
            _segments.Insert(index + 1, part2);

            if (onLog != null) onLog("Segment erfolgreich geteilt");
        }

        public void DeleteSegment(string segmentId, Action<string> onLog)
        {
            var segment = _segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment == null) return;

            _segments.Remove(segment);
            if (onLog != null) onLog("Segment gelöscht: " + segment.Name);
        }

        public void CopyToClipboard(string segmentId, Action<string> onLog)
        {
            var segment = _segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment == null) return;

            _clipboard = segment.Clone();
            if (onLog != null) onLog("In Zwischenspeicher kopiert: " + segment.Name);
        }

        public void CutToClipboard(string segmentId, Action<string> onLog)
        {
            var segment = _segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment == null) return;

            _clipboard = segment.Clone();
            _segments.Remove(segment);
            if (onLog != null) onLog("Ausgeschnitten in Zwischenspeicher: " + segment.Name);
        }

        public void PasteFromClipboard(int insertIndex, Action<string> onLog)
        {
            if (_clipboard == null)
            {
                if (onLog != null) onLog("Zwischenspeicher ist leer");
                return;
            }

            var newSegment = _clipboard.Clone();

            // Calculate start time based on position
            if (insertIndex > 0 && insertIndex <= _segments.Count)
            {
                var prevSegment = _segments[insertIndex - 1];
                newSegment.StartTime = prevSegment.StartTime + prevSegment.Duration;
            }
            else if (insertIndex == 0)
            {
                newSegment.StartTime = 0;
            }

            if (insertIndex >= _segments.Count)
            {
                _segments.Add(newSegment);
            }
            else
            {
                _segments.Insert(insertIndex, newSegment);
            }

            if (onLog != null) onLog("Aus Zwischenspeicher eingefügt");
        }

        public void MoveSegment(string segmentId, int newIndex, Action<string> onLog)
        {
            var segment = _segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment == null) return;

            int oldIndex = _segments.IndexOf(segment);
            if (oldIndex == newIndex) return;

            _segments.RemoveAt(oldIndex);

            if (newIndex > oldIndex)
            {
                newIndex--;
            }

            if (newIndex >= _segments.Count)
            {
                _segments.Add(segment);
            }
            else
            {
                _segments.Insert(newIndex, segment);
            }

            if (onLog != null) onLog(string.Format("Segment verschoben: {0} -> {1}", oldIndex, newIndex));
        }

        public bool ExportMerged(string outputPath, Action<string> onLog)
        {
            try
            {
                if (_segments.Count == 0)
                {
                    if (onLog != null) onLog("Keine Segmente zum Exportieren");
                    return false;
                }

                if (onLog != null) onLog("Exportiere zusammengefügtes Audio...");

                // Calculate total length
                int totalSamples = 0;
                foreach (var segment in _segments)
                {
                    totalSamples += segment.AudioData[0].Length;
                }

                // Merge all segments
                float[][] mergedData = new float[_channels][];
                for (int ch = 0; ch < _channels; ch++)
                {
                    mergedData[ch] = new float[totalSamples];
                }

                int position = 0;
                foreach (var segment in _segments)
                {
                    int segmentLength = segment.AudioData[0].Length;
                    for (int ch = 0; ch < _channels; ch++)
                    {
                        Array.Copy(segment.AudioData[ch], 0, mergedData[ch], position, segmentLength);
                    }
                    position += segmentLength;
                }

                // Write to file
                if (!WriteWavFile(outputPath, mergedData, _sampleRate, _channels))
                {
                    if (onLog != null) onLog("Fehler beim Schreiben der Datei");
                    return false;
                }

                if (onLog != null) onLog("Export erfolgreich: " + Path.GetFileName(outputPath));
                return true;
            }
            catch (Exception ex)
            {
                if (onLog != null) onLog("Fehler beim Export: " + ex.Message);
                return false;
            }
        }

        public float[] GetWaveformData(AudioSegment segment, int width)
        {
            if (segment == null || segment.AudioData == null || segment.AudioData.Length == 0)
            {
                return new float[width];
            }

            float[] waveform = new float[width];
            int totalSamples = segment.AudioData[0].Length;
            int samplesPerPixel = Math.Max(1, totalSamples / width);

            for (int i = 0; i < width; i++)
            {
                int startSample = i * samplesPerPixel;
                int endSample = Math.Min(startSample + samplesPerPixel, totalSamples);

                float max = 0;
                for (int s = startSample; s < endSample; s++)
                {
                    // Use average of all channels
                    float avg = 0;
                    for (int ch = 0; ch < segment.Channels; ch++)
                    {
                        avg += Math.Abs(segment.AudioData[ch][s]);
                    }
                    avg /= segment.Channels;

                    if (avg > max)
                    {
                        max = avg;
                    }
                }

                waveform[i] = max;
            }

            return waveform;
        }

        private bool ReadAudioFile(string path, out float[][] audioData, out int sampleRate, out int channels, Action<string> onLog)
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
                    // Convert to WAV first
                    if (onLog != null) onLog("Konvertiere zu WAV...");
                    string tempWav = Path.GetTempFileName() + ".wav";

                    if (AudioConverterService.ConvertAudio(path, tempWav, "wav", 0, onLog))
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
                    var riff = new string(br.ReadChars(4));
                    if (riff != "RIFF") return false;

                    br.ReadInt32();
                    var wave = new string(br.ReadChars(4));
                    if (wave != "WAVE") return false;

                    while (fs.Position < fs.Length)
                    {
                        var chunkId = new string(br.ReadChars(4));
                        var chunkSize = br.ReadInt32();

                        if (chunkId == "fmt ")
                        {
                            var audioFormat = br.ReadInt16();
                            channels = br.ReadInt16();
                            sampleRate = br.ReadInt32();
                            br.ReadInt32();
                            br.ReadInt16();
                            var bitsPerSample = br.ReadInt16();

                            if (chunkSize > 16)
                            {
                                br.ReadBytes(chunkSize - 16);
                            }
                        }
                        else if (chunkId == "data")
                        {
                            int sampleCount = chunkSize / (channels * 2);
                            audioData = new float[channels][];
                            for (int i = 0; i < channels; i++)
                            {
                                audioData[i] = new float[sampleCount];
                            }

                            for (int i = 0; i < sampleCount; i++)
                            {
                                for (int ch = 0; ch < channels; ch++)
                                {
                                    short sample = br.ReadInt16();
                                    audioData[ch][i] = sample / 32768.0f;
                                }
                            }

                            return true;
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

        private bool WriteWavFile(string path, float[][] audioData, int sampleRate, int channels)
        {
            try
            {
                using (var fs = File.Create(path))
                using (var bw = new BinaryWriter(fs))
                {
                    int sampleCount = audioData[0].Length;
                    int dataSize = sampleCount * channels * 2;
                    int fileSize = 36 + dataSize;

                    bw.Write("RIFF".ToCharArray());
                    bw.Write(fileSize);
                    bw.Write("WAVE".ToCharArray());

                    bw.Write("fmt ".ToCharArray());
                    bw.Write(16);
                    bw.Write((short)1);
                    bw.Write((short)channels);
                    bw.Write(sampleRate);
                    bw.Write(sampleRate * channels * 2);
                    bw.Write((short)(channels * 2));
                    bw.Write((short)16);

                    bw.Write("data".ToCharArray());
                    bw.Write(dataSize);

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
    }
}
