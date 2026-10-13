using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    public partial class MemoryWidget : Window
    {
        /// <summary>Past this much pointer travel the press was a drag, not a click.</summary>
        private const double DragSlop = 5;

        /// <summary>Dropped this close to a screen edge, the widget is pulled flush against it.</summary>
        private const int SnapWithin = 24;

        /// <summary>Where on screen the press landed, to measure travel against.</summary>
        private PixelPoint pressedAt;

        /// <summary>Where inside the window it was grabbed, so that spot stays under the pointer.</summary>
        private PixelPoint grabbedAt;

        private bool pressed;

        private bool dragged;

        private MemoryViewModel? model;

        public MemoryWidget() => InitializeComponent();

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (model is not null)
            {
                model.PropertyChanged -= ModelChanged;
            }

            model = DataContext as MemoryViewModel;

            if (model is null)
            {
                return;
            }

            model.PropertyChanged += ModelChanged;

            Width = model.WidgetDiameter;
            Height = model.WidgetDiameter;
            Opacity = model.WidgetOpacity;

            DrawHistory();
            DrawPeak();
        }

        /// <summary>Back where it was left, if that is still on a screen.</summary>
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (model?.WidgetPlace is { } place && Screens.ScreenFromPoint(place) is not null)
            {
                Position = place;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (model is not null)
            {
                model.PropertyChanged -= ModelChanged;
            }
        }

        private void ModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MemoryViewModel.WidgetDiameter):
                    Resize(model!.WidgetDiameter);
                    break;

                case nameof(MemoryViewModel.WidgetOpacity):
                    Opacity = model!.WidgetOpacity;
                    break;

                case nameof(MemoryViewModel.MemoryHistory):
                    DrawHistory();
                    break;

                case nameof(MemoryViewModel.PeakPercent):
                case nameof(MemoryViewModel.IsBusy):
                    DrawPeak();
                    break;
            }
        }

        /// <summary>
        ///  Changes size about the centre.
        /// </summary>
        /// <remarks>
        ///  A window grows from its top-left corner, so a dial in the bottom-right of the screen
        ///  would walk off it a step at a time under Ctrl+wheel. Moving it by half the change keeps
        ///  the middle of the dial where the user is looking.
        /// </remarks>
        private void Resize(double diameter)
        {
            double scaling = RenderScaling;
            int shift = (int)Math.Round((diameter - Width) * scaling / 2);

            Width = diameter;
            Height = diameter;

            Position = new PixelPoint(Position.X - shift, Position.Y - shift);
            model?.RememberWidgetPlace(Position);
        }

        /// <summary>Ctrl+wheel resizes, Shift+wheel fades; a plain wheel is left alone.</summary>
        private void Wheel(object? sender, PointerWheelEventArgs e)
        {
            if (model is null)
            {
                return;
            }

            // Shift turns a wheel sideways on some platforms, so either axis counts.
            double delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;

            if (delta == 0)
            {
                return;
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                model.ResizeWidgetBy(delta > 0 ? 10 : -10);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                model.FadeWidgetBy(delta > 0 ? 0.05 : -0.05);
                e.Handled = true;
            }
        }

        /// <summary>
        ///  The memory history as an area and a line, scaled to its own range.
        /// </summary>
        /// <remarks>
        ///  Scaled to the range it covers rather than to 0-100, padded a little: memory in use
        ///  moves a few per cent at a time, and on a full scale two minutes of it is a flat line.
        /// </remarks>
        private void DrawHistory()
        {
            IReadOnlyList<double> points = model?.MemoryHistory ?? [];

            if (points.Count < 2)
            {
                HistoryArea.Data = null;
                HistoryLine.Data = null;
                return;
            }

            const double width = 124;
            const double height = 46;

            double low = Math.Max(0, points.Min() - 4);
            double high = Math.Min(100, points.Max() + 4);

            if (high - low < 12)
            {
                double middle = (high + low) / 2;
                low = Math.Max(0, middle - 6);
                high = Math.Min(100, low + 12);
            }

            Point At(int index) => new(
                index * width / (points.Count - 1),
                height - ((points[index] - low) / (high - low) * height));

            StreamGeometry line = new();

            using (StreamGeometryContext context = line.Open())
            {
                context.BeginFigure(At(0), false);

                for (int index = 1; index < points.Count; index++)
                {
                    context.LineTo(At(index));
                }

                context.EndFigure(false);
            }

            StreamGeometry area = new();

            using (StreamGeometryContext context = area.Open())
            {
                context.BeginFigure(new Point(0, height), true);

                for (int index = 0; index < points.Count; index++)
                {
                    context.LineTo(At(index));
                }

                context.LineTo(new Point(width, height));
                context.EndFigure(true);
            }

            HistoryLine.Data = line;
            HistoryArea.Data = area;
        }

        /// <summary>Turns the notch to the peak, clockwise from twelve as the ring is drawn.</summary>
        private void DrawPeak()
        {
            double peak = model?.PeakPercent ?? 0;

            PeakMarker.IsVisible = peak > 0 && model is { IsBusy: false };
            PeakMarker.RenderTransform = new RotateTransform(Math.Clamp(peak, 0, 100) * 3.6);
        }

        /// <summary>
        ///  Takes hold of the widget.
        /// </summary>
        /// <remarks>
        ///  The window is moved by hand rather than with BeginMoveDrag, which is what the first cut
        ///  did and why dragging the widget always cleaned it. BeginMoveDrag hands the move to the
        ///  window manager and swallows the release, so there is no reliable moment afterwards at
        ///  which to ask "did it actually move" - comparing Position before and after reported no
        ///  movement, every drag was read as a click, and the cleaner ran every time.
        ///
        ///  Tracking the pointer instead makes the question trivial: a press that travels more than
        ///  a few pixels is a drag, and only a press that does not is a click.
        /// </remarks>
        private void DragStart(object? sender, PointerPressedEventArgs e)
        {
            // Left only: a right click belongs to the context menu, and taking hold of the window
            // here would eat it.
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            Point client = e.GetPosition(this);

            pressedAt = this.PointToScreen(client);
            grabbedAt = new PixelPoint(pressedAt.X - Position.X, pressedAt.Y - Position.Y);

            pressed = true;
            dragged = false;

            // Capture, so the widget keeps receiving moves when the pointer runs off its edge -
            // which on something this small happens almost immediately.
            e.Pointer.Capture(Face);
        }

        private void DragMove(object? sender, PointerEventArgs e)
        {
            if (!pressed)
            {
                return;
            }

            PixelPoint now = this.PointToScreen(e.GetPosition(this));

            if (!dragged && Distance(pressedAt, now) > DragSlop)
            {
                dragged = true;
            }

            if (dragged)
            {
                // Keep the grabbed spot under the pointer, rather than accumulating deltas, which
                // drifts over a long drag.
                Position = new PixelPoint(now.X - grabbedAt.X, now.Y - grabbedAt.Y);
            }
        }

        /// <summary>A press that went nowhere was a click on the face, which cleans.</summary>
        private void FaceReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!pressed || e.InitialPressMouseButton != MouseButton.Left)
            {
                return;
            }

            pressed = false;
            e.Pointer.Capture(null);

            if (!dragged)
            {
                Clean();
                return;
            }

            Snap();
            model?.RememberWidgetPlace(Position);
        }

        /// <summary>
        ///  Pulls the widget flush against a screen edge it was dropped near.
        /// </summary>
        /// <remarks>
        ///  Against the working area, not the whole screen, so it settles beside the taskbar rather
        ///  than under it. The dial is round and the window square, so flush means the window's
        ///  edge, which leaves the four pixels of margin round the face as a gap.
        /// </remarks>
        private void Snap()
        {
            if (model is not { WidgetSnaps: true } || Screens.ScreenFromWindow(this) is not { } screen)
            {
                return;
            }

            PixelRect area = screen.WorkingArea;
            int size = (int)Math.Round(Width * RenderScaling);
            int x = Position.X;
            int y = Position.Y;

            if (Math.Abs(x - area.X) < SnapWithin) x = area.X;
            if (Math.Abs(area.Right - (x + size)) < SnapWithin) x = area.Right - size;
            if (Math.Abs(y - area.Y) < SnapWithin) y = area.Y;
            if (Math.Abs(area.Bottom - (y + size)) < SnapWithin) y = area.Bottom - size;

            Position = new PixelPoint(x, y);
        }

        private void Clean()
        {
            if (model is not null && model.CleanCommand.CanExecute(null))
            {
                model.CleanCommand.Execute(null);
            }
        }

        private static double Distance(PixelPoint from, PixelPoint to)
        {
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;

            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        /// <summary>Closing must not also be read as a click on the face behind the button.</summary>
        private void CloseClick(object? sender, RoutedEventArgs e)
        {
            pressed = false;
            e.Handled = true;

            Close();
        }
    }
}
