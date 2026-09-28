using System;
using System.IO;
using System.Windows.Media;

namespace ErAudioTool.Audio
{
    public class SimpleAudioPlayer : IDisposable
    {
        private MediaPlayer _mediaPlayer;
        private string _currentFile;
        private bool _isPlaying;

        public event EventHandler PlaybackStarted;
        public event EventHandler PlaybackStopped;

        public bool IsPlaying { get { return _isPlaying; } }
        public string CurrentFile { get { return _currentFile; } }

        public SimpleAudioPlayer()
        {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            _isPlaying = false;
            var handler = PlaybackStopped;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void MediaPlayer_MediaFailed(object sender, ExceptionEventArgs e)
        {
            _isPlaying = false;
            var handler = PlaybackStopped;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Play(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            Stop();

            _currentFile = filePath;
            _mediaPlayer.Open(new Uri(filePath));
            _mediaPlayer.Play();
            _isPlaying = true;

            var handler = PlaybackStarted;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Stop()
        {
            if (_isPlaying)
            {
                try { _mediaPlayer.Stop(); } catch { }
                _isPlaying = false;
                var handler = PlaybackStopped;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Stop();
            try { _mediaPlayer.Close(); } catch { }
        }
    }
}
