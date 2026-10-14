using Avalonia;
using Avalonia.Media;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  A snow globe on a wooden stand, with a cabin in the pines and the weather inside the glass.
    /// </summary>
    /// <remarks>
    ///  The weather is the processor: a clear dusk with the moon and stars at idle, clouds drifting
    ///  in as the load rises, rain past the middle, and a thunderstorm, lightning and all, in the red.
    ///  The sky darkens with it. Memory is the snow piled in the bottom of the globe, burying the
    ///  cabin as it climbs, its edge glowing amber or red past the marks; a light snowfall drifts down
    ///  whatever the readings. A clean shakes the globe: the drift is thrown up into a blizzard that
    ///  settles again, lower, once the memory has come back. The memory reading sits in the sky on a
    ///  soft shadow, so it reads through any weather; the other gauges are on the stand's plaque.
    /// </remarks>
    internal sealed class GlobeScene : SkinScene
    {
        private const double Radius = 88;
        private const double Floor = 186;

        private static readonly Point GlobeCentre = new(115, 98);

        // Dusk when idle, slate when stormy; everything in the globe is mixed between the two.
        private static readonly Color DuskTop = Color.FromRgb(0x1C, 0x33, 0x63);
        private static readonly Color DuskLow = Color.FromRgb(0xE8, 0x9F, 0x7A);
        private static readonly Color StormTop = Color.FromRgb(0x0B, 0x0E, 0x16);
        private static readonly Color StormLow = Color.FromRgb(0x38, 0x40, 0x4E);
        private static readonly Color MoonLight = Color.FromRgb(0xFF, 0xF3, 0xD6);
        private static readonly Color MoonGlow = Color.FromRgb(0xFF, 0xE2, 0xA8);
        private static readonly Color CloudLight = Color.FromRgb(0xEE, 0xF1, 0xF7);
        private static readonly Color CloudDark = Color.FromRgb(0x4A, 0x52, 0x60);
        private static readonly Color CloudShadeLight = Color.FromRgb(0xB9, 0xC3, 0xD6);
        private static readonly Color CloudShadeDark = Color.FromRgb(0x2A, 0x30, 0x3B);
        private static readonly Color HillBack = Color.FromRgb(0x2B, 0x4A, 0x57);
        private static readonly Color HillFront = Color.FromRgb(0x1F, 0x3A, 0x44);
        private static readonly Color HillNight = Color.FromRgb(0x12, 0x1A, 0x22);
        private static readonly Color Pine = Color.FromRgb(0x1E, 0x4D, 0x3A);
        private static readonly Color PineNight = Color.FromRgb(0x0E, 0x22, 0x1A);
        private static readonly Color Wall = Color.FromRgb(0x8A, 0x4B, 0x2C);
        private static readonly Color Roof = Color.FromRgb(0x5A, 0x2A, 0x22);
        private static readonly Color Lamp = Color.FromRgb(0xFF, 0xC8, 0x6B);
        private static readonly Color Rain = Color.FromRgb(0xB8, 0xD2, 0xF0);
        private static readonly Color BoltGlow = Color.FromRgb(0xA9, 0xC8, 0xFF);
        private static readonly Color SnowLit = Color.FromRgb(0xF7, 0xFA, 0xFF);
        private static readonly Color SnowShade = Color.FromRgb(0xB4, 0xC6, 0xE2);
        private static readonly Color SnowDim = Color.FromRgb(0x8E, 0x9B, 0xB0);
        private static readonly Color Night = Color.FromRgb(0x06, 0x0B, 0x18);
        private static readonly Color GlassEdge = Color.FromRgb(0xC8, 0xDC, 0xF0);
        private static readonly Color WoodDeep = Color.FromRgb(0x2A, 0x18, 0x0E);
        private static readonly Color WoodLight = Color.FromRgb(0xC2, 0x84, 0x52);
        private static readonly Color BrassLight = Color.FromRgb(0xF0, 0xD3, 0x86);
        private static readonly Color BrassDark = Color.FromRgb(0xA0, 0x7C, 0x36);
        private static readonly Color Engraving = Color.FromRgb(0x3A, 0x28, 0x0C);
        private static readonly Color Caption = Color.FromRgb(0xC9, 0xD6, 0xEA);
        private static readonly Color White = Colors.White;

        private static readonly EllipseGeometry Inside = Circle(GlobeCentre, Radius - 1);

        // Lit from the left like a turned piece of wood: dark, a highlight, then round into shadow.
        private static readonly IImmutableBrush Wood = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x3B, 0x22, 0x14), 0),
                new GradientStop(Color.FromRgb(0xA8, 0x6A, 0x3C), 0.3),
                new GradientStop(Color.FromRgb(0x7A, 0x4A, 0x2A), 0.65),
                new GradientStop(Color.FromRgb(0x33, 0x1E, 0x12), 1),
            },
        }.ToImmutable();

        // Clear in the middle and darker at the edge, which is what makes a flat disc read as a ball.
        private static readonly IImmutableBrush Vignette = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0x05, 0x0A, 0x18), 0),
                new GradientStop(Color.FromArgb(0, 0x05, 0x0A, 0x18), 0.7),
                new GradientStop(Color.FromArgb(0x38, 0x05, 0x0A, 0x18), 0.9),
                new GradientStop(Color.FromArgb(0x80, 0x05, 0x0A, 0x18), 1),
            },
        }.ToImmutable();

        private static readonly Geometry BackHill = StreamGeometry.Parse(
            "M20,150 C50,136 82,131 112,139 C140,146 166,127 212,133 L212,194 L20,194 Z");

        private static readonly Geometry FrontHill = StreamGeometry.Parse(
            "M20,166 C55,154 90,158 120,162 C150,166 180,151 212,156 L212,194 L20,194 Z");

        // A pine one unit tall, standing on the origin, so one shape serves every tree.
        private static readonly Geometry PineShape = StreamGeometry.Parse(
            "M0,-1 L0.3,-0.62 L0.17,-0.62 L0.42,-0.3 L0.26,-0.3 L0.5,0 L-0.5,0 L-0.26,-0.3 L-0.42,-0.3 L-0.17,-0.62 L-0.3,-0.62 Z");

        private static readonly Geometry PineCap = StreamGeometry.Parse("M0,-1 L0.22,-0.72 L-0.22,-0.72 Z");

        private static readonly (double X, double Y, double Height)[] Pines =
        [
            (42, 150, 20), (55, 145, 28), (68, 142, 18), (98, 141, 16),
            (156, 137, 24), (170, 134, 30), (186, 134, 20), (84, 162, 17), (196, 157, 15),
        ];

        private static readonly Geometry RoofShape = StreamGeometry.Parse("M109,146 L124,133 L139,146 Z");

        private static readonly Geometry RoofSnow = StreamGeometry.Parse("M110,145 L124,132.6 L138,145");

        // One cloud, about its middle; the ellipses are unioned so a translucent cloud has no seams.
        private static readonly Geometry CloudShape = new GeometryGroup
        {
            FillRule = FillRule.NonZero,
            Children =
            {
                new EllipseGeometry(new Rect(-18, -9, 36, 18)),
                new EllipseGeometry(new Rect(-24, -4, 22, 14)),
                new EllipseGeometry(new Rect(1, -5, 24, 14)),
                new EllipseGeometry(new Rect(-14, -15, 22, 18)),
                new EllipseGeometry(new Rect(-2, -11, 18, 14)),
            },
        };

        // The window's reflections: a crescent of sky light top left, a thin rim of it lower right.
        private static readonly Geometry Highlight = StreamGeometry.Parse(
            "M42.5,64.2 A80,80 0 0 1 108,18.3 Q74.4,40.2 42.5,64.2 Z");

        private static readonly Geometry Reflection = StreamGeometry.Parse("M192.1,126 A82,82 0 0 1 156,169");

        private static readonly Geometry Stand = StreamGeometry.Parse(
            "M52,182 L178,182 L190,212 Q191,220 182,220 L48,220 Q39,220 40,212 Z");

        // The front of the collar the glass sits in, drawn over the glass.
        private static readonly Geometry Lip = StreamGeometry.Parse("M47,176 Q115,196 183,176 L183,184 Q115,204 47,184 Z");

        private static readonly Geometry LipEdge = StreamGeometry.Parse("M47,176 Q115,196 183,176");

        private struct Flake
        {
            public double X;
            public double Y;
            public double Vx;
            public double Vy;
            public double Size;
            public double Phase;
        }

        private struct Drop
        {
            public double X;
            public double Y;
            public double Speed;
            public double Length;
        }

        private struct Cloud
        {
            public double X;
            public double Y;
            public double Scale;
            public double Speed;
        }

        private struct Star
        {
            public double X;
            public double Y;
            public double Size;
            public double Phase;
        }

        private readonly Flake[] flakes = new Flake[120];
        private readonly Drop[] drops = new Drop[70];
        private readonly Cloud[] clouds = new Cloud[7];
        private readonly Star[] stars = new Star[26];

        /// <summary>0 settled, 1 a full blizzard; rises quickly with a clean and settles slowly after.</summary>
        private double swirl;

        /// <summary>How hard the globe is being shaken, in pixels.</summary>
        private double shake;

        private double flash;
        private double boltLife;
        private double strikeIn = 1;
        private Geometry? bolt;

        protected override void Seed()
        {
            for (int i = 0; i < flakes.Length; i++)
            {
                flakes[i].Size = 0.6 + Random.NextDouble() * Random.NextDouble() * 1.6;
                flakes[i].Phase = Random.NextDouble() * Math.Tau;
                Spawn(ref flakes[i], anywhere: true);
            }

            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = new Drop
                {
                    X = Random.NextDouble() * Side,
                    Y = Random.NextDouble() * Floor,
                    Speed = 170 + Random.NextDouble() * 60,
                    Length = 7 + Random.NextDouble() * 5,
                };
            }

            for (int i = 0; i < clouds.Length; i++)
            {
                clouds[i] = new Cloud
                {
                    X = 20 + (i * 67) % 200 + Random.NextDouble() * 10,
                    Y = 22 + Random.NextDouble() * 46,
                    Scale = 0.7 + Random.NextDouble() * 0.5,
                    Speed = 3 + Random.NextDouble() * 4,
                };
            }

            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = new Star
                {
                    X = 35 + Random.NextDouble() * 160,
                    Y = 16 + Random.NextDouble() * 90,
                    Size = 0.4 + Random.NextDouble() * Random.NextDouble() * 1.1,
                    Phase = Random.NextDouble() * Math.Tau,
                };
            }
        }

        private static double Smooth(double from, double to, double x)
        {
            double t = Math.Clamp((x - from) / (to - from), 0, 1);

            return t * t * (3 - 2 * t);
        }

        /// <summary>
        ///  Steps of a thirty-second, for anything mixed into a colour: the load moves continuously,
        ///  and every distinct colour is another cached brush.
        /// </summary>
        private static double Steps(double x) => Math.Round(x * 32) / 32;

        private double Gloom => Steps(Smooth(0.1, 0.95, Cpu));

        private double Cover => Smooth(0.15, 0.6, Cpu);

        private double Wet => Smooth(0.45, 0.8, Cpu);

        /// <summary>The level of the drift; a blizzard lifts much of it into the air.</summary>
        private double DriftTop => Floor - (5 + Memory * 52) * (1 - swirl * 0.55);

        protected override void CleanStarted()
        {
            // A shake starts with a jolt, whatever the globe was doing.
            shake = 3;
        }

        protected override void Advance(double dt)
        {
            swirl += ((Busy ? 1 : 0) - swirl) * (1 - Math.Exp(-dt * (Busy ? 3 : 0.7)));

            double wantShake = Busy ? 0.4 + 2.6 * Math.Exp(-BusyFor * 1.4) : 0;
            shake += (wantShake - shake) * (1 - Math.Exp(-dt * 6));

            // Lightning only in the red, and never during a shake, which has drama enough.
            if (CpuLevel == 2 && !Busy)
            {
                strikeIn -= dt;

                if (strikeIn <= 0)
                {
                    Strike();
                    strikeIn = 1.2 + Random.NextDouble() * 3.5;
                }
            }
            else
            {
                strikeIn = Math.Max(strikeIn, 0.6);
            }

            flash *= Math.Exp(-dt * 6);
            boltLife -= dt;

            MoveFlakes(dt);

            for (int i = 0; i < drops.Length; i++)
            {
                ref Drop d = ref drops[i];
                d.Y += d.Speed * dt;
                d.X += d.Speed * 0.28 * dt;

                if (d.Y > Floor)
                {
                    d.Y = 4 - Random.NextDouble() * 40;
                    d.X = Random.NextDouble() * (Side + 20) - 40;
                }
            }

            for (int i = 0; i < clouds.Length; i++)
            {
                ref Cloud c = ref clouds[i];
                c.X += c.Speed * dt * (1 + Cpu * 2.5);

                if (c.X > 235)
                {
                    c.X = -35;
                    c.Y = 22 + Random.NextDouble() * 46;
                }
            }
        }

        private void MoveFlakes(double dt)
        {
            double top = DriftTop;
            double wind = Cpu * 18;
            double ease = 1 - Math.Exp(-dt * 3);

            for (int i = 0; i < flakes.Length; i++)
            {
                ref Flake f = ref flakes[i];

                // Settled, a flake falls and sways; shaken, it is carried round the globe and up.
                double calmX = Math.Sin(Time * 0.9 + f.Phase) * 6 + wind;
                double calmY = 6 + f.Size * 6 + Wet * 10;
                double dx = f.X - GlobeCentre.X;
                double dy = f.Y - 120;
                double stormX = -dy * 2.2 + Math.Sin(Time * 4 + f.Phase) * 25;
                double stormY = dx * 2.2 - 20 + Math.Cos(Time * 3.3 + f.Phase) * 25;

                f.Vx += (calmX + (stormX - calmX) * swirl - f.Vx) * ease;
                f.Vy += (calmY + (stormY - calmY) * swirl - f.Vy) * ease;
                f.X += f.Vx * dt;
                f.Y += f.Vy * dt;

                double ox = f.X - GlobeCentre.X;
                double oy = f.Y - GlobeCentre.Y;
                bool outside = ox * ox + oy * oy > (Radius - 3) * (Radius - 3);

                if (outside || (swirl < 0.5 && f.Y > top + 2))
                {
                    Spawn(ref f, anywhere: swirl > 0.3);
                }
            }
        }

        private void Spawn(ref Flake f, bool anywhere)
        {
            if (anywhere)
            {
                double angle = Random.NextDouble() * Math.Tau;
                double r = Math.Sqrt(Random.NextDouble()) * (Radius - 8);
                f.X = GlobeCentre.X + Math.Cos(angle) * r;
                f.Y = GlobeCentre.Y + Math.Sin(angle) * r;
            }
            else
            {
                f.X = 35 + Random.NextDouble() * 160;
                double half = f.X - GlobeCentre.X;
                f.Y = GlobeCentre.Y - Math.Sqrt(Radius * Radius - half * half) + 4 + Random.NextDouble() * 8;
            }
        }

        private void Strike()
        {
            double x = 45 + Random.NextDouble() * 140;
            double y = 14;

            StreamGeometry path = new();

            using (StreamGeometryContext g = path.Open())
            {
                g.BeginFigure(new Point(x, y), false);
                Point fork = default;
                int step = 0;

                while (y < 136)
                {
                    y += 9 + Random.NextDouble() * 10;
                    x += (Random.NextDouble() - 0.5) * 22;
                    g.LineTo(new Point(x, y));

                    if (++step == 3)
                    {
                        fork = new Point(x, y);
                    }
                }

                g.EndFigure(false);

                // A short branch off the main bolt, which is what makes it read as lightning.
                g.BeginFigure(fork, false);
                double side = Random.NextDouble() < 0.5 ? -1 : 1;

                for (int i = 1; i <= 3; i++)
                {
                    g.LineTo(new Point(fork.X + side * (8 * i + Random.NextDouble() * 4), fork.Y + 7 * i + Random.NextDouble() * 4));
                }

                g.EndFigure(false);
            }

            bolt = path;
            boltLife = 0.32;
            flash = 1;
        }

        protected override void Draw(DrawingContext context)
        {
            double gloom = Gloom;

            // The shadow stays put while the globe rocks on it.
            context.DrawEllipse(Glow(Colors.Black, 0.45), null, new Point(115, 221), 86, 7);

            // Shaken about the foot of the stand, as a hand would rock it.
            const double footX = 115;
            const double footY = 218;
            double turn = Math.Sin(Time * 17) * shake * 1.0 * Math.PI / 180;
            double jx = Math.Sin(Time * 43) * shake * 1.2;
            double jy = Math.Sin(Time * 57) * shake * 0.6;
            Matrix rock = Matrix.CreateTranslation(-footX, -footY)
                * Matrix.CreateRotation(turn)
                * Matrix.CreateTranslation(footX + jx, footY + jy);

            using (context.PushTransform(rock))
            {
                DrawStand(context);

                using (context.PushGeometryClip(Inside))
                {
                    DrawSky(context, gloom);
                    DrawClouds(context, gloom);
                    DrawBolt(context);
                    DrawLand(context, gloom);
                    DrawRain(context);
                    DrawDrift(context, gloom);
                    DrawFlakes(context);

                    if (flash > 0.02)
                    {
                        context.DrawRectangle(Solid(White, flash * 0.5), null, new Rect(0, 0, Side, Side));
                    }

                    DrawReadout(context);
                    context.DrawEllipse(Vignette, null, GlobeCentre, Radius, Radius);
                }

                DrawGlass(context);

                context.DrawGeometry(Wood, null, Lip);
                context.DrawGeometry(null, Pen(WoodLight, 0.7, 1), LipEdge);
            }
        }

        private void DrawStand(DrawingContext context)
        {
            // The dark seat the glass sits in, behind the glass.
            context.DrawEllipse(Solid(WoodDeep), null, new Point(115, 178), 67, 10);

            context.DrawGeometry(Wood, null, Stand);
            context.DrawLine(Pen(WoodDeep, 0.7, 1.2), new Point(41, 213), new Point(189, 213));

            // The brass plaque, with the gauges the sky has no room for.
            Rect plaque = new(60, 195, 110, 22);
            context.DrawRectangle(Fade(BrassLight, BrassDark), Pen(Engraving, 0.6, 0.8), plaque, 3, 3);
            context.DrawLine(Pen(White, 0.35, 0.8), new Point(63, 196.2), new Point(167, 196.2));
            context.DrawEllipse(Solid(BrassDark), Pen(Engraving, 0.5, 0.5), new Point(64.5, 206), 1.4, 1.4);
            context.DrawEllipse(Solid(BrassDark), Pen(Engraving, 0.5, 0.5), new Point(165.5, 206), 1.4, 1.4);

            Text(context, $"{CpuCaption} {CpuText}", new Point(115, 201.5), 7.5, LevelColour(CpuLevel, Engraving), bold: true, mono: true);
            Text(context, $"{GpuCaption} {GpuText} · {DiskCaption} {DiskText}", new Point(115, 210.5), 7, Engraving, mono: true);
        }

        private void DrawSky(DrawingContext context, double gloom)
        {
            Color low = Mix(DuskLow, StormLow, gloom);
            context.DrawRectangle(Fade(Mix(DuskTop, StormTop, gloom), low), null, new Rect(20, 8, 190, 150));
            context.DrawRectangle(Solid(low), null, new Rect(20, 157, 190, 37));

            // Stars and the moon belong to a clear sky, and go as the cloud comes in.
            double clear = 1 - Cover;

            if (clear > 0.02)
            {
                foreach (Star s in stars)
                {
                    double twinkle = 0.5 + 0.5 * Math.Sin(Time * 2.1 + s.Phase);
                    context.DrawEllipse(Solid(White, clear * (0.3 + 0.55 * twinkle)), null, new Point(s.X, s.Y), s.Size, s.Size);
                }
            }

            Point moon = new(76, 36);
            context.DrawEllipse(Glow(MoonGlow, 0.5 * (1 - Cover * 0.8)), null, moon, 26, 26);
            context.DrawEllipse(Solid(MoonLight, 1 - Cover * 0.7), null, moon, 7.5, 7.5);
            context.DrawEllipse(Solid(MoonGlow, 0.5 * clear), null, new Point(moon.X + 2.2, moon.Y + 1.5), 1.8, 1.4);
            context.DrawEllipse(Solid(MoonGlow, 0.4 * clear), null, new Point(moon.X - 2.5, moon.Y - 2), 1.2, 1);
        }

        private void DrawClouds(DrawingContext context, double gloom)
        {
            // Clouds arrive one at a time as the load rises, rather than all fading in together.
            double count = Cover * clouds.Length;
            Color body = Mix(CloudLight, CloudDark, gloom);
            Color under = Mix(CloudShadeLight, CloudShadeDark, gloom);

            for (int i = 0; i < clouds.Length; i++)
            {
                double show = Math.Clamp(count - i, 0, 1);

                if (show <= 0)
                {
                    continue;
                }

                Cloud c = clouds[i];

                using (context.PushTransform(Matrix.CreateScale(c.Scale, c.Scale) * Matrix.CreateTranslation(c.X, c.Y + 3)))
                {
                    context.DrawGeometry(Solid(under, 0.85 * show), null, CloudShape);
                }

                using (context.PushTransform(Matrix.CreateScale(c.Scale, c.Scale) * Matrix.CreateTranslation(c.X, c.Y)))
                {
                    context.DrawGeometry(Solid(body, 0.92 * show), null, CloudShape);
                }
            }
        }

        private void DrawBolt(DrawingContext context)
        {
            if (bolt is null || boltLife <= 0)
            {
                return;
            }

            double alpha = Math.Clamp(boltLife / 0.32, 0, 1) * (0.65 + 0.35 * Math.Sin(Time * 90));
            context.DrawGeometry(null, Pen(BoltGlow, alpha * 0.35, 6), bolt);
            context.DrawGeometry(null, Pen(White, alpha, 1.6), bolt);
        }

        private void DrawLand(DrawingContext context, double gloom)
        {
            double dark = gloom * 0.7;

            context.DrawGeometry(Solid(Mix(HillBack, HillNight, dark)), null, BackHill);

            Color pine = Mix(Pine, PineNight, dark);
            Color cap = Mix(SnowLit, SnowDim, dark);

            // The cabin, its window lit, brighter against the gloom.
            context.DrawRectangle(Solid(Mix(Roof, HillNight, dark)), null, new Rect(130, 134, 4, 8));
            context.DrawRectangle(Solid(Mix(Wall, HillNight, dark)), null, new Rect(112, 145, 24, 14));
            context.DrawGeometry(Solid(Mix(Roof, HillNight, dark)), null, RoofShape);
            context.DrawGeometry(null, Pen(cap, 2.2), RoofSnow);
            context.DrawRectangle(Solid(Mix(Roof, HillNight, dark)), null, new Rect(116, 150, 5, 9));

            double lamp = 0.45 + gloom * 0.35 + Math.Sin(Time * 7) * 0.04;
            context.DrawEllipse(Glow(Lamp, lamp), null, new Point(128, 151.5), 11, 9);
            context.DrawRectangle(Solid(Lamp), null, new Rect(125, 149, 6, 5));

            foreach ((double x, double y, double height) in Pines)
            {
                using (context.PushTransform(Matrix.CreateScale(height, height) * Matrix.CreateTranslation(x, y)))
                {
                    context.DrawGeometry(Solid(pine), null, PineShape);
                    context.DrawGeometry(Solid(cap, 0.9), null, PineCap);
                }
            }

            context.DrawGeometry(Solid(Mix(HillFront, HillNight, dark)), null, FrontHill);
        }

        private void DrawRain(DrawingContext context)
        {
            int count = (int)(Wet * drops.Length);

            for (int i = 0; i < count; i++)
            {
                Drop d = drops[i];
                context.DrawLine(Pen(Rain, 0.45, 1), new Point(d.X, d.Y), new Point(d.X - d.Length * 0.28, d.Y - d.Length));
            }
        }

        private void DrawDrift(DrawingContext context, double gloom)
        {
            double level = DriftTop;
            StreamGeometry fill = new();
            StreamGeometry edge = new();

            using (StreamGeometryContext f = fill.Open())
            using (StreamGeometryContext e = edge.Open())
            {
                f.BeginFigure(new Point(20, 194), true);

                for (double x = 20; x <= 212; x += 4)
                {
                    // Two slow waves, so the drift has soft humps that shift a little as it settles.
                    double y = level + Math.Sin(x * 0.06 + Time * 0.5) * 2.2 + Math.Sin(x * 0.13 + 1.3) * 1.6;
                    Point p = new(x, y);
                    f.LineTo(p);

                    if (x == 20)
                    {
                        e.BeginFigure(p, false);
                    }
                    else
                    {
                        e.LineTo(p);
                    }
                }

                f.LineTo(new Point(212, 194));
                f.EndFigure(true);
                e.EndFigure(false);
            }

            double dim = gloom * 0.6;
            context.DrawGeometry(Fade(Mix(SnowLit, SnowDim, dim), Mix(SnowShade, SnowDim, dim)), null, fill);

            // The edge carries the memory level: a faint frost when normal, amber or red when not.
            Color mark = LevelColour(MemoryLevel, SnowLit);

            if (MemoryLevel > 0)
            {
                double pulse = MemoryLevel == 2 ? 0.6 + 0.4 * Math.Sin(Time * 5) : 1;
                context.DrawGeometry(null, Pen(mark, 0.3 * pulse, 6), edge);
                context.DrawGeometry(null, Pen(mark, 0.9, 1.5), edge);
            }
            else
            {
                context.DrawGeometry(null, Pen(White, 0.7, 1.2), edge);
            }
        }

        private void DrawFlakes(DrawingContext context)
        {
            // A light fall when settled; shaken, far more of the snow is in the air.
            int count = (int)(40 + swirl * 80);

            for (int i = 0; i < count; i++)
            {
                Flake f = flakes[i];
                context.DrawEllipse(Solid(White, 0.55 + f.Size * 0.2), null, new Point(f.X, f.Y), f.Size, f.Size);
            }
        }

        private void DrawReadout(DrawingContext context)
        {
            const double x = 115;

            context.DrawEllipse(Glow(Night, 0.7), null, new Point(x, 73), 66, 38);

            Text(context, MemoryCaption, new Point(x, 52), 8, Caption);

            if (Flashing)
            {
                Text(context, FlashText, new Point(x, 72), 20, FlashGood ? Good : White, bold: true);

                if (FlashGood)
                {
                    Text(context, FreedCaption, new Point(x, 90), 8.5, Caption);
                }

                return;
            }

            Text(context, MemoryText, new Point(x, 72), 28, LevelColour(MemoryLevel, White), bold: true);
            Text(context, Hovered ? ClickToClean : UsedOfTotal, new Point(x, 92), 8.5, Hovered ? Accent : Caption);
        }

        private void DrawGlass(DrawingContext context)
        {
            // The rim lights in the accent when the pointer is on it, as the dial's does.
            context.DrawEllipse(null, Pen(Hovered ? Accent : GlassEdge, Hovered ? 0.9 : 0.5, Hovered ? 2.2 : 1.6), GlobeCentre, Radius, Radius);
            context.DrawEllipse(null, Pen(White, 0.12, 1), GlobeCentre, Radius - 3, Radius - 3);

            context.DrawGeometry(Fade(WithAlpha(White, 0.4), WithAlpha(White, 0.02)), null, Highlight);
            context.DrawGeometry(null, Pen(White, 0.2, 2), Reflection);
            context.DrawEllipse(Glow(White, 0.75), null, new Point(150, 30), 5, 5);
        }
    }
}
