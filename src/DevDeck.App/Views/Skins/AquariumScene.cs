using Avalonia;
using Avalonia.Media;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  A round fish bowl on a stand, with the heaviest processes swimming in it.
    /// </summary>
    /// <remarks>
    ///  The water level is memory in use: a puddle over the gravel when the machine is idle, up to
    ///  the neck of the bowl when it is full, and past the high mark the water turns amber, then
    ///  red. Each fish is one of the six heaviest processes, the heaviest the biggest; a process
    ///  that arrives drops in as a new fish, and one that leaves fades away. The bubbles are the
    ///  processor - a lazy trickle at idle, a fizzing column under load. A clean pulls the plug: a
    ///  whirlpool opens, the water drops, the fish are swept round, and a burst of bubbles rises.
    /// </remarks>
    internal sealed class AquariumScene : SkinScene
    {
        private const double Radius = 98;
        private const double BowlX = 115;
        private const double BowlY = 118;
        private const double RimY = 36;
        private const double FloorY = 198;
        private const double WaterLow = 192;
        private const double WaterHigh = 54;
        private const int MaxFish = 6;

        /// <summary>Half the width of the bowl's opening, where the sphere is cut off.</summary>
        private static readonly double RimHalf = Math.Sqrt(Radius * Radius - (BowlY - RimY) * (BowlY - RimY));

        private static readonly Color WaterTop = Color.FromRgb(0x3F, 0xC1, 0xE8);
        private static readonly Color WaterDeep = Color.FromRgb(0x0A, 0x3A, 0x74);
        private static readonly Color GlassInside = Color.FromRgb(0x08, 0x16, 0x2A);
        private static readonly Color GlassEdge = Color.FromRgb(0xBF, 0xE9, 0xFF);
        private static readonly Color RimIdle = Color.FromRgb(0x7F, 0x9C, 0xB8);
        private static readonly Color Shadow = Color.FromRgb(0x02, 0x0A, 0x14);
        private static readonly Color Sand = Color.FromRgb(0xD9, 0xB7, 0x7E);
        private static readonly Color SandDeep = Color.FromRgb(0x8A, 0x6A, 0x3E);
        private static readonly Color Leaf = Color.FromRgb(0x3F, 0xC2, 0x6E);
        private static readonly Color LeafDark = Color.FromRgb(0x1E, 0x7D, 0x4A);
        private static readonly Color Stone = Color.FromRgb(0x8C, 0x96, 0xA8);
        private static readonly Color StoneDark = Color.FromRgb(0x4A, 0x52, 0x64);
        private static readonly Color StandTop = Color.FromRgb(0x4A, 0x55, 0x6E);
        private static readonly Color StandBottom = Color.FromRgb(0x1C, 0x22, 0x33);
        private static readonly Color Caption = Color.FromRgb(0xA6, 0xC8, 0xDC);
        private static readonly Color Soft = Color.FromRgb(0xC2, 0xD8, 0xE6);
        private static readonly Color White = Colors.White;

        private static readonly Color[] Pebbles =
        [
            Color.FromRgb(0xC9, 0xA2, 0x6B),
            Color.FromRgb(0x9E, 0x7B, 0x52),
            Color.FromRgb(0xE3, 0xCF, 0xA6),
            Color.FromRgb(0x7A, 0x84, 0x96),
            Color.FromRgb(0xB5, 0x6D, 0x4F),
        ];

        /// <summary>One colour per fish, far enough apart that six never look like a school of twins.</summary>
        private static readonly Color[] Scales =
        [
            Color.FromRgb(0xFF, 0x8C, 0x2B),
            Color.FromRgb(0xFF, 0xC8, 0x3D),
            Color.FromRgb(0xFF, 0x6B, 0x8A),
            Color.FromRgb(0xA7, 0x8B, 0xFA),
            Color.FromRgb(0x7E, 0xE0, 0x81),
            Color.FromRgb(0x5C, 0xE1, 0xE6),
        ];

        /// <summary>The sphere with its top cut off: the shape everything inside is clipped to.</summary>
        private static readonly Geometry Bowl = BowlShape(closed: true);

        /// <summary>The same outline left open across the neck, so the glass is not drawn over the opening.</summary>
        private static readonly Geometry BowlEdge = BowlShape(closed: false);

        // Light on the glass: a long streak at the upper left, a short one low on the right.
        private static readonly Geometry Highlight = GlassArc(198, 246, Radius - 9);
        private static readonly Geometry Reflection = GlassArc(18, 48, Radius - 7);

        private static readonly Geometry Stand = StreamGeometry.Parse(
            "M74,206 C96,213 134,213 156,206 L168,224 C136,231 94,231 62,224 Z");

        private static readonly Geometry SandBed = StreamGeometry.Parse(
            "M10,201 C48,192 82,196 115,197 C150,198 186,192 220,200 L220,230 L10,230 Z");

        // A fish of length 24 facing right about its own middle, so it can be scaled and flipped.
        private static readonly Geometry FishBody = StreamGeometry.Parse(
            "M11,0 C9,-5.6 1,-7.2 -6,-2.6 Q-8,0 -6,2.6 C1,7.2 9,5.6 11,0 Z");

        private static readonly Geometry FishTail = StreamGeometry.Parse("M-5,0 L-13,-6.5 Q-10.5,0 -13,6.5 Z");

        private static readonly Geometry FishFin = StreamGeometry.Parse("M-2.5,-5 Q1.5,-10.5 6.5,-4.4 Z");

        private static readonly Geometry FishBelly = StreamGeometry.Parse("M1,4.6 Q3,8.5 6,4.2 Z");

        private sealed class Fish
        {
            public double X;
            public double Y;
            public double Depth;
            public double Dir;
            public double Face;
            public double Speed;
            public double Length;
            public double TargetLength;
            public double Alpha;
            public double Phase;
            public bool Gone;
            public int Colour;
        }

        private struct Bubble
        {
            public double X;
            public double Y;
            public double R;
            public double Speed;
            public double Phase;
            public bool Alive;
        }

        private struct Ripple
        {
            public double X;
            public double Life;
        }

        private struct Pebble
        {
            public double X;
            public double Y;
            public double R;
            public int Colour;
        }

        private readonly Dictionary<string, Fish> fish = [];
        private readonly HashSet<string> seen = [];
        private readonly List<string> leaving = [];
        private readonly Bubble[] bubbles = new Bubble[64];
        private readonly Ripple[] ripples = new Ripple[16];
        private readonly Pebble[] pebbles = new Pebble[54];
        private int nextBubble;
        private int nextRipple;
        private double bubbleClock;

        /// <summary>The still water line, before the waves; set each step.</summary>
        private double level = WaterLow;

        /// <summary>How far the plug is pulled, 0 to 1, eased so the whirlpool opens and closes.</summary>
        private double drain;

        /// <summary>The water's colour, 0 clear, 1 amber, 2 red, eased between the bands.</summary>
        private double tint;

        protected override void Seed()
        {
            for (int i = 0; i < pebbles.Length; i++)
            {
                double x = 28 + Random.NextDouble() * 174;

                pebbles[i] = new Pebble
                {
                    X = x,
                    Y = FloorY - 1 + Random.NextDouble() * 16,
                    R = 1.6 + Random.NextDouble() * 1.8,
                    Colour = Random.Next(Pebbles.Length),
                };
            }

            tint = MemoryLevel;
        }

        protected override void Advance(double dt)
        {
            drain += ((Busy ? 1 : 0) - drain) * (1 - Math.Exp(-dt * 2.2));
            tint += (MemoryLevel - tint) * (1 - Math.Exp(-dt * 1.5));
            level = WaterLow - (WaterLow - WaterHigh) * Memory + drain * 16;

            AdvanceFish(dt);
            AdvanceBubbles(dt);

            for (int i = 0; i < ripples.Length; i++)
            {
                ripples[i].Life -= dt * 1.6;
            }
        }

        private void AdvanceFish(double dt)
        {
            // Match the fish to the heaviest processes: new ones drop in, gone ones fade away.
            seen.Clear();
            IReadOnlyList<ProcessRow> rows = Processes;

            for (int i = 0; i < Math.Min(MaxFish, rows.Count); i++)
            {
                ProcessRow row = rows[i];
                seen.Add(row.Key);

                if (!fish.TryGetValue(row.Key, out Fish? f))
                {
                    fish[row.Key] = f = Spawn(row.Key);
                }

                f.TargetLength = 13 + Math.Clamp(row.Bar, 0, 100) / 100 * 17;
            }

            leaving.Clear();
            double ease = 1 - Math.Exp(-dt * 2);
            double hurry = (1 + Cpu * 0.7) * (1 + drain * 1.6);

            foreach ((string key, Fish f) in fish)
            {
                f.Gone = !seen.Contains(key);
                f.Alpha = Math.Clamp(f.Alpha + (f.Gone ? -dt : dt) * 1.2, 0, 1);

                if (f.Gone && f.Alpha <= 0)
                {
                    leaving.Add(key);
                    continue;
                }

                f.Length += (f.TargetLength - f.Length) * ease;

                // A fish keeps to its own depth, drifting slowly, and stays under the surface
                // however low the water goes.
                f.Depth = Math.Clamp(f.Depth + Math.Sin(Time * 0.13 + f.Phase) * dt * 0.04, 0.1, 0.9);

                double half = f.Length * 0.35;
                double top = level + 7 + half + drain * 10;
                double bottom = FloorY - 6 - half;
                double y = top >= bottom
                    ? bottom
                    : top + (bottom - top) * f.Depth + Math.Sin(Time * 0.7 + f.Phase) * 3
                        + Math.Sin(Time * 3 + f.Phase) * 6 * drain;

                f.Y += (Math.Clamp(y, Math.Min(top, bottom), bottom) - f.Y) * ease;

                // Back and forth, turning at the glass, and now and then for no reason at all.
                f.X += f.Dir * f.Speed * hurry * dt;

                double reach = HalfWidth(f.Y) - 6 - f.Length * 0.5;

                if (f.X > BowlX + reach)
                {
                    f.Dir = -1;
                    f.X = Math.Min(f.X, BowlX + reach + 2);
                }
                else if (f.X < BowlX - reach)
                {
                    f.Dir = 1;
                    f.X = Math.Max(f.X, BowlX - reach - 2);
                }
                else if (Random.NextDouble() < dt * 0.04)
                {
                    f.Dir = -f.Dir;
                }

                // Turning through zero reads as the fish swinging round rather than snapping.
                f.Face += (f.Dir - f.Face) * (1 - Math.Exp(-dt * 7));
            }

            foreach (string key in leaving)
            {
                fish.Remove(key);
            }
        }

        private Fish Spawn(string key)
        {
            // The first colour no fish is wearing, starting somewhere different each time.
            int start = Random.Next(Scales.Length);
            int colour = start;

            for (int i = 0; i < Scales.Length; i++)
            {
                int c = (start + i) % Scales.Length;

                if (!fish.Values.Any(f => !f.Gone && f.Colour == c))
                {
                    colour = c;
                    break;
                }
            }

            double depth = 0.15 + Random.NextDouble() * 0.7;
            double y = level + (FloorY - level) * depth;

            return new Fish
            {
                X = BowlX + (Random.NextDouble() - 0.5) * HalfWidth(y),
                Y = y,
                Depth = depth,
                Dir = Random.NextDouble() < 0.5 ? -1 : 1,
                Face = 0,
                Speed = 9 + Random.NextDouble() * 9,
                Length = 6,
                TargetLength = 13,
                Phase = Random.NextDouble() * Math.Tau,
                Colour = colour,
            };
        }

        private void AdvanceBubbles(double dt)
        {
            // Most bubbles come from the air stone; a clean sets the whole floor fizzing.
            bubbleClock += dt * (Busy ? 42 : 1.2 + Cpu * 16);

            while (bubbleClock >= 1)
            {
                bubbleClock -= 1;
                Release(Busy || Random.NextDouble() < 0.2
                    ? 50 + Random.NextDouble() * 130
                    : 140 + (Random.NextDouble() - 0.5) * 6);
            }

            double lift = Busy ? 95 : 22 + Cpu * 45;

            for (int i = 0; i < bubbles.Length; i++)
            {
                ref Bubble b = ref bubbles[i];

                if (!b.Alive)
                {
                    continue;
                }

                b.Y -= (b.Speed + lift) * dt;
                b.X += Math.Sin(Time * 4 + b.Phase) * dt * 6;

                if (b.Y - b.R < Surface(b.X))
                {
                    b.Alive = false;
                    ripples[nextRipple] = new Ripple { X = b.X, Life = 1 };
                    nextRipple = (nextRipple + 1) % ripples.Length;
                }
            }
        }

        private void Release(double x)
        {
            ref Bubble b = ref bubbles[nextBubble];
            nextBubble = (nextBubble + 1) % bubbles.Length;

            b.X = x;
            b.Y = FloorY - 4;
            b.R = 1.2 + Random.NextDouble() * Random.NextDouble() * 2.8;
            b.Speed = Random.NextDouble() * 12 + b.R * 4;
            b.Phase = Random.NextDouble() * Math.Tau;
            b.Alive = true;
        }

        protected override void CleanFinished()
        {
            // The plug goes back in with a last gulp of air.
            for (int i = 0; i < 20; i++)
            {
                Release(55 + Random.NextDouble() * 120);
            }
        }

        /// <summary>Half the bowl's inside width at a height.</summary>
        private static double HalfWidth(double y)
        {
            double dy = y - BowlY;

            return Math.Sqrt(Math.Max(Radius * Radius - dy * dy, 0));
        }

        /// <summary>The water line at a point across the bowl, with its waves and the whirlpool's dip.</summary>
        private double Surface(double x)
        {
            double swell = 1.4 + Cpu * 1.4 + drain * 2.4;
            double funnel = drain * 22 * Math.Exp(-(x - BowlX) * (x - BowlX) / 500);

            return level
                + Math.Sin(x * 0.07 + Time * 2.1) * swell
                + Math.Sin(x * 0.13 - Time * 1.4) * swell * 0.5
                + funnel;
        }

        protected override void Draw(DrawingContext context)
        {
            DrawStand(context);

            using (context.PushGeometryClip(Bowl))
            {
                context.DrawGeometry(Fade(WithAlpha(GlassInside, 0.4), WithAlpha(GlassInside, 0.7)), null, Bowl);

                DrawWater(context);
                DrawFloor(context);
                DrawPlants(context);
                DrawBubbles(context);
                DrawFish(context);
                DrawWhirlpool(context);
                DrawWaterLine(context);

                // The glass thickens towards the edge, which is what makes the bowl read as round.
                context.DrawGeometry(null, Pen(Shadow, 0.4, 10), Bowl);
            }

            DrawGlass(context);
            DrawReadout(context);
        }

        private void DrawStand(DrawingContext context)
        {
            context.DrawEllipse(Glow(Shadow, 0.6), null, new Point(BowlX, 226), 70, 8);
            context.DrawGeometry(Fade(StandTop, StandBottom), null, Stand);
            context.DrawLine(Pen(White, 0.18, 1), new Point(78, 209), new Point(152, 209));
        }

        private (Color Top, Color Deep) WaterColours()
        {
            // Quantised, so a slow change of colour reuses a few gradients rather than minting one a frame.
            double t = Math.Round(tint * 12) / 12;
            Color warnTop = Mix(WaterTop, Warn, 0.6);
            Color warnDeep = Mix(WaterDeep, Warn, 0.3);
            Color badTop = Mix(WaterTop, Bad, 0.7);
            Color badDeep = Mix(WaterDeep, Bad, 0.38);

            return t <= 1
                ? (Mix(WaterTop, warnTop, t), Mix(WaterDeep, warnDeep, t))
                : (Mix(warnTop, badTop, t - 1), Mix(warnDeep, badDeep, t - 1));
        }

        private void DrawWater(DrawingContext context)
        {
            (Color top, Color deep) = WaterColours();

            context.DrawGeometry(Fade(WithAlpha(top, 0.82), WithAlpha(deep, 0.95)), null, WaterShape());

            // Shafts of light from the surface, swaying slowly.
            for (int i = 0; i < 3; i++)
            {
                double x = 70 + i * 38 + Math.Sin(Time * 0.4 + i * 1.7) * 8;
                double y = Surface(x);
                double lean = 16 + i * 4;

                StreamGeometry shaft = new();

                using (StreamGeometryContext g = shaft.Open())
                {
                    g.BeginFigure(new Point(x, y), true);
                    g.LineTo(new Point(x + 9, y));
                    g.LineTo(new Point(x + 9 + lean + 10, FloorY));
                    g.LineTo(new Point(x + lean, FloorY));
                    g.EndFigure(true);
                }

                context.DrawGeometry(Solid(White, 0.045), null, shaft);
            }
        }

        private StreamGeometry WaterShape()
        {
            StreamGeometry water = new();

            using (StreamGeometryContext g = water.Open())
            {
                g.BeginFigure(new Point(10, Surface(10)), true);

                for (double x = 16; x <= 220; x += 6)
                {
                    g.LineTo(new Point(x, Surface(x)));
                }

                g.LineTo(new Point(220, Side));
                g.LineTo(new Point(10, Side));
                g.EndFigure(true);
            }

            return water;
        }

        private void DrawWaterLine(DrawingContext context)
        {
            StreamGeometry line = new();

            using (StreamGeometryContext g = line.Open())
            {
                g.BeginFigure(new Point(10, Surface(10)), false);

                for (double x = 16; x <= 220; x += 6)
                {
                    g.LineTo(new Point(x, Surface(x)));
                }

                g.EndFigure(false);
            }

            (Color top, _) = WaterColours();
            context.DrawGeometry(null, Pen(Mix(top, White, 0.6), 0.75, 1.5), line);

            foreach (Ripple r in ripples)
            {
                if (r.Life > 0)
                {
                    double grow = 1 - r.Life;
                    context.DrawEllipse(null, Pen(White, r.Life * 0.5, 0.75), new Point(r.X, Surface(r.X)), 2 + grow * 7, 0.8 + grow * 1.6);
                }
            }
        }

        private void DrawFloor(DrawingContext context)
        {
            context.DrawGeometry(Fade(Sand, SandDeep), null, SandBed);

            foreach (Pebble p in pebbles)
            {
                Color c = Pebbles[p.Colour];
                context.DrawEllipse(Solid(c), null, new Point(p.X, p.Y), p.R * 1.25, p.R);
                context.DrawEllipse(Solid(White, 0.25), null, new Point(p.X - p.R * 0.35, p.Y - p.R * 0.4), p.R * 0.4, p.R * 0.3);
            }

            // The air stone the bubbles come from.
            context.DrawEllipse(Fade(Stone, StoneDark), null, new Point(140, 199), 13, 8);
            context.DrawEllipse(Solid(White, 0.2), null, new Point(136, 195), 5, 2);
        }

        private void DrawPlants(DrawingContext context)
        {
            DrawPlant(context, 76, 201, 46, 0);
            DrawPlant(context, 88, 202, 30, 1.3);
            DrawPlant(context, 166, 200, 38, 2.6);
        }

        private void DrawPlant(DrawingContext context, double x, double y, double height, double phase)
        {
            // A few blades, each bending a little more towards its tip, all swaying together.
            for (int i = 0; i < 4; i++)
            {
                double h = height * (0.65 + 0.12 * ((i * 7) % 4));
                double lean = (i - 1.5) * 5;
                double sway = Math.Sin(Time * 1.2 + phase + i * 0.6) * (5 + drain * 6) + lean;
                double bx = x + (i - 1.5) * 2.5;

                StreamGeometry blade = new();

                using (StreamGeometryContext g = blade.Open())
                {
                    g.BeginFigure(new Point(bx, y), false);
                    g.CubicBezierTo(
                        new Point(bx - lean * 0.3, y - h * 0.4),
                        new Point(bx + sway * 0.6, y - h * 0.75),
                        new Point(bx + sway, y - h));
                    g.EndFigure(false);
                }

                context.DrawGeometry(null, Pen(i % 2 == 0 ? Leaf : LeafDark, 3.25), blade);
            }
        }

        private void DrawBubbles(DrawingContext context)
        {
            foreach (Bubble b in bubbles)
            {
                if (!b.Alive)
                {
                    continue;
                }

                Point at = new(b.X, b.Y);
                context.DrawEllipse(Solid(White, 0.1), Pen(White, 0.55, 0.75), at, b.R, b.R);
                context.DrawEllipse(Solid(White, 0.75), null, new Point(b.X - b.R * 0.35, b.Y - b.R * 0.35), b.R * 0.3, b.R * 0.3);
            }
        }

        private void DrawFish(DrawingContext context)
        {
            // The smallest first, so the heaviest process swims in front.
            foreach (Fish f in fish.Values.OrderBy(f => f.Length))
            {
                if (f.Alpha <= 0)
                {
                    continue;
                }

                double k = f.Length / 24;
                double face = Math.Abs(f.Face) < 0.12 ? (f.Face < 0 ? -0.12 : 0.12) : f.Face;
                double tilt = Math.Sin(Time * 1.1 + f.Phase) * 0.08;
                double wag = Math.Sin(Time * (7 + drain * 8) + f.Phase) * 0.35;

                Color scale = Scales[f.Colour];
                Color light = Mix(scale, White, 0.5);
                Color fin = Mix(scale, Shadow, 0.25);

                Matrix place = Matrix.CreateScale(face * k, k) * Matrix.CreateRotation(tilt) * Matrix.CreateTranslation(f.X, f.Y);
                Matrix swish = Matrix.CreateTranslation(5, 0) * Matrix.CreateRotation(wag) * Matrix.CreateTranslation(-5, 0);

                using (context.PushOpacity(f.Alpha))
                using (context.PushTransform(place))
                {
                    context.DrawEllipse(Glow(scale, 0.25), null, new Point(0, 0), 16, 11);

                    using (context.PushTransform(swish))
                    {
                        context.DrawGeometry(Solid(fin, 0.9), null, FishTail);
                    }

                    context.DrawGeometry(Solid(fin, 0.9), null, FishFin);
                    context.DrawGeometry(Solid(fin, 0.8), null, FishBelly);
                    context.DrawGeometry(Fade(light, scale), Pen(fin, 0.5), FishBody);
                    context.DrawEllipse(Solid(White, 0.3), null, new Point(2, -2.2), 5, 1.3);

                    context.DrawEllipse(Solid(White), null, new Point(6.2, -1.2), 1.6, 1.6);
                    context.DrawEllipse(Solid(Shadow), null, new Point(6.6, -1.2), 0.9, 0.9);
                }
            }
        }

        private void DrawWhirlpool(DrawingContext context)
        {
            if (drain < 0.02)
            {
                return;
            }

            // Streaks circling the plug, closing in as they go down.
            double middle = (level + FloorY) / 2;

            for (int i = 0; i < 4; i++)
            {
                double rx = 60 - i * 13;
                double ry = 12 - i * 2.2;
                double cy = middle - 18 + i * 12;
                double a = Time * (2.6 + i * 0.8) + i * 1.9;
                double b = a + 1.7;

                StreamGeometry streak = new();

                using (StreamGeometryContext g = streak.Open())
                {
                    g.BeginFigure(new Point(BowlX + rx * Math.Cos(a), cy + ry * Math.Sin(a)), false);
                    g.ArcTo(new Point(BowlX + rx * Math.Cos(b), cy + ry * Math.Sin(b)), new Size(rx, ry), 0, false, SweepDirection.Clockwise);
                    g.EndFigure(false);
                }

                context.DrawGeometry(null, Pen(White, 0.35 * drain, 1.5), streak);
            }
        }

        private void DrawGlass(DrawingContext context)
        {
            context.DrawGeometry(null, Pen(GlassEdge, 0.5, 1.75), BowlEdge);
            context.DrawGeometry(null, Pen(White, 0.35, 4.5), Highlight);
            context.DrawGeometry(null, Pen(White, 0.18, 2.5), Reflection);
            context.DrawEllipse(Solid(White, 0.4), null, new Point(150, 60), 5, 2.4);

            // The neck, lit in the accent when the pointer is on it, as the dial's rim is.
            Color rim = Hovered ? Accent : RimIdle;
            context.DrawEllipse(Solid(GlassInside, 0.35), Pen(rim, 2.75), new Point(BowlX, RimY), RimHalf, 6);
            context.DrawEllipse(null, Pen(White, 0.25, 1), new Point(BowlX, RimY - 1), RimHalf - 4, 4);
        }

        private void DrawReadout(DrawingContext context)
        {
            const double x = BowlX;

            // A soft dark pool behind the figures, so they read over water, fish or empty glass.
            context.DrawEllipse(Glow(Shadow, 0.8), null, new Point(x, 106), 70, 44);

            Text(context, MemoryCaption, new Point(x, 82), 8, Caption);

            if (Flashing)
            {
                Text(context, FlashText, new Point(x, 104), 24, FlashGood ? Good : White, bold: true);

                if (FlashGood)
                {
                    Text(context, FreedCaption, new Point(x, 123), 8, Caption);
                }

                return;
            }

            Text(context, MemoryText, new Point(x, 104), 28, LevelColour(MemoryLevel, White), bold: true);
            Text(context, Hovered ? ClickToClean : UsedOfTotal, new Point(x, 124), 8.5, Hovered ? Accent : Soft);
            Text(context, $"{CpuCaption} {CpuText} · {GpuCaption} {GpuText} · {DiskCaption} {DiskText}", new Point(x, 137), 7, Caption, mono: true);
        }

        private static Geometry BowlShape(bool closed)
        {
            StreamGeometry shape = new();

            using (StreamGeometryContext g = shape.Open())
            {
                // The long way round, down the left side, under the bottom and up the right.
                g.BeginFigure(new Point(BowlX - RimHalf, RimY), closed);
                g.ArcTo(new Point(BowlX + RimHalf, RimY), new Size(Radius, Radius), 0, true, SweepDirection.CounterClockwise);
                g.EndFigure(closed);
            }

            return shape;
        }

        /// <summary>An arc on the glass between two angles, in degrees clockwise from the right.</summary>
        private static Geometry GlassArc(double from, double to, double radius)
        {
            double a = from * Math.PI / 180;
            double b = to * Math.PI / 180;
            StreamGeometry arc = new();

            using (StreamGeometryContext g = arc.Open())
            {
                g.BeginFigure(new Point(BowlX + radius * Math.Cos(a), BowlY + radius * Math.Sin(a)), false);
                g.ArcTo(new Point(BowlX + radius * Math.Cos(b), BowlY + radius * Math.Sin(b)), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                g.EndFigure(false);
            }

            return arc;
        }
    }
}
