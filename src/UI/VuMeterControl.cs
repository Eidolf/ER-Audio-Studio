#pragma warning disable 618
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ErAudioTool.UI
{
    public class VuMeterControl : FrameworkElement
    {
        private float _leftLevel;
        private float _rightLevel;
        private float _leftPeak;
        private float _rightPeak;

        private readonly Typeface _typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private readonly Brush _textBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        private readonly Brush _trackBrush = new SolidColorBrush(Color.FromRgb(22, 31, 48));
        private readonly Brush _borderBrush = new SolidColorBrush(Color.FromRgb(36, 48, 68));
        private readonly Brush _bgBrush = new SolidColorBrush(Color.FromRgb(11, 15, 25));
        private readonly Pen _borderPen;
        private readonly Pen _peakPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 1.5);

        private readonly LinearGradientBrush _gradientBrush;

        public float LeftLevel
        {
            get { return _leftLevel; }
            set
            {
                _leftLevel = Math.Max(0f, Math.Min(1f, value));
                if (_leftLevel > _leftPeak) _leftPeak = _leftLevel;
                else _leftPeak = Math.Max(_leftLevel, _leftPeak * 0.95f);
                InvalidateVisual();
            }
        }

        public float RightLevel
        {
            get { return _rightLevel; }
            set
            {
                _rightLevel = Math.Max(0f, Math.Min(1f, value));
                if (_rightLevel > _rightPeak) _rightPeak = _rightLevel;
                else _rightPeak = Math.Max(_rightLevel, _rightPeak * 0.95f);
                InvalidateVisual();
            }
        }

        public VuMeterControl()
        {
            _textBrush.Freeze();
            _trackBrush.Freeze();
            _borderBrush.Freeze();
            _bgBrush.Freeze();
            _peakPen.Freeze();
            _borderPen = new Pen(_borderBrush, 1);
            _borderPen.Freeze();

            var stops = new GradientStopCollection
            {
                new GradientStop(Color.FromRgb(16, 185, 129), 0.0),  // Green
                new GradientStop(Color.FromRgb(16, 185, 129), 0.65), // Green
                new GradientStop(Color.FromRgb(245, 158, 11), 0.85), // Amber
                new GradientStop(Color.FromRgb(239, 68, 68), 1.0)    // Red
            };
            _gradientBrush = new LinearGradientBrush(stops, new Point(0, 0), new Point(1, 0));
            _gradientBrush.Freeze();

            MinHeight = 56;
            MinWidth = 200;
        }

        public void SetLevels(float left, float right)
        {
            _leftLevel = Math.Max(0f, Math.Min(1f, left));
            if (_leftLevel > _leftPeak) _leftPeak = _leftLevel;
            else _leftPeak = Math.Max(_leftLevel, _leftPeak * 0.94f);

            _rightLevel = Math.Max(0f, Math.Min(1f, right));
            if (_rightLevel > _rightPeak) _rightPeak = _rightLevel;
            else _rightPeak = Math.Max(_rightLevel, _rightPeak * 0.94f);

            InvalidateVisual();
        }

        private static double LevelToDb(float level)
        {
            if (level <= 0.0001f) return -60.0;
            double db = 20.0 * Math.Log10(level);
            return Math.Max(-60.0, Math.Min(0.0, db));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 50 || height < 30) return;

            // Background card
            var cardRect = new Rect(0, 0, width, height);
            dc.DrawRoundedRectangle(_bgBrush, _borderPen, cardRect, 6, 6);

            double labelWidth = 24;
            double dbTextWidth = 55;
            double barLeft = labelWidth + 4;
            double barWidth = Math.Max(10, width - barLeft - dbTextWidth - 10);
            double barHeight = Math.Max(8, (height - 24) / 2);

            // Channel L
            DrawChannel(dc, "L", barLeft, 8, barWidth, barHeight, _leftLevel, _leftPeak);

            // Channel R
            DrawChannel(dc, "R", barLeft, 8 + barHeight + 4, barWidth, barHeight, _rightLevel, _rightPeak);
        }

        private void DrawChannel(DrawingContext dc, string label, double left, double top, double width, double height, float level, float peak)
        {
            // Label
            var ftLabel = new FormattedText(
                label,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                11,
                _textBrush);
            dc.DrawText(ftLabel, new Point(8, top + (height - ftLabel.Height) / 2));

            // Track background
            var trackRect = new Rect(left, top, width, height);
            dc.DrawRoundedRectangle(_trackBrush, null, trackRect, 3, 3);

            // Level fill
            double fillWidth = Math.Max(0, width * Math.Sqrt(level)); // Sqrt for natural perceived loudness scale
            if (fillWidth > 1)
            {
                var fillRect = new Rect(left, top, fillWidth, height);
                dc.PushClip(new RectangleGeometry(fillRect, 3, 3));
                dc.DrawRectangle(_gradientBrush, null, trackRect);
                dc.Pop();
            }

            // Peak line
            double peakX = left + (width * Math.Sqrt(peak));
            if (peakX > left + 2 && peakX <= left + width)
            {
                dc.DrawLine(_peakPen, new Point(peakX, top), new Point(peakX, top + height));
            }

            // dB value text
            double db = LevelToDb(level);
            string dbStr = db <= -59.5 ? "-∞ dB" : string.Format("{0:0.0} dB", db);
            var ftDb = new FormattedText(
                dbStr,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                10,
                _textBrush);
            dc.DrawText(ftDb, new Point(left + width + 8, top + (height - ftDb.Height) / 2));
        }
    }
}
