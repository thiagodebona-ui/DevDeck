using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  A ring gauge: a track, a reading drawn clockwise from twelve, and whatever the caller wants
    ///  in the middle.
    /// </summary>
    /// <remarks>
    ///  One implementation for both places a reading is shown - two of these side by side make the
    ///  page's memory and processor gauges, and two concentric ones make the widget's dial. The
    ///  arithmetic that turns a percentage into a dash pattern therefore exists once, which is the
    ///  point: the first version of the dial did it in the view model against a hard-coded copy of
    ///  the widget's geometry, and the two were free to drift apart.
    /// </remarks>
    public partial class Gauge : UserControl
    {
        public static readonly StyledProperty<double> PercentProperty =
            AvaloniaProperty.Register<Gauge, double>(nameof(Percent));

        /// <summary>Outside diameter of the ring, in pixels.</summary>
        public static readonly StyledProperty<double> RingSizeProperty =
            AvaloniaProperty.Register<Gauge, double>(nameof(RingSize), defaultValue: 120);

        public static readonly StyledProperty<double> ThicknessProperty =
            AvaloniaProperty.Register<Gauge, double>(nameof(Thickness), defaultValue: 10);

        /// <summary>
        ///  The colour of the reading. Set through a style class by the caller, so a gauge turns
        ///  amber and then red on the same bands as everything else.
        /// </summary>
        public static readonly StyledProperty<IBrush?> FillProperty =
            AvaloniaProperty.Register<Gauge, IBrush?>(nameof(Fill));

        /// <summary>Dims the track, for the quieter of two concentric rings.</summary>
        public static readonly StyledProperty<double> TrackOpacityProperty =
            AvaloniaProperty.Register<Gauge, double>(nameof(TrackOpacity), defaultValue: 1);

        /// <summary>How long the ring takes to travel to a new reading.</summary>
        private static readonly TimeSpan Glide = TimeSpan.FromMilliseconds(550);

        /// <summary>The reading being drawn right now, which trails <see cref="Percent"/> while gliding.</summary>
        private double shown;

        /// <summary>Where the current glide started from, and when.</summary>
        private double from;

        private DateTime startedAt;

        /// <summary>
        ///  Drives the glide. Only ticks while the ring is actually moving.
        /// </summary>
        /// <remarks>
        ///  A timer stepping a plain double rather than a Transition on the dash array: Avalonia
        ///  has no transition for a dash list, and animating the list directly would re-allocate it
        ///  anyway. Sixty steps a second for half a second, then it stops, so an idle page costs
        ///  nothing.
        /// </remarks>
        private readonly DispatcherTimer frames = new(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };

        private bool primed;

        public Gauge()
        {
            InitializeComponent();

            frames.Tick += (_, _) => Step();

            Redraw();
        }

        public double Percent
        {
            get => GetValue(PercentProperty);
            set => SetValue(PercentProperty, value);
        }

        public double RingSize
        {
            get => GetValue(RingSizeProperty);
            set => SetValue(RingSizeProperty, value);
        }

        public double Thickness
        {
            get => GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
        }

        public IBrush? Fill
        {
            get => GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public double TrackOpacity
        {
            get => GetValue(TrackOpacityProperty);
            set => SetValue(TrackOpacityProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == PercentProperty)
            {
                GlideTo(Percent);

                return;
            }

            if (change.Property == RingSizeProperty
                || change.Property == ThicknessProperty
                || change.Property == FillProperty
                || change.Property == TrackOpacityProperty)
            {
                Redraw();
            }
        }

        /// <summary>Starts the ring moving towards a new reading, from wherever it is now.</summary>
        private void GlideTo(double target)
        {
            // The first reading is drawn where it is, not swept up from zero: opening the page
            // should show the machine's state, not an animation of it.
            if (!primed)
            {
                primed = true;
                shown = target;
                Redraw();

                return;
            }

            from = shown;
            startedAt = DateTime.UtcNow;

            if (!frames.IsEnabled)
            {
                frames.Start();
            }
        }

        private void Step()
        {
            double t = Math.Clamp((DateTime.UtcNow - startedAt) / Glide, 0, 1);

            // Ease-out cubic: quick to leave, gentle to arrive, which reads as the needle settling.
            double eased = 1 - Math.Pow(1 - t, 3);

            shown = from + ((Percent - from) * eased);

            if (t >= 1)
            {
                shown = Percent;
                frames.Stop();
            }

            Redraw();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            frames.Stop();
            shown = Percent;
        }

        private void Redraw()
        {
            double size = Math.Max(1, RingSize);
            double stroke = Math.Clamp(Thickness, 0.5, size / 2);

            Track.Width = size;
            Track.Height = size;
            Track.StrokeThickness = stroke;
            Track.Opacity = TrackOpacity;

            Progress.Width = size;
            Progress.Height = size;
            Progress.StrokeThickness = stroke;
            Progress.Stroke = Fill;

            Width = size;
            Height = size;

            // Avalonia insets an ellipse's geometry by half the stroke, so the path the dash runs
            // along has a diameter of size - stroke, not size.
            double circumference = Math.PI * (size - stroke);

            // StrokeDashArray is measured in multiples of the stroke width, not in pixels.
            double total = circumference / stroke;
            double filled = Math.Clamp(shown, 0, 100) / 100 * total;

            // Never exactly zero: a zero-length dash with a round cap still paints a dot at twelve,
            // which reads as a percent or two on a completely idle machine.
            Progress.StrokeDashArray = filled < 0.01
                ? new AvaloniaList<double> { 0, total }
                : new AvaloniaList<double> { filled, Math.Max(0, total - filled) };
        }
    }
}
