using Avalonia;
using Avalonia.Media;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  A small solar system on a disc of deep space, with the machine's memory as its sun.
    /// </summary>
    /// <remarks>
    ///  The sun is memory in use: a small yellow-white star when there is room, swelling and
    ///  warming to orange as memory fills, and a throbbing red giant at the top. The planets are
    ///  the heaviest processes, the heaviest on the innermost orbit; a planet's size is its memory
    ///  next to the others', and its speed round the sun is its processor use, so an idle process
    ///  crawls and a busy one races. A clean is a comet: it streaks through the system, knocking
    ///  the small planets it passes outward, and when it is over a ring of light spreads from the
    ///  sun and the amount freed is shown below it.
    /// </remarks>
    internal sealed class SolarSystemScene : SkinScene
    {
        private const double Radius = 108;
        private const int MaxPlanets = 7;

        /// <summary>How flat the orbits are drawn: the system is seen from a little above its plane.</summary>
        private const double Tilt = 0.42;

        private const double InnerOrbit = 50;
        private const double OrbitStep = 8;

        private static readonly Color SpaceTop = Color.FromRgb(0x05, 0x06, 0x14);
        private static readonly Color SpaceBottom = Color.FromRgb(0x0D, 0x12, 0x2E);
        private static readonly Color Nebula = Color.FromRgb(0x6B, 0x3F, 0xA8);
        private static readonly Color NebulaCool = Color.FromRgb(0x1F, 0x7A, 0x96);
        private static readonly Color SunCore = Color.FromRgb(0xFF, 0xFB, 0xEA);
        private static readonly Color SunYellow = Color.FromRgb(0xFF, 0xD5, 0x4A);
        private static readonly Color SunOrange = Color.FromRgb(0xFF, 0x8A, 0x2A);
        private static readonly Color SunRed = Color.FromRgb(0xF0, 0x3E, 0x24);
        private static readonly Color GiantCore = Color.FromRgb(0xFF, 0xC9, 0x9A);
        private static readonly Color SunInk = Color.FromRgb(0x3B, 0x1D, 0x06);
        private static readonly Color CometCore = Color.FromRgb(0xE6, 0xFA, 0xFF);
        private static readonly Color CometTail = Color.FromRgb(0x7F, 0xD8, 0xFF);
        private static readonly Color Faint = Color.FromRgb(0x9A, 0xA8, 0xC6);
        private static readonly Color Orbit = Color.FromRgb(0xB8, 0xC4, 0xE6);
        private static readonly Color Black = Colors.Black;
        private static readonly Color White = Colors.White;

        /// <summary>
        ///  One colour per planet, far enough apart that seven of them never read as two families.
        /// </summary>
        private static readonly Color[] Palette =
        [
            Color.FromRgb(0x4F, 0xA3, 0xFF), // ocean blue
            Color.FromRgb(0xE0, 0x67, 0x3C), // rust
            Color.FromRgb(0x3F, 0xD0, 0xA0), // jade
            Color.FromRgb(0xE8, 0xB8, 0x5A), // ochre
            Color.FromRgb(0xA9, 0x8B, 0xFF), // lilac
            Color.FromRgb(0xFF, 0x7F, 0xA8), // rose
            Color.FromRgb(0x9F, 0xE6, 0xF2), // ice
        ];

        private static readonly EllipseGeometry Window = Circle(Centre, Radius);

        // A planet is drawn in its own unit space, scaled to its radius, so the clip that keeps its
        // shading on the ball and its ring halves are fixed shapes rather than one per frame.
        private static readonly EllipseGeometry UnitBall = Circle(default, 1);

        private static readonly Geometry RingBack = StreamGeometry.Parse("M-2.1,0 A2.1,0.55 0 0 1 2.1,0");
        private static readonly Geometry RingFront = StreamGeometry.Parse("M2.1,0 A2.1,0.55 0 0 1 -2.1,0");
        private static readonly Geometry InnerRingBack = StreamGeometry.Parse("M-1.65,0 A1.65,0.43 0 0 1 1.65,0");
        private static readonly Geometry InnerRingFront = StreamGeometry.Parse("M1.65,0 A1.65,0.43 0 0 1 -1.65,0");

        // Each orbit split at its widest, so the far half can go behind the sun and the near half over it.
        private static readonly Geometry[] OrbitBack = new Geometry[MaxPlanets];
        private static readonly Geometry[] OrbitFront = new Geometry[MaxPlanets];

        static SolarSystemScene()
        {
            for (int i = 0; i < MaxPlanets; i++)
            {
                double rx = OrbitX(i);
                double ry = rx * Tilt;
                double left = Centre.X - rx;
                double right = Centre.X + rx;

                OrbitBack[i] = StreamGeometry.Parse(FormattableString.Invariant(
                    $"M{left},{Centre.Y} A{rx},{ry} 0 0 1 {right},{Centre.Y}"));
                OrbitFront[i] = StreamGeometry.Parse(FormattableString.Invariant(
                    $"M{right},{Centre.Y} A{rx},{ry} 0 0 1 {left},{Centre.Y}"));
            }
        }

        private struct Star
        {
            public double X;
            public double Y;
            public double Size;
            public double Phase;
            public Color Tint;
        }

        private struct Spark
        {
            public double X;
            public double Y;
            public double Vx;
            public double Vy;
            public double Life;
        }

        /// <summary>A process in orbit. Kept by its row's key, so it stays the same planet from tick to tick.</summary>
        private sealed class Planet
        {
            public required string Key;
            public string Name = string.Empty;
            public int Colour;
            public bool Ringed;
            public bool Moon;
            public bool Banded;
            public double MoonPhase;

            public bool Present;
            public double Alpha;
            public double Slot;
            public int TargetSlot;
            public double Size;
            public double TargetSize;
            public double Cpu;
            public double Speed;
            public double Angle;
            public double Kick;

            // Where it is this frame, worked out in Advance so Draw only paints.
            public double X;
            public double Y;
            public double R;
            public double Depth;
        }

        private readonly Star[] stars = new Star[90];
        private readonly Spark[] sparks = new Spark[40];
        private int nextSpark;
        private double sparkClock;

        private readonly Dictionary<string, Planet> planets = [];
        private readonly List<Planet> order = [];
        private readonly List<string> gone = [];
        private readonly bool[] taken = new bool[Palette.Length];

        private bool cometOn;
        private double cometT;
        private double cometGap;
        private Point cometFrom;
        private Point cometTo;

        private static double OrbitX(double slot) => InnerOrbit + slot * OrbitStep;

        protected override void Seed()
        {
            for (int i = 0; i < stars.Length; i++)
            {
                double tint = Random.NextDouble();

                stars[i] = new Star
                {
                    X = Random.NextDouble() * Side,
                    Y = Random.NextDouble() * Side,
                    Size = 0.35 + Random.NextDouble() * Random.NextDouble() * 1.3,
                    Phase = Random.NextDouble() * Math.Tau,

                    // Mostly white, with the odd warm or blue star, as a real sky has.
                    Tint = tint < 0.15
                        ? Color.FromRgb(0xFF, 0xDD, 0xB0)
                        : tint < 0.3 ? Color.FromRgb(0xB8, 0xD4, 0xFF) : White,
                };
            }
        }

        #region Sun
        /// <summary>0 for a plain star, 1 for a full red giant; follows memory past the high mark.</summary>
        private double Giant => Math.Clamp((Memory - 0.88) / 0.08, 0, 1);

        private double SunRadius => 23 + Memory * 9 + Giant * 7;

        private Color SunBody()
        {
            double warm = Math.Clamp((Memory - 0.45) / 0.4, 0, 1);

            return Mix(Mix(SunYellow, SunOrange, warm), SunRed, Giant);
        }
        #endregion

        protected override void CleanStarted() => Launch();

        private void Launch()
        {
            // In from one side high, out the other side low, so it crosses the orbits rather than
            // running along them; which side varies, so a long clean is not the same pass on repeat.
            double high = 25 + Random.NextDouble() * 60;
            double low = 150 + Random.NextDouble() * 50;
            bool fromLeft = Random.NextDouble() < 0.5;

            cometFrom = new Point(fromLeft ? -30 : Side + 30, high);
            cometTo = new Point(fromLeft ? Side + 30 : -30, low);
            cometT = 0;
            cometOn = true;
        }

        private Point CometHead => new(
            cometFrom.X + (cometTo.X - cometFrom.X) * cometT,
            cometFrom.Y + (cometTo.Y - cometFrom.Y) * cometT);

        protected override void Advance(double dt)
        {
            AdvancePlanets(dt);
            AdvanceComet(dt);
        }

        private void AdvancePlanets(double dt)
        {
            foreach (Planet p in planets.Values)
            {
                p.Present = false;
            }

            IReadOnlyList<ProcessRow> rows = Processes;
            int count = Math.Min(rows.Count, MaxPlanets);

            for (int i = 0; i < count; i++)
            {
                ProcessRow row = rows[i];

                if (!planets.TryGetValue(row.Key, out Planet? p))
                {
                    int hash = Hash(row.Key);

                    p = new Planet
                    {
                        Key = row.Key,
                        Colour = PickColour(hash),
                        Ringed = hash % 3 == 0,
                        Moon = hash % 4 == 1,
                        Banded = hash % 5 < 2,
                        MoonPhase = Random.NextDouble() * Math.Tau,
                        Angle = Random.NextDouble() * Math.Tau,
                        Slot = i,
                    };

                    planets.Add(row.Key, p);
                }

                p.Present = true;
                p.Name = row.Name;
                p.TargetSlot = i;

                // Square root, so the fifth process is still a planet and not a speck beside the first.
                p.TargetSize = 2.4 + Math.Sqrt(Math.Clamp(row.Bar, 0, 100) / 100) * 4.6;
                p.Cpu = Math.Clamp(row.CpuPercent, 0, 100);
            }

            // A second or so to settle, as the readings do, so a change of rank is a drift between
            // orbits rather than a jump.
            double ease = 1 - Math.Exp(-dt * 2);

            foreach (Planet p in planets.Values)
            {
                p.Alpha = Math.Clamp(p.Alpha + (p.Present ? dt : -dt) * 1.4, 0, 1);
                p.Slot += (p.TargetSlot - p.Slot) * ease;
                p.Size += (p.TargetSize - p.Size) * ease;
                p.Kick *= Math.Exp(-dt * 1.3);

                // An idle process still creeps round, so the system never looks frozen; the outer
                // orbits run slower, as real ones do.
                double rx = OrbitX(p.Slot);
                double wanted = (0.05 + Math.Sqrt(p.Cpu / 100) * 1.3) * Math.Sqrt(InnerOrbit / rx);
                p.Speed += (wanted - p.Speed) * ease;
                p.Angle = (p.Angle + p.Speed * dt) % Math.Tau;

                // Small planets are the ones a comet can push about; the heavy ones barely notice.
                double small = Math.Clamp((6 - p.Size) / 3, 0.25, 1);
                rx += p.Kick * 13 * small;

                double sin = Math.Sin(p.Angle);
                p.Depth = sin;
                p.X = Centre.X + Math.Cos(p.Angle) * rx;
                p.Y = Centre.Y + sin * rx * Tilt;

                // Nearer the viewer is a touch bigger, which sells the tilt more than the ellipse alone.
                p.R = p.Size
                    * (0.86 + 0.14 * (sin + 1) / 2)
                    * (1 - p.Kick * 0.35 * small)
                    * (0.4 + 0.6 * p.Alpha);
            }

            gone.Clear();

            foreach (Planet p in planets.Values)
            {
                if (!p.Present && p.Alpha <= 0)
                {
                    gone.Add(p.Key);
                }
            }

            foreach (string key in gone)
            {
                planets.Remove(key);
            }
        }

        /// <summary>A stable number for a key, so a process keeps its ring and its moon all day.</summary>
        private static int Hash(string key)
        {
            int hash = 17;

            foreach (char c in key)
            {
                hash = unchecked(hash * 31 + c);
            }

            return hash & 0x7FFFFFFF;
        }

        /// <summary>The key's own colour if it is free, else the next free one, so no two planets match.</summary>
        private int PickColour(int hash)
        {
            Array.Clear(taken);

            foreach (Planet p in planets.Values)
            {
                taken[p.Colour] = true;
            }

            int start = hash % Palette.Length;

            for (int i = 0; i < Palette.Length; i++)
            {
                int at = (start + i) % Palette.Length;

                if (!taken[at])
                {
                    return at;
                }
            }

            return start;
        }

        private void AdvanceComet(double dt)
        {
            if (cometOn)
            {
                cometT += dt / 1.5;

                if (cometT >= 1)
                {
                    cometOn = false;
                    cometGap = 0.35;
                }
                else
                {
                    Point head = CometHead;

                    foreach (Planet p in planets.Values)
                    {
                        double dx = p.X - head.X;
                        double dy = p.Y - head.Y;

                        if (dx * dx + dy * dy < 36 * 36)
                        {
                            p.Kick = Math.Max(p.Kick, 1);
                        }
                    }

                    // Ice boiling off the head, left behind to fade.
                    sparkClock += dt * 45;

                    while (sparkClock >= 1)
                    {
                        sparkClock -= 1;

                        ref Spark s = ref sparks[nextSpark];
                        nextSpark = (nextSpark + 1) % sparks.Length;

                        s.X = head.X;
                        s.Y = head.Y;
                        s.Vx = (Random.NextDouble() - 0.5) * 22;
                        s.Vy = (Random.NextDouble() - 0.5) * 22;
                        s.Life = 1;
                    }
                }
            }
            else if (Busy)
            {
                // Another pass for as long as the clean runs.
                cometGap -= dt;

                if (cometGap <= 0)
                {
                    Launch();
                }
            }

            for (int i = 0; i < sparks.Length; i++)
            {
                ref Spark s = ref sparks[i];

                if (s.Life <= 0)
                {
                    continue;
                }

                s.X += s.Vx * dt;
                s.Y += s.Vy * dt;
                s.Life -= dt * 1.6;
            }
        }

        protected override void Draw(DrawingContext context)
        {
            using (context.PushGeometryClip(Window))
            {
                context.DrawRectangle(Fade(SpaceTop, SpaceBottom), null, new Rect(0, 0, Side, Side));

                DrawSky(context);

                int orbits = OrbitsInUse();

                for (int i = 0; i < orbits; i++)
                {
                    context.DrawGeometry(null, Pen(Orbit, 0.1, 0.75), OrbitBack[i]);
                }

                // Painter's order: the far half of the system, the sun, then the near half.
                order.Clear();
                order.AddRange(planets.Values);
                order.Sort(static (a, b) => a.Y.CompareTo(b.Y));

                foreach (Planet p in order)
                {
                    if (p.Depth < 0)
                    {
                        DrawPlanet(context, p);
                    }
                }

                DrawSun(context);

                for (int i = 0; i < orbits; i++)
                {
                    context.DrawGeometry(null, Pen(Orbit, 0.1, 0.75), OrbitFront[i]);
                }

                foreach (Planet p in order)
                {
                    if (p.Depth >= 0)
                    {
                        DrawPlanet(context, p);
                    }
                }

                DrawHeaviestName(context);
                DrawComet(context);
                DrawShockwave(context);
                DrawReadout(context);
            }

            // The rim, lit in the accent when the pointer is on it, as the dial's is.
            Color rim = Hovered ? Accent : Color.FromRgb(0x3C, 0x43, 0x66);
            context.DrawEllipse(null, Pen(rim, 3.5), Centre, Radius, Radius);
            context.DrawEllipse(null, Pen(White, 0.1, 1), Centre, Radius - 3, Radius - 3);
        }

        private int OrbitsInUse()
        {
            int orbits = 0;

            foreach (Planet p in planets.Values)
            {
                if (p.Present)
                {
                    orbits = Math.Max(orbits, p.TargetSlot + 1);
                }
            }

            return orbits;
        }

        private void DrawSky(DrawingContext context)
        {
            // Two slow clouds of gas, drifting just enough that the sky is not a still picture.
            double drift = Math.Sin(Time * 0.05) * 8;
            context.DrawEllipse(Glow(Nebula, 0.24), null, new Point(68 + drift, 64 - drift * 0.5), 95, 70);
            context.DrawEllipse(Glow(NebulaCool, 0.2), null, new Point(168 - drift, 166 + drift * 0.4), 85, 60);

            foreach (Star s in stars)
            {
                double twinkle = 0.55 + 0.45 * Math.Sin(Time * 1.8 + s.Phase);
                context.DrawEllipse(Solid(s.Tint, 0.25 + 0.55 * twinkle * s.Size / 1.65), null, new Point(s.X, s.Y), s.Size, s.Size);
            }
        }

        private void DrawSun(DrawingContext context)
        {
            double r = SunRadius;
            Color body = SunBody();
            Color core = Mix(SunCore, GiantCore, Giant);

            // A red giant throbs, and the sun flares while a clean runs.
            double throb = MemoryLevel == 2 ? 0.5 + 0.5 * Math.Sin(Time * 3.2) : 0;
            double flare = Busy ? 0.5 + 0.5 * Math.Sin(Time * 7) : 0;

            context.DrawEllipse(Glow(body, 0.22 + flare * 0.12), null, Centre, r * 3.2, r * 3.2);
            context.DrawEllipse(Glow(body, 0.55 + throb * 0.2), null, Centre, r * (1.75 + throb * 0.12 + flare * 0.2), r * (1.75 + throb * 0.12 + flare * 0.2));

            // The corona: a ring of soft rays turning slowly, each breathing on its own beat.
            for (int i = 0; i < 16; i++)
            {
                double a = i * Math.Tau / 16 + Time * 0.12;
                double reach = r * (1.18 + 0.16 * Math.Sin(Time * 1.7 + i * 2.3) + flare * 0.15);
                Point from = new(Centre.X + Math.Cos(a) * r * 0.98, Centre.Y + Math.Sin(a) * r * 0.98);
                Point to = new(Centre.X + Math.Cos(a) * reach, Centre.Y + Math.Sin(a) * reach);
                context.DrawLine(Pen(Mix(body, core, 0.3), 0.35, 2.5), from, to);
            }

            context.DrawEllipse(Solid(body), null, Centre, r, r);

            // A bright core and a darker limb, so it reads as a ball of fire rather than a disc.
            context.DrawEllipse(Glow(core), null, Centre, r * 1.05, r * 1.05);
            context.DrawEllipse(null, Pen(Mix(body, Black, 0.25), 0.45, 3), Centre, r - 1.5, r - 1.5);

            // Granules on the surface, drifting round.
            for (int i = 0; i < 5; i++)
            {
                double a = Time * 0.25 + i * 1.256;
                double d = r * (0.45 + 0.2 * Math.Sin(i * 1.7 + Time * 0.4));
                context.DrawEllipse(Glow(core, 0.35), null, new Point(Centre.X + Math.Cos(a) * d, Centre.Y + Math.Sin(a) * d * 0.8), r * 0.22, r * 0.22);
            }

            // The reading sits on the sun in a dark ink, which the bright core keeps readable at
            // every colour the sun turns.
            Color ink = Mix(SunInk, Color.FromRgb(0x2A, 0x08, 0x00), Giant);
            Text(context, MemoryText, new Point(Centre.X, Centre.Y), 24, ink, bold: true);
        }

        private void DrawPlanet(DrawingContext context, Planet p)
        {
            if (p.Alpha <= 0 || p.R < 0.5)
            {
                return;
            }

            Color colour = Palette[p.Colour];
            Color shade = Mix(colour, Black, 0.62);
            double r = p.R;
            Point at = new(p.X, p.Y);

            // Lit from the sun: the light falls on the side facing it.
            double lx = Centre.X - p.X;
            double ly = Centre.Y - p.Y;
            double length = Math.Sqrt(lx * lx + ly * ly);
            (lx, ly) = length < 1 ? (0, -1) : (lx / length, ly / length);

            // A planet behind the sun sits in its glare; one in front is seen against the dark.
            double haze = p.Depth < 0 ? 0.16 : 0.26;

            using (context.PushOpacity(p.Alpha))
            {
                context.DrawEllipse(Glow(colour, haze), null, at, r * 2.1, r * 2.1);

                double moonA = p.MoonPhase + Time * 1.6;
                bool moonBehind = Math.Sin(moonA) < 0;

                if (p.Moon && moonBehind)
                {
                    DrawMoon(context, p, moonA);
                }

                Matrix ring = Matrix.CreateRotation(-0.38) * Matrix.CreateScale(r, r) * Matrix.CreateTranslation(p.X, p.Y);

                if (p.Ringed)
                {
                    using (context.PushTransform(ring))
                    {
                        context.DrawGeometry(null, Pen(Mix(colour, White, 0.4), 0.45, 0.25), RingBack);
                        context.DrawGeometry(null, Pen(Mix(colour, White, 0.2), 0.3, 0.25), InnerRingBack);
                    }
                }

                using (context.PushTransform(Matrix.CreateScale(r, r) * Matrix.CreateTranslation(p.X, p.Y)))
                using (context.PushGeometryClip(UnitBall))
                {
                    context.DrawEllipse(Solid(shade), null, default, 1, 1);
                    context.DrawEllipse(Solid(colour), null, new Point(lx * 0.45, ly * 0.45), 1.02, 1.02);

                    if (p.Banded)
                    {
                        // Cloud belts, for the planets that look like gas giants.
                        context.DrawEllipse(Solid(shade, 0.35), null, new Point(0, -0.32), 1.3, 0.11);
                        context.DrawEllipse(Solid(shade, 0.3), null, new Point(0, 0.22), 1.3, 0.13);
                    }

                    context.DrawEllipse(Glow(White, 0.6), null, new Point(lx * 0.5, ly * 0.5), 0.55, 0.55);
                }

                if (p.Ringed)
                {
                    using (context.PushTransform(ring))
                    {
                        context.DrawGeometry(null, Pen(Mix(colour, White, 0.2), 0.35, 0.25), InnerRingFront);
                        context.DrawGeometry(null, Pen(Mix(colour, White, 0.4), 0.6, 0.25), RingFront);
                    }
                }

                if (p.Moon && !moonBehind)
                {
                    DrawMoon(context, p, moonA);
                }
            }
        }

        private void DrawMoon(DrawingContext context, Planet p, double angle)
        {
            double size = Math.Max(0.9, p.R * 0.28);
            Point at = new(p.X + Math.Cos(angle) * p.R * 2.1, p.Y + Math.Sin(angle) * p.R * 0.8);

            context.DrawEllipse(Solid(Color.FromRgb(0xC9, 0xCF, 0xDB)), null, at, size, size);
        }

        /// <summary>The heaviest process's name beside its planet, so the biggest world is not a mystery.</summary>
        private void DrawHeaviestName(DrawingContext context)
        {
            foreach (Planet p in planets.Values)
            {
                if (!p.Present || p.TargetSlot != 0 || p.Alpha < 0.5 || p.Name.Length == 0)
                {
                    continue;
                }

                string name = p.Name.Length > 14 ? p.Name[..13] + "…" : p.Name;
                Color colour = Mix(Palette[p.Colour], White, 0.55);
                Text(context, name, new Point(p.X, p.Y - p.R - 7), 7, WithAlpha(colour, 0.85 * p.Alpha));
            }
        }

        private void DrawComet(DrawingContext context)
        {
            foreach (Spark s in sparks)
            {
                if (s.Life > 0)
                {
                    context.DrawEllipse(Solid(CometTail, s.Life * 0.6), null, new Point(s.X, s.Y), 0.5 + s.Life, 0.5 + s.Life);
                }
            }

            if (!cometOn)
            {
                return;
            }

            Point head = CometHead;
            double dx = cometTo.X - cometFrom.X;
            double dy = cometTo.Y - cometFrom.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            dx /= length;
            dy /= length;

            // The tail: a soft glow down its length, and a tapering line of light on top.
            for (int k = 16; k >= 0; k -= 4)
            {
                Point at = new(head.X - dx * k * 3.4, head.Y - dy * k * 3.4);
                context.DrawEllipse(Glow(CometTail, 0.3 * (1 - k / 20.0)), null, at, 9, 9);
            }

            for (int k = 22; k >= 1; k--)
            {
                double fall = 1 - k / 23.0;
                Point at = new(head.X - dx * k * 3.2, head.Y - dy * k * 3.2);
                context.DrawEllipse(Solid(Mix(CometTail, CometCore, fall), Math.Pow(fall, 1.5) * 0.6), null, at, 0.5 + 2.6 * fall, 0.5 + 2.6 * fall);
            }

            context.DrawEllipse(Glow(CometCore, 0.95), null, head, 10, 10);
            context.DrawEllipse(Solid(White), null, head, 2.6, 2.6);
        }

        /// <summary>A ring of light spreading through the orbits once a clean is over.</summary>
        private void DrawShockwave(DrawingContext context)
        {
            const double Lasts = 1.4;

            if (Busy || SinceClean >= Lasts)
            {
                return;
            }

            double t = SinceClean / Lasts;
            double r = SunRadius + t * 95;
            Color colour = FlashGood ? Good : CometTail;

            context.DrawEllipse(null, Pen(colour, (1 - t) * 0.7, 2), Centre, r, r * Tilt);
            context.DrawEllipse(null, Pen(colour, (1 - t) * 0.3, 5), Centre, r * 0.97, r * 0.97 * Tilt);
        }

        private void DrawReadout(DrawingContext context)
        {
            Text(context, MemoryCaption, new Point(Centre.X, 24), 8, Faint);

            // A pool of darker space under the text, so stars and orbits do not show through it.
            context.DrawEllipse(Glow(SpaceTop, 0.85), null, new Point(Centre.X, 188), 80, 26);

            if (Flashing)
            {
                Text(context, FlashText, new Point(Centre.X, 179), 19, FlashGood ? Good : White, bold: true);

                if (FlashGood)
                {
                    Text(context, FreedCaption, new Point(Centre.X, 196), 8, Faint);
                }

                return;
            }

            Text(context, Hovered ? ClickToClean : UsedOfTotal, new Point(Centre.X, 180), 8.5, Hovered ? Accent : Color.FromRgb(0xB8, 0xC2, 0xD8));

            Color cpu = LevelColour(CpuLevel, Faint);
            Text(context, $"{CpuCaption} {CpuText}", new Point(Centre.X, 194), 7.5, cpu, mono: true);
            Text(context, $"{GpuCaption} {GpuText} · {DiskCaption} {DiskText}", new Point(Centre.X, 204), 7, Faint, mono: true);
        }
    }
}
