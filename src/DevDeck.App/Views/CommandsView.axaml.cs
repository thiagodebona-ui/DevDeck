using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using DevDeck.App.ViewModels;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    public partial class CommandsView : UserControl
    {
        /// <summary>The output collection currently being followed, so it can be let go of.</summary>
        private INotifyCollectionChanged? watched;

        /// <summary>
        ///  Set while a scroll-to-tail is already queued, so a burst of lines queues one.
        /// </summary>
        /// <remarks>
        ///  This flag is the difference between a chatty command being readable and the window
        ///  locking up. A batch of output arrives as one Add per line, so a command printing a few
        ///  thousand matches raised a few thousand CollectionChanged events - and every one of them
        ///  posted its own ScrollIntoView. Each of those walks the list to find an item whose
        ///  height is not known until it is measured, because the rows wrap, so the cost is not
        ///  merely repeated but repeated over a growing list. The window stopped answering clicks
        ///  until the command finished.
        ///
        ///  Only the last scroll in a burst was ever wanted: they all scroll to the same place, the
        ///  end. So the first one queues the job and the rest ride along with it.
        /// </remarks>
        private bool scrollQueued;

        private CommandsViewModel? model;

        public CommandsView()
        {
            InitializeComponent();

            TextMode.Wire(SelectOutput, Output, () => string.Join(
                Environment.NewLine, Output.Items.OfType<OutputLine>().Select(line => line.Text)));

            // Tunnelling, so these see the keys and the pointer before the list does: the list
            // treats Alt+Up as a plain Up and moves the selection, and marks a press on a row
            // handled as it selects it.
            CommandList.AddHandler(KeyDownEvent, ListKeyDown, RoutingStrategies.Tunnel);
            CommandList.AddHandler(PointerPressedEvent, DragStart, RoutingStrategies.Tunnel, handledEventsToo: true);
            CommandList.AddHandler(PointerMovedEvent, DragMove, RoutingStrategies.Tunnel, handledEventsToo: true);
            CommandList.AddHandler(PointerReleasedEvent, DragEnd, RoutingStrategies.Tunnel, handledEventsToo: true);
            CommandList.AddHandler(PointerCaptureLostEvent, (_, _) => dragged = null);

            DataContextChanged += (_, _) =>
            {
                if (model is not null)
                {
                    model.PropertyChanged -= SelectionChanged;
                }

                model = DataContext as CommandsViewModel;

                if (model is not null)
                {
                    model.PropertyChanged += SelectionChanged;
                    model.Prompt = Ask;
                    model.Copy = Copy;
                }

                Follow();
            };
        }

        #region Keyboard and dragging in the command list
        /// <summary>The row being dragged, once a press on it has moved far enough to be a drag.</summary>
        private CommandItem? dragged;

        private Point dragFrom;

        private bool dragging;

        /// <summary>How far a press has to travel before it is a drag rather than a click.</summary>
        private const double DragThreshold = 6;

        /// <summary>
        ///  Delete deletes, Alt+Up and Alt+Down move. Up and Down alone are the list's own.
        /// </summary>
        private void ListKeyDown(object? sender, KeyEventArgs e)
        {
            if (model is null)
            {
                return;
            }

            if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None)
            {
                // Refused quietly for a command in use, as the button is - except that nothing in
                // use can be selected, so in practice this only ever deletes.
                if (model.DeleteCommand.CanExecute(null))
                {
                    model.DeleteCommand.Execute(null);
                    Refocus();
                }

                e.Handled = true;

                return;
            }

            if (e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.Up or Key.Down)
            {
                IRelayCommand move = e.Key == Key.Up ? model.MoveUpCommand : model.MoveDownCommand;

                if (move.CanExecute(null))
                {
                    move.Execute(null);
                    Refocus();
                }

                e.Handled = true;
            }
        }

        /// <summary>
        ///  Puts keyboard focus back on the selected row.
        /// </summary>
        /// <remarks>
        ///  A row that is moved or deleted takes the focus with it, and the arrows then go nowhere
        ///  until the list is clicked again. Posted, because the row's new container is only made
        ///  on the next layout pass.
        /// </remarks>
        private void Refocus() => Dispatcher.UIThread.Post(() =>
        {
            if (model?.Selected is { } selected && CommandList.ContainerFromItem(selected) is { } row)
            {
                row.Focus(NavigationMethod.Directional);
            }
        }, DispatcherPriority.Background);

        private void DragStart(object? sender, PointerPressedEventArgs e)
        {
            dragging = false;
            dragged = null;

            if (!e.GetCurrentPoint(CommandList).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Disabled rows are not hit at all, so a command in use can never be picked up here.
            dragged = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as CommandItem;
            dragFrom = e.GetPosition(CommandList);
        }

        /// <summary>
        ///  Moves the dragged row to wherever the pointer is, as it goes.
        /// </summary>
        /// <remarks>
        ///  Live rather than on release, so the list itself is the drop indicator: the row is
        ///  already where it will land. That avoids drawing an insertion line, which in a
        ///  virtualising list means tracking rows that may not exist yet.
        /// </remarks>
        private void DragMove(object? sender, PointerEventArgs e)
        {
            if (dragged is null || model is null || !e.GetCurrentPoint(CommandList).Properties.IsLeftButtonPressed)
            {
                return;
            }

            Point at = e.GetPosition(CommandList);

            if (!dragging)
            {
                if (Math.Abs(at.Y - dragFrom.Y) < DragThreshold && Math.Abs(at.X - dragFrom.X) < DragThreshold)
                {
                    return;
                }

                dragging = true;
                e.Pointer.Capture(CommandList);
            }

            int to = RowAt(at);

            if (to >= 0)
            {
                model.MoveTo(dragged, to);
            }

            e.Handled = true;
        }

        private void DragEnd(object? sender, PointerReleasedEventArgs e)
        {
            if (dragging)
            {
                e.Pointer.Capture(null);
                e.Handled = true;
                Refocus();
            }

            dragging = false;
            dragged = null;
        }

        /// <summary>The index of the row under a point in the list, or -1 between rows or past the end.</summary>
        private int RowAt(Point at)
        {
            for (int index = 0; index < CommandList.ItemCount; index++)
            {
                if (CommandList.ContainerFromIndex(index) is not { } row
                    || row.TranslatePoint(default, CommandList) is not { } top)
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
        #endregion

        private async Task Copy(string text)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
        }

        /// <summary>
        ///  Picks the folder commands run in.
        /// </summary>
        /// <remarks>
        ///  In the view rather than the view model because the picker needs the window that owns
        ///  this control, and Avalonia's StorageProvider is reached through it.
        /// </remarks>
        private async void BrowseClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not CommandsViewModel picked || TopLevel.GetTopLevel(this) is not { } top)
            {
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await top.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = Strings.Text("PickWorkspace"),
                    AllowMultiple = false,
                });

            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            {
                picked.UseWorkspace(path);
            }
        }

        private void SelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CommandsViewModel.Selected))
            {
                Follow();
            }
        }

        /// <summary>
        ///  Watches the selected command's output so the view can stay at the tail.
        /// </summary>
        /// <remarks>
        ///  Re-hooked on every selection change, and the previous one released: a command left
        ///  running in the background goes on producing output, and following a list that is not
        ///  on screen scrolls a control the user is not looking at.
        /// </remarks>
        private void Follow()
        {
            if (watched is not null)
            {
                watched.CollectionChanged -= OutputChanged;
                watched = null;
            }

            if (model?.Selected?.Output is INotifyCollectionChanged changed)
            {
                watched = changed;
                watched.CollectionChanged += OutputChanged;
            }
        }

        /// <summary>
        ///  Shows the parameter prompt for a command about to run.
        /// </summary>
        /// <remarks>
        ///  In the view because a dialog needs the window that owns it. The view model holds this
        ///  as a delegate, so a command item can be run in a test - or before any window exists -
        ///  without a dialog to answer.
        /// </remarks>
        private async Task<IReadOnlyDictionary<string, string>?> Ask(
            CommandItem item,
            IReadOnlyList<CommandParameter> parameters)
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                // No window to parent a dialog to. The defaults are a better answer than refusing.
                return parameters.ToDictionary(p => p.Name, p => p.Default, StringComparer.OrdinalIgnoreCase);
            }

            return await ParameterPrompt.Ask(owner, item.Name, parameters);
        }

        /// <summary>
        ///  Opens the file an output line names, at the line it names.
        /// </summary>
        /// <remarks>
        ///  The single most repeated action after a failed build is reading the error, finding the
        ///  path in it and going there by hand. Every compiler already prints the answer.
        /// </remarks>
        private void LinkClick(object? sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is OutputLine { Link: { } link })
            {
                SourceLinks.Open(link);
            }
        }

        private void OutputChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (model is not { Follow: true } || e.Action != NotifyCollectionChangedAction.Add)
            {
                return;
            }

            if (scrollQueued)
            {
                return;
            }

            scrollQueued = true;

            // Posted at Background priority so it runs after the new rows have been measured;
            // scrolling before that lands one batch short of the end. Reading the tail inside the
            // job rather than capturing it here is what makes the coalescing correct: whatever
            // arrived while this was queued is already in the list by the time it runs.
            Dispatcher.UIThread.Post(
                () =>
                {
                    scrollQueued = false;

                    if (model?.Selected?.Output is { Count: > 0 } lines)
                    {
                        Output.ScrollIntoView(lines[^1]);
                    }
                },
                DispatcherPriority.Background);
        }
    }
}
