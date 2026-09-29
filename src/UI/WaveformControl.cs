using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ErAudioTool.UI
{
    public class WaveformControl : Canvas
    {
        private float[] _waveformData;
        private double _selectionStart = -1;
        private double _selectionEnd = -1;
        private bool _isSelecting = false;
        private Line _cursorLine;
        private Rectangle _selectionRect;
        private Line _playbackCursor;
        private double _playbackPosition = -1;
        private DispatcherTimer _playbackTimer;
        private DateTime _playbackStartTime;
        private double _totalDuration = 0;

        public event EventHandler<WaveformSelectionEventArgs> SelectionChanged;

        public WaveformControl()
        {
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            ClipToBounds = true;

            _cursorLine = new Line
            {
                Stroke = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed
            };
            Children.Add(_cursorLine);

            _selectionRect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(60, 59, 130, 246)),
                Stroke = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed
            };
            Children.Add(_selectionRect);

            _playbackCursor = new Line
            {
                Stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed
            };
            Children.Add(_playbackCursor);

            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            _playbackTimer.Tick += PlaybackTimer_Tick;

            MouseMove += OnMouseMove;
            MouseDown += OnMouseDown;
            MouseUp += OnMouseUp;
            MouseLeave += OnMouseLeave;
        }

        public void SetWaveformData(float[] data)
        {
            _waveformData = data;
            InvalidateVisual();
        }

        public void SetDuration(double durationSeconds)
        {
            _totalDuration = durationSeconds;
            InvalidateVisual();
        }

        public void StartPlaybackCursor(double selectionStart = 0)
        {
            _playbackPosition = selectionStart;
            _playbackStartTime = DateTime.Now;
            _playbackCursor.Visibility = Visibility.Visible;
            _playbackTimer.Start();
            UpdatePlaybackCursor();
        }

        public void StopPlaybackCursor()
        {
            _playbackTimer.Stop();
            _playbackCursor.Visibility = Visibility.Collapsed;
            _playbackPosition = -1;
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_playbackPosition >= 0)
            {
                double elapsed = (DateTime.Now - _playbackStartTime).TotalSeconds;

                if (HasSelection)
                {
                    double selectionDuration = (_selectionEnd - _selectionStart) * _totalDuration;
                    _playbackPosition = _selectionStart + (elapsed / _totalDuration);

                    if (_playbackPosition >= _selectionEnd)
                    {
                        StopPlaybackCursor();
                    }
                }
                else
                {
                    _playbackPosition = elapsed / _totalDuration;

                    if (_playbackPosition >= 1.0)
                    {
                        StopPlaybackCursor();
                    }
                }

                UpdatePlaybackCursor();
            }
        }

        private void UpdatePlaybackCursor()
        {
            if (_playbackPosition >= 0 && ActualWidth > 0)
            {
                double x = _playbackPosition * ActualWidth;
                _playbackCursor.X1 = x;
                _playbackCursor.X2 = x;
                _playbackCursor.Y1 = 0;
                _playbackCursor.Y2 = ActualHeight;
            }
        }

        public void ClearSelection()
        {
            _selectionStart = -1;
            _selectionEnd = -1;
            _selectionRect.Visibility = Visibility.Collapsed;
            InvalidateVisual();
        }

        public double SelectionStartTime
        {
            get { return _selectionStart; }
        }

        public double SelectionEndTime
        {
            get { return _selectionEnd; }
        }

        public bool HasSelection
        {
            get { return _selectionStart >= 0 && _selectionEnd >= 0 && _selectionStart != _selectionEnd; }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_waveformData == null || _waveformData.Length == 0 || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            double width = ActualWidth;
            double height = ActualHeight;
            double centerY = height / 2;

            // Reserve space for time scale at bottom
            double waveformHeight = height - 25;
            double waveformCenterY = waveformHeight / 2;

            // Draw center line
            dc.DrawLine(
                new Pen(new SolidColorBrush(Color.FromRgb(51, 65, 85)), 1),
                new Point(0, waveformCenterY),
                new Point(width, waveformCenterY)
            );

            // Draw waveform
            var waveformPen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.5);

            for (int i = 0; i < _waveformData.Length - 1; i++)
            {
                double x1 = (i / (double)_waveformData.Length) * width;
                double x2 = ((i + 1) / (double)_waveformData.Length) * width;

                double y1 = waveformCenterY - (_waveformData[i] * waveformCenterY * 0.9);
                double y2 = waveformCenterY - (_waveformData[i + 1] * waveformCenterY * 0.9);

                dc.DrawLine(waveformPen, new Point(x1, y1), new Point(x2, y2));
            }

            // Draw time scale
            if (_totalDuration > 0)
            {
                double timeScaleY = height - 20;

                // Draw time scale line
                dc.DrawLine(
                    new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1),
                    new Point(0, timeScaleY),
                    new Point(width, timeScaleY)
                );

                // Draw time markers
                int numMarkers = 10;
                var textBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                var typeface = new Typeface("Segoe UI");

                for (int i = 0; i <= numMarkers; i++)
                {
                    double x = (i / (double)numMarkers) * width;
                    double time = (i / (double)numMarkers) * _totalDuration;

                    // Draw tick
                    dc.DrawLine(
                        new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1),
                        new Point(x, timeScaleY),
                        new Point(x, timeScaleY + 5)
                    );

                    // Draw time text
                    string timeText = FormatTime(time);
                    var formattedText = new FormattedText(
                        timeText,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        9,
                        textBrush
                    );

                    dc.DrawText(formattedText, new Point(x - formattedText.Width / 2, timeScaleY + 6));
                }
            }
        }

        private string FormatTime(double seconds)
        {
            if (seconds < 60)
            {
                return string.Format("{0:0.0}s", seconds);
            }
            else
            {
                int minutes = (int)(seconds / 60);
                double secs = seconds % 60;
                return string.Format("{0}:{1:00.0}", minutes, secs);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (ActualWidth <= 0) return;

            Point pos = e.GetPosition(this);

            // Update cursor line
            _cursorLine.X1 = pos.X;
            _cursorLine.X2 = pos.X;
            _cursorLine.Y1 = 0;
            _cursorLine.Y2 = ActualHeight - 25;
            _cursorLine.Visibility = Visibility.Visible;

            // Handle selection
            if (_isSelecting)
            {
                _selectionEnd = pos.X / ActualWidth;
                UpdateSelectionRect();
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ActualWidth <= 0) return;

            Point pos = e.GetPosition(this);
            _selectionStart = pos.X / ActualWidth;
            _selectionEnd = _selectionStart;
            _isSelecting = true;
            _selectionRect.Visibility = Visibility.Visible;

            CaptureMouse();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelecting && ActualWidth > 0)
            {
                Point pos = e.GetPosition(this);
                _selectionEnd = pos.X / ActualWidth;

                // Ensure start < end
                if (_selectionStart > _selectionEnd)
                {
                    double temp = _selectionStart;
                    _selectionStart = _selectionEnd;
                    _selectionEnd = temp;
                }

                UpdateSelectionRect();

                if (SelectionChanged != null)
                {
                    SelectionChanged(this, new WaveformSelectionEventArgs(_selectionStart, _selectionEnd));
                }
            }

            _isSelecting = false;
            ReleaseMouseCapture();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _cursorLine.Visibility = Visibility.Collapsed;
        }

        private void UpdateSelectionRect()
        {
            if (_selectionStart < 0 || _selectionEnd < 0 || ActualWidth <= 0)
            {
                return;
            }

            double start = Math.Min(_selectionStart, _selectionEnd);
            double end = Math.Max(_selectionStart, _selectionEnd);

            double x = start * ActualWidth;
            double width = (end - start) * ActualWidth;

            Canvas.SetLeft(_selectionRect, x);
            Canvas.SetTop(_selectionRect, 0);
            _selectionRect.Width = width;
            _selectionRect.Height = ActualHeight - 25;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateSelectionRect();
            UpdatePlaybackCursor();
        }
    }

    public class WaveformSelectionEventArgs : EventArgs
    {
        public double StartTime { get; private set; }
        public double EndTime { get; private set; }

        public WaveformSelectionEventArgs(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }
    }
}
