using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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

            // Dragging a row and Alt+Up / Alt+Down are shared with the rail. Delete is only here:
            // tunnelling, so it is seen before the list, which would otherwise take the key.
            ListReorder.Attach(CommandList, (item, to) => model?.MoveTo((CommandItem)item, to));
            CommandList.AddHandler(KeyDownEvent, ListKeyDown, RoutingStrategies.Tunnel);

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

        /// <summary>Delete deletes the selected command. Up and Down alone are the list's own.</summary>
        private void ListKeyDown(object? sender, KeyEventArgs e)
        {
            if (model is null || e.Key != Key.Delete || e.KeyModifiers != KeyModifiers.None)
            {
                return;
            }

            // Refused quietly for a command in use, as the button is - except that nothing in use
            // can be selected, so in practice this only ever deletes.
            if (model.DeleteCommand.CanExecute(null))
            {
                model.DeleteCommand.Execute(null);
                ListReorder.Refocus(CommandList);
            }

            e.Handled = true;
        }

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
