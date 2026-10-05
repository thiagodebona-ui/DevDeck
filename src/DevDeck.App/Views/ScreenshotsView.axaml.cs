using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  The Screenshots page. Here for the page's lifetime, double-click, and dragging a tile out.
    /// </summary>
    /// <remarks>
    ///  The drag is the platform's own drag and drop, unlike every drag inside the app: a tile is
    ///  dragged out of DevDeck into Teams, a Jira comment, a browser upload or a folder, and only
    ///  a real file on the system's drag carries there. The file is looked up when the button
    ///  goes down, so it is ready by the time the pointer has moved far enough to be a drag.
    ///
    ///  Several can be selected with Ctrl and Shift, and dragging any one of them takes them
    ///  all, as in Explorer. That means a press on a tile that is already part of a selection
    ///  must not collapse the selection to that tile straight away - only when the button comes
    ///  up without having dragged.
    /// </remarks>
    public partial class ScreenshotsView : UserControl
    {
        /// <summary>How far a press has to travel before it is a drag rather than a click.</summary>
        private const double DragThreshold = 6;

        private PointerPressedEventArgs? pressed;

        /// <summary>The list the press was in, which positions are measured against.</summary>
        private Control? pressedIn;

        private Point pressedAt;

        /// <summary>The files a drag from the current press would carry: the selection, or the one tile.</summary>
        private List<IStorageItem>? dragFiles;

        /// <summary>A tile pressed inside a selection of several, which becomes the only one if no drag follows.</summary>
        private (ListBox List, ShotItem Item)? collapseTo;

        public ScreenshotsView()
        {
            InitializeComponent();

            // The same handlers on all three layouts: only one is visible, and each is a list of the
            // same items.
            foreach (ListBox list in new[] { Tiles, Rows, Details })
            {
                list.AddHandler(PointerPressedEvent, TilePressed, RoutingStrategies.Tunnel, handledEventsToo: true);
                list.AddHandler(PointerMovedEvent, TileMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
                list.AddHandler(PointerReleasedEvent, (_, _) => Released(), RoutingStrategies.Tunnel, handledEventsToo: true);
                list.SelectionChanged += (_, _) => Picked(list);
                list.DoubleTapped += (_, e) =>
                {
                    // A double click on the star is two toggles, not a request to open the file.
                    if ((e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is null)
                    {
                        Open();
                    }
                };
            }

            Preview.DoubleTapped += (_, _) => Open();

            DataContextChanged += (_, _) =>
            {
                if (DataContext is ScreenshotsViewModel model)
                {
                    model.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(ScreenshotsViewModel.Layout))
                        {
                            // After the bindings have shown the new list, not before.
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => Relayout(model));
                        }
                    };

                    sizing = true;
                    Split.ColumnDefinitions[0].MinWidth = 260;
                    PreviewColumn.MinWidth = model.PreviewFloor;
                    PreviewColumn.Width = new GridLength(model.PreviewWidth, GridUnitType.Pixel);
                    sizing = false;
                }
            };

            // A drag changes the column, not the model, so the model is told as it goes - and
            // writes it down once the button comes up, rather than on every step.
            PreviewColumn.PropertyChanged += (_, e) =>
            {
                if (!sizing
                    && e.Property == ColumnDefinition.WidthProperty
                    && PreviewColumn.Width is { IsAbsolute: true } width
                    && DataContext is ScreenshotsViewModel model)
                {
                    model.PreviewWidth = width.Value;
                }
            };

            Divider.AddHandler(
                PointerReleasedEvent,
                (_, _) => (DataContext as ScreenshotsViewModel)?.KeepLayout(),
                RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        /// <summary>The preview's column, which the divider drags. Index matches the XAML.</summary>
        private ColumnDefinition PreviewColumn => Split.ColumnDefinitions[2];

        /// <summary>Set while the view is placing the saved width, so that is not taken for a drag.</summary>
        private bool sizing;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            (DataContext as ScreenshotsViewModel)?.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            (DataContext as ScreenshotsViewModel)?.Stop();
        }

        private void Open() => (DataContext as ScreenshotsViewModel)?.OpenCommand.Execute(null);

        /// <summary>Tells the model what is selected, from whichever layout is showing.</summary>
        private void Picked(ListBox list)
        {
            if (list.IsVisible && DataContext is ScreenshotsViewModel model)
            {
                model.Pick(list.SelectedItems?.OfType<ShotItem>() ?? []);
            }
        }

        /// <summary>
        ///  A new layout starts from the shot being previewed alone.
        /// </summary>
        /// <remarks>
        ///  Each layout is its own list with its own selection, and carrying a multiple selection
        ///  across would mean three lists kept in step on every click. Starting the new one from
        ///  the previewed shot keeps the one thing the user is looking at.
        /// </remarks>
        private void Relayout(ScreenshotsViewModel model)
        {
            ShotItem? keep = model.Selected;

            foreach (ListBox list in new[] { Tiles, Rows, Details })
            {
                list.SelectedItems?.Clear();
            }

            model.Selected = keep;

            foreach (ListBox list in new[] { Tiles, Rows, Details })
            {
                Picked(list);
            }
        }

        private async void TilePressed(object? sender, PointerPressedEventArgs e)
        {
            EndDrag();

            if (sender is not Control list
                || !e.GetCurrentPoint(list).Properties.IsLeftButtonPressed
                || (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext
                    is not ShotItem item
                || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            {
                return;
            }

            pressed = e;
            pressedIn = list;
            pressedAt = e.GetPosition(list);

            List<ShotItem> carried = [item];

            if (DataContext is ScreenshotsViewModel model
                && model.Picked.Count > 1
                && model.Picked.Contains(item)
                && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) == 0)
            {
                // Inside a selection of several: keep it for the drag, and let the release decide.
                carried = [.. model.Picked];
                collapseTo = ((ListBox)list, item);
                e.Handled = true;
            }

            try
            {
                List<IStorageItem> files = [];

                foreach (ShotItem shot in carried)
                {
                    if (await storage.TryGetFileFromPathAsync(shot.Path) is { } file)
                    {
                        files.Add(file);
                    }
                }

                // Released, or pressed somewhere else, while the lookup ran.
                if (pressed == e)
                {
                    dragFiles = files;
                }
            }
            catch (Exception)
            {
                // Not a file the system will hand over. The tile still selects.
            }
        }

        private async void TileMoved(object? sender, PointerEventArgs e)
        {
            if (pressed is not { } start || dragFiles is not { Count: > 0 } files || pressedIn is not { } list)
            {
                return;
            }

            Point now = e.GetPosition(list);

            if (Math.Abs(now.X - pressedAt.X) < DragThreshold && Math.Abs(now.Y - pressedAt.Y) < DragThreshold)
            {
                return;
            }

            EndDrag();

            DataTransfer data = new();

            foreach (IStorageItem file in files)
            {
                data.Add(DataTransferItem.CreateFile(file));
            }

            try
            {
                await DragDrop.DoDragDropAsync(start, data, DragDropEffects.Copy);
            }
            catch (Exception)
            {
                // A drop target that refused or threw. The file is where it was.
            }
        }

        /// <summary>The button came up without a drag: a press inside a selection now picks just that tile.</summary>
        private void Released()
        {
            if (collapseTo is { } only)
            {
                only.List.SelectedItems?.Clear();
                only.List.SelectedItem = only.Item;
            }

            EndDrag();
        }

        private void EndDrag()
        {
            pressed = null;
            pressedIn = null;
            dragFiles = null;
            collapseTo = null;
        }
    }
}
