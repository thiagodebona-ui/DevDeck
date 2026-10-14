using Avalonia;
using Avalonia.Media;

namespace DevDeck.App.Views.Skins
{
    /// <summary>
    ///  A pocket pet on an egg-shaped handheld, living on a little green LCD.
    /// </summary>
    /// <remarks>
    ///  The pet's mood is the machine's. Little memory in use and an idle processor: it bounces.
    ///  An ordinary load: it potters from side to side. Memory past the high mark: it sweats and
    ///  stares. In the red: it turns green, its eyes go to crosses and it wobbles on its feet. A
    ///  busy processor sets it running back and forth; a processor left idle for a while sends it
    ///  to sleep. The hearts under the reading are the memory still free. A clean is a meal: the
    ///  middle button is pressed, food drops in, it munches and hearts float up.
    ///
    ///  Everything on the screen sits on one grid of three pixel cells, and moves a whole cell at
    ///  a time, as a real LCD can only. Smooth motion there would look like a picture of a pet
    ///  rather than the toy itself.
    /// </remarks>
    internal sealed class PetScene : SkinScene
    {
        // The LCD, and the cell everything on it is drawn in.
        private const double LcdX = 46;
        private const double LcdY = 52;
        private const double LcdW = 138;
        private const double LcdH = 102;
        private const double Cell = 3;

        // Where the pet stands, in cells: feet on the ground row.
        private const int PetCol = 4;
        private const int PetRow = 9;
        private const int GroundRow = 25;

        // Where food lands: beside the pet's mouth, clear of the reading.
        private const int FoodCol = 21;
        private const int FoodTopRow = 6;
        private const int FoodRow = 20;

        private const double ReadoutX = 153;
        private const double FoodCycle = 2.0;
        private const double DozeAfter = 30;
        private const double BlinkLength = 0.15;

        private static readonly Rect LcdRect = new(LcdX, LcdY, LcdW, LcdH);
        private static readonly Rect BezelRect = new(38, 44, 154, 124);

        private static readonly Color ShellLight = Color.FromRgb(0xFF, 0xA3, 0xC0);
        private static readonly Color ShellDark = Color.FromRgb(0xE0, 0x52, 0x80);
        private static readonly Color ShellEdge = Color.FromRgb(0xB0, 0x35, 0x5F);
        private static readonly Color BezelLight = Color.FromRgb(0xFF, 0xF5, 0xEC);
        private static readonly Color BezelDark = Color.FromRgb(0xF0, 0xD6, 0xCA);
        private static readonly Color BezelEdge = Color.FromRgb(0xD3, 0xA8, 0xA0);
        private static readonly Color LcdLight = Color.FromRgb(0xCF, 0xD9, 0xBC);
        private static readonly Color LcdDark = Color.FromRgb(0xA8, 0xB8, 0x95);
        private static readonly Color LcdInk = Color.FromRgb(0x24, 0x2E, 0x20);
        private static readonly Color SickGreen = Color.FromRgb(0x5E, 0x9A, 0x2C);
        private static readonly Color ButtonLight = Color.FromRgb(0xFF, 0xDE, 0x6E);
        private static readonly Color ButtonDark = Color.FromRgb(0xF0, 0xA8, 0x2A);
        private static readonly Color ButtonEdge = Color.FromRgb(0xC2, 0x7D, 0x14);
        private static readonly Color Metal = Color.FromRgb(0xD0, 0xD6, 0xDF);
        private static readonly Color MetalShade = Color.FromRgb(0x86, 0x8F, 0x9D);
        private static readonly Color White = Colors.White;
        private static readonly Color Black = Colors.Black;

        // The shell: narrow at the top, full at the bottom, like an egg stood on its end.
        private static readonly Geometry Egg = StreamGeometry.Parse(
            "M115,14 C170,14 213,80 213,140 C213,192 170,224 115,224 C60,224 17,192 17,140 C17,80 60,14 115,14 Z");

        // The seam a few pixels inside it, where the two halves of the shell meet.
        private static readonly Geometry Seam = StreamGeometry.Parse(
            "M115,19 C166,19 208,82 208,140 C208,189 167,219 115,219 C63,219 22,189 22,140 C22,82 64,19 115,19 Z");

        #region Sprites
        // '#' is ink. In the body 'o' is the inside, filled only faintly, or green when it is sick.
        private static readonly string[] Body =
        [
            "..##........##..",
            "..#o#......#o#..",
            "..#oo######oo#..",
            ".#oooooooooooo#.",
            "#oooooooooooooo#",
            "#oooooooooooooo#",
            "#oooooooooooooo#",
            "#oooooooooooooo#",
            "#oooooooooooooo#",
            "#oooooooooooooo#",
            "#oooooooooooooo#",
            ".#oooooooooooo#.",
            "..#oooooooooo#..",
            "...##########...",
        ];

        private static readonly string[] FeetStand =
        [
            "...##......##...",
            "..###......###..",
        ];

        private static readonly string[] FeetStep =
        [
            "..##........##..",
            ".##..........##.",
        ];

        // Eyes are three cells square, drawn as the left eye and mirrored for the right.
        private static readonly string[] EyeOpen = ["...", ".##", ".##"];
        private static readonly string[] EyeShut = ["...", "...", "###"];
        private static readonly string[] EyeHappy = ["...", ".#.", "#.#"];
        private static readonly string[] EyeWide = ["###", "#.#", "###"];
        private static readonly string[] EyeCross = ["#.#", ".#.", "#.#"];
        private static readonly string[] EyeFierce = ["#..", ".##", ".##"];
        private static readonly string[] EyeAsleep = ["...", "#.#", ".#."];

        // Mouths are four cells wide, under the eyes.
        private static readonly string[] MouthSmile = ["#..#", ".##."];
        private static readonly string[] MouthGrin = ["####", ".##."];
        private static readonly string[] MouthOoh = [".##.", "#..#", ".##."];
        private static readonly string[] MouthWavy = [".#.#", "#.#."];
        private static readonly string[] MouthShout = ["####", "#..#", "####"];
        private static readonly string[] MouthSleepy = ["....", ".##."];
        private static readonly string[] MouthMunch = [".##.", "#..#", "#..#", ".##."];
        private static readonly string[] MouthChew = ["....", "####"];

        private static readonly string[] Cheek = ["##"];

        private static readonly string[] Apple =
        [
            "..#..",
            ".###.",
            "#####",
            "#####",
            ".###.",
        ];

        private static readonly string[] HeartFull =
        [
            ".##.##.",
            "#######",
            "#######",
            ".#####.",
            "..###..",
            "...#...",
        ];

        private static readonly string[] HeartEmpty =
        [
            ".##.##.",
            "#..#..#",
            "#.....#",
            ".#...#.",
            "..#.#..",
            "...#...",
        ];

        private static readonly string[] Zed =
        [
            "####",
            "..#.",
            ".#..",
            "####",
        ];

        private static readonly string[] Drop =
        [
            ".#.",
            "###",
            "###",
            ".#.",
        ];
        #endregion

        private enum Mood
        {
            Happy,
            Content,
            Sweating,
            Sick,
            Frantic,
            Dozing,
            Eating,
        }

        /// <summary>Seconds the processor has been near idle; long enough and the pet dozes off.</summary>
        private double quietFor;

        private double nextBlink;

        protected override void Seed() => nextBlink = Time + 2;

        protected override void Advance(double dt)
        {
            // Two thresholds, so the probe's jitter around idle neither wakes it nor keeps it up.
            if (Busy || Cpu > 0.18)
            {
                quietFor = 0;
            }
            else if (Cpu < 0.1)
            {
                quietFor += dt;
            }

            if (Time >= nextBlink + BlinkLength)
            {
                nextBlink = Time + 2.5 + Random.NextDouble() * 3.5;
            }
        }

        private bool Blinking => Time >= nextBlink && Time < nextBlink + BlinkLength;

        private Mood CurrentMood()
        {
            if (Busy)
            {
                return Mood.Eating;
            }

            if (MemoryLevel == 2)
            {
                return Mood.Sick;
            }

            if (CpuLevel >= 1)
            {
                return Mood.Frantic;
            }

            if (MemoryLevel == 1)
            {
                return Mood.Sweating;
            }

            // Fed and pleased with itself for a little while after a meal.
            if (SinceClean < 4)
            {
                return Mood.Happy;
            }

            if (quietFor > DozeAfter)
            {
                return Mood.Dozing;
            }

            return Memory < 0.5 && Cpu < 0.3 ? Mood.Happy : Mood.Content;
        }

        /// <summary>How far through the current meal the clean is: one piece of food per cycle.</summary>
        private double MealTime => BusyFor % FoodCycle;

        protected override void Draw(DrawingContext context)
        {
            Mood mood = CurrentMood();

            DrawKeyring(context, mood);
            DrawShell(context);
            DrawButtons(context);

            // The bezel, a raised cream frame round the screen.
            context.DrawRectangle(Solid(ShellEdge, 0.35), null, BezelRect.Translate(new Vector(0, 2)), 16, 16);
            context.DrawRectangle(Fade(BezelLight, BezelDark), Pen(BezelEdge, 1), BezelRect, 16, 16);

            // A little brighter under the pointer, as if tilted to the light to read it.
            Color top = Hovered ? Mix(LcdLight, White, 0.15) : LcdLight;
            Color bottom = Hovered ? Mix(LcdDark, White, 0.15) : LcdDark;
            context.DrawRectangle(Fade(top, bottom), null, LcdRect, 5, 5);

            using (context.PushClip(LcdRect))
            {
                DrawGrid(context);
                DrawScenery(context);
                DrawPet(context, mood);
                DrawEffects(context, mood);
                DrawReadout(context);

                // Shade under the bezel's lip, and glare across the glass.
                context.DrawRectangle(Fade(WithAlpha(LcdInk, 0.18), WithAlpha(LcdInk, 0)), null, new Rect(LcdX, LcdY, LcdW, 5));
                context.DrawRectangle(Fade(WithAlpha(White, 0.14), WithAlpha(White, 0)), null, new Rect(LcdX, LcdY, LcdW, 34));
            }

            context.DrawRectangle(null, Pen(LcdInk, 0.4, 1.5), LcdRect, 5, 5);
        }

        #region The device
        private void DrawKeyring(DrawingContext context, Mood mood)
        {
            // The ring swings on its loop, and rattles when the pet is running about.
            double swing = Math.Sin(Time * 1.3) * 6
                + (mood == Mood.Frantic ? Math.Sin(Time * 11) * 9 : 0)
                + (mood == Mood.Eating ? Math.Sin(Time * 7) * 4 : 0);

            context.DrawRectangle(Solid(ShellDark), Pen(ShellEdge, 1), new Rect(108, 10, 14, 9), 3, 3);

            Matrix hang = Matrix.CreateRotation(swing * Math.PI / 180) * Matrix.CreateTranslation(115, 12);

            using (context.PushTransform(hang))
            {
                context.DrawEllipse(null, Pen(MetalShade, 3), new Point(0, -3), 6, 7.5);
                context.DrawEllipse(null, Pen(Metal, 1.6), new Point(0, -3), 6, 7.5);
                context.DrawEllipse(Solid(White, 0.8), null, new Point(-3.5, -7.5), 0.9, 0.9);
            }
        }

        private void DrawShell(DrawingContext context)
        {
            context.DrawGeometry(Fade(ShellLight, ShellDark), null, Egg);

            // Light from the upper left: a shade across, then a soft highlight and its hard spot.
            context.DrawGeometry(Fade(WithAlpha(White, 0.14), WithAlpha(Black, 0.14), horizontal: true), null, Egg);

            using (context.PushTransform(Matrix.CreateRotation(-0.55) * Matrix.CreateTranslation(70, 54)))
            {
                context.DrawEllipse(Glow(White, 0.55), null, new Point(0, 0), 34, 17);
                context.DrawEllipse(Solid(White, 0.75), null, new Point(-6, -5), 8, 3);
            }

            context.DrawGeometry(null, Pen(White, 0.22, 1), Seam);

            // The rim, lit in the accent when the pointer is on it, as the dial's is.
            context.DrawGeometry(null, Hovered ? Pen(Accent, 2.5) : Pen(ShellEdge, 1.5), Egg);

            // Speaker holes under the buttons.
            for (int i = 0; i < 5; i++)
            {
                context.DrawEllipse(Solid(ShellEdge, 0.6), null, new Point(103 + i * 6, 211), 1.3, 1.3);
            }
        }

        private void DrawButtons(DrawingContext context)
        {
            // The middle button is pressed for each piece of food a clean serves.
            bool feeding = Busy && MealTime < 0.25;

            DrawButton(context, new Point(86, 186), false);
            DrawButton(context, new Point(115, 191), feeding);
            DrawButton(context, new Point(144, 186), false);
        }

        private void DrawButton(DrawingContext context, Point at, bool pressed)
        {
            const double r = 8;
            double sink = pressed ? 1.5 : 0;

            context.DrawEllipse(Solid(ShellEdge, 0.55), null, new Point(at.X, at.Y + 2), r, r);

            Point face = new(at.X, at.Y + sink);
            context.DrawEllipse(pressed ? Fade(ButtonDark, ButtonLight) : Fade(ButtonLight, ButtonDark), Pen(ButtonEdge, 1), face, r, r);
            context.DrawEllipse(Solid(White, pressed ? 0.3 : 0.65), null, new Point(face.X - 2.8, face.Y - 3), 2.4, 1.5);
        }
        #endregion

        #region The screen
        private void DrawGrid(DrawingContext context)
        {
            // The faint gaps between the LCD's cells, on the same grid every sprite is drawn to.
            IPen grid = Pen(LcdInk, 0.05, 0.5);

            for (double x = LcdX + Cell; x < LcdX + LcdW; x += Cell)
            {
                context.DrawLine(grid, new Point(x, LcdY), new Point(x, LcdY + LcdH));
            }

            for (double y = LcdY + Cell; y < LcdY + LcdH; y += Cell)
            {
                context.DrawLine(grid, new Point(LcdX, y), new Point(LcdX + LcdW, y));
            }
        }

        private void DrawScenery(DrawingContext context)
        {
            IBrush dots = Solid(LcdInk, 0.35);

            // A dotted rule under the status line, and the ground the pet stands on.
            for (double x = LcdX + 4; x < LcdX + LcdW - 3; x += 3)
            {
                context.DrawRectangle(dots, null, new Rect(x, 67, 1.5, 1));
            }

            for (int col = 1; col < 29; col += 2)
            {
                context.DrawRectangle(dots, null, Cellular(col, GroundRow, 1, 0.5));
            }

            Text(
                context, $"{CpuCaption} {CpuText} · {GpuCaption} {GpuText} · {DiskCaption} {DiskText}",
                new Point(115, 60), 7, WithAlpha(LcdInk, 0.8), mono: true);
        }

        private static Rect Cellular(int col, int row, double width, double height) =>
            new(LcdX + col * Cell, LcdY + row * Cell, width * Cell, height * Cell);

        private void DrawPet(DrawingContext context, Mood mood)
        {
            int col = PetCol;
            int row = PetRow;
            bool step = false;

            switch (mood)
            {
                case Mood.Happy:
                    row -= (int)Math.Round(Math.Abs(Math.Sin(Time * 4.2)) * 2);
                    break;

                case Mood.Content:
                    // Pottering a cell either way, stepping as it goes.
                    col += (int)Math.Round(Math.Sin(Time * 0.7) * 1.4);
                    step = Math.Abs(Math.Cos(Time * 0.7)) > 0.35 && (int)(Time / 0.4) % 2 == 1;
                    break;

                case Mood.Sick:
                    // Unsteady: a cell to one side, back, a cell to the other.
                    col += ((int)(Time * 2.5) % 4) switch
                    {
                        1 => 1,
                        3 => -1,
                        _ => 0,
                    };
                    break;

                case Mood.Frantic:
                    col += (int)Math.Round(Math.Sin(Time * 3.2) * 3);
                    step = (int)(Time / 0.1) % 2 == 1;
                    break;
            }

            double x = LcdX + col * Cell;
            double y = LcdY + row * Cell;
            bool sick = mood == Mood.Sick;

            Pixels(context, Body, x, y, Cell, sick ? Solid(SickGreen, 0.55) : Solid(LcdInk, 0.06), 'o');
            DrawInk(context, Body, x, y, Cell, LcdInk);
            DrawInk(context, step ? FeetStep : FeetStand, x, y + 14 * Cell, Cell, LcdInk);

            DrawEyes(context, x, y, mood switch
            {
                Mood.Happy => EyeHappy,
                Mood.Eating => EyeHappy,
                Mood.Sick => EyeCross,
                Mood.Dozing => EyeAsleep,
                _ when Blinking => EyeShut,
                Mood.Sweating => EyeWide,
                Mood.Frantic => EyeFierce,
                _ => EyeOpen,
            });

            DrawInk(context, mood switch
            {
                Mood.Happy => MouthGrin,
                Mood.Sweating => MouthOoh,
                Mood.Sick => MouthWavy,
                Mood.Frantic => MouthShout,
                Mood.Dozing => MouthSleepy,
                Mood.Eating => EatingMouth(),
                _ => MouthSmile,
            }, x + 6 * Cell, y + 9 * Cell, Cell, LcdInk);

            if (mood is Mood.Happy or Mood.Content or Mood.Eating or Mood.Dozing)
            {
                DrawInk(context, Cheek, x + 2 * Cell, y + 8 * Cell, Cell, LcdInk, 0.35);
                DrawInk(context, Cheek, x + 12 * Cell, y + 8 * Cell, Cell, LcdInk, 0.35);
            }

            if (mood is Mood.Sweating or Mood.Frantic)
            {
                DrawSweat(context, x, y);
            }

            if (sick)
            {
                DrawDizzy(context, x, y);
            }

            if (mood == Mood.Frantic)
            {
                DrawSpeedLines(context, x, y);
            }
        }

        private void DrawEyes(DrawingContext context, double x, double y, string[] eye)
        {
            DrawInk(context, eye, x + 3 * Cell, y + 5 * Cell, Cell, LcdInk);
            DrawInk(context, eye, x + 10 * Cell, y + 5 * Cell, Cell, LcdInk, mirror: true);
        }

        private string[] EatingMouth()
        {
            double t = MealTime;

            // Agape while the food falls, then chewing until it is gone.
            if (t < 0.45)
            {
                return MouthMunch;
            }

            return (int)((t - 0.45) / 0.16) % 2 == 0 ? MouthMunch : MouthChew;
        }

        private void DrawSweat(DrawingContext context, double x, double y)
        {
            for (int i = 0; i < 2; i++)
            {
                double phase = (Time * 1.2 + i * 0.5) % 1;
                double dropX = i == 0 ? x - 2 * Cell : x + 16 * Cell + 0.5;
                double dropY = y + 2 * Cell + phase * 18;

                DrawInk(context, Drop, dropX, dropY, 1.5, LcdInk, 1 - phase);
            }
        }

        private void DrawDizzy(DrawingContext context, double x, double y)
        {
            // Three specks circling its head, cartoon-dazed.
            for (int i = 0; i < 3; i++)
            {
                double a = Time * 3.4 + i * Math.Tau / 3;
                double sx = x + 8 * Cell + Math.Cos(a) * 14;
                double sy = y - 4 + Math.Sin(a) * 2.5;

                // The far side of the circle passes behind its ears, so it is drawn fainter.
                double alpha = Math.Sin(a) > 0 ? 0.9 : 0.4;
                context.DrawRectangle(Solid(LcdInk, alpha), null, new Rect(sx - 1.25, sy - 1.25, 2.5, 2.5));
            }
        }

        private void DrawSpeedLines(DrawingContext context, double x, double y)
        {
            double velocity = Math.Cos(Time * 3.2);

            if (Math.Abs(velocity) < 0.3)
            {
                return;
            }

            // Behind it, whichever way it is running.
            IBrush ink = Solid(LcdInk, 0.6);

            for (int i = 0; i < 3; i++)
            {
                int length = 2 + (i % 2);
                double lineY = y + (5 + i * 3) * Cell + Cell * 0.35;
                double lineX = velocity > 0 ? x - (length + 1) * Cell : x + 17 * Cell;

                context.DrawRectangle(ink, null, new Rect(lineX, lineY, length * Cell, Cell * 0.4));
            }
        }

        private void DrawEffects(DrawingContext context, Mood mood)
        {
            if (mood == Mood.Eating)
            {
                DrawFood(context);
            }

            // Hearts while it eats and for a moment after; zeds while it sleeps. Both rise in the
            // clear column between the pet and the reading.
            if (Busy || SinceClean < 2.5)
            {
                for (int i = 0; i < 3; i++)
                {
                    double phase = (Time * 0.7 + i / 3.0) % 1;
                    double hx = 107 + Math.Sin(phase * 6 + i * 2) * 3;
                    double hy = 98 - phase * 28;

                    DrawInk(context, HeartFull, hx, hy, 1.5, LcdInk, Math.Sin(phase * Math.PI));
                }
            }
            else if (mood == Mood.Dozing)
            {
                for (int i = 0; i < 3; i++)
                {
                    double phase = (Time * 0.4 + i / 3.0) % 1;
                    double cell = 1.1 + phase * 1.2;

                    DrawInk(context, Zed, 106 + phase * 8, 100 - phase * 30, cell, LcdInk, Math.Sin(phase * Math.PI));
                }
            }
        }

        private void DrawFood(DrawingContext context)
        {
            double t = MealTime;
            int row;
            int bites;

            if (t < 0.45)
            {
                // Dropped in from above, gathering speed as it falls.
                double fall = t / 0.45;
                row = FoodTopRow + (int)Math.Round((FoodRow - FoodTopRow) * fall * fall);
                bites = 0;
            }
            else
            {
                row = FoodRow;
                bites = Math.Min((int)((t - 0.45) / (FoodCycle - 0.45) * 6), 5);
            }

            // Eaten from the side that faces the pet, a column a bite.
            DrawInk(context, Apple, LcdX + FoodCol * Cell, LcdY + row * Cell, Cell, LcdInk, skip: bites);
        }

        private void DrawReadout(DrawingContext context)
        {
            Color faint = WithAlpha(LcdInk, 0.75);

            Text(context, MemoryCaption, new Point(ReadoutX, 80), 7, faint, mono: true);

            if (Flashing)
            {
                // Fitted to the column: a short amount large, a reason why nothing was freed smaller.
                double size = Math.Clamp(Math.Floor(64 / (Math.Max(FlashText.Length, 1) * 0.6)), 8, 18);
                Color colour = FlashGood ? Mix(LcdInk, Good, 0.45) : LcdInk;

                Text(context, FlashText, new Point(ReadoutX, 98), size, colour, bold: true, mono: true);

                if (FlashGood)
                {
                    Text(context, FreedCaption, new Point(ReadoutX, 115), 7, faint, mono: true);
                }
            }
            else
            {
                Color ink = MemoryLevel == 0 ? LcdInk : Mix(LcdInk, LevelColour(MemoryLevel, LcdInk), 0.6);
                Text(context, MemoryText, new Point(ReadoutX, 98), 22, ink, bold: true, mono: true);

                DrawHealth(context);
            }

            Text(
                context, Hovered ? ClickToClean : UsedOfTotal, new Point(115, 141), 7.5,
                Hovered ? Mix(LcdInk, Accent, 0.5) : faint, bold: Hovered, mono: true);
        }

        /// <summary>Four hearts for the memory still free; in the red they flash for attention.</summary>
        private void DrawHealth(DrawingContext context)
        {
            const double cell = 1.5;
            const double width = 7 * cell;
            const double gap = 2.5;

            if (MemoryLevel == 2 && (int)(Time * 2) % 2 == 1)
            {
                return;
            }

            int lit = (int)Math.Round((1 - Memory) * 4);
            double left = ReadoutX - (4 * width + 3 * gap) / 2;

            for (int i = 0; i < 4; i++)
            {
                DrawInk(context, i < lit ? HeartFull : HeartEmpty, left + i * (width + gap), 110, cell, LcdInk);
            }
        }
        #endregion

        #region Pixels
        /// <summary>
        ///  Ink on the LCD: a faint copy down and to the right first, the shadow a real LCD's
        ///  segments cast on the reflector behind them, then the ink itself.
        /// </summary>
        private void DrawInk(
            DrawingContext context, string[] rows, double x, double y, double cell, Color colour,
            double alpha = 1, bool mirror = false, int skip = 0)
        {
            if (alpha <= 0.02)
            {
                return;
            }

            double offset = cell * 0.3;
            Pixels(context, rows, x + offset, y + offset, cell, Solid(colour, 0.16 * alpha), '#', mirror, skip);
            Pixels(context, rows, x, y, cell, Solid(colour, 0.9 * alpha), '#', mirror, skip);
        }

        /// <summary>One small square per lit cell, inset a touch so the grid shows between them.</summary>
        private static void Pixels(
            DrawingContext context, string[] rows, double x, double y, double cell, IBrush brush,
            char on, bool mirror = false, int skip = 0)
        {
            double gap = cell * 0.08;
            double size = cell - gap * 2;

            for (int r = 0; r < rows.Length; r++)
            {
                string line = rows[r];

                for (int c = skip; c < line.Length; c++)
                {
                    if (line[c] != on)
                    {
                        continue;
                    }

                    int at = mirror ? line.Length - 1 - c : c;
                    context.DrawRectangle(brush, null, new Rect(x + at * cell + gap, y + r * cell + gap, size, size));
                }
            }
        }
        #endregion
    }
}
