using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  The living layer behind the window: aurora, falling glyphs, stars, a neon grid, embers,
    ///  snow or petals, drawn in the palette's own colours.
    /// </summary>
    /// <remarks>
    ///  One control drawing straight into its <see cref="DrawingContext"/>, rather than a few
    ///  hundred shapes in the tree: a particle is a struct in an array here, and the layout system
    ///  never hears about any of them. That is the difference between an effect that costs a
    ///  percent of one core and one that makes the rest of the window stutter.
    ///
    ///  Three rules keep it a background rather than a show. It never takes input. It stops
    ///  completely while the window is minimised or hidden - an animation nobody can see is just
    ///  heat - and drops to a slow tick when the window is not the one in front. And every effect
    ///  is faint: panels are translucent in the living themes, so the effect shows through the
    ///  rail and the cards, and it has to stay quieter than the text on top of them.
    /// </remarks>
    public sealed class Backdrop : Control
    {
        private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(33);

        private static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(80);

        private readonly DispatcherTimer timer = new(DispatcherPriority.Background);
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new();

        private ThemeBackdrop effect;
        private double last;

        // The palette, read once per theme change rather than once per frame.
        private Color accent;
        private Color accentSoft;
        private Color text;
        private Color ink;
        private Color keyword;
        private Color stringColour;
        private Color warn;
        private Color bad;
        private Color variable;

        /// <summary>One moving thing: position, velocity, size, and whatever else the effect wants.</summary>
        private struct Mote
        {
            public double X;
            public double Y;
            public double Vx;
            public double Vy;
            public double Size;
            public double Phase;
            public double Spin;
            public double Life;
            public int Kind;
        }

        private Mote[] motes = [];

        private Size seededFor;

        public Backdrop()
        {
            IsHitTestVisible = false;
            ClipToBounds = true;

            timer.Interval = Fast;
            timer.Tick += (_, _) => Tick();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            ThemeManager.Changed += OnThemeChanged;
            OnThemeChanged();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ThemeManager.Changed -= OnThemeChanged;
            timer.Stop();

            base.OnDetachedFromVisualTree(e);
        }

        private void OnThemeChanged()
        {
            effect = ThemeManager.Effect;

            accent = Read("Accent", Colors.SteelBlue);
            accentSoft = Read("AccentSoft", accent);
            text = Read("Text", Colors.White);
            ink = Read("Ink", Colors.Black);
            keyword = Read("SynKeyword", accent);
            stringColour = Read("SynString", accent);
            warn = Read("Warn", Colors.Orange);
            bad = Read("Bad", Colors.OrangeRed);
            variable = Read("SynVariable", accent);

            glyphs.Clear();
            seededFor = default;

            if (effect == ThemeBackdrop.None)
            {
                timer.Stop();
            }
            else if (!timer.IsEnabled)
            {
                last = clock.Elapsed.TotalSeconds;
                timer.Start();
            }

            InvalidateVisual();
        }

        private Color Read(string key, Color fallback) =>
            Application.Current is { } app
            && app.TryGetResource(key, app.ActualThemeVariant, out object? found)
            && found is ISolidColorBrush brush
                ? brush.Color
                : fallback;

        /// <summary>
        ///  Moves the world on and asks for a frame - unless nobody can see it.
        /// </summary>
        private void Tick()
        {
            if (TopLevel.GetTopLevel(this) is Window window)
            {
                if (!window.IsVisible || window.WindowState == WindowState.Minimized)
                {
                    // Keep the clock honest, so nothing leaps across the window on the way back.
                    last = clock.Elapsed.TotalSeconds;
                    return;
                }

                TimeSpan wanted = window.IsActive ? Fast : Slow;

                if (timer.Interval != wanted)
                {
                    timer.Interval = wanted;
                }
            }

            double now = clock.Elapsed.TotalSeconds;

            // Capped, so a long stall - a debugger, a sleeping laptop - is one ordinary step.
            double step = Math.Min(now - last, 0.1);
            last = now;

            Advance(step);
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            Size size = Bounds.Size;

            if (effect == ThemeBackdrop.None || size.Width < 2 || size.Height < 2)
            {
                return;
            }

            if (seededFor != size)
            {
                Seed(size);
            }

            double t = clock.Elapsed.TotalSeconds;

            switch (effect)
            {
                case ThemeBackdrop.Aurora:
                    DrawAurora(context, size, t);
                    break;
                case ThemeBackdrop.Rain:
                    DrawRain(context, size, t);
                    break;
                case ThemeBackdrop.Stars:
                    DrawStars(context, size, t);
                    break;
                case ThemeBackdrop.Grid:
                    DrawGrid(context, size, t);
                    break;
                case ThemeBackdrop.Embers:
                    DrawEmbers(context, size);
                    break;
                case ThemeBackdrop.Snow:
                    DrawSnow(context);
                    break;
                case ThemeBackdrop.Petals:
                    DrawPetals(context);
                    break;
            }
        }

        #region Seeding and moving
        /// <summary>Scatters a fresh set of motes for this effect across a window of this size.</summary>
        private void Seed(Size size)
        {
            seededFor = size;

            int count = effect switch
            {
                ThemeBackdrop.Rain => (int)Math.Ceiling(size.Width / RainCell),
                ThemeBackdrop.Stars => 190,
                ThemeBackdrop.Embers => 110,
                ThemeBackdrop.Snow => 130,
                ThemeBackdrop.Petals => 34,
                _ => 0,
            };

            motes = new Mote[count];

            for (int i = 0; i < count; i++)
            {
                motes[i] = Spawn(size, i, scatter: true);
            }
        }

        /// <summary>A new mote. Scattered over the whole window at first, from the edge it enters by after that.</summary>
        private Mote Spawn(Size size, int index, bool scatter)
        {
            double w = size.Width;
            double h = size.Height;
            Mote m = new() { Phase = random.NextDouble() * Math.Tau };

            switch (effect)
            {
                case ThemeBackdrop.Rain:
                    double rows = h / RainCell;
                    m.X = index * RainCell;
                    m.Size = 8 + random.Next(16);
                    m.Y = scatter ? random.NextDouble() * (rows + m.Size) : -random.NextDouble() * rows * 0.8;
                    m.Vy = 5 + random.NextDouble() * 12;
                    m.Kind = random.Next(1000);
                    break;

                case ThemeBackdrop.Stars:
                    // Three depths: many small slow ones, fewer bright fast ones.
                    m.Kind = index < 120 ? 0 : index < 175 ? 1 : 2;
                    m.X = random.NextDouble() * w;
                    m.Y = random.NextDouble() * h;
                    m.Size = m.Kind switch { 0 => 0.7, 1 => 1.2, _ => 1.9 };
                    m.Vx = -(m.Kind switch { 0 => 2.5, 1 => 6, _ => 12 });
                    m.Spin = 0.6 + random.NextDouble() * 1.6;
                    break;

                case ThemeBackdrop.Embers:
                    m.X = random.NextDouble() * w;
                    m.Y = scatter ? random.NextDouble() * h : h + random.NextDouble() * 30;
                    m.Vy = -(18 + random.NextDouble() * 46);
                    m.Size = 1.6 + random.NextDouble() * 2.2;
                    m.Life = scatter ? random.NextDouble() : 1;
                    m.Kind = random.Next(3);
                    m.Spin = 0.8 + random.NextDouble() * 2;
                    break;

                case ThemeBackdrop.Snow:
                    m.X = random.NextDouble() * w;
                    m.Y = scatter ? random.NextDouble() * h : -10;
                    m.Size = 0.8 + random.NextDouble() * 2.6;
                    m.Vy = 10 + m.Size * 11;
                    m.Spin = 0.3 + random.NextDouble() * 0.9;
                    m.Life = 0.3 + random.NextDouble() * 0.5;
                    break;

                case ThemeBackdrop.Petals:
                    m.X = scatter ? random.NextDouble() * w : -20 - random.NextDouble() * w * 0.3;
                    m.Y = scatter ? random.NextDouble() * h : random.NextDouble() * h * 0.6 - 40;
                    m.Vx = 14 + random.NextDouble() * 22;
                    m.Vy = 16 + random.NextDouble() * 22;
                    m.Size = 7 + random.NextDouble() * 5;
                    m.Spin = (random.NextDouble() - 0.5) * 2.4;
                    m.Life = 0.5 + random.NextDouble() * 0.35;
                    m.Kind = random.Next(2);
                    break;
            }

            return m;
        }

        private void Advance(double dt)
        {
            Size size = seededFor;

            if (size.Width < 2)
            {
                return;
            }

            for (int i = 0; i < motes.Length; i++)
            {
                ref Mote m = ref motes[i];

                switch (effect)
                {
                    case ThemeBackdrop.Rain:
                        m.Y += m.Vy * dt;

                        if (m.Y - m.Size > size.Height / RainCell)
                        {
                            m = Spawn(size, i, scatter: false);
                        }

                        break;

                    case ThemeBackdrop.Stars:
                        m.X += m.Vx * dt;

                        if (m.X < -4)
                        {
                            m.X = size.Width + 4;
                            m.Y = random.NextDouble() * size.Height;
                        }

                        break;

                    case ThemeBackdrop.Embers:
                        m.Y += m.Vy * dt;
                        m.X += Math.Sin(clock.Elapsed.TotalSeconds * m.Spin + m.Phase) * 14 * dt;
                        m.Life -= dt * 0.16;

                        if (m.Life <= 0 || m.Y < -10)
                        {
                            m = Spawn(size, i, scatter: false);
                        }

                        break;

                    case ThemeBackdrop.Snow:
                        m.Y += m.Vy * dt;
                        m.X += Math.Sin(clock.Elapsed.TotalSeconds * m.Spin + m.Phase) * 16 * dt;

                        if (m.Y > size.Height + 6)
                        {
                            m = Spawn(size, i, scatter: false);
                        }

                        break;

                    case ThemeBackdrop.Petals:
                        m.X += m.Vx * dt + Math.Sin(clock.Elapsed.TotalSeconds * 0.9 + m.Phase) * 10 * dt;
                        m.Y += m.Vy * dt;
                        m.Phase += m.Spin * dt;

                        if (m.Y > size.Height + 20 || m.X > size.Width + 20)
                        {
                            m = Spawn(size, i, scatter: false);
                        }

                        break;
                }
            }

            // A shooting star now and then, about one every ten seconds.
            if (effect == ThemeBackdrop.Stars && meteor <= 0 && random.NextDouble() < dt / 10)
            {
                meteor = 1;
                meteorFrom = new Point(random.NextDouble() * size.Width, random.NextDouble() * size.Height * 0.5);
            }

            if (meteor > 0)
            {
                meteor -= dt * 1.4;
            }
        }
        #endregion

        #region Aurora
        /// <summary>
        ///  Four great soft lights in the palette's colours, each on its own slow orbit, so the
        ///  colour wells up in one corner and fades in another without ever repeating exactly.
        /// </summary>
        private void DrawAurora(DrawingContext context, Size size, double t)
        {
            Color[] colours = [accent, keyword, variable, stringColour];
            double reach = Math.Max(size.Width, size.Height) * 0.48;

            for (int i = 0; i < colours.Length; i++)
            {
                double x = size.Width * (0.5 + 0.42 * Math.Sin(t * 0.045 * (i + 1) + i * 1.7));
                double y = size.Height * (0.4 + 0.32 * Math.Cos(t * 0.037 * (i + 1.3) + i * 2.3));
                double r = reach * (0.8 + 0.2 * Math.Sin(t * 0.11 + i));

                context.DrawEllipse(Glow(colours[i], 0x4A), null, new Point(x, y), r, r * 0.7);
            }

            // A band of light rippling across the top third, the curtain itself.
            for (int band = 0; band < 3; band++)
            {
                StreamGeometry curtain = new();

                using (StreamGeometryContext g = curtain.Open())
                {
                    double top = size.Height * (0.12 + band * 0.07);
                    g.BeginFigure(new Point(0, top), true);

                    for (double x = 0; x <= size.Width + 40; x += 40)
                    {
                        g.LineTo(new Point(x, top + 26 * Math.Sin(x / 170 + t * (0.35 + band * 0.12) + band * 2)));
                    }

                    g.LineTo(new Point(size.Width + 40, top + 160));
                    g.LineTo(new Point(0, top + 160));
                    g.EndFigure(true);
                }

                Color c = band == 1 ? keyword : accent;

                LinearGradientBrush fade = new()
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0x36, c.R, c.G, c.B), 0),
                        new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1),
                    },
                };

                context.DrawGeometry(fade, null, curtain);
            }
        }
        #endregion

        #region Rain
        private const double RainCell = 17;

        private const string RainGlyphs = "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ0123456789Z:.=*+<>";

        /// <summary>Glyphs already laid out, by glyph and brightness step. Rebuilt per theme.</summary>
        private readonly Dictionary<(int Glyph, int Level), FormattedText> glyphs = [];

        private const int Levels = 8;

        private FormattedText Glyph(int glyph, int level)
        {
            if (!glyphs.TryGetValue((glyph, level), out FormattedText? laid))
            {
                // The head of a column is near white; the trail is the accent fading out.
                // Kept well under the text's strength: the rain falls behind page headers that have
                // no panel of their own, and a bright glyph there reads as part of the title.
                Color c = level == Levels ? Mix(accent, text, 0.45) : accent;
                byte alpha = level == Levels ? (byte)0x80 : (byte)(0x0A + level * 0x0B);

                laid = new FormattedText(
                    RainGlyphs[glyph].ToString(),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Consolas, Cascadia Mono, MS Gothic, monospace"),
                    14,
                    new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B)));

                glyphs[(glyph, level)] = laid;
            }

            return laid;
        }

        private void DrawRain(DrawingContext context, Size size, double t)
        {
            int rows = (int)(size.Height / RainCell) + 1;

            foreach (Mote column in motes)
            {
                int head = (int)column.Y;
                int length = (int)column.Size;

                for (int k = 0; k < length; k++)
                {
                    int row = head - k;

                    if (row < 0 || row > rows)
                    {
                        continue;
                    }

                    // Each cell changes its glyph now and then, which is what makes it read as rain
                    // rather than as columns of text sliding down.
                    int pick = Math.Abs(HashCode.Combine(column.Kind, row, (int)(t * 1.5 + row * 0.37))) % RainGlyphs.Length;
                    int level = k == 0 ? Levels : Math.Max(0, (int)((1 - (double)k / length) * (Levels - 1)));

                    context.DrawText(Glyph(pick, level), new Point(column.X + 2, row * RainCell));
                }
            }
        }
        #endregion

        #region Stars
        private double meteor;

        private Point meteorFrom;

        private void DrawStars(DrawingContext context, Size size, double t)
        {
            // A faint nebula, so the dark is not flat.
            context.DrawEllipse(
                Glow(accent, 0x1C), null,
                new Point(size.Width * (0.7 + 0.05 * Math.Sin(t * 0.03)), size.Height * 0.3),
                size.Width * 0.45, size.Height * 0.35);
            context.DrawEllipse(
                Glow(keyword, 0x14), null,
                new Point(size.Width * 0.2, size.Height * (0.75 + 0.04 * Math.Cos(t * 0.025))),
                size.Width * 0.35, size.Height * 0.3);

            foreach (Mote star in motes)
            {
                double twinkle = 0.55 + 0.45 * Math.Sin(t * star.Spin + star.Phase);
                byte alpha = (byte)((star.Kind switch { 0 => 90, 1 => 150, _ => 210 }) * twinkle);
                Color c = star.Kind == 2 && star.Phase > 4 ? accentSoft : text;

                context.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B)), null,
                    new Point(star.X, star.Y), star.Size, star.Size);
            }

            if (meteor > 0)
            {
                double travelled = (1 - meteor) * 380;
                Point head = new(meteorFrom.X + travelled, meteorFrom.Y + travelled * 0.45);
                Point tail = new(head.X - 120, head.Y - 54);

                LinearGradientBrush streak = new()
                {
                    StartPoint = new RelativePoint(tail, RelativeUnit.Absolute),
                    EndPoint = new RelativePoint(head, RelativeUnit.Absolute),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0, text.R, text.G, text.B), 0),
                        new GradientStop(Color.FromArgb((byte)(220 * Math.Min(1, meteor * 2)), text.R, text.G, text.B), 1),
                    },
                };

                context.DrawLine(new Pen(streak, 1.6, lineCap: PenLineCap.Round), tail, head);
            }
        }
        #endregion

        #region Grid
        /// <summary>
        ///  The synthwave horizon: a striped sun sinking behind a perspective grid that rolls
        ///  towards the viewer.
        /// </summary>
        private void DrawGrid(DrawingContext context, Size size, double t)
        {
            double w = size.Width;
            double h = size.Height;
            double horizon = h * 0.6;
            double radius = Math.Min(w, h) * 0.24;
            Point centre = new(w * 0.5, horizon);

            // The glow the sun throws on the sky.
            context.DrawEllipse(Glow(accent, 0x30), null, centre, radius * 2.6, radius * 1.6);

            // The sun, cut off at the horizon, banded across its lower half.
            using (context.PushClip(new Rect(0, 0, w, horizon)))
            {
                LinearGradientBrush sun = new()
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0x70, warn.R, warn.G, warn.B), 0),
                        new GradientStop(Color.FromArgb(0x70, accent.R, accent.G, accent.B), 1),
                    },
                };

                context.DrawEllipse(sun, null, centre, radius, radius);

                SolidColorBrush gap = new(ink);

                for (int band = 0; band < 6; band++)
                {
                    double y = horizon - radius * 0.62 + band * radius * 0.12;
                    context.FillRectangle(gap, new Rect(centre.X - radius, y, radius * 2, 1.5 + band * 1.3));
                }
            }

            // The ground: a dark plane with the grid on it.
            LinearGradientBrush ground = new()
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0x30, keyword.R, keyword.G, keyword.B), 0),
                    new GradientStop(Color.FromArgb(0x08, keyword.R, keyword.G, keyword.B), 1),
                },
            };

            context.FillRectangle(ground, new Rect(0, horizon, w, h - horizon));

            Pen horizonLine = new(new SolidColorBrush(Color.FromArgb(0xA0, accent.R, accent.G, accent.B)), 1.5);
            context.DrawLine(horizonLine, new Point(0, horizon), new Point(w, horizon));

            // Lines across, bunched towards the horizon and rolling forward.
            const int across = 14;
            double roll = t * 0.35 % 1;

            for (int k = 0; k < across; k++)
            {
                double z = (k + roll) / across;
                double y = horizon + (h - horizon) * z * z;
                byte alpha = (byte)(0x18 + 0x70 * z);

                context.DrawLine(
                    new Pen(new SolidColorBrush(Color.FromArgb(alpha, accent.R, accent.G, accent.B)), 1),
                    new Point(0, y), new Point(w, y));
            }

            // Lines into the distance, all meeting at the centre of the horizon.
            Pen depth = new(new SolidColorBrush(Color.FromArgb(0x50, accent.R, accent.G, accent.B)), 1);

            for (int i = -16; i <= 16; i++)
            {
                double spread = w / 7;
                context.DrawLine(depth, new Point(centre.X + i * spread * 0.06, horizon), new Point(centre.X + i * spread, h));
            }
        }
        #endregion

        #region Embers, snow, petals
        private void DrawEmbers(DrawingContext context, Size size)
        {
            // Heat at the bottom of the window, where the sparks come from.
            context.DrawEllipse(
                Glow(accent, 0x26), null,
                new Point(size.Width * 0.5, size.Height * 1.05),
                size.Width * 0.6, size.Height * 0.35);

            foreach (Mote spark in motes)
            {
                Color c = spark.Kind switch { 0 => warn, 1 => accent, _ => bad };
                double flicker = 0.7 + 0.3 * Math.Sin(clock.Elapsed.TotalSeconds * 9 * spark.Spin + spark.Phase);
                double life = Math.Clamp(spark.Life, 0, 1);
                Point at = new(spark.X, spark.Y);

                context.DrawEllipse(Glow(c, (byte)(0x40 * life * flicker)), null, at, spark.Size * 4, spark.Size * 4);
                context.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb((byte)(0xE0 * life * flicker), c.R, c.G, c.B)), null,
                    at, spark.Size, spark.Size);
            }
        }

        private void DrawSnow(DrawingContext context)
        {
            foreach (Mote flake in motes)
            {
                context.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb((byte)(0xFF * flake.Life), text.R, text.G, text.B)), null,
                    new Point(flake.X, flake.Y), flake.Size, flake.Size);
            }
        }

        private void DrawPetals(DrawingContext context)
        {
            foreach (Mote petal in motes)
            {
                Color c = petal.Kind == 0 ? accent : accentSoft;
                SolidColorBrush fill = new(Color.FromArgb((byte)(0xFF * petal.Life), c.R, c.G, c.B));

                // Turning about its own centre, and squashed as it turns, so it reads as tumbling
                // rather than as a flat shape spinning.
                Matrix turn = Matrix.CreateScale(1, 0.45 + 0.55 * Math.Abs(Math.Cos(petal.Phase * 0.7)))
                    * Matrix.CreateRotation(petal.Phase)
                    * Matrix.CreateTranslation(petal.X, petal.Y);

                using (context.PushTransform(turn))
                {
                    context.DrawEllipse(fill, null, default, petal.Size, petal.Size * 0.62);
                }
            }
        }
        #endregion

        /// <summary>A soft round light: the colour at the centre, nothing at the edge.</summary>
        private static RadialGradientBrush Glow(Color c, byte alpha) => new()
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(alpha, c.R, c.G, c.B), 0),
                new GradientStop(Color.FromArgb((byte)(alpha / 3), c.R, c.G, c.B), 0.55),
                new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1),
            },
        };

        private static Color Mix(Color a, Color b, double amount) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * amount),
            (byte)(a.G + (b.G - a.G) * amount),
            (byte)(a.B + (b.B - a.B) * amount));
    }
}
