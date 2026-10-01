using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Lets the rows of a list be put in a different order: by dragging one, or with Alt+Up and
    ///  Alt+Down on the selected one.
    /// </summary>
    /// <remarks>
    ///  Shared by the deck and the rail, so the two lists that can be rearranged are rearranged the
    ///  same way. The list only reports where a row should go; the move itself is the caller's,
    ///  because each list has a saved order of its own to keep in step with the rows.
    ///
    ///  The handlers tunnel, so they see the keys and the pointer before the list does: a list
    ///  treats Alt+Up as a plain Up and moves the selection, and marks a press on a row handled as
    ///  it selects it.
    ///
    ///  A drag moves the row as it goes rather than on release, so the list itself is the drop
    ///  indicator - the row is already where it will land. That avoids drawing an insertion line,
    ///  which in a virtualising list means tracking rows that may not exist yet. Disabled rows are
    ///  not hit at all, so a row that is out of reach can never be picked up.
    /// </remarks>
    internal sealed class ListReorder
    {
        /// <summary>How far a press has to travel before it is a drag rather than a click.</summary>
        private const double Threshold = 6;

        private readonly ListBox list;
        private readonly Action<object, int> move;

        private object? dragged;
        private Point from;
        private bool dragging;

        private ListReorder(ListBox list, Action<object, int> move)
        {
            this.list = list;
            this.move = move;
        }

        /// <summary>Makes <paramref name="list"/> reorderable, with <paramref name="move"/> doing the moving.</summary>
        /// <param name="move">Puts an item at a new index in the list's source, and saves the order.</param>
        public static void Attach(ListBox list, Action<object, int> move)
        {
            ListReorder reorder = new(list, move);

            list.AddHandler(InputElement.KeyDownEvent, reorder.KeyDown, RoutingStrategies.Tunnel);
            list.AddHandler(InputElement.PointerPressedEvent, reorder.Pressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            list.AddHandler(InputElement.PointerMovedEvent, reorder.Moved, RoutingStrategies.Tunnel, handledEventsToo: true);
            list.AddHandler(InputElement.PointerReleasedEvent, reorder.Released, RoutingStrategies.Tunnel, handledEventsToo: true);
            list.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => reorder.dragged = null);
        }

        /// <summary>
        ///  Puts keyboard focus back on the selected row.
        /// </summary>
        /// <remarks>
        ///  A row that is moved or deleted takes the focus with it, and the arrows then go nowhere
        ///  until the list is clicked again. Posted, because the row's new container is only made
        ///  on the next layout pass.
        /// </remarks>
        public static void Refocus(ListBox list) => Dispatcher.UIThread.Post(() =>
        {
            if (list.SelectedItem is { } selected && list.ContainerFromItem(selected) is { } row)
            {
                row.Focus(NavigationMethod.Directional);
            }
        }, DispatcherPriority.Background);

        private void KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers != KeyModifiers.Alt || e.Key is not (Key.Up or Key.Down)
                || list.SelectedItem is not { } item)
            {
                return;
            }

            int to = list.SelectedIndex + (e.Key == Key.Up ? -1 : 1);

            if (to >= 0 && to < list.ItemCount)
            {
                move(item, to);
                Refocus(list);
            }

            e.Handled = true;
        }

        private void Pressed(object? sender, PointerPressedEventArgs e)
        {
            dragging = false;
            dragged = null;

            if (!e.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
            {
                return;
            }

            dragged = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext;
            from = e.GetPosition(list);
        }

        private void Moved(object? sender, PointerEventArgs e)
        {
            if (dragged is null || !e.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
            {
                return;
            }

            Point at = e.GetPosition(list);

            if (!dragging)
            {
                if (Math.Abs(at.Y - from.Y) < Threshold && Math.Abs(at.X - from.X) < Threshold)
                {
                    return;
                }

                dragging = true;
                e.Pointer.Capture(list);
            }

            if (RowAt(at) is var to and >= 0)
            {
                move(dragged, to);
            }

            e.Handled = true;
        }

        private void Released(object? sender, PointerReleasedEventArgs e)
        {
            if (dragging)
            {
                e.Pointer.Capture(null);
                e.Handled = true;
                Refocus(list);
            }

            dragging = false;
            dragged = null;
        }

        /// <summary>The index of the row under a point in the list, or -1 between rows or past the end.</summary>
        private int RowAt(Point at)
        {
            for (int index = 0; index < list.ItemCount; index++)
            {
                if (list.ContainerFromIndex(index) is not { } row
                    || row.TranslatePoint(default, list) is not { } top)
                {
                    continue;
                }

                if (at.Y >= top.Y && at.Y < top.Y + row.Bounds.Height)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
