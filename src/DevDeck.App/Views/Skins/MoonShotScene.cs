using Avalonia;
using Avalonia.Media;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  A rocket between the Earth and the Moon, seen through a porthole.
    /// </summary>
    /// <remarks>
    ///  Altitude is memory in use: on the pad at nothing, at the Moon when full. The flame is the
    ///  processor - a long hot flame under load, a pilot light at idle. Past the high mark the
    ///  rocket starts to shake, and in the red it burns red. A clean is a launch: full thrust, the
    ///  rocket leaves through the top of the porthole while the stars stretch into streaks, and
    ///  once the clean is done it comes back down to the new, lower altitude.
    /// </remarks>
    internal sealed class MoonShotScene : SkinScene
    {
        private const double Radius = 108;
        private const double PadY = 163;
        private const double TopY = 50;
        private const double RocketX = 142;
        private const double LadderX = 200;

        private static readonly Color SpaceTop = Color.FromRgb(0x05, 0x07, 0x18);
        private static readonly Color SpaceBottom = Color.FromRgb(0x0F, 0x1B, 0x3D);
        private static readonly Color Ocean = Color.FromRgb(0x1E, 0x6F, 0xD9);
        private static readonly Color OceanDeep = Color.FromRgb(0x0B, 0x33, 0x7A);
        private static readonly Color Land = Color.FromRgb(0x3C, 0xB3, 0x71);
        private static readonly Color MoonLight = Color.FromRgb(0xD9, 0xDC, 0xE3);
        private static readonly Color MoonShade = Color.FromRgb(0x8E, 0x93, 0xA1);
        private static readonly Color Hull = Color.FromRgb(0xEE, 0xF1, 0xF6);
        private static readonly Color HullShade = Color.FromRgb(0xB4, 0xBC, 0xCB);
        private static readonly Color Fin = Color.FromRgb(0xE2, 0x3D, 0x4F);
        private static readonly Color Glass = Color.FromRgb(0x5E, 0xC8, 0xFF);
        private static readonly Color FlameCore = Color.FromRgb(0xFF, 0xF4, 0xC2);
        private static readonly Color FlameOuter = Color.FromRgb(0xFF, 0x8A, 0x1E);
        private static readonly Color White = Colors.White;

        private static readonly EllipseGeometry Porthole = Circle(Centre, Radius);

        // The rocket, drawn about its own middle with the nose up, so it can be moved and tilted.
        private static readonly Geometry Body = StreamGeometry.Parse(
            "M0,-26 C6,-18 8,-8 8,4 L8,14 L-8,14 L-8,4 C-8,-8 -6,-18 0,-26 Z");

        private static readonly Geometry Nose = StreamGeometry.Parse(
            "M0,-26 C4,-21 6,-17 6.6,-13 L-6.6,-13 C-6,-17 -4,-21 0,-26 Z");

        private static readonly Geometry LeftFin = StreamGeometry.Parse("M-8,2 L-15,14 L-15,18 L-8,13 Z");

        private static readonly Geometry RightFin = StreamGeometry.Parse("M8,2 L15,14 L15,18 L8,13 Z");

        private static readonly Geometry Nozzle = StreamGeometry.Parse("M-5,14 L5,14 L6.5,19 L-6.5,19 Z");

        private struct Star
        {
            public double X;
            public double Y;
            public double Size;
            public double Phase;
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

        private readonly Star[] stars = new Star[70];
        private readonly Puff[] puffs = new Puff[48];
        private int nextPuff;
        private double puffClock;

        /// <summary>How fast the world is going by, so the stars can streak during a burn.</summary>
        private double speed;

        protected override void Seed()
        {
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = new Star
                {
                    X = Random.NextDouble() * Side,
                    Y = Random.NextDouble() * Side,
                    Size = 0.4 + Random.NextDouble() * Random.NextDouble() * 1.6,
                    Phase = Random.NextDouble() * Math.Tau,
                };
            }
        }

        /// <summary>Where the reading puts the rocket.</summary>
        private double Altitude => PadY - (PadY - TopY) * Memory;

        /// <summary>Above the porthole, out of sight, where a clean sends the rocket.</summary>
        private const double SkyY = -50;

        /// <summary>The time a launch takes to leave the porthole, and a landing to settle.</summary>
        private const double AscentSeconds = 1.6;

        private const double DescentSeconds = 2.6;

        private bool flying;
        private bool landing;
        private double ascent;
        private double descent;

        /// <summary>
        ///  How far into the sky the rocket is: 0 at its reading, 1 out of the top of the porthole.
        /// </summary>
        /// <remarks>
        ///  Squared on the way up, so it pulls away slowly and then goes; eased out on the way down,
        ///  so it comes in fast and settles on its new, lower altitude rather than thumping into it.
        /// </remarks>
        private double Lift => !flying ? 0
            : !landing ? ascent * ascent
            : Math.Pow(1 - descent, 3);

        /// <summary>Where the rocket actually is, the reading or the flight.</summary>
        private double RocketY => Altitude + (SkyY - Altitude) * Lift;

        /// <summary>A clean is a launch: the rocket leaves for the sky and stays there until it is done.</summary>
        protected override void CleanStarted()
        {
            flying = true;
            landing = false;
            ascent = 0;
        }

        private void Fly(double dt)
        {
            if (!flying)
            {
                return;
            }

            if (!landing)
            {
                ascent = Math.Min(1, ascent + dt / AscentSeconds);

                // Down only once it has gone and the clean is over: a clean quicker than the climb
                // still gets the whole launch, rather than turning back halfway up.
                if (ascent >= 1 && !Busy)
                {
                    landing = true;
                    descent = 0;
                }
            }
            else
            {
                descent = Math.Min(1, descent + dt / DescentSeconds);
                flying = descent < 1;
            }
        }

        protected override void Advance(double dt)
        {
            Fly(dt);

            // Stars drift down as the rocket climbs; a burn sends them past in streaks.
            double wanted = Busy ? 260 : 6 + Cpu * 24;
            speed += (wanted - speed) * (1 - Math.Exp(-dt * 2.5));

            for (int i = 0; i < stars.Length; i++)
            {
                ref Star s = ref stars[i];
                s.Y += speed * dt * (0.4 + s.Size * 0.6);

                if (s.Y > Side + 10)
                {
                    s.Y = -10;
                    s.X = Random.NextDouble() * Side;
                }
            }

            // Exhaust, more of it the harder the engine works.
            puffClock += dt * (Busy ? 40 : 4 + Cpu * 22);

            while (puffClock >= 1)
            {
                puffClock -= 1;

                ref Puff p = ref puffs[nextPuff];
                nextPuff = (nextPuff + 1) % puffs.Length;

                p.X = RocketX + (Random.NextDouble() - 0.5) * 6;
                p.Y = RocketY + 22 + FlameLength() * 0.6;
                p.Vx = (Random.NextDouble() - 0.5) * 14;
                p.Vy = 18 + Random.NextDouble() * 20 + (Busy ? 60 : 0);
                p.Life = 1;
                p.Size = 2 + Random.NextDouble() * 3;
            }

            for (int i = 0; i < puffs.Length; i++)
            {
                ref Puff p = ref puffs[i];

                if (p.Life <= 0)
                {
                    continue;
                }

                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                p.Size += dt * 9;
                p.Life -= dt * 0.9;
            }
        }

        private double FlameLength()
        {
            double flicker = Math.Sin(Time * 31) * 1.6 + Math.Sin(Time * 47) * 1.1;

            return (Busy ? 34 : 6 + Cpu * 26) + flicker;
        }

        protected override void Draw(DrawingContext context)
        {
            using (context.PushGeometryClip(Porthole))
            {
                context.DrawRectangle(Fade(SpaceTop, SpaceBottom), null, new Rect(0, 0, Side, Side));

                DrawStars(context);
                DrawMoon(context);
                DrawEarth(context);
                DrawLadder(context);
                DrawExhaust(context);
                DrawRocket(context);
                DrawReadout(context);
            }

            // The porthole's rim, lit in the accent when the pointer is on it, as the dial's is.
            Color rim = Hovered ? Accent : Color.FromRgb(0x4A, 0x55, 0x70);
            context.DrawEllipse(null, Pen(rim, 3.5), Centre, Radius, Radius);
            context.DrawEllipse(null, Pen(White, 0.12, 1), Centre, Radius - 3, Radius - 3);
        }

        private void DrawStars(DrawingContext context)
        {
            bool streaks = speed > 40;

            foreach (Star s in stars)
            {
                double twinkle = 0.55 + 0.45 * Math.Sin(Time * 2.2 + s.Phase);

                if (streaks)
                {
                    double length = Math.Min(speed * 0.06 * s.Size, 22);
                    context.DrawLine(Pen(White, 0.5 * twinkle, s.Size), new Point(s.X, s.Y), new Point(s.X, s.Y - length));
                }
                else
                {
                    context.DrawEllipse(Solid(White, 0.35 + 0.6 * twinkle * s.Size / 2), null, new Point(s.X, s.Y), s.Size, s.Size);
                }
            }
        }

        private void DrawMoon(DrawingContext context)
        {
            // The Moon grows as the rocket nears it.
            double r = 46 + Memory * 22;
            Point at = new(88, -r + 30 + Memory * 14);

            context.DrawEllipse(Glow(MoonLight, 0.25), null, at, r * 1.35, r * 1.35);
            context.DrawEllipse(Fade(MoonLight, MoonShade), null, at, r, r);

            // Craters, placed on the visible underside.
            context.DrawEllipse(Solid(MoonShade, 0.55), null, new Point(at.X - r * 0.35, at.Y + r * 0.62), r * 0.14, r * 0.1);
            context.DrawEllipse(Solid(MoonShade, 0.45), null, new Point(at.X + r * 0.3, at.Y + r * 0.75), r * 0.1, r * 0.07);
            context.DrawEllipse(Solid(MoonShade, 0.4), null, new Point(at.X + r * 0.02, at.Y + r * 0.86), r * 0.07, r * 0.05);
            context.DrawEllipse(Solid(MoonShade, 0.5), null, new Point(at.X - r * 0.68, at.Y + r * 0.4), r * 0.08, r * 0.06);
        }

        private void DrawEarth(DrawingContext context)
        {
            // The Earth sinks away as the rocket climbs.
            double r = 120;
            Point at = new(115, Side + r - 46 + Memory * 30);

            context.DrawEllipse(Glow(Glass, 0.35), null, at, r * 1.18, r * 1.18);
            context.DrawEllipse(Fade(Ocean, OceanDeep), null, at, r, r);

            // Continents drift slowly round.
            double turn = (Time * 4) % 360;

            using (context.PushGeometryClip(Circle(at, r)))
            {
                for (int i = 0; i < 4; i++)
                {
                    double x = at.X - r + ((turn * 1.4 + i * 70) % (r * 2.8)) - r * 0.4;
                    double y = at.Y - r + 10 + (i % 2) * 14;
                    context.DrawEllipse(Solid(Land, 0.85), null, new Point(x, y), 22 + i * 4, 7 + (i % 3) * 3);
                }

                // Night side, so the planet reads as a ball.
                context.DrawEllipse(Solid(Colors.Black, 0.22), null, new Point(at.X + r * 0.55, at.Y + r * 0.1), r, r);
            }

            // The pad, while there is ground near enough to stand on.
            double pad = at.Y - r;
            context.DrawRectangle(Solid(HullShade, 0.9), null, new Rect(RocketX - 16, pad - 3, 32, 3), 1, 1);
        }

        /// <summary>An altitude scale up the right of the porthole, with the reading on it.</summary>
        private void DrawLadder(DrawingContext context)
        {
            context.DrawLine(Pen(White, 0.18, 1), new Point(LadderX, TopY), new Point(LadderX, PadY));

            for (int i = 0; i <= 10; i++)
            {
                double y = PadY - (PadY - TopY) * i / 10;
                double w = i % 5 == 0 ? 6 : 3;
                context.DrawLine(Pen(White, 0.3, 1), new Point(LadderX - w, y), new Point(LadderX, y));
            }

            Color mark = LevelColour(MemoryLevel, Accent);
            double at = Altitude;
            context.DrawGeometry(Solid(mark), null, StreamGeometry.Parse(
                FormattableString.Invariant($"M{LadderX + 1},{at} L{LadderX + 7},{at - 4} L{LadderX + 7},{at + 4} Z")));
        }

        private void DrawExhaust(DrawingContext context)
        {
            foreach (Puff p in puffs)
            {
                if (p.Life > 0)
                {
                    context.DrawEllipse(Solid(Color.FromRgb(0xC9, 0xCF, 0xDB), p.Life * 0.35), null, new Point(p.X, p.Y), p.Size, p.Size);
                }
            }
        }

        private void DrawRocket(DrawingContext context)
        {
            double shake = MemoryLevel switch
            {
                2 => 1.6,
                1 => 0.6,
                _ => 0,
            } + (Busy ? 0.8 : 0);

            double x = RocketX + Math.Sin(Time * 1.3) * 1.5 + Math.Sin(Time * 53) * shake;
            double y = RocketY + Math.Sin(Time * 61) * shake * 0.6;
            double tilt = Math.Sin(Time * 0.9) * 3;

            Matrix place = Matrix.CreateRotation(tilt * Math.PI / 180) * Matrix.CreateTranslation(x, y);

            using (context.PushTransform(place))
            {
                // Flame first, so the nozzle sits over it.
                double length = FlameLength();
                bool red = MemoryLevel == 2;
                Color outer = red ? Bad : FlameOuter;

                context.DrawEllipse(Glow(outer, 0.55), null, new Point(0, 20 + length * 0.45), 9 + length * 0.18, length * 0.75);
                context.DrawGeometry(Solid(outer, 0.9), null, Flame(length, 6));
                context.DrawGeometry(Solid(FlameCore), null, Flame(length * 0.55, 3.2));

                context.DrawGeometry(Solid(Fin), null, LeftFin);
                context.DrawGeometry(Solid(Fin), null, RightFin);
                context.DrawGeometry(Fade(Hull, HullShade, horizontal: true), Pen(HullShade, 0.6), Body);
                context.DrawGeometry(Solid(Fin), null, Nose);
                context.DrawGeometry(Solid(HullShade), null, Nozzle);

                // The window, with the theme's accent as the light inside.
                context.DrawEllipse(Solid(Color.FromRgb(0x24, 0x2E, 0x45)), null, new Point(0, -3), 4.2, 4.2);
                context.DrawEllipse(Solid(Glass), null, new Point(0, -3), 3.1, 3.1);
                context.DrawEllipse(Solid(White, 0.8), null, new Point(-1, -4.2), 1, 1);
            }
        }

        private static Geometry Flame(double length, double width) => StreamGeometry.Parse(FormattableString.Invariant(
            $"M{-width},19 C{-width},{19 + length * 0.4} {-width * 0.3},{19 + length * 0.8} 0,{19 + length} C{width * 0.3},{19 + length * 0.8} {width},{19 + length * 0.4} {width},19 Z"));

        private void DrawReadout(DrawingContext context)
        {
            const double x = 62;

            Text(context, MemoryCaption, new Point(x, 92), 8, Color.FromRgb(0x9A, 0xA8, 0xC6));

            if (Flashing)
            {
                Text(context, FlashText, new Point(x, 112), 19, FlashGood ? Good : White, bold: true);

                if (FlashGood)
                {
                    Text(context, FreedCaption, new Point(x, 129), 8, Color.FromRgb(0x9A, 0xA8, 0xC6));
                }

                return;
            }

            Text(context, MemoryText, new Point(x, 112), 28, LevelColour(MemoryLevel, White), bold: true);
            Text(context, Hovered ? ClickToClean : UsedOfTotal, new Point(x, 131), 8.5, Hovered ? Accent : Color.FromRgb(0xB8, 0xC2, 0xD8));

            Color faint = Color.FromRgb(0x9A, 0xA8, 0xC6);
            Text(context, $"{CpuCaption} {CpuText}", new Point(x, 146), 8, faint, mono: true);
            Text(context, $"{GpuCaption} {GpuText} · {DiskCaption} {DiskText}", new Point(x, 157), 7, faint, mono: true);
        }
    }
}
