using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    public partial class MemoryWidget : Window
    {
        /// <summary>Past this much pointer travel the press was a drag, not a click.</summary>
        private const double DragSlop = 5;

        /// <summary>Where on screen the press landed, to measure travel against.</summary>
        private PixelPoint pressedAt;

        /// <summary>Where inside the window it was grabbed, so that spot stays under the pointer.</summary>
        private PixelPoint grabbedAt;

        private bool pressed;

        private bool dragged;

        public MemoryWidget() => InitializeComponent();

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
            // which on something 180 pixels across happens almost immediately.
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
            }
        }

        private void Clean()
        {
            if (DataContext is MemoryViewModel model && model.CleanCommand.CanExecute(null))
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
