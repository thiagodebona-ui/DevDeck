using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DevDeck.App.ViewModels;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Dragging requests: up and down a list to reorder them, into a group, into another one, or
    ///  out of groups altogether. Alt+Up and Alt+Down move the selected one, as in the deck.
    /// </summary>
    /// <remarks>
    ///  The same pointer-capture drag <see cref="ListReorder"/> uses rather than the platform's
    ///  drag and drop: the request never leaves this window, and a drag that stays inside one
    ///  control needs no data package, no drag image, and none of the per-platform differences
    ///  that come with both. ListReorder itself does not fit, because it knows one list and this
    ///  side panel is several - the ungrouped one and one per group.
    ///
    ///  So the drop is worked out by hit-testing whatever is under the pointer and walking up to
    ///  the first thing that names a place: a row (land where it is), a group's header or list
    ///  (file it there), the ungrouped list, or the strip that appears at the top while a grouped
    ///  request is being dragged. That one exists because with every request filed the ungrouped
    ///  list is empty and has no height to drop on.
    ///
    ///  Unlike the deck, rows are not moved while the pointer travels: a drop can also mean "file
    ///  it in that group", and a row that had already jumped into a group on the way past would
    ///  have changed something the user never let go of. The row it will land on carries an
    ///  accent line on the side it will land, which is the whole of the feedback needed.
    /// </remarks>
    public partial class HttpView
    {
        /// <summary>How far a press has to travel before it is a drag rather than a click.</summary>
        private const double DragThreshold = 6;

        private HttpRequest? dragged;

        private Point dragFrom;

        private bool dragging;

        /// <summary>The drop target currently lit, and the class it was lit with.</summary>
        private (Control Control, string Class)? lit;

        private void AttachDrag()
        {
            RequestTree.AddHandler(PointerPressedEvent, DragPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            RequestTree.AddHandler(PointerMovedEvent, DragMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            RequestTree.AddHandler(PointerReleasedEvent, DragReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            RequestTree.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag());
            RequestTree.AddHandler(KeyDownEvent, MoveKeyDown, RoutingStrategies.Tunnel);
        }

        /// <summary>
        ///  Alt+Up and Alt+Down: the selected request swaps with its neighbour in the same list.
        /// </summary>
        /// <remarks>
        ///  Tunnelled, because a ListBox reads Alt+Up as a plain Up and moves the selection instead.
        /// </remarks>
        private void MoveKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers != KeyModifiers.Alt
                || e.Key is not (Key.Up or Key.Down)
                || DataContext is not HttpViewModel model)
            {
                return;
            }

            e.Handled = true;

            HttpRequest moved = model.Selected;

            if (model.Neighbour(moved, e.Key == Key.Up ? -1 : 1) is { } next)
            {
                model.Reorder(moved, next);
                Refocus(moved);
            }
        }

        /// <summary>Puts keyboard focus back on a request's row after it moved, so the keys keep working.</summary>
        private void Refocus(HttpRequest request) => Dispatcher.UIThread.Post(() =>
        {
            foreach (ListBox list in RequestTree.GetVisualDescendants().OfType<ListBox>())
            {
                if (list.ContainerFromItem(request) is { } row)
                {
                    row.Focus(NavigationMethod.Directional);
                    return;
                }
            }
        }, DispatcherPriority.Background);

        private void DragPressed(object? sender, PointerPressedEventArgs e)
        {
            EndDrag();

            if (!e.GetCurrentPoint(RequestTree).Properties.IsLeftButtonPressed
                || e.Source is not Visual source
                || source.FindAncestorOfType<Button>(includeSelf: true) is { } button && button.Classes.Contains("play"))
            {
                return;
            }

            dragged = source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as HttpRequest;
            dragFrom = e.GetPosition(RequestTree);
        }

        private void DragMoved(object? sender, PointerEventArgs e)
        {
            if (dragged is not { } request || !e.GetCurrentPoint(RequestTree).Properties.IsLeftButtonPressed)
            {
                return;
            }

            Point at = e.GetPosition(RequestTree);

            if (!dragging)
            {
                if (Math.Abs(at.Y - dragFrom.Y) < DragThreshold && Math.Abs(at.X - dragFrom.X) < DragThreshold)
                {
                    return;
                }

                dragging = true;
                e.Pointer.Capture(RequestTree);

                // Only a filed request has a group to come out of.
                Ungroup.IsVisible = request.Group.Length > 0;
            }

            Drop? drop = Target(at);

            Light(drop is null ? null : (drop.Control, drop.Mark));
            e.Handled = true;
        }

        private void DragReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!dragging || dragged is not { } request)
            {
                dragged = null;
                return;
            }

            e.Handled = true;

            Drop? drop = Target(e.GetPosition(RequestTree));

            e.Pointer.Capture(null);
            EndDrag();

            if (drop is null || DataContext is not HttpViewModel model)
            {
                return;
            }

            if (!string.Equals(drop.Group, request.Group.Trim(), StringComparison.Ordinal))
            {
                model.MoveRequestTo(request, drop.Group);
            }

            // Dropped on a row: it lands where that row is, whether it came from this list or
            // has just been filed into it.
            if (drop.Over is { } over && over != request)
            {
                model.Reorder(request, over);
            }

            Refocus(request);
        }

        private void EndDrag()
        {
            Light(null);
            Ungroup.IsVisible = false;
            dragging = false;
            dragged = null;
        }

        /// <summary>Where a drop would land: the control to light, how, the group it means, and the row it is on.</summary>
        private sealed record Drop(Control Control, string Mark, string Group, HttpRequest? Over);

        private Drop? Target(Point at)
        {
            if (RequestTree.InputHitTest(at) is not Visual hit || dragged is not { } request)
            {
                return null;
            }

            HttpRequest? over = null;
            ListBoxItem? row = null;

            foreach (Visual visual in hit.GetSelfAndVisualAncestors())
            {
                if (visual == RequestTree)
                {
                    break;
                }

                switch (visual)
                {
                    case ListBoxItem { DataContext: HttpRequest under } item when row is null:
                        row = item;
                        over = under;
                        break;

                    case Border border when border == Ungroup:
                        return new Drop(border, "drop", string.Empty, null);

                    case Button { Tag: RequestGroup group } header when header.Classes.Contains("group"):
                        return new Drop(header, "drop", group.Name, null);

                    case ListBox list when list == Loose || list.DataContext is RequestGroup:
                        string name = list.DataContext is RequestGroup owner ? owner.Name : string.Empty;

                        if (row is not null && over is not null && over != request)
                        {
                            // A line above the row it would take the place of when it is coming
                            // up the list, below it when it is going down - the same place the
                            // row will be once it lands.
                            bool sameList = string.Equals(name, request.Group.Trim(), StringComparison.Ordinal);
                            bool down = sameList && Index(list, request) < Index(list, over);

                            return new Drop(row, down ? "after" : "before", name, over);
                        }

                        return new Drop(list, "drop", name, null);
                }
            }

            return null;
        }

        private static int Index(ListBox list, HttpRequest request) =>
            list.ItemsSource is IList<HttpRequest> items ? items.IndexOf(request) : -1;

        private void Light((Control Control, string Class)? target)
        {
            if (lit == target)
            {
                return;
            }

            if (lit is { } was)
            {
                was.Control.Classes.Remove(was.Class);
            }

            lit = target;

            if (lit is { } now)
            {
                now.Control.Classes.Add(now.Class);
            }
        }
    }
}
