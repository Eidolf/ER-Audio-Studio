using System;
using System.IO;

namespace ErAudioTool.Audio
{
    public enum WavOutputFormat
    {
        Pcm16,
        Float32
    }

    public class WavWriter : IDisposable
    {
        private readonly string _filePath;
        private readonly WavOutputFormat _format;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _bitsPerSample;
        private readonly int _blockAlign;
        private readonly int _avgBytesPerSec;

        private FileStream _fileStream;
        private BinaryWriter _writer;
        private long _dataChunkPos;
        private long _totalDataBytes;
        private bool _isDisposed;

        private byte[] _byteBuffer;

        public string FilePath { get { return _filePath; } }
        public WavOutputFormat Format { get { return _format; } }
        public int SampleRate { get { return _sampleRate; } }
        public int Channels { get { return _channels; } }
        public long TotalDataBytes { get { return _totalDataBytes; } }
        public TimeSpan Duration
        {
            get
            {
                if (_avgBytesPerSec <= 0) return TimeSpan.Zero;
                double seconds = (double)_totalDataBytes / _avgBytesPerSec;
                return TimeSpan.FromSeconds(seconds);
            }
        }

        public WavWriter(string filePath, int sampleRate, int channels, WavOutputFormat format)
        {
            _filePath = filePath;
            _sampleRate = sampleRate;
            _channels = channels;
            _format = format;

            _bitsPerSample = format == WavOutputFormat.Pcm16 ? 16 : 32;
            _blockAlign = _channels * (_bitsPerSample / 8);
            _avgBytesPerSec = _sampleRate * _blockAlign;

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            _writer = new BinaryWriter(_fileStream);
            _byteBuffer = new byte[8192];

            WriteHeaderPlaceholder();
        }

        private void WriteHeaderPlaceholder()
        {
            // RIFF header
            _writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            _writer.Write(0); // placeholder for RIFF size
            _writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            // fmt chunk
            _writer.Write(new char[] { 'f', 'm', 't', ' ' });
            if (_format == WavOutputFormat.Pcm16)
            {
                _writer.Write(16); // Subchunk1Size
                _writer.Write((short)1); // 1 = PCM
                _writer.Write((short)_channels);
                _writer.Write(_sampleRate);
                _writer.Write(_avgBytesPerSec);
                _writer.Write((short)_blockAlign);
                _writer.Write((short)_bitsPerSample);
            }
            else
            {
                // IEEE Float
                _writer.Write(18); // Subchunk1Size with cbSize = 0
                _writer.Write((short)3); // 3 = IEEE Float
                _writer.Write((short)_channels);
                _writer.Write(_sampleRate);
                _writer.Write(_avgBytesPerSec);
                _writer.Write((short)_blockAlign);
                _writer.Write((short)_bitsPerSample);
                _writer.Write((short)0); // cbSize
            }

            // data chunk header
            _writer.Write(new char[] { 'd', 'a', 't', 'a' });
            _dataChunkPos = _fileStream.Position;
            _writer.Write(0); // placeholder for data size
        }

        public void WriteSamples(float[] floatSamples, int count)
        {
            if (_isDisposed || count <= 0) return;

            if (_format == WavOutputFormat.Pcm16)
            {
                int bytesNeeded = count * 2;
                if (_byteBuffer.Length < bytesNeeded)
                {
                    _byteBuffer = new byte[bytesNeeded];
                }

                for (int i = 0; i < count; i++)
                {
                    float f = floatSamples[i];
                    if (f > 1.0f) f = 1.0f;
                    else if (f < -1.0f) f = -1.0f;

                    short s = (short)(f * 32767.0f);
                    _byteBuffer[i * 2] = (byte)(s & 0xFF);
                    _byteBuffer[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                }

                _writer.Write(_byteBuffer, 0, bytesNeeded);
                _totalDataBytes += bytesNeeded;
            }
            else
            {
                int bytesNeeded = count * 4;
                if (_byteBuffer.Length < bytesNeeded)
                {
                    _byteBuffer = new byte[bytesNeeded];
                }

                Buffer.BlockCopy(floatSamples, 0, _byteBuffer, 0, bytesNeeded);
                _writer.Write(_byteBuffer, 0, bytesNeeded);
                _totalDataBytes += bytesNeeded;
            }
        }

        public void WriteSilence(int frameCount)
        {
            if (_isDisposed || frameCount <= 0) return;
            int bytesNeeded = frameCount * _blockAlign;
            if (_byteBuffer.Length < bytesNeeded)
            {
                _byteBuffer = new byte[bytesNeeded];
            }
            Array.Clear(_byteBuffer, 0, bytesNeeded);
            _writer.Write(_byteBuffer, 0, bytesNeeded);
            _totalDataBytes += bytesNeeded;
        }

        public void FinalizeHeader()
        {
            if (_fileStream == null || !_fileStream.CanSeek) return;

            try
            {
                _writer.Flush();

                // Update data chunk size
                _fileStream.Seek(_dataChunkPos, SeekOrigin.Begin);
                _writer.Write((uint)_totalDataBytes);

                // Update RIFF chunk size (file length - 8 bytes)
                long riffSize = _fileStream.Length - 8;
                _fileStream.Seek(4, SeekOrigin.Begin);
                _writer.Write((uint)riffSize);

                _writer.Flush();
            }
            catch { }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                FinalizeHeader();
            }
            catch { }

            try
            {
                if (_writer != null) { _writer.Close(); _writer = null; }
                if (_fileStream != null) { _fileStream.Close(); _fileStream = null; }
            }
            catch { }
        }

        public static void WriteWav(string filePath, float[] samples, int sampleRate, int channels, WavOutputFormat format)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException("filePath");
            if (samples == null) throw new ArgumentNullException("samples");

            using (var writer = new WavWriter(filePath, sampleRate, channels, format))
            {
                writer.WriteSamples(samples, samples.Length);
                writer.FinalizeHeader();
            }
        }
    }
}
