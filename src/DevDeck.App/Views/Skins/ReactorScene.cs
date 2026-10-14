using Avalonia;
using Avalonia.Media;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  A reactor core seen from above, in its round metal housing.
    /// </summary>
    /// <remarks>
    ///  The plasma in the middle is memory in use: a cool blue at rest, amber past the high mark and
    ///  an angry red in the critical band, swelling and beating faster as memory fills. The three
    ///  rings round it are the processor, the graphics card and the disk, outermost first; each spins
    ///  with its own load and carries a lit gauge of it along its inner edge. Sparks orbit between
    ///  the rings and arcs jump across them, more of both the harder the processor works, and in the
    ///  red the housing itself throbs red under a warning triangle. A clean vents coolant: steam
    ///  bursts outward, the rings spin down and the core dims, then it all winds back up once the
    ///  clean is over.
    /// </remarks>
    internal sealed class ReactorScene : SkinScene
    {
        private const double Housing = 113;
        private const double Floor = 99;
        private const double Band = 106;
        private const double Window = 39;

        // Outermost first: processor, graphics, disk.
        private static readonly double[] RingRadius = [88, 74, 60];
        private static readonly double[] RingWidth = [6, 5, 4];
        private static readonly int[] NodeCount = [3, 2, 4];

        private static readonly Color MetalTop = Color.FromRgb(0x4C, 0x55, 0x66);
        private static readonly Color MetalBottom = Color.FromRgb(0x17, 0x1B, 0x24);
        private static readonly Color FloorTop = Color.FromRgb(0x0C, 0x12, 0x1F);
        private static readonly Color FloorBottom = Color.FromRgb(0x04, 0x06, 0x0D);
        private static readonly Color Groove = Color.FromRgb(0x03, 0x05, 0x0A);
        private static readonly Color Slot = Color.FromRgb(0x0E, 0x10, 0x15);
        private static readonly Color Hazard = Color.FromRgb(0xF2, 0xC0, 0x1E);
        private static readonly Color BoltDark = Color.FromRgb(0x14, 0x17, 0x1E);
        private static readonly Color BoltLight = Color.FromRgb(0x8D, 0x96, 0xA8);
        private static readonly Color PlateFill = Color.FromRgb(0x08, 0x0B, 0x12);
        private static readonly Color WindowFill = Color.FromRgb(0x04, 0x07, 0x10);
        private static readonly Color CoreCool = Color.FromRgb(0x4D, 0xD8, 0xFF);
        private static readonly Color CoreAmber = Color.FromRgb(0xFF, 0xAA, 0x2B);
        private static readonly Color CoreRed = Color.FromRgb(0xFF, 0x34, 0x2E);
        private static readonly Color CpuBlue = Color.FromRgb(0x33, 0x9C, 0xFF);
        private static readonly Color GpuViolet = Color.FromRgb(0xB2, 0x7D, 0xFF);
        private static readonly Color DiskMint = Color.FromRgb(0x48, 0xEE, 0xAE);
        private static readonly Color Coolant = Color.FromRgb(0xD8, 0xF6, 0xFF);
        private static readonly Color Faint = Color.FromRgb(0x9A, 0xA8, 0xC6);
        private static readonly Color White = Colors.White;
        private static readonly Color Black = Colors.Black;

        private static readonly EllipseGeometry Outline = Circle(Centre, Housing);

        // Everything that never moves is built once; only the gauges and the arcs are per frame.
        private static readonly Geometry[] RingSegments =
        [
            Segments(88, 24, 4),
            Segments(74, 16, 6),
            Segments(60, 10, 10),
        ];

        private static readonly Geometry Ticks = BuildTicks();
        private static readonly Geometry Spokes = BuildSpokes();
        private static readonly Geometry StripeSlots = BuildStripeSlots();
        private static readonly Geometry Stripes = BuildStripes();

        // The warning triangle sits in the housing band at twelve o'clock.
        private static readonly Geometry Triangle = StreamGeometry.Parse("M115,3.4 L121.6,14.2 L108.4,14.2 Z");
        private static readonly Point MarkTop = new(115, 6.9);
        private static readonly Point MarkBottom = new(115, 10.3);
        private static readonly Point MarkDot = new(115, 12.2);

        // Clear of the stripes, the triangle and the label plates along the bottom.
        private static readonly Point[] Bolts =
            [.. new double[] { 338, 0, 22, 42, 138, 158, 180, 202, 250, 290 }.Select(a => At(Band, a))];

        // Where the processor, graphics and disk plates sit on the band, left to right along the bottom.
        private static readonly double[] PlateAngle = [118, 90, 62];

        private struct Spark
        {
            public double Radius;
            public double Angle;
            public double Speed;
            public double Size;
            public double Phase;
        }

        private struct Zap
        {
            public Point[] Points;
            public double Life;
            public int Ring;
        }

        private struct Puff
        {
            public double X;
            public double Y;
            public double Vx;
            public double Vy;
            public double Life;
            public double Size;
        }

        private readonly Spark[] sparks = new Spark[36];
        private readonly Zap[] zaps = new Zap[8];
        private readonly Puff[] puffs = new Puff[80];
        private readonly double[] turn = new double[3];
        private int nextZap;
        private int nextPuff;
        private double zapClock;
        private double puffClock;

        /// <summary>1 when running, near 0 while a clean has the rings spun down.</summary>
        private double spin = 1;

        /// <summary>1 when burning, lower while coolant is going in; recovers slowly, as a reactor would.</summary>
        private double dim = 1;

        /// <summary>The memory band, eased, so the core warms and cools through amber rather than snapping.</summary>
        private double heat;

        // Accumulated rather than Time times a rate: the rate follows the readings, and a changing rate
        // multiplied by hours of Time would make the pulse jump.
        private double corePhase;
        private double coreTurn;

        protected override void Seed()
        {
            heat = MemoryLevel;

            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = new Spark
                {
                    Radius = 44 + Random.NextDouble() * 50,
                    Angle = Random.NextDouble() * 360,
                    Speed = (20 + Random.NextDouble() * 40) * (Random.Next(2) == 0 ? 1 : -1),
                    Size = 0.7 + Random.NextDouble() * 0.9,
                    Phase = Random.NextDouble() * Math.Tau,
                };
            }

            for (int i = 0; i < zaps.Length; i++)
            {
                zaps[i].Points = new Point[5];
            }
        }

        private double Load(int ring) => ring switch
        {
            0 => Cpu,
            1 => Gpu,
            _ => Disk,
        };

        protected override void Advance(double dt)
        {
            spin += ((Busy ? 0.06 : 1) - spin) * (1 - Math.Exp(-dt * (Busy ? 1.8 : 0.6)));
            dim += ((Busy ? 0.3 : 1) - dim) * (1 - Math.Exp(-dt * (Busy ? 2.2 : 0.5)));
            heat += (MemoryLevel - heat) * (1 - Math.Exp(-dt * 1.5));

            // Neighbouring rings turn opposite ways, so the motion reads as machinery, not a spinning plate.
            for (int i = 0; i < turn.Length; i++)
            {
                turn[i] = (turn[i] + dt * spin * (8 + Load(i) * 220) * (i % 2 == 0 ? 1 : -1)) % 360;
            }

            corePhase += dt * (2 + Memory * 4 + (MemoryLevel == 2 ? 4 : 0));
            coreTurn += dt * (25 + Memory * 70) * (0.3 + 0.7 * spin);

            for (int i = 0; i < sparks.Length; i++)
            {
                ref Spark s = ref sparks[i];
                s.Angle = (s.Angle + dt * spin * s.Speed * (0.5 + Cpu * 2.5)) % 360;
            }

            for (int i = 0; i < zaps.Length; i++)
            {
                zaps[i].Life -= dt * 5;
            }

            zapClock += dt * spin * (0.3 + Cpu * 7);

            while (zapClock >= 1)
            {
                zapClock -= 1;
                SpawnZap();
            }

            if (Busy)
            {
                puffClock += dt * 26;

                while (puffClock >= 1)
                {
                    puffClock -= 1;
                    SpawnPuff(40, 100);
                }
            }

            for (int i = 0; i < puffs.Length; i++)
            {
                ref Puff p = ref puffs[i];

                if (p.Life <= 0)
                {
                    continue;
                }

                double drag = Math.Exp(-dt * 1.2);
                p.Vx *= drag;
                p.Vy *= drag;
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                p.Size += dt * 14;
                p.Life -= dt * 0.75;
            }
        }

        protected override void CleanStarted()
        {
            // The vent opens all at once, then keeps breathing while the clean runs.
            for (int i = 0; i < 44; i++)
            {
                SpawnPuff(80, 170);
            }
        }

        private void SpawnPuff(double slowest, double fastest)
        {
            ref Puff p = ref puffs[nextPuff];
            nextPuff = (nextPuff + 1) % puffs.Length;

            double a = Random.NextDouble() * Math.Tau;
            double v = slowest + Random.NextDouble() * (fastest - slowest);

            p.X = Centre.X + Math.Cos(a) * Window;
            p.Y = Centre.Y + Math.Sin(a) * Window;
            p.Vx = Math.Cos(a) * v;
            p.Vy = Math.Sin(a) * v;
            p.Life = 0.7 + Random.NextDouble() * 0.3;
            p.Size = 3 + Random.NextDouble() * 4;
        }

        private void SpawnZap()
        {
            ref Zap z = ref zaps[nextZap];
            nextZap = (nextZap + 1) % zaps.Length;

            // From the inner edge of one ring to the outer edge of the next, or from the disk ring into the core.
            int ring = Random.Next(3);
            double from = RingRadius[ring] - RingWidth[ring] / 2;
            double to = ring < 2 ? RingRadius[ring + 1] + RingWidth[ring + 1] / 2 : Window + 3;
            double angle = Random.NextDouble() * 360;

            for (int k = 0; k < z.Points.Length; k++)
            {
                double t = k / (double)(z.Points.Length - 1);
                double jitter = k == 0 || k == z.Points.Length - 1 ? 0 : (Random.NextDouble() - 0.5) * 9;
                z.Points[k] = At(from + (to - from) * t, angle + jitter);
            }

            z.Ring = ring;
            z.Life = 1;
        }

        private Color RingColour(int ring) => ring switch
        {
            0 => LevelColour(CpuLevel, CpuBlue),
            1 => GpuViolet,
            _ => DiskMint,
        };

        /// <summary>Cool, amber or red, washed towards coolant white while the core is dimmed.</summary>
        private Color CoreColour()
        {
            // Stepped, so the brushes it picks come from a short list rather than one per frame.
            double h = Math.Round(Math.Clamp(heat, 0, 2) * 12) / 12;
            Color c = h <= 1 ? Mix(CoreCool, CoreAmber, h) : Mix(CoreAmber, CoreRed, h - 1);

            return Mix(c, Coolant, Math.Round((1 - dim) * 10) / 10);
        }

        protected override void Draw(DrawingContext context)
        {
            Color core = CoreColour();

            using (context.PushGeometryClip(Outline))
            {
                DrawHousing(context);
                DrawFloor(context, core);
                DrawRings(context);
                DrawSparks(context);
                DrawZaps(context);
                DrawCore(context, core);
                DrawSteam(context);
                DrawReadout(context, core);
                DrawPlates(context);
            }

            // The rim, lit in the accent when the pointer is on it, as the dial's is.
            Color rim = Hovered ? Accent : Color.FromRgb(0x5E, 0x68, 0x7C);
            context.DrawEllipse(null, Pen(rim, 2), Centre, Housing - 1, Housing - 1);
            context.DrawEllipse(null, Pen(White, 0.14, 1), Centre, Housing - 2.8, Housing - 2.8);
        }

        private void DrawHousing(DrawingContext context)
        {
            context.DrawEllipse(Fade(MetalTop, MetalBottom), null, Centre, Housing, Housing);

            // Two machined grooves, each with a lit lower lip, so the band reads as a raised collar.
            context.DrawEllipse(null, Pen(Black, 0.45, 1), Centre, Floor + 2.2, Floor + 2.2);
            context.DrawEllipse(null, Pen(White, 0.08, 1), Centre, Floor + 3.1, Floor + 3.1);
            context.DrawEllipse(null, Pen(Black, 0.45, 1), Centre, Housing - 4, Housing - 4);

            context.DrawGeometry(null, Pen(Slot, 10), StripeSlots);
            context.DrawGeometry(Solid(Hazard, 0.9), null, Stripes);

            foreach (Point bolt in Bolts)
            {
                context.DrawEllipse(Solid(BoltDark), null, bolt, 2.7, 2.7);
                context.DrawEllipse(Solid(BoltLight), null, new Point(bolt.X - 0.3, bolt.Y - 0.3), 1.8, 1.8);
                context.DrawEllipse(Solid(White, 0.55), null, new Point(bolt.X - 0.8, bolt.Y - 0.8), 0.6, 0.6);
            }

            // In the red the whole collar throbs, and the triangle with it; amber holds it steady.
            double pulse = 0.5 + 0.5 * Math.Sin(Time * 6);

            if (MemoryLevel == 2)
            {
                context.DrawEllipse(null, Pen(Bad, 0.5 * pulse, 13), Centre, Band, Band);
                context.DrawEllipse(null, Pen(Bad, 0.3 * pulse, 5), Centre, Floor - 2, Floor - 2);
            }

            Color mark = MemoryLevel switch
            {
                2 => WithAlpha(Bad, 0.45 + 0.55 * pulse),
                1 => WithAlpha(Warn, 0.9),
                _ => WithAlpha(Hazard, 0.35),
            };

            if (MemoryLevel == 2)
            {
                context.DrawEllipse(Glow(Bad, 0.6 * pulse), null, new Point(115, 10), 16, 16);
            }

            context.DrawGeometry(Solid(mark), Pen(Slot, 0.75), Triangle);
            context.DrawLine(Pen(Slot, 1.5), MarkTop, MarkBottom);
            context.DrawEllipse(Solid(Slot), null, MarkDot, 0.85, 0.85);
        }

        private void DrawFloor(DrawingContext context, Color core)
        {
            context.DrawEllipse(Fade(FloorTop, FloorBottom), null, Centre, Floor, Floor);

            // The core lights the floor of the chamber, which is what makes it read as a light.
            context.DrawEllipse(Glow(core, 0.22 * dim), null, Centre, Floor, Floor);

            context.DrawGeometry(null, Pen(White, 0.05, 1), Spokes);
            context.DrawGeometry(null, Pen(White, 0.16, 1), Ticks);

            context.DrawEllipse(null, Pen(Black, 0.7, 2.5), Centre, Floor, Floor);
        }

        private void DrawRings(DrawingContext context)
        {
            for (int i = 0; i < RingRadius.Length; i++)
            {
                double r = RingRadius[i];
                double w = RingWidth[i];
                double load = Load(i);
                Color colour = RingColour(i);

                context.DrawEllipse(null, Pen(Groove, w + 4), Centre, r, r);
                context.DrawEllipse(null, Pen(White, 0.06, 1), Centre, r + w / 2 + 2, r + w / 2 + 2);

                using (context.PushTransform(Around(turn[i])))
                {
                    context.DrawGeometry(null, Pen(colour, 0.3 + 0.45 * load, w), RingSegments[i]);
                    context.DrawGeometry(null, Pen(White, 0.12 + 0.2 * load, 1), RingSegments[i]);

                    for (int j = 0; j < NodeCount[i]; j++)
                    {
                        Point node = At(r, j * 360.0 / NodeCount[i] + i * 20);
                        context.DrawEllipse(Glow(colour, 0.5 + 0.4 * load), null, node, 7, 7);
                        context.DrawEllipse(Solid(colour), null, node, 2.6, 2.6);
                        context.DrawEllipse(Solid(White, 0.9), null, node, 1.2, 1.2);
                    }
                }

                // The gauge stays still while the ring turns: it is the reading, the ring is the motion.
                double inner = r - w / 2 - 2.5;
                context.DrawEllipse(null, Pen(colour, 0.12, 1.5), Centre, inner, inner);

                if (load * 360 >= 359.5)
                {
                    context.DrawEllipse(null, Pen(colour, 0.9, 1.5), Centre, inner, inner);
                }
                else if (load * 360 > 0.5)
                {
                    context.DrawGeometry(null, Pen(colour, 0.9, 1.5), Arc(inner, -90, load * 360));
                }
            }
        }

        private void DrawSparks(DrawingContext context)
        {
            int shown = 6 + (int)(Cpu * 30);
            Color colour = Mix(CpuBlue, White, 0.5);

            for (int i = 0; i < shown && i < sparks.Length; i++)
            {
                Spark s = sparks[i];
                double twinkle = 0.5 + 0.5 * Math.Sin(Time * 5 + s.Phase);
                Point at = At(s.Radius, s.Angle);

                context.DrawEllipse(Glow(colour, 0.5 * twinkle * spin), null, at, s.Size * 3, s.Size * 3);
                context.DrawEllipse(Solid(White, 0.4 + 0.5 * twinkle), null, at, s.Size * 0.6, s.Size * 0.6);
            }
        }

        private void DrawZaps(DrawingContext context)
        {
            foreach (Zap z in zaps)
            {
                if (z.Life <= 0 || z.Points is null)
                {
                    continue;
                }

                StreamGeometry bolt = new();

                using (StreamGeometryContext g = bolt.Open())
                {
                    g.BeginFigure(z.Points[0], false);

                    for (int k = 1; k < z.Points.Length; k++)
                    {
                        g.LineTo(z.Points[k]);
                    }

                    g.EndFigure(false);
                }

                // A flicker on top of the fade, as a discharge does.
                double alpha = z.Life * (0.6 + 0.4 * Math.Sin(Time * 90 + z.Ring));
                Color colour = RingColour(z.Ring);

                context.DrawGeometry(null, Pen(colour, 0.35 * alpha, 4), bolt);
                context.DrawGeometry(null, Pen(Mix(colour, White, 0.7), alpha, 1.1), bolt);
            }
        }

        private void DrawCore(DrawingContext context, Color core)
        {
            double beat = 0.85 + 0.15 * Math.Sin(corePhase);
            double bright = dim * beat;
            double r = 26 + Memory * 10 + Math.Sin(corePhase) * 1.8 * dim;

            context.DrawEllipse(Glow(core, 0.42 * bright), null, Centre, r * 2, r * 2);
            context.DrawEllipse(Glow(core, 0.95 * bright), null, Centre, r * 1.25, r * 1.25);
            context.DrawEllipse(Glow(Mix(core, White, 0.6), 0.9 * bright), null, Centre, r * 0.7, r * 0.7);

            // Plasma filaments: thin loops turning against each other round the core.
            Color filament = Mix(core, White, 0.3);

            for (int j = 0; j < 4; j++)
            {
                double angle = coreTurn * (j % 2 == 0 ? 1 : -1) * (1 + j * 0.3) + j * 45;
                double rx = r * (1.02 + 0.08 * Math.Sin(corePhase + j));
                double ry = r * (0.3 + 0.08 * j);

                using (context.PushTransform(Around(angle)))
                {
                    context.DrawEllipse(null, Pen(filament, 0.4 * bright, 1.2), Centre, rx, ry);
                }
            }
        }

        private void DrawSteam(DrawingContext context)
        {
            // The ring of light a finished clean sends out as the core relights.
            if (!Busy && SinceClean < 1)
            {
                double radius = Window + SinceClean * 70;
                double alpha = 0.7 * (1 - SinceClean);

                context.DrawEllipse(null, Pen(Coolant, 0.3 * alpha, 8), Centre, radius, radius);
                context.DrawEllipse(null, Pen(Coolant, alpha, 2), Centre, radius, radius);
            }

            foreach (Puff p in puffs)
            {
                if (p.Life > 0)
                {
                    context.DrawEllipse(Glow(Coolant, p.Life * 0.6), null, new Point(p.X, p.Y), p.Size, p.Size);
                }
            }
        }

        private void DrawReadout(DrawingContext context, Color core)
        {
            // A dark window over the core, so the reading stays legible whatever colour burns under it.
            context.DrawEllipse(Solid(WindowFill, 0.72), null, Centre, Window, Window);
            context.DrawEllipse(null, Pen(core, 0.55 * dim, 1.2), Centre, Window, Window);
            context.DrawEllipse(null, Pen(White, 0.07, 1), Centre, Window - 2.5, Window - 2.5);

            Text(context, MemoryCaption, new Point(Centre.X, Centre.Y - 20), 7.5, Faint);

            if (Flashing)
            {
                Text(context, FlashText, new Point(Centre.X, Centre.Y - 1), 19, FlashGood ? Good : White, bold: true);

                if (FlashGood)
                {
                    Text(context, FreedCaption, new Point(Centre.X, Centre.Y + 17), 7.5, Faint);
                }

                return;
            }

            Text(context, MemoryText, new Point(Centre.X, Centre.Y - 1), 27, LevelColour(MemoryLevel, White), bold: true);
            Text(
                context,
                Hovered ? ClickToClean : UsedOfTotal,
                new Point(Centre.X, Centre.Y + 17),
                7.5,
                Hovered ? Mix(Accent, White, 0.35) : Color.FromRgb(0xB8, 0xC2, 0xD8));
        }

        /// <summary>A label plate per ring on the bottom of the collar, turned to follow its curve.</summary>
        private void DrawPlates(DrawingContext context)
        {
            for (int i = 0; i < PlateAngle.Length; i++)
            {
                double angle = PlateAngle[i];
                Color colour = RingColour(i);
                string label = i switch
                {
                    0 => $"{CpuCaption} {CpuText}",
                    1 => $"{GpuCaption} {GpuText}",
                    _ => $"{DiskCaption} {DiskText}",
                };

                Matrix place = Matrix.CreateRotation((angle - 90) * Math.PI / 180) * Matrix.CreateTranslation(At(Band, angle));

                using (context.PushTransform(place))
                {
                    context.DrawRectangle(Solid(PlateFill, 0.92), Pen(colour, 0.5, 0.75), new Rect(-23, -5.5, 46, 11), 3, 3);
                    Text(context, label, new Point(0, 0), 7, Mix(colour, White, 0.4), mono: true);
                }
            }
        }

        #region Geometry
        private static Point At(double radius, double degrees)
        {
            double a = degrees * Math.PI / 180;

            return new Point(Centre.X + radius * Math.Cos(a), Centre.Y + radius * Math.Sin(a));
        }

        /// <summary>A rotation about the middle of the square.</summary>
        private static Matrix Around(double degrees) =>
            Matrix.CreateTranslation(-Centre.X, -Centre.Y)
            * Matrix.CreateRotation(degrees * Math.PI / 180)
            * Matrix.CreateTranslation(Centre.X, Centre.Y);

        /// <summary>An open arc clockwise from <paramref name="start"/> degrees, 0 being three o'clock.</summary>
        private static StreamGeometry Arc(double radius, double start, double sweep)
        {
            StreamGeometry arc = new();

            using (StreamGeometryContext g = arc.Open())
            {
                g.BeginFigure(At(radius, start), false);
                g.ArcTo(At(radius, start + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise);
                g.EndFigure(false);
            }

            return arc;
        }

        private static StreamGeometry Segments(double radius, int count, double gap)
        {
            StreamGeometry ring = new();
            double step = 360.0 / count;

            using (StreamGeometryContext g = ring.Open())
            {
                for (int i = 0; i < count; i++)
                {
                    double start = i * step + gap / 2;
                    g.BeginFigure(At(radius, start), false);
                    g.ArcTo(At(radius, start + step - gap), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                    g.EndFigure(false);
                }
            }

            return ring;
        }

        private static StreamGeometry BuildTicks()
        {
            StreamGeometry ticks = new();

            using (StreamGeometryContext g = ticks.Open())
            {
                for (int i = 0; i < 72; i++)
                {
                    double inner = i % 6 == 0 ? 93 : 95.2;
                    g.BeginFigure(At(inner, i * 5), false);
                    g.LineTo(At(96.8, i * 5));
                    g.EndFigure(false);
                }
            }

            return ticks;
        }

        private static StreamGeometry BuildSpokes()
        {
            StreamGeometry spokes = new();

            using (StreamGeometryContext g = spokes.Open())
            {
                for (int i = 0; i < 12; i++)
                {
                    g.BeginFigure(At(Window + 4, i * 30 + 15), false);
                    g.LineTo(At(Floor - 8, i * 30 + 15));
                    g.EndFigure(false);
                }
            }

            return spokes;
        }

        private static StreamGeometry BuildStripeSlots()
        {
            StreamGeometry slots = new();

            using (StreamGeometryContext g = slots.Open())
            {
                foreach (double middle in new double[] { 225, 315 })
                {
                    g.BeginFigure(At(Band, middle - 17), false);
                    g.ArcTo(At(Band, middle + 18), new Size(Band, Band), 0, false, SweepDirection.Clockwise);
                    g.EndFigure(false);
                }
            }

            return slots;
        }

        /// <summary>Slanted yellow bars across the band: the hazard marking on a reactor collar.</summary>
        private static StreamGeometry BuildStripes()
        {
            StreamGeometry stripes = new();

            using (StreamGeometryContext g = stripes.Open())
            {
                foreach (double middle in new double[] { 225, 315 })
                {
                    for (double a = middle - 16; a < middle + 15; a += 5)
                    {
                        g.BeginFigure(At(Band - 4, a), true);
                        g.LineTo(At(Band - 4, a + 2.4));
                        g.LineTo(At(Band + 4, a + 4.4));
                        g.LineTo(At(Band + 4, a + 2));
                        g.EndFigure(true);
                    }
                }
            }

            return stripes;
        }
        #endregion
    }
}
