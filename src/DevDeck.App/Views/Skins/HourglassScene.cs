using Avalonia;
using Avalonia.Media;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  An hourglass in a walnut and brass frame.
    /// </summary>
    /// <remarks>
    ///  The sand is memory: what is still in the top bulb is free, what has piled up in the bottom
    ///  is in use, so a machine filling up looks like time running out. The stream through the
    ///  neck is the processor - a thread of a few grains at idle, a thick fast pour under load.
    ///  Past the high mark the sand turns amber, and in the red it glows red. A clean turns the
    ///  glass over: it swings through half a turn, lands with the used sand back on top, and pours
    ///  out at full speed until the new reading settles. The memory figure sits to the left, the
    ///  processor, graphics and disk to the right, so the glass keeps the middle.
    /// </remarks>
    internal sealed class HourglassScene : SkinScene
    {
        // The glass is drawn about its neck, so a flip is a rotation about one point.
        private const double GlassX = 124;
        private const double NeckY = 115;

        // Half the height of one bulb, its widest half-width, and the half-width of the neck.
        private const double H = 72;
        private const double W = 26;
        private const double Neck = 2.2;

        private const double PostX = 32;
        private const double ReadoutX = 46;
        private const double CounterX = 196;

        private const double FlipTime = 0.8;
        private const double PourTime = 1.8;
        private const double Gravity = 420;

        // Fine steps for the volume tables, coarser ones for the outline.
        private const int Steps = 120;
        private const int OutlineSteps = 36;

        private static readonly Color CardTop = Color.FromArgb(0xEB, 0x23, 0x1B, 0x15);
        private static readonly Color CardBottom = Color.FromArgb(0xEB, 0x0E, 0x0B, 0x0A);
        private static readonly Color WoodLight = Color.FromRgb(0x7A, 0x47, 0x27);
        private static readonly Color WoodDark = Color.FromRgb(0x3A, 0x20, 0x10);
        private static readonly Color BrassLight = Color.FromRgb(0xF4, 0xD9, 0x8E);
        private static readonly Color BrassDark = Color.FromRgb(0x84, 0x5C, 0x1E);
        private static readonly Color GlassEdge = Color.FromRgb(0xD8, 0xE6, 0xF0);
        private static readonly Color SandLight = Color.FromRgb(0xF7, 0xD3, 0x7A);
        private static readonly Color SandDark = Color.FromRgb(0xBE, 0x86, 0x2C);
        private static readonly Color Cream = Color.FromRgb(0xF4, 0xE9, 0xD0);
        private static readonly Color Faint = Color.FromRgb(0xB3, 0xA2, 0x84);
        private static readonly Color White = Colors.White;
        private static readonly Color Black = Colors.Black;

        private static readonly double[] Posts = [-PostX, PostX];
        private static readonly double[] Beads = [-58, 0, 58];
        private static readonly double[] Rings = [-70, 70];

        // Sand area from the neck up, and from the base up, so a share of memory becomes a level
        // that fills the curved bulb honestly rather than a straight fraction of its height.
        private static readonly double[] FromNeck = Areas(fromBase: false);
        private static readonly double[] FromBase = Areas(fromBase: true);

        // Most of a bulb, so a glass that is all free still shows air above the sand.
        private static readonly double Capacity = FromNeck[Steps] * 0.8;

        private static readonly Geometry GlassShape = Outline();
        private static readonly Geometry TopShine = Streak(0.3, 0.85, 4.5, -1, -1);
        private static readonly Geometry BottomShine = Streak(0.22, 0.72, 4.5, 1, -1);
        private static readonly Geometry TopGlint = Streak(0.45, 0.7, 3.5, -1, 1);
        private static readonly Geometry BottomGlint = Streak(0.35, 0.6, 3.5, 1, 1);

        private struct Grain
        {
            public double X;
            public double Y;
            public double Vx;
            public double Vy;
            public bool Alive;
            public bool Bounced;
        }

        private struct Speck
        {
            public double X;
            public double Y;
            public bool Dark;
        }

        private readonly Grain[] grains = new Grain[90];
        private readonly Speck[] specks = new Speck[60];
        private int nextGrain;
        private double grainClock;

        /// <summary>0 to 1 through a flip; negative when the glass is standing still.</summary>
        private double flip = -1;

        /// <summary>
        ///  1 just after a flip, when the used sand is on top, falling to 0 as it pours back down.
        /// </summary>
        private double swap;

        /// <summary>1 as the glass lands, fading, for the bump and the flash of light.</summary>
        private double settle;

        /// <summary>The memory band, eased, so the sand warms to amber rather than switching.</summary>
        private double heat;

        // The shape of the sand this frame, worked out in Advance and drawn in Draw.
        private double share;
        private double topLevel;
        private double peakY;
        private double wallY;
        private bool flowing;

        protected override void Seed()
        {
            for (int i = 0; i < specks.Length; i++)
            {
                specks[i] = new Speck
                {
                    X = (Random.NextDouble() - 0.5) * W * 2,
                    Y = (Random.NextDouble() - 0.5) * H * 2,
                    Dark = Random.NextDouble() < 0.6,
                };
            }

            heat = MemoryLevel / 2.0;
            Shape();
        }

        protected override void CleanStarted()
        {
            flip = 0;

            // Grains in the air would hang upside down through the turn.
            for (int i = 0; i < grains.Length; i++)
            {
                grains[i].Alive = false;
            }
        }

        protected override void Advance(double dt)
        {
            heat += (MemoryLevel / 2.0 - heat) * (1 - Math.Exp(-dt * 2));

            if (flip >= 0)
            {
                flip += dt / FlipTime;

                if (flip >= 1)
                {
                    // Landed: the glass is upright again, with what was below now above.
                    flip = -1;
                    swap = 1;
                    settle = 1;
                }
            }
            else
            {
                // Held part-full while the clean runs, so the pour ends with the new reading.
                double floor = Busy ? 0.3 : 0;

                if (swap > floor)
                {
                    swap = Math.Max(floor, swap - dt / PourTime);
                }
            }

            settle = Math.Max(0, settle - dt * 1.6);

            Shape();
            Pour(dt);
        }

        /// <summary>Sand levels for this frame, from memory and how far a flip has poured back.</summary>
        private void Shape()
        {
            share = (1 - Memory) * (1 - swap) + Memory * swap;
            topLevel = LevelFor(FromNeck, share * Capacity);

            double below = 1 - share;
            double bottomLevel = LevelFor(FromBase, below * Capacity);
            double baseY = H - bottomLevel;

            // A cone where the stream lands, flattening to nothing as the pile runs out.
            double cone = Math.Min(7, 1.5 + bottomLevel * 0.25) * Math.Min(1, below * 12);
            peakY = Math.Max(4, baseY - cone * 0.7);
            wallY = baseY + cone * 0.3;

            flowing = flip < 0 && share > 0.004;
        }

        private double StreamWidth => swap > 0 ? 2.6 : 0.5 + Cpu * 1.6;

        private double SurfaceAt(double x) => peakY + (wallY - peakY) * Math.Min(1, Math.Abs(x) / (W * 0.8));

        private void Pour(double dt)
        {
            double rate = !flowing ? 0 : swap > 0 ? 180 : 22 + Cpu * 150;
            grainClock += dt * rate;

            while (grainClock >= 1)
            {
                grainClock -= 1;

                ref Grain g = ref grains[nextGrain];
                nextGrain = (nextGrain + 1) % grains.Length;

                g.X = (Random.NextDouble() - 0.5) * (StreamWidth + 0.6);
                g.Y = 1;
                g.Vx = (Random.NextDouble() - 0.5) * 4;
                g.Vy = 20 + Random.NextDouble() * 25;
                g.Alive = true;
                g.Bounced = false;
            }

            for (int i = 0; i < grains.Length; i++)
            {
                ref Grain g = ref grains[i];

                if (!g.Alive)
                {
                    continue;
                }

                g.Vy += Gravity * dt;
                g.X += g.Vx * dt;
                g.Y += g.Vy * dt;

                double surface = SurfaceAt(g.X);

                if (g.Y < surface)
                {
                    continue;
                }

                // Some grains kick off the pile and roll a little way down it.
                if (!g.Bounced && Random.NextDouble() < 0.35)
                {
                    g.Bounced = true;
                    g.Y = surface - 0.5;
                    g.Vy = -g.Vy * 0.18;
                    g.Vx = (Random.NextDouble() - 0.5) * 44;
                }
                else
                {
                    g.Alive = false;
                }
            }
        }

        protected override void Draw(DrawingContext context)
        {
            DrawCard(context);
            DrawReadout(context);
            DrawCounters(context);
            DrawHourglass(context);
        }

        /// <summary>A dark rounded card, so the brass reads on a white desktop as well as a black one.</summary>
        private void DrawCard(DrawingContext context)
        {
            Rect card = new(4, 4, Side - 8, Side - 8);
            context.DrawRectangle(Fade(CardTop, CardBottom), Pen(Hovered ? Accent : White, Hovered ? 0.7 : 0.08, 1.2), card, 22, 22);

            // Lamplight behind the glass, brighter as it lands after a flip.
            Color sand = Sand(SandLight);
            double light = 0.12 + settle * 0.3 + (Busy ? 0.1 : 0);
            context.DrawEllipse(Glow(sand, light), null, new Point(GlassX, NeckY), 62, 100);

            // The shadow it stands on.
            context.DrawEllipse(Glow(Black, 0.6), null, new Point(GlassX, NeckY + 92), 46, 6);
        }

        private void DrawHourglass(DrawingContext context)
        {
            double turn = flip >= 0 ? Ease(flip) : 0;
            double lift = flip >= 0 ? Math.Sin(Math.PI * flip) : 0;
            double scale = 1 - 0.1 * lift;
            double bump = Math.Sin((1 - settle) * Math.PI * 2) * settle * 1.4;

            // Lifted a little and turned about the neck, then set back down with a small bump.
            Matrix place = Matrix.CreateScale(scale, scale)
                * Matrix.CreateRotation(Math.PI * turn)
                * Matrix.CreateTranslation(GlassX, NeckY - lift * 3 + bump);

            using (context.PushTransform(place))
            {
                DrawPosts(context);

                context.DrawGeometry(Fade(WithAlpha(White, 0.12), WithAlpha(White, 0.03), horizontal: true), null, GlassShape);

                using (context.PushGeometryClip(GlassShape))
                {
                    DrawSand(context);
                    DrawStream(context);
                }

                context.DrawGeometry(null, Pen(White, 0.5, 1.6), TopShine);
                context.DrawGeometry(null, Pen(White, 0.4, 1.6), BottomShine);
                context.DrawGeometry(null, Pen(White, 0.18, 1), TopGlint);
                context.DrawGeometry(null, Pen(White, 0.14, 1), BottomGlint);
                context.DrawGeometry(null, Pen(GlassEdge, 0.5, 1.1), GlassShape);

                DrawPlates(context);
            }
        }

        private void DrawPosts(DrawingContext context)
        {
            IBrush brass = Fade(BrassLight, BrassDark, horizontal: true);

            foreach (double x in Posts)
            {
                context.DrawRectangle(brass, null, new Rect(x - 1.7, -76, 3.4, 152));
                context.DrawLine(Pen(White, 0.35, 0.6), new Point(x - 0.7, -74), new Point(x - 0.7, 74));

                // Turned beads and collars, the way a lathe would leave them.
                foreach (double y in Beads)
                {
                    context.DrawEllipse(brass, null, new Point(x, y), 3.4, 4.8);
                    context.DrawEllipse(Solid(White, 0.45), null, new Point(x - 1.2, y - 1.6), 0.9, 1.3);
                }

                foreach (double y in Rings)
                {
                    context.DrawEllipse(brass, null, new Point(x, y), 3, 1.8);
                }
            }
        }

        private void DrawPlates(DrawingContext context)
        {
            IBrush wood = Fade(WoodLight, WoodDark);
            IBrush brass = Fade(BrassLight, BrassDark);
            IPen grain = Pen(WoodDark, 0.6, 0.6);

            // Top: the walnut plate, a brass lip where it holds the glass, and a finial.
            context.DrawEllipse(brass, null, new Point(0, -88), 6, 2.2);
            context.DrawRectangle(wood, null, new Rect(-38, -87, 76, 11), 3.5, 3.5);
            context.DrawLine(Pen(White, 0.2, 0.8), new Point(-35, -86.2), new Point(35, -86.2));
            context.DrawLine(grain, new Point(-30, -82.5), new Point(10, -83.2));
            context.DrawLine(grain, new Point(-6, -79.5), new Point(31, -80.2));
            context.DrawRectangle(brass, null, new Rect(-33, -76, 66, 4), 1.5, 1.5);

            // Bottom: the same, with two brass feet.
            context.DrawRectangle(brass, null, new Rect(-33, 72, 66, 4), 1.5, 1.5);
            context.DrawRectangle(wood, null, new Rect(-38, 76, 76, 11), 3.5, 3.5);
            context.DrawLine(Pen(White, 0.2, 0.8), new Point(-35, 76.8), new Point(35, 76.8));
            context.DrawLine(grain, new Point(-28, 80.5), new Point(14, 79.8));
            context.DrawLine(grain, new Point(-10, 83.5), new Point(30, 82.8));
            context.DrawEllipse(brass, null, new Point(-29, 88), 5, 1.8);
            context.DrawEllipse(brass, null, new Point(29, 88), 5, 1.8);
        }

        private void DrawSand(DrawingContext context)
        {
            Color light = Sand(SandLight);
            Color dark = Sand(SandDark);
            IBrush fill = Fade(light, dark);
            IPen rim = Pen(Mix(light, White, 0.35), 0.7, 0.8);

            // The free sand, dimpled over the neck while it runs out.
            if (share > 0.002)
            {
                double sy = -topLevel;
                double dip = flowing ? Math.Min(2.5 + Cpu * 2, topLevel * 0.5) : 0;
                double span = Math.Min(W * 0.7, HalfWidth(topLevel) * 0.8);

                Geometry top = StreamGeometry.Parse(FormattableString.Invariant(
                    $"M{-W - 4},{sy} L{-span},{sy} C{-span * 0.4},{sy} {-span * 0.25},{sy + dip} 0,{sy + dip} C{span * 0.25},{sy + dip} {span * 0.4},{sy} {span},{sy} L{W + 4},{sy} L{W + 4},1 L{-W - 4},1 Z"));

                // Stroked as well as filled: the clip hides every edge but the surface.
                context.DrawGeometry(fill, rim, top);
            }

            // The used sand: a mound, with a cone where the stream lands.
            if (share < 0.998)
            {
                Geometry pile = StreamGeometry.Parse(FormattableString.Invariant(
                    $"M{-W - 4},{wallY} L{-W * 0.9},{wallY} C{-W * 0.45},{wallY} -5,{peakY + 1.5} 0,{peakY} C5,{peakY + 1.5} {W * 0.45},{wallY} {W * 0.9},{wallY} L{W + 4},{wallY} L{W + 4},{H + 4} L{-W - 4},{H + 4} Z"));

                context.DrawGeometry(fill, rim, pile);
            }

            // A few darker and brighter grains in the body of the sand, so it reads as sand.
            foreach (Speck s in specks)
            {
                bool inTop = s.Y < 0 && s.Y > -topLevel + 1 && share > 0.002;
                bool inPile = s.Y > 0 && s.Y > SurfaceAt(s.X) + 1.5 && share < 0.998;

                if (inTop || inPile)
                {
                    context.DrawEllipse(Solid(s.Dark ? dark : White, s.Dark ? 0.55 : 0.35), null, new Point(s.X, s.Y), 0.6, 0.6);
                }
            }
        }

        private void DrawStream(DrawingContext context)
        {
            Color light = Sand(SandLight);

            if (flowing)
            {
                double width = StreamWidth;
                double sway = Math.Sin(Time * 9) * 0.25;

                context.DrawLine(Pen(light, 0.25, width + 1.5), new Point(0, -1), new Point(sway, peakY));
                context.DrawLine(Pen(light, 0.85, width), new Point(0, -1), new Point(sway, peakY));
            }

            foreach (Grain g in grains)
            {
                if (g.Alive)
                {
                    context.DrawEllipse(Solid(light), null, new Point(g.X, g.Y), 0.75, 0.75);
                }
            }
        }

        private void DrawReadout(DrawingContext context)
        {
            Text(context, MemoryCaption, new Point(ReadoutX, 88), 8, Faint);

            if (Flashing)
            {
                Text(context, FlashText, new Point(ReadoutX, 110), 17, FlashGood ? Good : Cream, bold: true);

                if (FlashGood)
                {
                    Text(context, FreedCaption, new Point(ReadoutX, 128), 8, Faint);
                }

                return;
            }

            Text(context, MemoryText, new Point(ReadoutX, 110), 26, LevelColour(MemoryLevel, Cream), bold: true);
            Text(context, Hovered ? ClickToClean : UsedOfTotal, new Point(ReadoutX, 130), 7.5, Hovered ? Accent : Faint);

            // A brass rule, so the figure reads as a plaque rather than loose text.
            context.DrawLine(Pen(BrassLight, 0.4, 0.8), new Point(ReadoutX - 18, 141), new Point(ReadoutX + 18, 141));
        }

        /// <summary>Processor, graphics and disk, each with a small brass gauge under its figure.</summary>
        private void DrawCounters(DrawingContext context)
        {
            DrawCounter(context, 0, CpuCaption, CpuText, Cpu, LevelColour(CpuLevel, BrassLight));
            DrawCounter(context, 1, GpuCaption, GpuText, Gpu, BrassLight);
            DrawCounter(context, 2, DiskCaption, DiskText, Disk, BrassLight);
        }

        private void DrawCounter(DrawingContext context, int row, string caption, string value, double level, Color colour)
        {
            double y = 76 + row * 36;

            Text(context, caption, new Point(CounterX, y), 7, Faint);
            Text(context, value, new Point(CounterX, y + 12), 11, Mix(colour, Cream, 0.3), bold: true, mono: true);

            context.DrawRectangle(Solid(White, 0.12), null, new Rect(CounterX - 16, y + 21, 32, 2.5), 1.25, 1.25);

            if (level > 0.005)
            {
                context.DrawRectangle(Solid(colour), null, new Rect(CounterX - 16, y + 21, 32 * level, 2.5), 1.25, 1.25);
            }
        }

        /// <summary>
        ///  The sand's colour for the memory band: gold, amber when high, red when critical. Stepped
        ///  in sixteenths, so the easing between bands reuses a few brushes instead of one a frame.
        /// </summary>
        private Color Sand(Color cool)
        {
            double h = Math.Round(heat * 16) / 16;
            Color warm = Mix(cool, Warn, 0.6);
            Color hot = Mix(cool, Bad, 0.7);

            return h <= 0.5 ? Mix(cool, warm, h * 2) : Mix(warm, hot, (h - 0.5) * 2);
        }

        private static double Ease(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

        /// <summary>The glass's half-width at a distance from the neck: a funnel, a full bulb, a rounded shoulder.</summary>
        private static double HalfWidth(double t)
        {
            double u = Math.Clamp(t / H, 0, 1);
            double swell = Math.Sin(Math.Min(u / 0.62, 1) * Math.PI / 2);
            double shoulder = u > 0.8 ? 1 - 0.28 * Math.Pow((u - 0.8) / 0.2, 2) : 1;

            return Neck + (W - Neck) * Math.Pow(swell, 1.2) * shoulder;
        }

        private static double[] Areas(bool fromBase)
        {
            double[] areas = new double[Steps + 1];

            for (int i = 1; i <= Steps; i++)
            {
                double s0 = H * (i - 1) / Steps;
                double s1 = H * i / Steps;
                double w0 = HalfWidth(fromBase ? H - s0 : s0);
                double w1 = HalfWidth(fromBase ? H - s1 : s1);

                // Full width is twice the half-width, so the trapezium is (w0 + w1) times the step.
                areas[i] = areas[i - 1] + (w0 + w1) * (s1 - s0);
            }

            return areas;
        }

        /// <summary>The height, from wherever the table starts, that holds this much sand.</summary>
        private static double LevelFor(double[] table, double area)
        {
            if (area <= 0)
            {
                return 0;
            }

            for (int i = 1; i <= Steps; i++)
            {
                if (table[i] >= area)
                {
                    double part = (area - table[i - 1]) / (table[i] - table[i - 1]);
                    return H * (i - 1 + part) / Steps;
                }
            }

            return H;
        }

        private static Geometry Outline()
        {
            StreamGeometry glass = new();

            using (StreamGeometryContext c = glass.Open())
            {
                c.BeginFigure(new Point(HalfWidth(H), -H), true);

                for (int i = OutlineSteps; i >= 0; i--)
                {
                    double t = H * i / OutlineSteps;
                    c.LineTo(new Point(HalfWidth(t), -t));
                }

                for (int i = 1; i <= OutlineSteps; i++)
                {
                    double t = H * i / OutlineSteps;
                    c.LineTo(new Point(HalfWidth(t), t));
                }

                for (int i = OutlineSteps; i >= 0; i--)
                {
                    double t = H * i / OutlineSteps;
                    c.LineTo(new Point(-HalfWidth(t), t));
                }

                for (int i = 1; i <= OutlineSteps; i++)
                {
                    double t = H * i / OutlineSteps;
                    c.LineTo(new Point(-HalfWidth(t), -t));
                }

                c.EndFigure(true);
            }

            return glass;
        }

        /// <summary>A highlight following the inside of one bulb, on one side.</summary>
        private static Geometry Streak(double from, double to, double inset, int bulb, int side)
        {
            StreamGeometry streak = new();

            using (StreamGeometryContext c = streak.Open())
            {
                const int count = 16;

                for (int i = 0; i <= count; i++)
                {
                    double t = H * (from + (to - from) * i / count);
                    Point p = new(side * (HalfWidth(t) - inset), bulb * t);

                    if (i == 0)
                    {
                        c.BeginFigure(p, false);
                    }
                    else
                    {
                        c.LineTo(p);
                    }
                }

                c.EndFigure(false);
            }

            return streak;
        }
    }
}
