using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  A phosphor radar scope, with the heaviest processes as contacts on it.
    /// </summary>
    /// <remarks>
    ///  Each contact is one of the heaviest processes: its bearing comes from its key, so it stays
    ///  put from one reading to the next, and the heavier it is the closer it sits to the centre
    ///  and the bigger its echo. The sweep turns faster as the processor works harder, and a
    ///  contact glows when the beam crosses it and fades until the next pass, as phosphor does.
    ///  The phosphor itself is the memory level - green, amber when high, red when critical. A
    ///  clean is a sonar ping: rings run out from the centre, the small contacts flicker out under
    ///  them, and the beam spins until the result comes back.
    /// </remarks>
    internal sealed class RadarScene : SkinScene
    {
        private const double Glass = 99;
        private const double Scope = 98;
        private const double BezelOuter = 114;
        private const double DiscRadius = 39;
        private const double NearRange = 50;
        private const double FarRange = 92;
        private const double TrailSpan = 75 * Math.PI / 180;
        private const int TrailSlices = 28;
        private const int Contacts = 8;

        private static readonly Color PhosphorGreen = Color.FromRgb(0x3C, 0xF5, 0x8C);
        private static readonly Color PhosphorAmber = Color.FromRgb(0xFF, 0xB2, 0x38);
        private static readonly Color PhosphorRed = Color.FromRgb(0xFF, 0x4B, 0x3E);
        private static readonly Color GlassTop = Color.FromRgb(0x0A, 0x1F, 0x14);
        private static readonly Color GlassBottom = Color.FromRgb(0x02, 0x08, 0x05);
        private static readonly Color Disc = Color.FromRgb(0x02, 0x09, 0x05);
        private static readonly Color BezelLight = Color.FromRgb(0x3A, 0x41, 0x3D);
        private static readonly Color BezelDark = Color.FromRgb(0x0A, 0x0D, 0x0B);
        private static readonly Color BezelInk = Color.FromRgb(0x8F, 0xA6, 0x98);
        private static readonly Color Black = Colors.Black;
        private static readonly Color White = Colors.White;

        private static readonly EllipseGeometry GlassClip = Circle(Centre, Glass);

        // The fixed parts of the scope are built once: they never move, only change colour, and
        // colour comes from the pen they are stroked with.
        private static readonly Geometry Bezel = new CombinedGeometry(
            GeometryCombineMode.Exclude, Circle(Centre, BezelOuter), Circle(Centre, Glass));

        private static readonly StreamGeometry Grid = Build(g =>
        {
            for (int i = 1; i <= 4; i++)
            {
                AddCircle(g, Scope * i / 4);
            }

            AddLine(g, new Point(Centre.X - Scope, Centre.Y), new Point(Centre.X + Scope, Centre.Y));
            AddLine(g, new Point(Centre.X, Centre.Y - Scope), new Point(Centre.X, Centre.Y + Scope));
        });

        private static readonly StreamGeometry Diagonals = Build(g =>
        {
            double d = Scope * Math.Sqrt(0.5);
            AddLine(g, new Point(Centre.X - d, Centre.Y - d), new Point(Centre.X + d, Centre.Y + d));
            AddLine(g, new Point(Centre.X - d, Centre.Y + d), new Point(Centre.X + d, Centre.Y - d));
        });

        // Every 10 degrees, longer every 30; the four cardinals carry their labels instead.
        private static readonly StreamGeometry Ticks = Build(g =>
        {
            for (int i = 0; i < 36; i++)
            {
                if (i % 9 == 0)
                {
                    continue;
                }

                double length = i % 3 == 0 ? 5 : 3;
                AddLine(g, At(i * Math.PI / 18, Glass), At(i * Math.PI / 18, Glass + length));
            }
        });

        private static readonly StreamGeometry Scanlines = Build(g =>
        {
            for (double y = Centre.Y - Glass; y < Centre.Y + Glass; y += 2.5)
            {
                AddLine(g, new Point(Centre.X - Glass, y), new Point(Centre.X + Glass, y));
            }
        });

        // A highlight along the top left of the glass, where a room's light would catch it.
        private static readonly StreamGeometry Highlight = Build(g =>
        {
            double from = -70 * Math.PI / 180;
            double to = 10 * Math.PI / 180;
            g.BeginFigure(At(from, Scope - 4), false);
            g.ArcTo(At(to, Scope - 4), new Size(Scope - 4, Scope - 4), 0, false, SweepDirection.Clockwise);
            g.EndFigure(false);
        });

        // One thin slice of the trail, its point at the origin and opening clockwise from north.
        // The trail is this slice drawn many times, turned, each fainter than the last - a conic
        // fade that needs no geometry built per frame. The slight overlap hides the seams.
        private static readonly StreamGeometry Slice = Build(g =>
        {
            double w = TrailSpan / TrailSlices + 0.3 * Math.PI / 180;
            g.BeginFigure(new Point(0, 0), true);
            g.LineTo(new Point(0, -Scope));
            g.ArcTo(new Point(Scope * Math.Sin(w), -Scope * Math.Cos(w)), new Size(Scope, Scope), 0, false, SweepDirection.Clockwise);
            g.EndFigure(true);
        });

        private sealed class Contact
        {
            public string Name = string.Empty;
            public double Bearing;
            public double Range;
            public double TargetRange;
            public double Size;
            public double TargetSize;
            public double LastLit = -100;
            public double Presence;
            public bool Seen;
            public int Rank;
            public bool Hot;
            public double WipedAt = -1;
        }

        private struct Speck
        {
            public double Bearing;
            public double Range;
            public double Strength;
            public double LastLit;
        }

        private struct Ping
        {
            public double Born;
            public bool Live;
        }

        private readonly Dictionary<string, Contact> contacts = [];
        private readonly List<string> gone = [];
        private readonly Speck[] specks = new Speck[40];
        private readonly Ping[] pings = new Ping[6];
        private int nextPing;
        private double pingClock;

        // Transparent in the middle, dark at the rim: the curve of the tube, and depth for free.
        private readonly IImmutableBrush vignette = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.62),
                new GradientStop(Color.FromArgb(0x90, 0, 0, 0), 1),
            },
        }.ToImmutable();

        /// <summary>The beam's bearing, radians clockwise from north.</summary>
        private double sweep;

        /// <summary>Turns a second, eased so the beam winds up and down rather than jumping.</summary>
        private double turns;

        /// <summary>0 green, 1 amber, 2 red, eased so the phosphor shifts rather than switches.</summary>
        private double heat;

        /// <summary>The phosphor colour for this frame.</summary>
        private Color ink;

        protected override void Seed()
        {
            for (int i = 0; i < specks.Length; i++)
            {
                specks[i] = new Speck
                {
                    Bearing = Random.NextDouble() * Math.Tau,
                    Range = 8 + Random.NextDouble() * (Scope - 12),
                    Strength = 0.25 + Random.NextDouble() * 0.5,
                    LastLit = -100,
                };
            }

            heat = MemoryLevel;
            turns = 0.22 + Cpu * 0.55;
            Track(1);
        }

        protected override void CleanStarted()
        {
            pingClock = 0;
            Launch();
        }

        protected override void CleanFinished()
        {
            // One last ping as the result comes back, and the contacts the clean wiped out return
            // dark, to be picked up again as the beam finds them.
            Launch();

            foreach (Contact c in contacts.Values)
            {
                if (c.WipedAt >= 0)
                {
                    c.WipedAt = -1;
                    c.LastLit = -100;
                }
            }
        }

        private void Launch()
        {
            pings[nextPing] = new Ping { Born = Time, Live = true };
            nextPing = (nextPing + 1) % pings.Length;
        }

        protected override void Advance(double dt)
        {
            double ease = 1 - Math.Exp(-dt * 2);
            heat += (MemoryLevel - heat) * ease;

            double wanted = Busy ? 1.4 : 0.22 + Cpu * 0.55;
            turns += (wanted - turns) * (1 - Math.Exp(-dt * 2.5));

            double from = sweep;
            double step = turns * Math.Tau * dt;
            sweep = (sweep + step) % Math.Tau;

            // A ping every so often while the clean runs, so the scope keeps sounding.
            if (Busy)
            {
                pingClock += dt;

                if (pingClock >= 0.8)
                {
                    pingClock -= 0.8;
                    Launch();
                }
            }

            Track(dt);

            foreach (Contact c in contacts.Values)
            {
                if (Crossed(from, step, c.Bearing))
                {
                    c.LastLit = Time;
                }
            }

            for (int i = 0; i < specks.Length; i++)
            {
                ref Speck s = ref specks[i];

                if (Crossed(from, step, s.Bearing))
                {
                    s.LastLit = Time;

                    // Clutter shifts a little between passes, so the scope never looks painted on.
                    if (Random.NextDouble() < 0.35)
                    {
                        s.Bearing = (s.Bearing + (Random.NextDouble() - 0.5) * 0.08 + Math.Tau) % Math.Tau;
                        s.Range = Math.Clamp(s.Range + (Random.NextDouble() - 0.5) * 8, 8, Scope - 4);
                    }
                }
            }

            for (int i = 0; i < pings.Length; i++)
            {
                ref Ping p = ref pings[i];

                if (!p.Live)
                {
                    continue;
                }

                double radius = PingRadius(p);

                if (radius > Scope + 4)
                {
                    p.Live = false;
                    continue;
                }

                // The front of the ring echoes off the big contacts and wipes out the small ones.
                foreach (Contact c in contacts.Values)
                {
                    if (radius >= c.Range && radius < c.Range + 10)
                    {
                        if (c.Rank >= 3 && Busy)
                        {
                            if (c.WipedAt < 0)
                            {
                                c.WipedAt = Time;
                            }
                        }
                        else if (c.WipedAt < 0)
                        {
                            c.LastLit = Time;
                        }
                    }
                }
            }
        }

        /// <summary>Brings the contacts in line with the heaviest processes.</summary>
        private void Track(double dt)
        {
            foreach (Contact c in contacts.Values)
            {
                c.Seen = false;
            }

            IReadOnlyList<ProcessRow> rows = Processes;

            for (int i = 0; i < Math.Min(rows.Count, Contacts); i++)
            {
                ProcessRow row = rows[i];
                double range = NearRange + (1 - Math.Clamp(row.Bar / 100, 0, 1)) * (FarRange - NearRange);
                double size = Math.Clamp(1.6 + Math.Sqrt(Math.Max(row.Share, 0)) * 1.1, 2, 6);

                if (!contacts.TryGetValue(row.Key, out Contact? c))
                {
                    // New contacts start dark, and show up when the beam first finds them.
                    contacts[row.Key] = c = new Contact
                    {
                        Bearing = BearingOf(row.Key),
                        Range = range,
                        Size = size,
                    };
                }

                c.Name = row.Name.Length > 12 ? row.Name[..11] + "…" : row.Name;
                c.TargetRange = range;
                c.TargetSize = size;
                c.Rank = i;
                c.Hot = row.CpuPercent >= 20;
                c.Seen = true;
            }

            double ease = 1 - Math.Exp(-dt * 3);
            gone.Clear();

            foreach ((string key, Contact c) in contacts)
            {
                c.Range += (c.TargetRange - c.Range) * ease;
                c.Size += (c.TargetSize - c.Size) * ease;
                c.Presence += ((c.Seen ? 1 : 0) - c.Presence) * ease;

                if (!c.Seen && c.Presence < 0.02)
                {
                    gone.Add(key);
                }
            }

            foreach (string key in gone)
            {
                contacts.Remove(key);
            }
        }

        /// <summary>A fixed bearing for a key: FNV-1a, which unlike string hashing is the same every run.</summary>
        private static double BearingOf(string key)
        {
            uint hash = 2166136261;

            foreach (char ch in key)
            {
                hash = (hash ^ ch) * 16777619;
            }

            return hash % 3600 / 3600.0 * Math.Tau;
        }

        /// <summary>The beam went from <paramref name="from"/> through <paramref name="step"/> and over this bearing.</summary>
        private static bool Crossed(double from, double step, double bearing)
        {
            double d = (bearing - from) % Math.Tau;

            if (d < 0)
            {
                d += Math.Tau;
            }

            return d < step;
        }

        /// <summary>How bright a return lit at <paramref name="lit"/> still is: phosphor decays, it does not switch off.</summary>
        private double Afterglow(double lit) => Math.Exp(-(Time - lit) * 0.9);

        private double PingRadius(Ping p) => (Time - p.Born) * 115;

        private Color Phosphor()
        {
            // Stepped in eighths, so a change of level mints a few brushes rather than one a frame.
            double h = Math.Round(heat * 8) / 8;

            return h <= 1 ? Mix(PhosphorGreen, PhosphorAmber, h) : Mix(PhosphorAmber, PhosphorRed, h - 1);
        }

        private static Color LevelInk(int level, Color normal) => level switch
        {
            2 => PhosphorRed,
            1 => PhosphorAmber,
            _ => normal,
        };

        protected override void Draw(DrawingContext context)
        {
            ink = Phosphor();

            using (context.PushGeometryClip(GlassClip))
            {
                DrawGlass(context);
                DrawClutter(context);
                DrawSweep(context);
                DrawPings(context);
                DrawContacts(context);

                context.DrawGeometry(null, Pen(Black, 0.3, 1), Scanlines);
                DrawRefresh(context);
                context.DrawEllipse(vignette, null, Centre, Glass, Glass);

                DrawReadout(context);
                DrawGauges(context);

                // The glass over it all: a soft sheen and a thin catch of light.
                context.DrawEllipse(Fade(WithAlpha(White, 0.09), WithAlpha(White, 0)), null, new Point(92, 60), 68, 34);
                context.DrawGeometry(null, Pen(White, 0.12, 1.5), Highlight);
            }

            DrawBezel(context);
        }

        private void DrawGlass(DrawingContext context)
        {
            context.DrawEllipse(Fade(GlassTop, GlassBottom), null, Centre, Glass, Glass);
            context.DrawEllipse(Glow(ink, 0.12), null, Centre, Scope, Scope);

            context.DrawGeometry(null, Pen(ink, 0.2, 0.75), Grid);
            context.DrawGeometry(null, Pen(ink, 0.09, 0.5), Diagonals);
        }

        private void DrawClutter(DrawingContext context)
        {
            foreach (Speck s in specks)
            {
                double glow = Afterglow(s.LastLit) * s.Strength;

                if (glow > 0.03)
                {
                    context.DrawEllipse(Solid(ink, glow * 0.6), null, At(s.Bearing, s.Range), 0.8, 0.8);
                }
            }
        }

        private void DrawSweep(DrawingContext context)
        {
            double w = TrailSpan / TrailSlices;

            for (int i = 0; i < TrailSlices; i++)
            {
                double fall = 1 - (double)i / TrailSlices;
                double turn = sweep - (i + 1) * w;

                using (context.PushTransform(Matrix.CreateRotation(turn) * Matrix.CreateTranslation(Centre.X, Centre.Y)))
                {
                    context.DrawGeometry(Solid(ink, 0.38 * fall * fall), null, Slice);
                }
            }

            // The leading edge: a hot line with a soft spill either side.
            Point tip = At(sweep, Scope);
            context.DrawLine(Pen(ink, 0.22, 5), Centre, tip);
            context.DrawLine(Pen(Mix(ink, White, 0.45), 0.95, 1.3), Centre, tip);
        }

        private void DrawPings(DrawingContext context)
        {
            foreach (Ping p in pings)
            {
                if (!p.Live)
                {
                    continue;
                }

                double radius = PingRadius(p);
                double alpha = Math.Clamp(1 - radius / Scope, 0, 1);

                if (radius < 1 || alpha <= 0)
                {
                    continue;
                }

                context.DrawEllipse(null, Pen(ink, 0.25 * alpha, 7), Centre, radius, radius);
                context.DrawEllipse(null, Pen(Mix(ink, White, 0.5), 0.9 * alpha, 1.6), Centre, radius, radius);
            }
        }

        private void DrawContacts(DrawingContext context)
        {
            foreach (Contact c in contacts.Values)
            {
                double glow = (0.08 + 0.92 * Afterglow(c.LastLit)) * c.Presence;

                if (c.WipedAt >= 0)
                {
                    // Flickers under the ping, then is gone until the clean is over.
                    double age = Time - c.WipedAt;
                    glow = age < 0.6 && (int)(Time * 30) % 2 == 0 ? 1 : 0;
                }

                if (glow < 0.02)
                {
                    continue;
                }

                Point at = At(c.Bearing, c.Range);
                context.DrawEllipse(Glow(ink, 0.75 * glow), null, at, c.Size * 3.4, c.Size * 3.4);

                // Smeared along the arc of the sweep, as a real echo is.
                using (context.PushTransform(Matrix.CreateRotation(c.Bearing) * Matrix.CreateTranslation(at.X, at.Y)))
                {
                    context.DrawEllipse(Solid(Mix(ink, White, 0.5 * glow), glow), null, new Point(0, 0), c.Size * 1.5, c.Size * 0.85);
                }

                // A busy process is marked as a tracked target.
                if (c.Hot)
                {
                    context.DrawEllipse(null, Pen(ink, 0.7 * glow, 0.75), at, c.Size + 4.5, c.Size + 4.5);
                }

                if (c.Rank < 3 && glow > 0.25 && c.WipedAt < 0)
                {
                    double width = c.Name.Length * 7 * 0.56;
                    double side = at.X >= Centre.X ? 1 : -1;
                    double x = Math.Clamp(at.X + side * (c.Size + 5 + width / 2), 20 + width / 2, Side - 20 - width / 2);
                    Text(context, c.Name, new Point(x, at.Y - c.Size - 4), 7, WithAlpha(Mix(ink, White, 0.3), glow), mono: true);
                }
            }
        }

        /// <summary>The brighter band a CRT's refresh rolls down the tube.</summary>
        private void DrawRefresh(DrawingContext context)
        {
            double y = (Time * 28) % (Side + 60) - 30;
            Color clear = WithAlpha(ink, 0);
            Color lit = WithAlpha(ink, 0.05);

            context.DrawRectangle(Fade(clear, lit), null, new Rect(0, y - 12, Side, 12));
            context.DrawRectangle(Fade(lit, clear), null, new Rect(0, y, Side, 12));
        }

        private void DrawReadout(DrawingContext context)
        {
            // A dark disc of its own, so the beam never passes over the figures.
            context.DrawEllipse(Glow(ink, 0.22), null, Centre, DiscRadius + 12, DiscRadius + 12);
            context.DrawEllipse(Solid(Disc, 0.92), null, Centre, DiscRadius, DiscRadius);
            context.DrawEllipse(null, Pen(ink, 0.5, 1), Centre, DiscRadius, DiscRadius);
            context.DrawEllipse(null, Pen(ink, 0.12, 0.75), Centre, DiscRadius - 4, DiscRadius - 4);

            Color dim = WithAlpha(ink, 0.65);
            Text(context, MemoryCaption.ToUpperInvariant(), new Point(Centre.X, 97), 7, dim, mono: true);

            if (Flashing)
            {
                Color result = FlashGood ? Good : Mix(ink, White, 0.4);
                double size = FlashText.Length > 8 ? 10 : 19;
                context.DrawEllipse(Glow(result, 0.3), null, new Point(Centre.X, 113), 30, 14);
                Text(context, FlashText, new Point(Centre.X, 113), size, result, bold: true);

                if (FlashGood)
                {
                    Text(context, FreedCaption, new Point(Centre.X, 131), 7, dim);
                }

                return;
            }

            Color figure = Mix(ink, White, 0.3);
            Point middle = new(Centre.X, 114);

            // Phosphor bloom: the figure drawn faintly a hair off each way, then sharp on top.
            context.DrawEllipse(Glow(ink, 0.3), null, middle, 32, 15);
            Color bloom = WithAlpha(ink, 0.3);
            Text(context, MemoryText, new Point(middle.X - 0.9, middle.Y), 26, bloom, bold: true, mono: true);
            Text(context, MemoryText, new Point(middle.X + 0.9, middle.Y), 26, bloom, bold: true, mono: true);
            Text(context, MemoryText, new Point(middle.X, middle.Y - 0.9), 26, bloom, bold: true, mono: true);
            Text(context, MemoryText, new Point(middle.X, middle.Y + 0.9), 26, bloom, bold: true, mono: true);
            Text(context, MemoryText, middle, 26, figure, bold: true, mono: true);

            Text(context, Hovered ? ClickToClean : UsedOfTotal, new Point(Centre.X, 132), 7,
                Hovered ? Mix(ink, White, 0.55) : dim);
        }

        /// <summary>Processor, graphics and disk in a strip along the bottom of the scope.</summary>
        private void DrawGauges(DrawingContext context)
        {
            context.DrawRectangle(Solid(Disc, 0.6), Pen(ink, 0.2, 0.75), new Rect(64, 175, 102, 26), 8, 8);

            Color dim = WithAlpha(ink, 0.6);
            Gauge(context, 85, CpuCaption, CpuText, LevelInk(CpuLevel, ink), dim);
            Gauge(context, 115, GpuCaption, GpuText, ink, dim);
            Gauge(context, 145, DiskCaption, DiskText, ink, dim);
        }

        private void Gauge(DrawingContext context, double x, string caption, string value, Color colour, Color dim)
        {
            Text(context, caption.ToUpperInvariant(), new Point(x, 182), 7, dim, mono: true);
            Text(context, value, new Point(x, 193), 8.5, Mix(colour, White, 0.2), bold: true, mono: true);
        }

        private void DrawBezel(DrawingContext context)
        {
            context.DrawGeometry(Fade(BezelLight, BezelDark), null, Bezel);

            // A shadow where the bezel meets the glass, and the rim lit in the accent on hover.
            context.DrawEllipse(null, Pen(Black, 0.7, 2), Centre, Glass + 0.5, Glass + 0.5);
            context.DrawEllipse(null, Pen(White, 0.14, 1), Centre, BezelOuter - 1.5, BezelOuter - 1.5);
            context.DrawEllipse(null, Pen(Hovered ? Accent : Color.FromRgb(0x1C, 0x21, 0x1E), 1.5), Centre, BezelOuter - 0.5, BezelOuter - 0.5);

            context.DrawGeometry(null, Pen(BezelInk, 0.7, 0.8), Ticks);

            Color label = WithAlpha(BezelInk, 0.95);
            Text(context, "0", At(0, 107), 7, label, mono: true);
            Text(context, "90", At(Math.PI / 2, 106), 7, label, mono: true);
            Text(context, "180", At(Math.PI, 107), 7, label, mono: true);
            Text(context, "270", At(Math.PI * 1.5, 106), 7, label, mono: true);
        }

        #region Geometry
        /// <summary>A point at a bearing (radians clockwise from north) and range from the centre.</summary>
        private static Point At(double bearing, double range) =>
            new(Centre.X + range * Math.Sin(bearing), Centre.Y - range * Math.Cos(bearing));

        private static StreamGeometry Build(Action<StreamGeometryContext> draw)
        {
            StreamGeometry geometry = new();

            using (StreamGeometryContext g = geometry.Open())
            {
                draw(g);
            }

            return geometry;
        }

        private static void AddLine(StreamGeometryContext g, Point from, Point to)
        {
            g.BeginFigure(from, false);
            g.LineTo(to);
            g.EndFigure(false);
        }

        private static void AddCircle(StreamGeometryContext g, double radius)
        {
            Size size = new(radius, radius);
            g.BeginFigure(new Point(Centre.X + radius, Centre.Y), false);
            g.ArcTo(new Point(Centre.X - radius, Centre.Y), size, 0, false, SweepDirection.Clockwise);
            g.ArcTo(new Point(Centre.X + radius, Centre.Y), size, 0, false, SweepDirection.Clockwise);
            g.EndFigure(true);
        }
        #endregion
    }
}
