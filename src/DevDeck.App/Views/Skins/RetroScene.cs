using System.Text;
using Avalonia;
using Avalonia.Media;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  A piece of 1970s hi-fi: two lit VU meters over an LED readout, in walnut and brushed aluminium.
    /// </summary>
    /// <remarks>
    ///  The left meter is memory in use and the right one the processor. Each needle hangs on a
    ///  spring, so it overshoots a little and settles as a real movement does, and trembles in
    ///  proportion to the load it reads. The red peak lamp in a meter's corner comes on in the red
    ///  band. Under them a seven-segment readout counts memory in percent: amber, orange when high,
    ///  red when critical. Graphics and disk are two pilot lamps, brighter the busier they are,
    ///  going from green through amber to red. A clean is the set's self-test: both needles drop to
    ///  the stop and sweep the scale, the readout runs a chase round its digits and the power lamp
    ///  blinks; when it ends, the readout shows what was freed.
    /// </remarks>
    internal sealed class RetroScene : SkinScene
    {
        // A meter's scale spans 76 degrees either side of upright, as a VU meter's does.
        private const double HalfSweep = 38;
        private const double ScaleRadius = 52;
        private const double NeedleLength = 56;

        // The needle's spring: underdamped by about a third, so it passes its mark and comes back.
        private const double Stiffness = 120;
        private const double Damping = 14;

        // The stop pins either end of the scale, which a sweeping needle bounces off.
        private const double LowPin = -0.03;
        private const double HighPin = 1.04;

        // Seven-segment cells: 18 by 32, three of them, slanted as the old readouts were.
        private const double DigitWidth = 18;
        private const double DigitHeight = 32;
        private const double DigitSlant = 0.12;
        private const double DigitStep = 24;

        private static readonly Rect CaseRect = new(4, 10, 222, 210);
        private static readonly Rect PlateRect = new(12, 18, 206, 194);
        private static readonly Rect LeftMeter = new(24, 34, 89, 70);
        private static readonly Rect RightMeter = new(117, 34, 89, 70);
        private static readonly Rect DisplayRect = new(24, 112, 108, 46);
        private static readonly Point DigitOrigin = new(33, 119);
        private static readonly Point GpuLamp = new(152, 124);
        private static readonly Point DiskLamp = new(152, 148);
        private static readonly Point PowerLamp = new(32, 195);

        private static readonly Color WoodLight = Color.FromRgb(0x6B, 0x3E, 0x22);
        private static readonly Color WoodDark = Color.FromRgb(0x3A, 0x1F, 0x10);
        private static readonly Color Grain = Color.FromRgb(0x1E, 0x0F, 0x07);
        private static readonly Color MetalTop = Color.FromRgb(0xE4, 0xE6, 0xE9);
        private static readonly Color MetalBottom = Color.FromRgb(0xA9, 0xAE, 0xB5);
        private static readonly Color Chrome = Color.FromRgb(0xF4, 0xF5, 0xF7);
        private static readonly Color ChromeShade = Color.FromRgb(0x6E, 0x73, 0x7A);
        private static readonly Color Engraving = Color.FromRgb(0x2E, 0x31, 0x36);
        private static readonly Color Bezel = Color.FromRgb(0x17, 0x11, 0x0D);
        private static readonly Color CreamTop = Color.FromRgb(0xFB, 0xEF, 0xCF);
        private static readonly Color CreamBottom = Color.FromRgb(0xEB, 0xC9, 0x83);
        private static readonly Color Lamp = Color.FromRgb(0xFF, 0xB0, 0x40);
        private static readonly Color Ink = Color.FromRgb(0x2B, 0x20, 0x17);
        private static readonly Color RedZone = Color.FromRgb(0xC9, 0x37, 0x2C);
        private static readonly Color HubTop = Color.FromRgb(0x4A, 0x3A, 0x2E);
        private static readonly Color HubBottom = Color.FromRgb(0x12, 0x0C, 0x08);
        private static readonly Color GlassTop = Color.FromRgb(0x1E, 0x0E, 0x08);
        private static readonly Color GlassBottom = Color.FromRgb(0x0B, 0x05, 0x03);
        private static readonly Color LedAmber = Color.FromRgb(0xFF, 0xA8, 0x1E);
        private static readonly Color LedOrange = Color.FromRgb(0xFF, 0x6A, 0x1A);
        private static readonly Color LedRed = Color.FromRgb(0xFF, 0x2E, 0x1E);
        private static readonly Color LampGreen = Color.FromRgb(0x4B, 0xE3, 0x6B);
        private static readonly Color LampAmber = Color.FromRgb(0xFF, 0xB2, 0x1E);
        private static readonly Color LampRed = Color.FromRgb(0xFF, 0x3B, 0x2A);
        private static readonly Color LampOff = Color.FromRgb(0x2A, 0x16, 0x12);
        private static readonly Color White = Colors.White;
        private static readonly Color Black = Colors.Black;

        // The scale, drawn about the needle's pivot so both meters share it.
        private static readonly Geometry ScaleArc = Arc(0, 0.8, ScaleRadius);
        private static readonly Geometry RedArc = Arc(0.8, 1, ScaleRadius + 2);
        private static readonly Geometry Ticks = TickMarks(0, 16);
        private static readonly Geometry RedTicks = TickMarks(17, 20);

        private static readonly string[] ScaleLabels = ["0", "25", "50", "75", "100"];
        private static readonly Point[] LabelPoints =
            [.. Enumerable.Range(0, 5).Select(i => OnScale(i / 4.0, 64))];

        // The glare across the glass of a meter and of the readout, in their own coordinates.
        private static readonly Geometry MeterGlare = StreamGeometry.Parse("M0,0 L89,0 L89,8 C60,14 30,24 0,40 Z");
        private static readonly Geometry DisplayGlare = StreamGeometry.Parse("M0,0 L108,0 L108,6 C70,10 30,16 0,24 Z");

        // Brushed metal and walnut grain: fixed scratches, laid out once from a fixed seed.
        private static readonly Geometry BrushLight = Brushing(11);
        private static readonly Geometry BrushDark = Brushing(23);
        private static readonly Geometry WoodGrain = Graining();

        /// <summary>The seven segments, a to g, of one slanted cell.</summary>
        private static readonly Geometry[] Segments = BuildSegments();

        /// <summary>Which segments light for each digit, bit 0 for a up to bit 6 for g.</summary>
        private static readonly byte[] DigitMasks = [0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F];

        // The chase a clean runs: along the tops of the three cells, down the right, back along
        // the bottoms and up the left.
        private static readonly int[] ChaseCell = [0, 1, 2, 2, 2, 2, 1, 0, 0, 0];
        private static readonly int[] ChaseSegment = [0, 0, 0, 1, 2, 3, 3, 3, 4, 5];

        private static readonly Point[] Screws =
            [new(21, 27), new(209, 27), new(21, 203), new(209, 203)];

        // Each screw left at its own angle, as a set put together by hand would be.
        private static readonly double[] ScrewTurns = [20, 75, -30, 110];

        private struct Needle
        {
            public double Position;
            public double Velocity;
        }

        private Needle left;
        private Needle right;

        protected override void Seed()
        {
            // Both needles rest on the stop, so the set warms up: they rise to their readings
            // and settle, as they would at switch-on.
            left = default;
            right = default;
        }

        protected override void Advance(double dt)
        {
            double leftTarget = Memory;
            double rightTarget = Cpu;

            if (Busy)
            {
                // The self-test: down to the stop, across to the red and back again, the right
                // meter a beat behind the left.
                leftTarget = 0.5 - 0.5 * Math.Cos(BusyFor * 2.4);
                rightTarget = 0.5 - 0.5 * Math.Cos(Math.Max(BusyFor - 0.25, 0) * 2.4);
            }

            Swing(ref left, leftTarget, Memory, dt);
            Swing(ref right, rightTarget, Cpu, dt);
        }

        private void Swing(ref Needle needle, double target, double load, double dt)
        {
            if (dt <= 0)
            {
                return;
            }

            // A nudge each frame, sized by the load: a busy machine's needle trembles.
            needle.Velocity += (Random.NextDouble() - 0.5) * load * dt * 14;

            // Small steps, because a spring this stiff stepped once per frame can blow up.
            int steps = (int)Math.Ceiling(dt / 0.004);
            double h = dt / steps;

            for (int i = 0; i < steps; i++)
            {
                needle.Velocity += (Stiffness * (target - needle.Position) - Damping * needle.Velocity) * h;
                needle.Position += needle.Velocity * h;

                if (needle.Position < LowPin)
                {
                    needle.Position = LowPin;
                    needle.Velocity = Math.Abs(needle.Velocity) * 0.35;
                }
                else if (needle.Position > HighPin)
                {
                    needle.Position = HighPin;
                    needle.Velocity = -Math.Abs(needle.Velocity) * 0.35;
                }
            }
        }

        protected override void Draw(DrawingContext context)
        {
            DrawCase(context);
            DrawPlate(context);

            DrawMeter(context, LeftMeter, left.Position, MemoryCaption, MemoryLevel);
            DrawMeter(context, RightMeter, right.Position, CpuCaption, CpuLevel);

            DrawDisplay(context);

            DrawLamp(context, GpuLamp, Gpu, GpuCaption, GpuText);
            DrawLamp(context, DiskLamp, Disk, DiskCaption, DiskText);

            DrawFooter(context);
        }

        private void DrawCase(DrawingContext context)
        {
            context.DrawRectangle(Fade(WoodLight, WoodDark), null, CaseRect, 16, 16);

            using (context.PushClip(new RoundedRect(CaseRect, 16)))
            {
                context.DrawGeometry(null, Pen(Grain, 0.3, 0.8), WoodGrain);
            }

            // Oiled wood catches the light along its top edge.
            context.DrawRectangle(null, Pen(White, 0.14, 1), CaseRect.Deflate(1), 15, 15);
            context.DrawRectangle(null, Pen(Black, 0.5, 1), CaseRect, 16, 16);
        }

        private void DrawPlate(DrawingContext context)
        {
            // The plate's shadow on the wood, so it sits proud of the case.
            context.DrawRectangle(Solid(Black, 0.35), null, PlateRect.Translate(new Vector(0, 1.5)).Inflate(1), 9, 9);
            context.DrawRectangle(Fade(MetalTop, MetalBottom), null, PlateRect, 8, 8);

            using (context.PushClip(new RoundedRect(PlateRect, 8)))
            {
                context.DrawGeometry(null, Pen(White, 0.22, 0.6), BrushLight);
                context.DrawGeometry(null, Pen(Black, 0.06, 0.6), BrushDark);

                // A broad sheen from the top left, where the room's light falls.
                context.DrawEllipse(Glow(White, 0.35), null, new Point(60, 40), 120, 70);
            }

            // The bevel, lit in the accent when the pointer is on it, as the dial's rim is.
            context.DrawRectangle(null, Hovered ? Pen(Accent, 1.4) : Pen(White, 0.4, 1), PlateRect.Deflate(0.5), 7.5, 7.5);
            context.DrawRectangle(null, Pen(Black, 0.45, 0.8), PlateRect.Inflate(0.4), 8.4, 8.4);

            for (int i = 0; i < Screws.Length; i++)
            {
                DrawScrew(context, Screws[i], ScrewTurns[i]);
            }
        }

        private void DrawScrew(DrawingContext context, Point at, double turn)
        {
            double a = turn * Math.PI / 180;
            Vector slot = new(Math.Cos(a) * 2.6, Math.Sin(a) * 2.6);

            context.DrawEllipse(Solid(White, 0.5), null, new Point(at.X, at.Y + 0.7), 3.8, 3.8);
            context.DrawEllipse(Fade(Chrome, ChromeShade), Pen(Black, 0.45, 0.6), at, 3.6, 3.6);
            context.DrawLine(Pen(Black, 0.6, 0.9), at - slot, at + slot);
        }

        private void DrawMeter(DrawingContext context, Rect window, double position, string caption, int level)
        {
            // The bezel, set into the plate: dark frame, lit lip under it.
            context.DrawRectangle(Solid(White, 0.45), null, window.Inflate(2.5).Translate(new Vector(0, 0.8)), 5, 5);
            context.DrawRectangle(Solid(Bezel), null, window.Inflate(2.5), 5, 5);

            using (context.PushClip(new RoundedRect(window, 3.5)))
            {
                // The card, lit from behind by a warm bulb low in the case, brighter on test.
                context.DrawRectangle(Fade(CreamTop, CreamBottom), null, window);
                double bulb = 0.45 + (Busy ? 0.18 : 0) + Math.Sin(Time * 0.7) * 0.03;
                context.DrawEllipse(Glow(Lamp, bulb), null, new Point(window.Center.X, window.Bottom - 6), window.Width * 0.75, window.Height * 0.8);

                Point pivot = new(window.Center.X, window.Bottom + 4);

                using (context.PushTransform(Matrix.CreateTranslation(pivot.X, pivot.Y)))
                {
                    context.DrawGeometry(null, Pen(Ink, 1), ScaleArc);
                    context.DrawGeometry(null, Pen(RedZone, 4), RedArc);
                    context.DrawGeometry(null, Pen(Ink, 0.9), Ticks);
                    context.DrawGeometry(null, Pen(RedZone, 0.9), RedTicks);

                    for (int i = 0; i < ScaleLabels.Length; i++)
                    {
                        Text(context, ScaleLabels[i], LabelPoints[i], 7, i == ScaleLabels.Length - 1 ? RedZone : Ink);
                    }

                    Text(context, caption, new Point(0, -26), 7.5, Ink, bold: true);

                    // The needle, with its shadow thrown onto the card by the bulb behind.
                    Point root = OnScale(position, 9);
                    Point tip = OnScale(position, NeedleLength);
                    Vector drop = new(1.2, 1.6);
                    context.DrawLine(Pen(Black, 0.16, 1.6), root + drop, tip + drop);
                    context.DrawLine(Pen(Ink, 1.3), root, tip);

                    context.DrawEllipse(Fade(HubTop, HubBottom), Pen(White, 0.2, 0.6), default, 11, 11);
                }

                // The peak lamp, lit in the red band or when the reading is critical.
                bool peak = level == 2 || position > 0.88;
                Point lampAt = new(window.Right - 9, window.Top + 9);
                context.DrawEllipse(Solid(peak ? LedRed : LampOff), Pen(Bezel, 0.6), lampAt, 2.6, 2.6);

                if (peak)
                {
                    context.DrawEllipse(Glow(LedRed, 0.7), null, lampAt, 8, 8);
                    context.DrawEllipse(Solid(White, 0.7), null, new Point(lampAt.X - 0.8, lampAt.Y - 0.9), 0.8, 0.8);
                }

                // The card sits back behind its glass: a shadow round the edge, the glare on top.
                context.DrawRectangle(null, Pen(Black, 0.22, 3), window, 3.5, 3.5);

                using (context.PushTransform(Matrix.CreateTranslation(window.X, window.Y)))
                {
                    context.DrawGeometry(Fade(WithAlpha(White, 0.3), WithAlpha(White, 0)), null, MeterGlare);
                }
            }
        }

        private void DrawDisplay(DrawingContext context)
        {
            Rect window = DisplayRect;

            context.DrawRectangle(Solid(White, 0.45), null, window.Inflate(2.5).Translate(new Vector(0, 0.8)), 5, 5);
            context.DrawRectangle(Solid(Bezel), null, window.Inflate(2.5), 5, 5);

            using (context.PushClip(new RoundedRect(window, 3.5)))
            {
                context.DrawRectangle(Fade(GlassTop, GlassBottom), null, window);

                if (Busy)
                {
                    DrawChase(context);
                }
                else if (Flashing)
                {
                    Color colour = FlashGood ? Good : LedAmber;
                    double size = FlashText.Length > 8 ? 12 : 16;

                    context.DrawEllipse(Glow(colour, 0.3), null, window.Center, 60, 24);
                    Text(context, FlashText, window.Center, size, colour, bold: true, mono: true);
                }
                else
                {
                    DrawReading(context);
                }

                using (context.PushTransform(Matrix.CreateTranslation(window.X, window.Y)))
                {
                    context.DrawGeometry(Fade(WithAlpha(White, 0.14), WithAlpha(White, 0)), null, DisplayGlare);
                }
            }
        }

        private void DrawReading(DrawingContext context)
        {
            Color led = MemoryLevel switch
            {
                2 => LedRed,
                1 => LedOrange,
                _ => LedAmber,
            };

            int value = Math.Clamp((int)Math.Round(Memory * 100), 0, 100);

            // Leading zeros stay dark, as a counter's do.
            Span<int> digits = [value >= 100 ? 1 : -1, value >= 10 ? value / 10 % 10 : -1, value % 10];

            context.DrawEllipse(Glow(led, 0.22), null, DisplayRect.Center, 64, 28);

            for (int cell = 0; cell < digits.Length; cell++)
            {
                int mask = digits[cell] < 0 ? 0 : DigitMasks[digits[cell]];

                using (context.PushTransform(CellPlace(cell)))
                {
                    for (int s = 0; s < Segments.Length; s++)
                    {
                        DrawSegment(context, s, (mask >> s & 1) == 1 ? 1 : 0, led);
                    }
                }
            }

            Text(context, "%", new Point(DisplayRect.Right - 13, DigitOrigin.Y + DigitHeight - 7), 12, led, bold: true, mono: true);
        }

        private void DrawChase(DrawingContext context)
        {
            // Every segment dark first, so the chase runs over a readout that is plainly off.
            for (int cell = 0; cell < 3; cell++)
            {
                using (context.PushTransform(CellPlace(cell)))
                {
                    for (int s = 0; s < Segments.Length; s++)
                    {
                        DrawSegment(context, s, 0, LedAmber);
                    }
                }
            }

            // A bright head and a fading tail three segments long, going round about once a second.
            double head = BusyFor * 11 % ChaseCell.Length;

            for (int p = 0; p < ChaseCell.Length; p++)
            {
                double behind = (head - p + ChaseCell.Length) % ChaseCell.Length;
                double light = behind < 4 ? 1 - behind / 4 : 0;

                if (light > 0)
                {
                    using (context.PushTransform(CellPlace(ChaseCell[p])))
                    {
                        DrawSegment(context, ChaseSegment[p], light, LedAmber);
                    }
                }
            }
        }

        private static Matrix CellPlace(int cell) =>
            Matrix.CreateTranslation(DigitOrigin.X + cell * DigitStep, DigitOrigin.Y);

        /// <summary>One segment: a faint ghost when dark, lit with a halo of its own light.</summary>
        private void DrawSegment(DrawingContext context, int segment, double light, Color led)
        {
            Geometry shape = Segments[segment];

            if (light <= 0)
            {
                context.DrawGeometry(Solid(led, 0.07), null, shape);
                return;
            }

            context.DrawGeometry(Solid(led, 0.07 + 0.93 * light), Pen(led, 0.3 * light, 2.5), shape);
        }

        private void DrawLamp(DrawingContext context, Point at, double reading, string caption, string value)
        {
            // Stepped, so a reading easing between values reuses a few brushes rather than many.
            double r = Math.Round(reading * 16) / 16;
            Color colour = Mix(Mix(LampGreen, LampAmber, (r - 0.6) / 0.2), LampRed, (r - 0.8) / 0.15);
            double light = 0.2 + 0.8 * r;

            context.DrawEllipse(Solid(White, 0.5), null, new Point(at.X, at.Y + 0.7), 5.8, 5.8);
            context.DrawEllipse(Fade(Chrome, ChromeShade), Pen(Black, 0.4, 0.6), at, 5.6, 5.6);
            context.DrawEllipse(Solid(LampOff), null, at, 4, 4);
            context.DrawEllipse(Solid(colour, light), null, at, 4, 4);
            context.DrawEllipse(Glow(colour, 0.15 + 0.6 * r), null, at, 9 + 5 * r, 9 + 5 * r);
            context.DrawEllipse(Glow(White, 0.6 * r), null, at, 3, 3);
            context.DrawEllipse(Solid(White, 0.7), null, new Point(at.X - 1.3, at.Y - 1.4), 1.1, 1.1);

            Engraved(context, caption, new Point(at.X + 30, at.Y - 5), 7, Engraving, bold: true);
            Engraved(context, value, new Point(at.X + 30, at.Y + 5.5), 7.5, Engraving, mono: true);
        }

        private void DrawFooter(DrawingContext context)
        {
            const double line = 173;
            const double row = 195;

            if (Flashing && !Busy)
            {
                if (FlashGood)
                {
                    Engraved(context, FreedCaption, new Point(Centre.X, line), 8.5, Mix(Engraving, Good, 0.6), bold: true);
                }
            }
            else if (Hovered)
            {
                Engraved(context, ClickToClean, new Point(Centre.X, line), 8.5, Mix(Engraving, Accent, 0.6), bold: true);
            }
            else
            {
                Engraved(context, UsedOfTotal, new Point(Centre.X, line), 8.5, Engraving);
            }

            // The power lamp: steady while the set is on, blinking through a self-test.
            bool lit = !Busy || Math.Sin(Time * Math.Tau * 2.5) > 0;
            context.DrawEllipse(Solid(White, 0.5), null, new Point(PowerLamp.X, PowerLamp.Y + 0.7), 4.6, 4.6);
            context.DrawEllipse(Fade(Chrome, ChromeShade), Pen(Black, 0.4, 0.6), PowerLamp, 4.4, 4.4);
            context.DrawEllipse(Solid(lit ? LedRed : LampOff), null, PowerLamp, 3, 3);

            if (lit)
            {
                context.DrawEllipse(Glow(LedRed, 0.6), null, PowerLamp, 10, 10);
                context.DrawEllipse(Solid(White, 0.7), null, new Point(PowerLamp.X - 1, PowerLamp.Y - 1), 0.8, 0.8);
            }

            // A vent grille, cut through the plate: dark slot, lit lower lip.
            for (int i = 0; i < 12; i++)
            {
                double x = 46 + i * 7;
                context.DrawRectangle(Solid(White, 0.5), null, new Rect(x, row - 5.3, 3, 12), 1.5, 1.5);
                context.DrawRectangle(Solid(Black, 0.55), null, new Rect(x, row - 6, 3, 12), 1.5, 1.5);
            }

            Engraved(context, $"{CpuCaption} {CpuText}", new Point(176, row), 7.5, Engraving, mono: true);
        }

        /// <summary>Text cut into the plate: the ink, with the light catching the lower edge of the cut.</summary>
        private void Engraved(DrawingContext context, string text, Point at, double size, Color ink, bool bold = false, bool mono = false)
        {
            Text(context, text, new Point(at.X, at.Y + 0.8), size, WithAlpha(White, 0.6), bold, mono);
            Text(context, text, at, size, ink, bold, mono);
        }

        #region Fixed shapes
        /// <summary>A point on a meter's scale, <paramref name="fraction"/> of the way across, about the pivot.</summary>
        private static Point OnScale(double fraction, double radius)
        {
            double a = (-HalfSweep + 2 * HalfSweep * fraction) * Math.PI / 180;

            return new Point(radius * Math.Sin(a), -radius * Math.Cos(a));
        }

        private static Geometry Arc(double from, double to, double radius)
        {
            Point a = OnScale(from, radius);
            Point b = OnScale(to, radius);

            return StreamGeometry.Parse(FormattableString.Invariant(
                $"M{a.X:0.###},{a.Y:0.###} A{radius},{radius} 0 0 1 {b.X:0.###},{b.Y:0.###}"));
        }

        /// <summary>Ticks every twentieth of the scale, the quarters long, from <paramref name="first"/> to <paramref name="last"/>.</summary>
        private static Geometry TickMarks(int first, int last)
        {
            StringBuilder path = new();

            for (int i = first; i <= last; i++)
            {
                double length = i % 5 == 0 ? 7 : 4;
                Point a = OnScale(i / 20.0, ScaleRadius);
                Point b = OnScale(i / 20.0, ScaleRadius + length);
                path.Append(FormattableString.Invariant($"M{a.X:0.###},{a.Y:0.###} L{b.X:0.###},{b.Y:0.###} "));
            }

            return StreamGeometry.Parse(path.ToString());
        }

        /// <summary>Short horizontal scratches at random across the plate, the grain of brushed aluminium.</summary>
        private static Geometry Brushing(int seed)
        {
            Random random = new(seed);
            StringBuilder path = new();

            for (double y = PlateRect.Top + 1; y < PlateRect.Bottom; y += 1.6)
            {
                for (int i = 0; i < 3; i++)
                {
                    double x = PlateRect.Left + random.NextDouble() * PlateRect.Width;
                    double length = 20 + random.NextDouble() * 90;
                    double yy = y + random.NextDouble() * 0.8;
                    path.Append(FormattableString.Invariant($"M{x:0.##},{yy:0.##} L{x + length:0.##},{yy:0.##} "));
                }
            }

            return StreamGeometry.Parse(path.ToString());
        }

        /// <summary>Gently wandering lines across the case, the figure of walnut.</summary>
        private static Geometry Graining()
        {
            Random random = new(5);
            StringBuilder path = new();

            for (double y = CaseRect.Top - 4; y < CaseRect.Bottom + 4; y += 2.6)
            {
                double w1 = (random.NextDouble() - 0.5) * 8;
                double w2 = (random.NextDouble() - 0.5) * 8;
                double end = (random.NextDouble() - 0.5) * 4;
                path.Append(FormattableString.Invariant(
                    $"M{CaseRect.Left:0.##},{y:0.##} C{CaseRect.Left + 70:0.##},{y + w1:0.##} {CaseRect.Left + 150:0.##},{y + w2:0.##} {CaseRect.Right:0.##},{y + end:0.##} "));
            }

            return StreamGeometry.Parse(path.ToString());
        }

        private static Geometry[] BuildSegments()
        {
            const double half = 2;
            const double gap = 0.9;
            const double l = 2;
            const double r = DigitWidth - 2;
            const double top = 2;
            const double mid = DigitHeight / 2;
            const double bottom = DigitHeight - 2;

            return
            [
                Across(top, l + gap, r - gap),
                Down(r, top + gap, mid - gap),
                Down(r, mid + gap, bottom - gap),
                Across(bottom, l + gap, r - gap),
                Down(l, mid + gap, bottom - gap),
                Down(l, top + gap, mid - gap),
                Across(mid, l + gap, r - gap),
            ];

            static Geometry Across(double y, double x0, double x1) => Polygon(
                (x0, y), (x0 + half, y - half), (x1 - half, y - half), (x1, y), (x1 - half, y + half), (x0 + half, y + half));

            static Geometry Down(double x, double y0, double y1) => Polygon(
                (x, y0), (x + half, y0 + half), (x + half, y1 - half), (x, y1), (x - half, y1 - half), (x - half, y0 + half));
        }

        /// <summary>A closed shape, leaned forward by <see cref="DigitSlant"/>.</summary>
        private static Geometry Polygon(params (double X, double Y)[] points)
        {
            StringBuilder path = new();

            for (int i = 0; i < points.Length; i++)
            {
                double x = points[i].X + (DigitHeight - points[i].Y) * DigitSlant;
                path.Append(FormattableString.Invariant($"{(i == 0 ? "M" : "L")}{x:0.###},{points[i].Y:0.###} "));
            }

            path.Append('Z');

            return StreamGeometry.Parse(path.ToString());
        }
        #endregion
    }
}
