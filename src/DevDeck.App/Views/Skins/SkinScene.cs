using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using DevDeck.App.ViewModels;
using DevDeck.Core;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  One of the scenes the memory widget can wear in place of its dial.
    /// </summary>
    /// <remarks>
    ///  Every scene draws into the same 230 pixel square the dial is laid out in, and the widget's
    ///  Viewbox scales it to whatever size is chosen, so a scene is written once at one size. The
    ///  window round it keeps the click, the drag, the wheel and the menu; a scene only paints.
    ///
    ///  Drawn straight into the <see cref="DrawingContext"/>, as <see cref="Backdrop"/> is, and
    ///  for the same reason: a scene is a few dozen moving things, and as controls in the tree each
    ///  would cost a layout pass. The same rule about brushes applies too. Everything drawn with is
    ///  immutable and the repeating ones are cached - a mutable brush per frame becomes a
    ///  compositor resource, and those pile up for as long as the widget floats, which is all day.
    ///
    ///  The readings are eased rather than jumped: the probe reads every two seconds, and a rocket
    ///  that teleports between altitudes looks broken where one that climbs looks alive.
    /// </remarks>
    internal abstract class SkinScene : Control
    {
        /// <summary>The side of the square every scene is drawn in.</summary>
        protected const double Side = 230;

        /// <summary>The middle of that square.</summary>
        protected static readonly Point Centre = new(Side / 2, Side / 2);

        private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(33);

        private static readonly TimeSpan RestingFrame = TimeSpan.FromMilliseconds(66);

        private readonly DispatcherTimer timer = new(DispatcherPriority.Background) { Interval = Frame };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Dictionary<uint, IImmutableBrush> solids = [];
        private readonly Dictionary<(uint, double), ImmutablePen> pens = [];
        private readonly Dictionary<uint, IImmutableBrush> glows = [];
        private readonly Dictionary<(uint, uint, bool), IImmutableBrush> fades = [];
        private readonly Dictionary<(string, double, uint, bool, bool), FormattedText> texts = [];

        private double last;
        private double sinceSweep;
        private bool wasBusy;
        private bool seeded;

        protected SkinScene()
        {
            ClipToBounds = true;
            timer.Tick += (_, _) => Tick();
        }

        #region What a scene reads
        protected MemoryViewModel? Model { get; private set; }

        /// <summary>Memory in use, 0 to 1, eased.</summary>
        protected double Memory { get; private set; }

        /// <summary>Processor load, 0 to 1, eased.</summary>
        protected double Cpu { get; private set; }

        /// <summary>Graphics load, 0 to 1, eased.</summary>
        protected double Gpu { get; private set; }

        /// <summary>Disk activity, 0 to 1, eased.</summary>
        protected double Disk { get; private set; }

        /// <summary>Seconds since the scene was first shown; the clock every animation runs on.</summary>
        protected double Time { get; private set; }

        /// <summary>A clean is running.</summary>
        protected bool Busy => Model?.IsBusy ?? false;

        /// <summary>Seconds since the running clean started; 0 when none is.</summary>
        protected double BusyFor { get; private set; }

        /// <summary>Seconds since the last clean finished; a large number before the first.</summary>
        protected double SinceClean { get; private set; } = 1e6;

        /// <summary>The result of a clean is being shown: <see cref="FlashText"/>, for a few seconds.</summary>
        protected bool Flashing => Model?.IsFlashing ?? false;

        /// <summary>"1.2 GB", or why nothing was freed.</summary>
        protected string FlashText => Model?.FlashText ?? string.Empty;

        /// <summary>The clean freed something worth showing in green.</summary>
        protected bool FlashGood => Model?.FlashGood ?? false;

        /// <summary>The pointer is on the widget, which is when a scene says it can be clicked.</summary>
        protected bool Hovered { get; private set; }

        /// <summary>0 below 75%, 1 high, 2 critical - the same bands as the dial's amber and red.</summary>
        protected int MemoryLevel => Model is { IsCritical: true } ? 2 : Model is { IsHigh: true } ? 1 : 0;

        protected int CpuLevel => Model is { IsCpuCritical: true } ? 2 : Model is { IsCpuHigh: true } ? 1 : 0;

        protected string MemoryText => Model?.UsedPercentText ?? "—";

        protected string CpuText => Model?.CpuPercentText ?? "—";

        protected string GpuText => Model?.GpuPercentText ?? "—";

        protected string DiskText => Model?.DiskPercentText ?? "—";

        /// <summary>"9.1 GB / 16 GB".</summary>
        protected string UsedOfTotal => Model is null ? "—" : $"{Model.Used} / {Model.Total}";

        /// <summary>The heaviest processes, heaviest first, as the Memory page lists them.</summary>
        protected IReadOnlyList<ProcessRow> Processes => (IReadOnlyList<ProcessRow>?)Model?.Processes ?? [];

        protected Random Random { get; } = new();

        // The palette, so a scene can borrow the theme's accent where it wants one.
        protected Color Accent { get; private set; }
        protected Color TextColour { get; private set; }
        protected Color Good { get; private set; }
        protected Color Warn { get; private set; }
        protected Color Bad { get; private set; }

        /// <summary>Translated captions, so scenes need no strings of their own.</summary>
        protected static string MemoryCaption => Strings.Text("MemoryWidgetMemory");
        protected static string CpuCaption => Strings.Text("MemoryWidgetCpu");
        protected static string GpuCaption => Strings.Text("MemoryWidgetGpu");
        protected static string DiskCaption => Strings.Text("MemoryWidgetDisk");
        protected static string FreedCaption => Strings.Text("MemoryWidgetFreed");
        protected static string ClickToClean => Strings.Text("MemoryWidgetClickToClean");
        #endregion

        #region What a scene does
        /// <summary>Called once, before the first frame, to scatter whatever the scene moves.</summary>
        protected virtual void Seed() { }

        /// <summary>Moves the world on by <paramref name="dt"/> seconds. Readings are already eased.</summary>
        protected virtual void Advance(double dt) { }

        /// <summary>Paints one frame into the 230 pixel square.</summary>
        protected abstract void Draw(DrawingContext context);

        protected virtual void CleanStarted() { }

        protected virtual void CleanFinished() { }
        #endregion

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            ThemeManager.Changed += ReadPalette;
            ReadPalette();

            last = clock.Elapsed.TotalSeconds;
            timer.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ThemeManager.Changed -= ReadPalette;
            timer.Stop();

            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            Model = DataContext as MemoryViewModel;

            // Straight to the readings the first time, rather than climbing from nothing.
            if (Model is not null)
            {
                Memory = Target(Model.UsedPercent);
                Cpu = Target(Model.CpuPercent);
                Gpu = Target(Model.GpuPercent);
                Disk = Target(Model.DiskPercent);
                wasBusy = Model.IsBusy;
            }
        }

        private void ReadPalette()
        {
            Accent = Read("Accent", Colors.SteelBlue);
            TextColour = Read("Text", Colors.White);
            Good = Read("Good", Colors.MediumSeaGreen);
            Warn = Read("Warn", Colors.Orange);
            Bad = Read("Bad", Colors.OrangeRed);

            InvalidateVisual();
        }

        private static Color Read(string key, Color fallback) =>
            Application.Current is { } app
            && app.TryGetResource(key, app.ActualThemeVariant, out object? found)
            && found is ISolidColorBrush brush
                ? brush.Color
                : fallback;

        private static double Target(double percent) => Math.Clamp(percent / 100, 0, 1);

        private void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;

            // Capped, so a sleeping laptop wakes to one ordinary step rather than a leap.
            double dt = Math.Min(now - last, 0.1);
            last = now;
            Time += dt;

            Hovered = TopLevel.GetTopLevel(this)?.IsPointerOver ?? false;

            if (Model is { } model)
            {
                // About a second to settle, whatever the frame rate.
                double ease = 1 - Math.Exp(-dt * 3);

                Memory += (Target(model.UsedPercent) - Memory) * ease;
                Cpu += (Target(model.CpuPercent) - Cpu) * ease;
                Gpu += (Target(model.GpuPercent) - Gpu) * ease;
                Disk += (Target(model.DiskPercent) - Disk) * ease;

                if (model.IsBusy && !wasBusy)
                {
                    BusyFor = 0;
                    CleanStarted();
                }
                else if (!model.IsBusy && wasBusy)
                {
                    SinceClean = 0;
                    CleanFinished();
                }

                wasBusy = model.IsBusy;
            }

            if (Busy)
            {
                BusyFor += dt;
            }
            else
            {
                BusyFor = 0;
                SinceClean += dt;
            }

            if (!seeded)
            {
                seeded = true;
                Seed();
            }

            Advance(dt);
            InvalidateVisual();

            // A shape built for one frame is a native path that only a finalizer frees, and a scene
            // allocates so little else that the collector can go many minutes without running -
            // long enough for thousands of dead paths to sit in memory. A young-generation
            // collection now and then costs well under a millisecond and lets them go.
            sinceSweep += dt;

            if (sinceSweep > 20)
            {
                sinceSweep = 0;
                GC.Collect(0, GCCollectionMode.Forced, blocking: false);
            }

            // Full rate only while something is happening that is worth watching closely: a clean,
            // its landing, or the pointer on the widget. The rest of the day the scene is ambient,
            // and every frame of a transparent window is composed again in full, so the resting
            // rate is what the widget really costs.
            TimeSpan wanted = Busy || SinceClean < 6 || Hovered ? Frame : RestingFrame;

            if (timer.Interval != wanted)
            {
                timer.Interval = wanted;
            }
        }

        public sealed override void Render(DrawingContext context)
        {
            Size size = Bounds.Size;

            if (size.Width < 2 || size.Height < 2)
            {
                return;
            }

            if (!seeded)
            {
                seeded = true;
                Seed();
            }

            using (context.PushTransform(Matrix.CreateScale(size.Width / Side, size.Height / Side)))
            {
                Draw(context);
            }
        }

        #region Drawing helpers
        protected static Color WithAlpha(Color c, double alpha) =>
            Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B);

        protected static Color Mix(Color a, Color b, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);

            return Color.FromArgb(
                (byte)(a.A + (b.A - a.A) * amount),
                (byte)(a.R + (b.R - a.R) * amount),
                (byte)(a.G + (b.G - a.G) * amount),
                (byte)(a.B + (b.B - a.B) * amount));
        }

        /// <summary>Normal, amber or red for a level from <see cref="MemoryLevel"/>.</summary>
        protected Color LevelColour(int level, Color normal) => level switch
        {
            2 => Bad,
            1 => Warn,
            _ => normal,
        };

        /// <summary>
        ///  Every channel is quantised to 64 steps, so a colour that eases over many frames reuses a
        ///  handful of brushes instead of minting a new one each frame. Four levels in 256 is below
        ///  what anyone can see on a widget this size.
        /// </summary>
        private static Color Quantise(Color c) =>
            Color.FromArgb((byte)(c.A & 0xFC), (byte)(c.R & 0xFC), (byte)(c.G & 0xFC), (byte)(c.B & 0xFC));

        /// <summary>
        ///  A last guard for a scene whose colours vary more than quantising can fold: past this many
        ///  entries a cache starts again, rather than growing for as long as the widget floats.
        /// </summary>
        private const int MostCached = 600;

        private static void Bound<TKey, TValue>(Dictionary<TKey, TValue> cache)
            where TKey : notnull
        {
            if (cache.Count > MostCached)
            {
                cache.Clear();
            }
        }

        protected IImmutableBrush Solid(Color c)
        {
            c = Quantise(c);

            Bound(solids);

            if (!solids.TryGetValue(c.ToUInt32(), out IImmutableBrush? brush))
            {
                solids[c.ToUInt32()] = brush = new ImmutableSolidColorBrush(c);
            }

            return brush;
        }

        protected IImmutableBrush Solid(Color c, double alpha) => Solid(WithAlpha(c, alpha));

        /// <summary>A round pen; thickness is rounded to a quarter pixel for the cache.</summary>
        protected ImmutablePen Pen(Color c, double thickness)
        {
            c = Quantise(c);
            thickness = Math.Round(thickness * 4) / 4;

            Bound(pens);

            if (!pens.TryGetValue((c.ToUInt32(), thickness), out ImmutablePen? pen))
            {
                pens[(c.ToUInt32(), thickness)] = pen = new ImmutablePen(
                    (IImmutableBrush)Solid(c), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            }

            return pen;
        }

        protected ImmutablePen Pen(Color c, double alpha, double thickness) => Pen(WithAlpha(c, alpha), thickness);

        /// <summary>A soft round light: the colour at the centre, nothing at the edge. Fill an ellipse with it.</summary>
        protected IImmutableBrush Glow(Color c)
        {
            c = Quantise(c);

            Bound(glows);

            if (!glows.TryGetValue(c.ToUInt32(), out IImmutableBrush? brush))
            {
                // Built mutable and frozen once, which keeps the default centre and radius.
                glows[c.ToUInt32()] = brush = new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(c, 0),
                        new GradientStop(Color.FromArgb((byte)(c.A / 3), c.R, c.G, c.B), 0.55),
                        new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1),
                    },
                }.ToImmutable();
            }

            return brush;
        }

        protected IImmutableBrush Glow(Color c, double alpha) => Glow(WithAlpha(c, alpha));

        /// <summary>One colour to another across the shape it fills: top to bottom, or left to right.</summary>
        protected IImmutableBrush Fade(Color from, Color to, bool horizontal = false)
        {
            from = Quantise(from);
            to = Quantise(to);
            (uint, uint, bool) key = (from.ToUInt32(), to.ToUInt32(), horizontal);

            Bound(fades);

            if (!fades.TryGetValue(key, out IImmutableBrush? brush))
            {
                fades[key] = brush = new ImmutableLinearGradientBrush(
                    [new ImmutableGradientStop(0, from), new ImmutableGradientStop(1, to)],
                    startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
                    endPoint: horizontal
                        ? new RelativePoint(1, 0, RelativeUnit.Relative)
                        : new RelativePoint(0, 1, RelativeUnit.Relative));
            }

            return brush;
        }

        /// <summary>
        ///  Text centred on a point. Cached by content, so a reading that has not changed costs
        ///  nothing; the cache is emptied when it grows, which a few dozen distinct readings will.
        /// </summary>
        protected void Text(
            DrawingContext context, string text, Point centre, double size, Color colour,
            bool bold = false, bool mono = false)
        {
            if (text.Length == 0)
            {
                return;
            }

            colour = Quantise(colour);
            (string, double, uint, bool, bool) key = (text, size, colour.ToUInt32(), bold, mono);

            if (!texts.TryGetValue(key, out FormattedText? formatted))
            {
                if (texts.Count > 400)
                {
                    texts.Clear();
                }

                Typeface face = new(
                    mono ? MonoFamily() : FontFamily.Default,
                    FontStyle.Normal,
                    bold ? FontWeight.Bold : FontWeight.SemiBold);

                texts[key] = formatted = new FormattedText(
                    text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, size, Solid(colour));
            }

            context.DrawText(formatted, new Point(centre.X - formatted.Width / 2, centre.Y - formatted.Height / 2));
        }

        private static FontFamily MonoFamily() =>
            Application.Current is { } app
            && app.TryGetResource("MonoFont", app.ActualThemeVariant, out object? found)
            && found is FontFamily family
                ? family
                : new FontFamily("Consolas, Menlo, monospace");

        /// <summary>A circle path, for clipping a scene to a round window of glass.</summary>
        protected static EllipseGeometry Circle(Point centre, double radius) =>
            new(new Rect(centre.X - radius, centre.Y - radius, radius * 2, radius * 2));
        #endregion
    }
}
