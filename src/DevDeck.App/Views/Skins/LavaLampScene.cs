using Avalonia;
using Avalonia.Media;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  A lava lamp on the desk, wax rising and falling through violet liquid.
    /// </summary>
    /// <remarks>
    ///  The wax is memory in use: more of it, in bigger blobs over a deeper pool, the fuller memory
    ///  is. The processor is the heat - blobs drift up and sink back over half a minute at idle and
    ///  churn in a few seconds under load. Past the high mark the wax turns amber, in the red it
    ///  turns red, and the halo round the glass brightens with the load. A clean is the lamp
    ///  overheating: the bulb goes white-hot, every blob races to the top and bursts into droplets,
    ///  and the wax that reforms from the pool is smaller, because there is less memory to make it.
    ///
    ///  The wax is a real metaball field, sampled on a two pixel grid over the glass and contoured
    ///  each frame, so blobs neck and merge as goo does rather than overlapping as circles.
    /// </remarks>
    internal sealed class LavaLampScene : SkinScene
    {
        private const double LampX = 115;
        private const double GlassTop = 44;
        private const double GlassBottom = 166;

        // The highest and lowest a blob's centre travels: just under the cap, down into the pool.
        private const double RiseTop = 60;
        private const double RiseBottom = 164;

        // The metaball grid: two pixel cells over the glass's bounding box.
        private const double GridX = 90;
        private const double GridY = 42;
        private const double Cell = 2;
        private const int Cols = 26;
        private const int Rows = 63;

        private const double LeftX = 46;
        private const double RightX = 184;

        private static readonly Color Backdrop = Color.FromRgb(0x0C, 0x0A, 0x1A);
        private static readonly Color LiquidTop = Color.FromRgb(0x16, 0x0C, 0x38);
        private static readonly Color LiquidBottom = Color.FromRgb(0x3B, 0x1A, 0x7A);
        private static readonly Color MetalLight = Color.FromRgb(0x9A, 0x9E, 0xB8);
        private static readonly Color MetalDark = Color.FromRgb(0x22, 0x23, 0x33);
        private static readonly Color Bulb = Color.FromRgb(0xFF, 0xB8, 0x5C);
        private static readonly Color Faint = Color.FromRgb(0x9C, 0x98, 0xBC);
        private static readonly Color Soft = Color.FromRgb(0xBE, 0xB8, 0xD8);
        private static readonly Color White = Colors.White;

        // The wax in three moods, each a top and a bottom: calm magenta into orange, amber, red.
        private static readonly Color WaxTop = Color.FromRgb(0xFF, 0x4F, 0xB0);
        private static readonly Color WaxBottom = Color.FromRgb(0xFF, 0x7A, 0x45);
        private static readonly Color AmberTop = Color.FromRgb(0xFF, 0xC4, 0x4A);
        private static readonly Color AmberBottom = Color.FromRgb(0xFF, 0x8C, 0x2A);
        private static readonly Color RedTop = Color.FromRgb(0xFF, 0x5A, 0x48);
        private static readonly Color RedBottom = Color.FromRgb(0xD0, 0x22, 0x2A);

        // The vessel: narrow under the cap, widest a little below the middle, narrowing into the base.
        private static readonly Geometry Glass = StreamGeometry.Parse(
            "M102,44 C96,70 91,90 91,110 C91,135 97,152 100,166 L130,166 C133,152 139,135 139,110 C139,90 134,70 128,44 Z");

        private static readonly Geometry Cap = StreamGeometry.Parse("M108,17 L122,17 L129,46 L101,46 Z");

        private static readonly Geometry CapShine = StreamGeometry.Parse("M110,17 L113,17 L109,46 L104,46 Z");

        private static readonly Geometry Base = StreamGeometry.Parse("M100,168 L130,168 L150,211 L80,211 Z");

        private static readonly Geometry BaseShine = StreamGeometry.Parse("M105,168 L110,168 L97,211 L89,211 Z");

        // Light caught on the curve of the glass, long on the lit side and short on the other.
        private static readonly Geometry Streak = StreamGeometry.Parse("M103,54 C98,76 95,94 95.5,112 C96,132 99,146 102,158");

        private static readonly Geometry FarStreak = StreamGeometry.Parse("M133,84 C134.5,94 135,104 134.8,114");

        private struct Blob
        {
            public double X;
            public double Y;
            public double Vy;

            /// <summary>Where along its rise and fall the blob is; 0 is down in the pool.</summary>
            public double Phase;
            public double Rate;

            /// <summary>How far up the glass this blob gets, as a share of the whole rise.</summary>
            public double Reach;
            public double Sway;
            public double SwayRate;
            public double Scale;

            /// <summary>0 to 1: grown, or melted back into the pool.</summary>
            public double Size;

            /// <summary>Shooting for the top during a clean, to burst there.</summary>
            public bool Racing;
        }

        private struct Drop
        {
            public double X;
            public double Y;
            public double Vx;
            public double Vy;
            public double R;
            public double Life;
        }

        private readonly Blob[] blobs = new Blob[10];
        private readonly Drop[] drops = new Drop[72];
        private readonly double[] field = new double[(Cols + 1) * (Rows + 1)];
        private readonly Point[] polygon = new Point[8];
        private int nextDrop;

        /// <summary>0 cold to 1 white-hot; follows a clean in and lingers a little after.</summary>
        private double heat;

        /// <summary>0 calm to 1 red, eased so the wax changes colour slowly rather than on a frame.</summary>
        private double warmth;

        protected override void Seed()
        {
            for (int i = 0; i < blobs.Length; i++)
            {
                ref Blob b = ref blobs[i];
                b.Phase = Random.NextDouble() * Math.Tau;
                b.Rate = 0.22 + Random.NextDouble() * 0.2;
                b.Reach = 0.55 + Random.NextDouble() * 0.45;
                b.Sway = Random.NextDouble() * Math.Tau;
                b.SwayRate = 0.25 + Random.NextDouble() * 0.3;
                b.Scale = 0.8 + Random.NextDouble() * 0.45;
                b.Size = WantedSize(i);
                b.Y = Altitude(b);
                b.X = Drift(b, b.Y);
            }
        }

        /// <summary>How many blobs memory makes, the last one part grown.</summary>
        private double WantedSize(int index)
        {
            double count = 3 + Memory * (blobs.Length - 3);
            double size = Math.Clamp(count - index, 0, 1);

            // Wax that reforms during a clean is smaller, as the memory behind it is going.
            return Busy ? size * 0.55 : size;
        }

        private static double Altitude(Blob b) =>
            RiseBottom - (RiseBottom - RiseTop) * b.Reach * (0.5 - 0.5 * Math.Cos(b.Phase));

        private static double Drift(Blob b, double y) => LampX + HalfWidth(y) * 0.45 * Math.Sin(b.Sway);

        /// <summary>The inside half width of the glass at a height, near enough to keep wax off the walls.</summary>
        private static double HalfWidth(double y)
        {
            if (y < 110)
            {
                return 13 + 11 * Math.Sin(Math.PI / 2 * Math.Clamp((y - GlassTop) / 66, 0, 1));
            }

            double t = Math.Clamp((y - 110) / 56, 0, 1);

            return 24 - 9 * t * t;
        }

        private double BlobRadius(Blob b) => (5.5 + Memory * 3.5) * b.Scale * b.Size;

        private double PoolDepth() => (3 + Memory * 14) * (1 - 0.45 * heat);

        protected override void Advance(double dt)
        {
            heat += ((Busy ? 1 : 0) - heat) * (1 - Math.Exp(-dt * (Busy ? 2 : 0.6)));

            double level = MemoryLevel switch
            {
                2 => 1,
                1 => 0.55,
                _ => 0,
            };
            warmth += (level - warmth) * (1 - Math.Exp(-dt * 0.8));

            // The processor is the heat under the wax: a slow half-minute cycle at idle, seconds when busy.
            double pace = 0.5 + Cpu * 2;
            double follow = 1 - Math.Exp(-dt * 2);
            double grow = 1 - Math.Exp(-dt * 0.8);

            for (int i = 0; i < blobs.Length; i++)
            {
                ref Blob b = ref blobs[i];
                b.Size += (WantedSize(i) - b.Size) * grow;
                b.Sway += dt * b.SwayRate * pace * 0.5;

                if (Busy && !b.Racing && b.Size > 0.25 && Random.NextDouble() < dt * 1.5)
                {
                    b.Racing = true;
                }

                if (b.Racing)
                {
                    b.Vy = Math.Max(b.Vy - dt * 260, -180);
                    b.Y += b.Vy * dt;

                    if (b.Y <= RiseTop)
                    {
                        Burst(b);

                        // Melted back into the pool, to grow again from the bottom.
                        b.Racing = false;
                        b.Size = 0;
                        b.Vy = 0;
                        b.Phase = 0;
                        b.Y = RiseBottom + 2;
                        b.X = LampX + (Random.NextDouble() - 0.5) * 8;
                    }

                    continue;
                }

                b.Phase += dt * b.Rate * pace;

                // Followed rather than placed, so a blob that has just burst drifts back into its cycle.
                double y = b.Y + (Altitude(b) - b.Y) * follow;
                b.Vy = (y - b.Y) / Math.Max(dt, 0.001);
                b.Y = y;
                b.X += (Drift(b, y) - b.X) * follow;
            }

            for (int i = 0; i < drops.Length; i++)
            {
                ref Drop d = ref drops[i];

                if (d.Life <= 0)
                {
                    continue;
                }

                // Thrown out by the burst, then slowed by the liquid and sinking back.
                double drag = Math.Exp(-dt * 2.2);
                d.Vx *= drag;
                d.Vy = d.Vy * drag + dt * 30;
                d.X += d.Vx * dt;
                d.Y += d.Vy * dt;

                double wall = HalfWidth(d.Y) - d.R - 1;
                d.X = Math.Clamp(d.X, LampX - wall, LampX + wall);
                d.Life -= dt * 0.3;

                if (d.Y > GlassBottom - PoolDepth())
                {
                    d.Life = 0;
                }
            }
        }

        private void Burst(Blob b)
        {
            int count = 5 + Random.Next(4);

            for (int k = 0; k < count; k++)
            {
                ref Drop d = ref drops[nextDrop];
                nextDrop = (nextDrop + 1) % drops.Length;

                double angle = Random.NextDouble() * Math.Tau;
                double speed = 25 + Random.NextDouble() * 45;
                d.X = b.X;
                d.Y = b.Y;
                d.Vx = Math.Cos(angle) * speed;
                d.Vy = Math.Sin(angle) * speed * 0.7 - 10;
                d.R = 1.1 + Random.NextDouble() * 1.6;
                d.Life = 1;
            }
        }

        protected override void Draw(DrawingContext context)
        {
            // A dark card behind the whole lamp, so the readings hold up on any wallpaper.
            Color edge = Hovered ? WithAlpha(Accent, 0.6) : WithAlpha(White, 0.08);
            context.DrawRectangle(Solid(Backdrop, 0.8), Pen(edge, 1.2), new Rect(5, 5, Side - 10, Side - 10), 26, 26);

            (Color top, Color bottom) = WaxColours();
            Color mid = Mix(top, bottom, 0.5);

            // The halo: the lamp lighting the room round it, brighter the harder the machine works.
            double load = Memory * 0.5 + Cpu * 0.5;
            context.DrawEllipse(Glow(mid, 0.1 + load * 0.3 + heat * 0.25), null, new Point(LampX, 108), 56, 88);

            using (context.PushGeometryClip(Glass))
            {
                context.DrawRectangle(Fade(LiquidTop, LiquidBottom), null, new Rect(GridX, GlassTop, 52, GlassBottom - GlassTop));

                DrawBulbLight(context);
                DrawWax(context, top, bottom, mid);
                DrawDrops(context, mid);

                // Shade both sides, so the vessel reads as round rather than flat.
                context.DrawRectangle(Fade(WithAlpha(Colors.Black, 0.45), WithAlpha(Colors.Black, 0), horizontal: true), null, new Rect(90, GlassTop, 16, 124));
                context.DrawRectangle(Fade(WithAlpha(Colors.Black, 0), WithAlpha(Colors.Black, 0.5), horizontal: true), null, new Rect(122, GlassTop, 18, 124));
            }

            context.DrawGeometry(null, Pen(White, 0.18, 1.1), Glass);
            context.DrawGeometry(null, Pen(White, 0.3, 2.2), Streak);
            context.DrawGeometry(null, Pen(White, 0.14, 1.4), FarStreak);

            DrawMetal(context);
            DrawReadout(context, mid);
        }

        /// <summary>The wax's top and bottom colours, stepped so a slow change reuses a few brushes.</summary>
        private (Color Top, Color Bottom) WaxColours()
        {
            double w = Math.Round(warmth * 12) / 12;

            return w < 0.5
                ? (Mix(WaxTop, AmberTop, w * 2), Mix(WaxBottom, AmberBottom, w * 2))
                : (Mix(AmberTop, RedTop, w * 2 - 1), Mix(AmberBottom, RedBottom, w * 2 - 1));
        }

        private void DrawBulbLight(DrawingContext context)
        {
            // Warm through the bottom of the liquid, and white-hot while a clean runs.
            double h = Math.Round(heat * 12) / 12;
            Color light = Mix(Bulb, White, h);

            context.DrawEllipse(Glow(light, 0.4 + Memory * 0.15 + h * 0.45), null, new Point(LampX, GlassBottom + 4), 30 + h * 10, 26 + h * 26);
        }

        private void DrawWax(DrawingContext context, Color top, Color bottom, Color mid)
        {
            // A soft light round every blob first, the glow wax gives off into the liquid.
            foreach (Blob b in blobs)
            {
                double r = BlobRadius(b);

                if (r > 0.5)
                {
                    context.DrawEllipse(Glow(mid, 0.3), null, new Point(b.X, b.Y), r * 2.4, r * 2.6);
                }
            }

            Sample();

            context.DrawGeometry(Fade(top, bottom), null, Contour(1, 0, 0));

            // The same field at a higher level, nudged up and left, is the sheen on the goo.
            context.DrawGeometry(Solid(Mix(top, White, 0.55), 0.35), null, Contour(2.4, -0.8, -1.4));
        }

        /// <summary>Fills the metaball field: every blob plus the pool lying on the bottom of the glass.</summary>
        private void Sample()
        {
            double pool = PoolDepth();
            double floor = GlassBottom + 1;

            for (int j = 0; j <= Rows; j++)
            {
                double y = GridY + j * Cell;

                for (int i = 0; i <= Cols; i++)
                {
                    double x = GridX + i * Cell;

                    // The pool's surface wobbles a little, as warm wax does.
                    double depth = pool * (1 + 0.07 * Math.Sin(x * 0.25 + Time * 0.8));
                    double gap = Math.Max(floor - y, 0.6);
                    double sum = depth * depth / (gap * gap);

                    foreach (Blob b in blobs)
                    {
                        double r = BlobRadius(b);

                        if (r < 0.3)
                        {
                            continue;
                        }

                        // A moving blob is drawn out along its path, rising or falling.
                        double stretch = 1 + Math.Min(Math.Abs(b.Vy) / 50, 0.9);
                        double dx = x - b.X;
                        double dy = (y - b.Y) / stretch;
                        sum += r * r / (dx * dx + dy * dy + 0.01);
                    }

                    field[j * (Cols + 1) + i] = sum;
                }
            }
        }

        /// <summary>
        ///  The part of the grid at or above <paramref name="level"/>, as one filled shape.
        /// </summary>
        /// <remarks>
        ///  Each cell becomes a polygon of its inside corners and the points where its edges cross
        ///  the level, always walked the same way round. Every cell is wound alike and neighbours
        ///  share their edges exactly, so the whole shape fills without seams between cells.
        /// </remarks>
        private StreamGeometry Contour(double level, double offsetX, double offsetY)
        {
            StreamGeometry geometry = new();

            using StreamGeometryContext g = geometry.Open();
            g.SetFillRule(FillRule.NonZero);

            Span<double> v = stackalloc double[4];
            Span<Point> corner = stackalloc Point[4];

            for (int j = 0; j < Rows; j++)
            {
                double y0 = GridY + j * Cell + offsetY;

                for (int i = 0; i < Cols; i++)
                {
                    v[0] = field[j * (Cols + 1) + i];
                    v[1] = field[j * (Cols + 1) + i + 1];
                    v[2] = field[(j + 1) * (Cols + 1) + i + 1];
                    v[3] = field[(j + 1) * (Cols + 1) + i];

                    if (v[0] < level && v[1] < level && v[2] < level && v[3] < level)
                    {
                        continue;
                    }

                    double x0 = GridX + i * Cell + offsetX;
                    corner[0] = new Point(x0, y0);
                    corner[1] = new Point(x0 + Cell, y0);
                    corner[2] = new Point(x0 + Cell, y0 + Cell);
                    corner[3] = new Point(x0, y0 + Cell);

                    int n = 0;

                    for (int k = 0; k < 4; k++)
                    {
                        int next = (k + 1) & 3;
                        bool inside = v[k] >= level;

                        if (inside)
                        {
                            polygon[n++] = corner[k];
                        }

                        if (inside != (v[next] >= level))
                        {
                            double t = (level - v[k]) / (v[next] - v[k]);
                            polygon[n++] = corner[k] + (corner[next] - corner[k]) * t;
                        }
                    }

                    g.BeginFigure(polygon[0], true);

                    for (int k = 1; k < n; k++)
                    {
                        g.LineTo(polygon[k]);
                    }

                    g.EndFigure(true);
                }
            }

            return geometry;
        }

        private void DrawDrops(DrawingContext context, Color colour)
        {
            foreach (Drop d in drops)
            {
                if (d.Life > 0)
                {
                    Point at = new(d.X, d.Y);
                    context.DrawEllipse(Glow(colour, 0.4 * d.Life), null, at, d.R * 2.6, d.R * 2.6);
                    context.DrawEllipse(Solid(colour, 0.35 + 0.6 * d.Life), null, at, d.R, d.R);
                }
            }
        }

        private void DrawMetal(DrawingContext context)
        {
            IImmutableBrush metal = Fade(MetalLight, MetalDark, horizontal: true);
            IImmutableBrush shine = Fade(WithAlpha(White, 0.5), WithAlpha(White, 0.06));

            context.DrawGeometry(metal, Pen(Colors.Black, 0.35, 0.8), Cap);
            context.DrawGeometry(shine, null, CapShine);
            context.DrawEllipse(Solid(MetalLight), null, new Point(LampX, 17), 7, 2.2);

            context.DrawGeometry(metal, Pen(Colors.Black, 0.35, 0.8), Base);
            context.DrawGeometry(shine, null, BaseShine);
            context.DrawRectangle(Fade(MetalLight, MetalDark, horizontal: true), null, new Rect(78, 209, 74, 5), 2, 2);

            // The collar the glass sits in, lit from below by the bulb when it runs hot.
            context.DrawRectangle(Fade(MetalLight, MetalDark, horizontal: true), Pen(Colors.Black, 0.35, 0.6), new Rect(97, 163, 36, 6), 2, 2);

            if (heat > 0.05)
            {
                context.DrawEllipse(Glow(White, heat * 0.7), null, new Point(LampX, 166), 26, 7);
            }
        }

        private void DrawReadout(DrawingContext context, Color wax)
        {
            Text(context, MemoryCaption, new Point(LeftX, 92), 8, Faint);

            if (Flashing)
            {
                Text(context, FlashText, new Point(LeftX, 114), 19, FlashGood ? Good : White, bold: true);

                if (FlashGood)
                {
                    Text(context, FreedCaption, new Point(LeftX, 134), 8, Faint);
                }
            }
            else
            {
                Text(context, MemoryText, new Point(LeftX, 114), 26, LevelColour(MemoryLevel, White), bold: true);
                Text(context, Hovered ? ClickToClean : UsedOfTotal, new Point(LeftX, 135), 7.5, Hovered ? Accent : Soft);
            }

            Reading(context, CpuCaption, CpuText, Cpu, 82, LevelColour(CpuLevel, White), wax);
            Reading(context, GpuCaption, GpuText, Gpu, 114, White, wax);
            Reading(context, DiskCaption, DiskText, Disk, 146, White, wax);
        }

        /// <summary>One of the small readings on the right: caption, value, and a thin bar of it.</summary>
        private void Reading(DrawingContext context, string caption, string value, double share, double y, Color colour, Color wax)
        {
            Text(context, caption, new Point(RightX, y), 7.5, Faint, mono: true);
            Text(context, value, new Point(RightX, y + 13), 12, colour, bold: true);

            const double half = 15;
            double left = RightX - half;
            double barY = y + 23;
            context.DrawLine(Pen(White, 0.12, 1.5), new Point(left, barY), new Point(RightX + half, barY));

            if (share > 0.01)
            {
                context.DrawLine(Pen(wax, 0.85, 1.5), new Point(left, barY), new Point(left + half * 2 * share, barY));
            }
        }
    }
}
