using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InfraScan.Controls
{
    public partial class CircularGauge : UserControl
    {
        // Layout constants
        private const double Cx = 55, Cy = 55, R = 42;
        private const double StartDeg = 135.0;   // bottom-left
        private const double SweepDeg = 270.0;   // clockwise to bottom-right

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(CircularGauge),
                new PropertyMetadata(0.0, OnVisualChanged));

        public static readonly DependencyProperty GaugeColorProperty =
            DependencyProperty.Register(nameof(GaugeColor), typeof(Color), typeof(CircularGauge),
                new PropertyMetadata(Color.FromRgb(34, 197, 94), OnVisualChanged));

        public static readonly DependencyProperty LabelTextProperty =
            DependencyProperty.Register(nameof(LabelText), typeof(string), typeof(CircularGauge),
                new PropertyMetadata(string.Empty, OnLabelChanged));

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(CircularGauge),
                new PropertyMetadata("0%", OnDisplayTextChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public Color GaugeColor
        {
            get => (Color)GetValue(GaugeColorProperty);
            set => SetValue(GaugeColorProperty, value);
        }

        public string LabelText
        {
            get => (string)GetValue(LabelTextProperty);
            set => SetValue(LabelTextProperty, value);
        }

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            set => SetValue(DisplayTextProperty, value);
        }

        public CircularGauge()
        {
            InitializeComponent();
            Loaded += (_, _) => Refresh();
        }

        private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((CircularGauge)d).Refresh();

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((CircularGauge)d).LabelTextBlock.Text = e.NewValue as string ?? string.Empty;

        private static void OnDisplayTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((CircularGauge)d).ValueTextBlock.Text = e.NewValue as string ?? string.Empty;

        private void Refresh()
        {
            // Guard: named elements may not exist yet if called before InitializeComponent
            if (TrackPath == null || ValuePath == null || GlowPath == null) return;

            // Background track (full 270° arc)
            TrackPath.Data = BuildArc(StartDeg, SweepDeg);

            // Value arc
            double clampedValue = Math.Min(100, Math.Max(0, Value));
            double sweep = clampedValue / 100.0 * SweepDeg;

            var brush = new SolidColorBrush(GaugeColor);
            if (sweep < 0.5)
            {
                ValuePath.Data = null;
                GlowPath.Data = null;
            }
            else
            {
                var arc = BuildArc(StartDeg, sweep);
                ValuePath.Data = arc;
                ValuePath.Stroke = brush;

                GlowPath.Data = arc;
                GlowPath.Stroke = brush;
            }

            LabelTextBlock.Text = LabelText;
            ValueTextBlock.Text = DisplayText;
        }

        private static Geometry BuildArc(double startDeg, double sweep)
        {
            if (sweep <= 0) return Geometry.Empty;

            double startRad = startDeg * Math.PI / 180.0;
            double endRad = (startDeg + sweep) * Math.PI / 180.0;

            var startPt = new Point(Cx + R * Math.Cos(startRad), Cy + R * Math.Sin(startRad));
            var endPt = new Point(Cx + R * Math.Cos(endRad), Cy + R * Math.Sin(endRad));

            bool isLargeArc = sweep > 180.0;

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = startPt, IsClosed = false };
            fig.Segments.Add(new ArcSegment(endPt, new Size(R, R), 0,
                isLargeArc, SweepDirection.Clockwise, true));
            geo.Figures.Add(fig);
            return geo;
        }
    }
}
