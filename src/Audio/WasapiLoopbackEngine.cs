using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ErAudioTool.Audio
{
    public enum RecordingState
    {
        Idle,
        Recording,
        Paused,
        Stopping
    }

    public class PeakEventArgs : EventArgs
    {
        public float Left { get; private set; }
        public float Right { get; private set; }
        public float Master { get; private set; }

        public PeakEventArgs(float left, float right, float master)
        {
            Left = left;
            Right = right;
            Master = master;
        }
    }

    public class StatsEventArgs : EventArgs
    {
        public TimeSpan Elapsed { get; private set; }
        public long BytesWritten { get; private set; }
        public int TotalFrames { get; private set; }

        public StatsEventArgs(TimeSpan elapsed, long bytesWritten, int totalFrames)
        {
            Elapsed = elapsed;
            BytesWritten = bytesWritten;
            TotalFrames = totalFrames;
        }
    }

    public class FinishedEventArgs : EventArgs
    {
        public string FilePath { get; private set; }
        public TimeSpan Duration { get; private set; }
        public long FileSizeBytes { get; private set; }

        public FinishedEventArgs(string filePath, TimeSpan duration, long fileSizeBytes)
        {
            FilePath = filePath;
            Duration = duration;
            FileSizeBytes = fileSizeBytes;
        }
    }

    public class WasapiLoopbackEngine : IDisposable
    {
        private readonly object _lock = new object();
        private RecordingState _state = RecordingState.Idle;

        private AudioDeviceInfo _selectedDevice;
        private WavOutputFormat _outputFormat = WavOutputFormat.Pcm16;
        private string _outputPath;

        private Thread _captureThread;
        private Thread _meterThread;
        private volatile bool _stopCapture;
        private volatile bool _stopMeter;
        private volatile bool _isPaused;

        private WavWriter _writer;
        private Stopwatch _recordStopwatch;
        private TimeSpan _accumulatedTime;
        private int _totalFrames;

        public event EventHandler<PeakEventArgs> PeakUpdated;
        public event EventHandler<StatsEventArgs> StatsUpdated;
        public event EventHandler<FinishedEventArgs> RecordingFinished;
        public event EventHandler<RecordingState> StateChanged;
        public event EventHandler<Exception> ErrorOccurred;

        public RecordingState State { get { return _state; } }
        public AudioDeviceInfo CurrentDevice { get { return _selectedDevice; } }
        public WavOutputFormat OutputFormat { get { return _outputFormat; } set { _outputFormat = value; } }

        public WasapiLoopbackEngine()
        {
            StartMeterThread();
        }

        public void SetDevice(AudioDeviceInfo device)
        {
            lock (_lock)
            {
                if (_state == RecordingState.Recording || _state == RecordingState.Paused)
                {
                    throw new InvalidOperationException("Audiogerät kann während der Aufnahme nicht geändert werden.");
                }
                _selectedDevice = device;
            }
        }

        public void StartRecording(AudioDeviceInfo device, string outputPath, WavOutputFormat format)
        {
            lock (_lock)
            {
                if (_state != RecordingState.Idle)
                {
                    throw new InvalidOperationException("Eine Aufnahme läuft bereits.");
                }

                _selectedDevice = device ?? AudioDeviceEnumerator.GetDefaultRenderDevice();
                if (_selectedDevice == null)
                {
                    throw new InvalidOperationException("Kein aktives Wiedergabegerät gefunden.");
                }

                _outputPath = outputPath;
                _outputFormat = format;
                _stopCapture = false;
                _isPaused = false;
                _totalFrames = 0;
                _accumulatedTime = TimeSpan.Zero;
                _recordStopwatch = new Stopwatch();

                SetState(RecordingState.Recording);

                _captureThread = new Thread(CaptureLoop)
                {
                    Name = "WASAPI-CaptureThread",
                    Priority = ThreadPriority.AboveNormal,
                    IsBackground = true
                };
                _captureThread.Start();
            }
        }

        public void PauseRecording()
        {
            lock (_lock)
            {
                if (_state == RecordingState.Recording)
                {
                    _isPaused = true;
                    if (_recordStopwatch != null && _recordStopwatch.IsRunning)
                    {
                        _recordStopwatch.Stop();
                        _accumulatedTime += _recordStopwatch.Elapsed;
                        _recordStopwatch.Reset();
                    }
                    SetState(RecordingState.Paused);
                }
            }
        }

        public void ResumeRecording()
        {
            lock (_lock)
            {
                if (_state == RecordingState.Paused)
                {
                    _isPaused = false;
                    if (_recordStopwatch != null)
                    {
                        _recordStopwatch.Start();
                    }
                    SetState(RecordingState.Recording);
                }
            }
        }

        public void StopRecording()
        {
            lock (_lock)
            {
                if (_state == RecordingState.Idle || _state == RecordingState.Stopping)
                {
                    return;
                }

                SetState(RecordingState.Stopping);
                _stopCapture = true;
            }

            if (_captureThread != null && _captureThread.IsAlive)
            {
                _captureThread.Join(3000);
            }

            lock (_lock)
            {
                SetState(RecordingState.Idle);
            }
        }

        private void SetState(RecordingState newState)
        {
            _state = newState;
            var handler = StateChanged;
            if (handler != null)
            {
                try { handler(this, newState); } catch { }
            }
        }

        private void CaptureLoop()
        {
            IMMDevice device = null;
            IAudioClient captureAudioClient = null;
            IAudioCaptureClient captureClient = null;
            IAudioClient silenceAudioClient = null;
            IAudioRenderClient silenceRenderClient = null;
            IntPtr pFormat = IntPtr.Zero;

            string finalFile = _outputPath;
            TimeSpan duration = TimeSpan.Zero;
            long fileLength = 0;

            try
            {
                device = AudioDeviceEnumerator.ActivateDevice(_selectedDevice != null ? _selectedDevice.Id : null);
                if (device == null)
                {
                    throw new Exception("Konnte das ausgewählte Audiogerät nicht aktivieren.");
                }

                // 1. Initialize Loopback Audio Client
                object objClient;
                Guid iidClient = WasapiGuids.IID_IAudioClient;
                int hr = device.Activate(ref iidClient, 1, IntPtr.Zero, out objClient);
                if (hr != 0 || objClient == null)
                {
                    throw new Exception("Fehler beim Aktivieren von IAudioClient (0x" + hr.ToString("X8") + ")");
                }
                captureAudioClient = (IAudioClient)objClient;

                hr = captureAudioClient.GetMixFormat(out pFormat);
                if (hr != 0 || pFormat == IntPtr.Zero)
                {
                    throw new Exception("MixFormat konnte nicht ermittelt werden (0x" + hr.ToString("X8") + ")");
                }
                var fmt = (WAVEFORMATEX)Marshal.PtrToStructure(pFormat, typeof(WAVEFORMATEX));

                // 2. Setup Silence Keeper (plays silent buffer in shared mode to ensure loopback receives continuous clock ticks)
                try
                {
                    object objSilence;
                    if (device.Activate(ref iidClient, 1, IntPtr.Zero, out objSilence) == 0 && objSilence != null)
                    {
                        silenceAudioClient = (IAudioClient)objSilence;
                        Guid empty = Guid.Empty;
                        if (silenceAudioClient.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.None, 20000000, 0, pFormat, ref empty) == 0)
                        {
                            object objRender;
                            Guid iidRender = WasapiGuids.IID_IAudioRenderClient;
                            if (silenceAudioClient.GetService(ref iidRender, out objRender) == 0 && objRender != null)
                            {
                                silenceRenderClient = (IAudioRenderClient)objRender;
                                uint bufferSize;
                                silenceAudioClient.GetBufferSize(out bufferSize);
                                IntPtr buf;
                                if (silenceRenderClient.GetBuffer(bufferSize, out buf) == 0)
                                {
                                    byte[] zeros = new byte[bufferSize * fmt.nBlockAlign];
                                    Marshal.Copy(zeros, 0, buf, zeros.Length);
                                    silenceRenderClient.ReleaseBuffer(bufferSize, AudioClientBufferFlags.Silent);
                                }
                                silenceAudioClient.Start();
                            }
                        }
                    }
                }
                catch { }

                // 3. Initialize Loopback
                Guid emptyGuid = Guid.Empty;
                hr = captureAudioClient.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.Loopback, 20000000, 0, pFormat, ref emptyGuid);
                if (hr != 0)
                {
                    throw new Exception("IAudioClient Loopback Initialisierung fehlgeschlagen (0x" + hr.ToString("X8") + ")");
                }

                object objCapture;
                Guid iidCapture = WasapiGuids.IID_IAudioCaptureClient;
                hr = captureAudioClient.GetService(ref iidCapture, out objCapture);
                if (hr != 0 || objCapture == null)
                {
                    throw new Exception("IAudioCaptureClient konnte nicht bezogen werden (0x" + hr.ToString("X8") + ")");
                }
                captureClient = (IAudioCaptureClient)objCapture;

                // 4. Create WAV Writer
                _writer = new WavWriter(finalFile, (int)fmt.nSamplesPerSec, fmt.nChannels, _outputFormat);
                _recordStopwatch.Start();

                hr = captureAudioClient.Start();
                if (hr != 0)
                {
                    throw new Exception("CaptureClient konnte nicht gestartet werden (0x" + hr.ToString("X8") + ")");
                }

                float[] floatBuffer = new float[8192 * fmt.nChannels];
                DateTime lastStatsTime = DateTime.UtcNow;

                float maxLeft = 0f;
                float maxRight = 0f;

                // 5. Active Capture Loop
                while (!_stopCapture)
                {
                    uint packetSize;
                    hr = captureClient.GetNextPacketSize(out packetSize);
                    if (hr != 0)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    while (packetSize > 0 && !_stopCapture)
                    {
                        IntPtr pData;
                        uint numFramesToRead;
                        AudioClientBufferFlags flags;
                        ulong devPos, qpcPos;

                        hr = captureClient.GetBuffer(out pData, out numFramesToRead, out flags, out devPos, out qpcPos);
                        if (hr == 0 && numFramesToRead > 0)
                        {
                            int totalSamples = (int)(numFramesToRead * fmt.nChannels);
                            if (floatBuffer.Length < totalSamples)
                            {
                                floatBuffer = new float[totalSamples];
                            }

                            bool isSilent = (flags & AudioClientBufferFlags.Silent) != 0;

                            if (isSilent)
                            {
                                Array.Clear(floatBuffer, 0, totalSamples);
                            }
                            else
                            {
                                Marshal.Copy(pData, floatBuffer, 0, totalSamples);

                                // Compute live peak from recorded audio samples
                                for (int i = 0; i < totalSamples; i += fmt.nChannels)
                                {
                                    float l = Math.Abs(floatBuffer[i]);
                                    if (l > maxLeft) maxLeft = l;
                                    if (fmt.nChannels > 1)
                                    {
                                        float r = Math.Abs(floatBuffer[i + 1]);
                                        if (r > maxRight) maxRight = r;
                                    }
                                    else
                                    {
                                        maxRight = maxLeft;
                                    }
                                }
                            }

                            // Write to WAV if not paused
                            if (!_isPaused)
                            {
                                if (isSilent)
                                {
                                    _writer.WriteSilence((int)numFramesToRead);
                                }
                                else
                                {
                                    _writer.WriteSamples(floatBuffer, totalSamples);
                                }
                                _totalFrames += (int)numFramesToRead;
                            }

                            captureClient.ReleaseBuffer(numFramesToRead);
                        }

                        captureClient.GetNextPacketSize(out packetSize);
                    }

                    // Periodic stats and peak update (every 50ms)
                    if ((DateTime.UtcNow - lastStatsTime).TotalMilliseconds >= 50)
                    {
                        lastStatsTime = DateTime.UtcNow;
                        TimeSpan currentElapsed = _accumulatedTime + (_recordStopwatch.IsRunning ? _recordStopwatch.Elapsed : TimeSpan.Zero);
                        long currentBytes = _writer != null ? _writer.TotalDataBytes : 0;

                        var peakHandler = PeakUpdated;
                        if (peakHandler != null)
                        {
                            float master = Math.Max(maxLeft, maxRight);
                            try { peakHandler(this, new PeakEventArgs(maxLeft, maxRight, master)); } catch { }
                        }

                        // Decay peak values
                        maxLeft = 0f;
                        maxRight = 0f;

                        var statsHandler = StatsUpdated;
                        if (statsHandler != null)
                        {
                            try { statsHandler(this, new StatsEventArgs(currentElapsed, currentBytes, _totalFrames)); } catch { }
                        }
                    }

                    Thread.Sleep(8);
                }

                // 6. Finalize recording
                if (_recordStopwatch != null)
                {
                    _recordStopwatch.Stop();
                    duration = _accumulatedTime + _recordStopwatch.Elapsed;
                }

                if (_writer != null)
                {
                    _writer.Dispose();
                    _writer = null;
                }

                try
                {
                    var fi = new FileInfo(finalFile);
                    fileLength = fi.Exists ? fi.Length : 0;
                }
                catch { }

                var finHandler = RecordingFinished;
                if (finHandler != null)
                {
                    try { finHandler(this, new FinishedEventArgs(finalFile, duration, fileLength)); } catch { }
                }
            }
            catch (Exception ex)
            {
                var errHandler = ErrorOccurred;
                if (errHandler != null)
                {
                    try { errHandler(this, ex); } catch { }
                }
            }
            finally
            {
                if (captureAudioClient != null)
                {
                    try { captureAudioClient.Stop(); } catch { }
                }
                if (silenceAudioClient != null)
                {
                    try { silenceAudioClient.Stop(); } catch { }
                }
                if (pFormat != IntPtr.Zero)
                {
                    try { Marshal.FreeCoTaskMem(pFormat); } catch { }
                }
                if (_writer != null)
                {
                    try { _writer.Dispose(); } catch { }
                    _writer = null;
                }
            }
        }

        private void StartMeterThread()
        {
            _stopMeter = false;
            _meterThread = new Thread(MeterLoop)
            {
                Name = "WASAPI-IdleMeterThread",
                Priority = ThreadPriority.Lowest,
                IsBackground = true
            };
            _meterThread.Start();
        }

        private void MeterLoop()
        {
            IMMDevice currentDev = null;
            IAudioMeterInformation meter = null;
            string lastDevId = null;

            while (!_stopMeter)
            {
                try
                {
                    // Only run idle meter when NOT actively recording (during recording, CaptureLoop handles peaks)
                    if (_state == RecordingState.Idle)
                    {
                        AudioDeviceInfo devInfo = _selectedDevice ?? AudioDeviceEnumerator.GetDefaultRenderDevice();
                        if (devInfo != null)
                        {
                            if (devInfo.Id != lastDevId || meter == null)
                            {
                                lastDevId = devInfo.Id;
                                currentDev = AudioDeviceEnumerator.ActivateDevice(devInfo.Id);
                                if (currentDev != null)
                                {
                                    object objMeter;
                                    Guid iidMeter = WasapiGuids.IID_IAudioMeterInformation;
                                    if (currentDev.Activate(ref iidMeter, 1, IntPtr.Zero, out objMeter) == 0 && objMeter != null)
                                    {
                                        meter = (IAudioMeterInformation)objMeter;
                                    }
                                }
                            }

                            if (meter != null)
                            {
                                float masterPeak = 0;
                                if (meter.GetPeakValue(out masterPeak) == 0)
                                {
                                    var handler = PeakUpdated;
                                    if (handler != null)
                                    {
                                        try { handler(this, new PeakEventArgs(masterPeak, masterPeak, masterPeak)); } catch { }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                Thread.Sleep(40); // 25 fps idle metering
            }
        }

        public void Dispose()
        {
            StopRecording();
            _stopMeter = true;
            if (_meterThread != null && _meterThread.IsAlive)
            {
                _meterThread.Join(500);
            }
        }
    }
}
