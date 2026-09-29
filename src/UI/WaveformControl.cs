using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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

            // Draw center line
            dc.DrawLine(
                new Pen(new SolidColorBrush(Color.FromRgb(51, 65, 85)), 1),
                new Point(0, centerY),
                new Point(width, centerY)
            );

            // Draw waveform
            var waveformPen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.5);

            for (int i = 0; i < _waveformData.Length - 1; i++)
            {
                double x1 = (i / (double)_waveformData.Length) * width;
                double x2 = ((i + 1) / (double)_waveformData.Length) * width;

                double y1 = centerY - (_waveformData[i] * centerY * 0.9);
                double y2 = centerY - (_waveformData[i + 1] * centerY * 0.9);

                dc.DrawLine(waveformPen, new Point(x1, y1), new Point(x2, y2));
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
            _cursorLine.Y2 = ActualHeight;
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
            _selectionRect.Height = ActualHeight;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateSelectionRect();
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
