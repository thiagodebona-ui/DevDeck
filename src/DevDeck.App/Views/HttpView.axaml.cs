using System.ComponentModel;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using DevDeck.App.ViewModels;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>The HTTP panel. Like the toolbox, only here for the clipboard.</summary>
    public partial class HttpView : UserControl
    {
        /// <summary>Set while the view is writing the rows itself, to break the echo.</summary>
        private bool sizing;

        /// <summary>Set while the view is moving a selection between lists, for the same reason.</summary>
        private bool picking;

        /// <summary>The header pane's row. Indices match the RowDefinitions in the XAML.</summary>
        private RowDefinition HeaderRowSize => Panes.RowDefinitions[1];

        private RowDefinition BodyRowSize => Panes.RowDefinitions[3];

        /// <summary>The request tree's column, which the vertical splitter drags.</summary>
        private ColumnDefinition TreeColumnSize => Shell.ColumnDefinitions[0];

        public HttpView()
        {
            InitializeComponent();

            DataContextChanged += (_, _) =>
            {
                if (DataContext is HttpViewModel model)
                {
                    model.Copy = Copy;
                    model.Paste = Paste;
                    model.AskForName = AskForName;
                    model.AskForGroup = AskForGroup;
                    model.AskForIcon = AskForIcon;
                    model.AskWhereToSave = AskWhereToSave;

                    model.PropertyChanged += ModelChanged;

                    Place(model);
                    Sync(model);
                    Complete(UrlBox);
                    Complete(BodyBox);
                }
            };

            // A drag changes the row, not the model, so the model is told afterwards. Property
            // changed rather than a drag-completed event: a GridSplitter reports neither the start
            // nor the end of a gesture, only the sizes it leaves behind.
            // Ctrl+Enter sends from anywhere on the page - the URL, a header, the body. Tunnel
            // route, so it is seen before the body's TextBox turns the Enter into a line break
            // and before the {{ completion popup can claim it.
            AddHandler(KeyDownEvent, SendKeyDown, RoutingStrategies.Tunnel);

            HeaderRowSize.PropertyChanged += RowResized;
            BodyRowSize.PropertyChanged += RowResized;
            TreeColumnSize.PropertyChanged += ColumnResized;
        }

        /// <summary>
        ///  Gives a text box the <c>{{</c> completion, pointed at the active environment.
        /// </summary>
        /// <remarks>
        ///  Read through a function rather than handed a list, because the active environment is
        ///  swapped by the picker above the panel and a list captured now would go stale the first
        ///  time it changed. Reading it when the list opens also means an environment edited in the
        ///  editor window offers its new variables without the view knowing the editor exists.
        /// </remarks>
        private void Complete(TextBox box) =>
            VariableCompletion.Attach(box, () =>
                DataContext is HttpViewModel model && model.ActiveEnvironment is { } environment
                    ? environment.Values.Select(value => value.Name)
                    : []);

        /// <summary>
        ///  The same, for the header rows - which are template instances, so there is no name to
        ///  reach them by and no single moment they all exist at.
        /// </summary>
        private void CompleteHere(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBox box)
            {
                Complete(box);
            }
        }

        /// <summary>
        ///  Puts the saved sizes onto the parts of the layout the user owns.
        /// </summary>
        /// <remarks>
        ///  Done here rather than in the XAML because neither a RowDefinition nor a
        ///  ColumnDefinition is a control: they have no DataContext, so a binding on one resolves
        ///  against nothing and is dropped without a word - which presents as a splitter that
        ///  drags freely and forgets every time.
        /// </remarks>
        private void Place(HttpViewModel model)
        {
            sizing = true;

            HeaderRowSize.MinHeight = model.PaneFloor;
            BodyRowSize.MinHeight = model.PaneFloor;

            HeaderRowSize.Height = new GridLength(model.HeaderPaneHeight, GridUnitType.Pixel);
            BodyRowSize.Height = new GridLength(model.BodyPaneHeight, GridUnitType.Pixel);

            TreeColumnSize.MinWidth = model.TreeFloor;
            TreeColumnSize.Width = new GridLength(model.TreeWidth, GridUnitType.Pixel);

            sizing = false;
        }

        /// <summary>Records a drag, so the layout is where it was left next time.</summary>
        private void RowResized(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (sizing
                || e.Property != RowDefinition.HeightProperty
                || DataContext is not HttpViewModel model)
            {
                return;
            }

            if (HeaderRowSize.Height is { IsAbsolute: true } header)
            {
                model.HeaderPaneHeight = header.Value;
            }

            if (BodyRowSize.Height is { IsAbsolute: true } body)
            {
                model.BodyPaneHeight = body.Value;
            }
        }

        private void ColumnResized(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (sizing
                || e.Property != ColumnDefinition.WidthProperty
                || DataContext is not HttpViewModel model)
            {
                return;
            }

            if (TreeColumnSize.Width is { IsAbsolute: true } width)
            {
                model.TreeWidth = width.Value;
            }
        }

        /// <summary>
        ///  Every ListBox in the tree: the ungrouped one, and one per group.
        /// </summary>
        /// <remarks>
        ///  Walked rather than held, because the ones inside groups are created and destroyed by
        ///  the ItemsControl as groups come and go, and a list of them kept by hand would go stale
        ///  in exactly the cases that matter.
        /// </remarks>
        private IEnumerable<ListBox> Lists()
        {
            foreach (ListBox list in this.GetVisualDescendants().OfType<ListBox>())
            {
                if (list.Classes.Contains("saved"))
                {
                    yield return list;
                }
            }
        }

        /// <summary>
        ///  Moves the selection to the row that was clicked, wherever in the tree it is.
        /// </summary>
        /// <remarks>
        ///  The tree is several independent ListBoxes rather than one, so clearing the others is
        ///  the view's job - two rows looking selected at once, only one of which the editor is
        ///  showing, is worse than no highlight at all.
        /// </remarks>
        private void RequestPicked(object? sender, SelectionChangedEventArgs e)
        {
            if (picking
                || DataContext is not HttpViewModel model
                || sender is not ListBox chosen
                || chosen.SelectedItem is not HttpRequest request)
            {
                return;
            }

            picking = true;

            foreach (ListBox other in Lists().Where(list => !ReferenceEquals(list, chosen)))
            {
                other.SelectedItem = null;
            }

            model.Selected = request;

            picking = false;
        }

        private void ModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (DataContext is HttpViewModel model
                && e.PropertyName == nameof(HttpViewModel.Selected))
            {
                Sync(model);
            }
        }

        /// <summary>Highlights whichever row holds the selected request, and no other.</summary>
        /// <remarks>
        ///  Needed because the selection can change from the model's side - a new request, a
        ///  delete, a duplicate - and the lists have nothing bound to Selected to follow.
        /// </remarks>
        private void Sync(HttpViewModel model)
        {
            if (picking)
            {
                return;
            }

            picking = true;

            foreach (ListBox list in Lists())
            {
                list.SelectedItem = list.Items.Contains(model.Selected) ? model.Selected : null;
            }

            picking = false;
        }

        /// <summary>
        ///  Points the row's menu at the view model, and selects the row it belongs to.
        /// </summary>
        /// <remarks>
        ///  Both halves are needed, and the first is what made the menu open dead.
        ///
        ///  A ContextMenu is shown in a popup root of its own rather than inside the control it
        ///  hangs off, so it inherits no DataContext from the row, and an ancestor binding written
        ///  inside it walks up into nothing. Every Command in it therefore bound to null, and a
        ///  MenuItem whose Command is null draws itself disabled: the whole menu opened and not one
        ///  line of it did anything. The ⋯ button was never affected, because a MenuFlyout on a
        ///  button in the panel binds straight to the panel's own view model - which is exactly why
        ///  one worked and the other did not.
        ///
        ///  Hung on the menu's own Opening rather than on the routed request event, because this
        ///  is the one moment both late enough to know which row the menu belongs to and early
        ///  enough to be before it is drawn. That routed event would also do, but only
        ///  bubbling: it is declared to bubble only, so a tunnelling handler is never called.
        ///
        ///  The row is selected here too: Avalonia does not select on a right click, and the menu's
        ///  items all act on "the selected request", so without this the menu would open on one row
        ///  and act on whichever other row happened to be selected - which is how a request gets
        ///  deleted that nobody pointed at.
        /// </remarks>
        private void RowMenuOpening(object? sender, CancelEventArgs e)
        {
            if (DataContext is not HttpViewModel model || sender is not ContextMenu menu)
            {
                return;
            }

            menu.DataContext = model;

            // Which row this menu hangs off. PlacementTarget is the control it was opened from,
            // and the request is that control's DataContext - or an ancestor's, since the pointer
            // is as often over a TextBlock inside the row as over the row itself.
            if (menu.PlacementTarget is not Control target)
            {
                return;
            }

            HttpRequest? request = target.GetSelfAndVisualAncestors()
                .OfType<Control>()
                .Select(control => control.DataContext)
                .OfType<HttpRequest>()
                .FirstOrDefault();

            if (request is not null)
            {
                model.Selected = request;
            }
        }

        /// <summary>Ctrl+Enter: send the request, as if the Send button had been pressed.</summary>
        private void SendKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter
                || e.KeyModifiers != KeyModifiers.Control
                || DataContext is not HttpViewModel model)
            {
                return;
            }

            // Handled even when a send is already running, so the chord never falls through to
            // the TextBox as a stray newline.
            e.Handled = true;

            if (model.SendCommand.CanExecute(null))
            {
                model.SendCommand.Execute(null);
            }
        }

        /// <summary>Opens or shuts a group, and remembers which.</summary>
        private void GroupClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not HttpViewModel model
                || sender is not Button button
                || button.Tag is not RequestGroup group)
            {
                return;
            }

            group.Collapsed = !group.Collapsed;

            model.SetCollapsed(group.Name, group.Collapsed);

            // Expanding a group builds its list fresh, with nothing selected in it.
            Sync(model);
        }

        /// <summary>
        ///  Hands the header row's menu the view model and the header it was opened on.
        /// </summary>
        /// <remarks>
        ///  The header goes on the menu's Tag rather than its DataContext, because the menu's
        ///  DataContext has to be the view model for the command to resolve; the item is passed
        ///  through as the command's parameter instead.
        /// </remarks>
        private void HeaderMenuOpening(object? sender, CancelEventArgs e)
        {
            if (DataContext is not HttpViewModel model || sender is not ContextMenu menu)
            {
                return;
            }

            menu.DataContext = model;

            menu.Tag = menu.PlacementTarget is Control target ? target.DataContext : null;
        }

        /// <summary>
        ///  Makes sure the group header's menu knows the group it was opened on.
        /// </summary>
        /// <remarks>
        ///  The XAML binds the group onto the menu's Tag, which is the reliable route. This is the
        ///  fallback for when that binding has not resolved yet, and it must not depend on
        ///  PlacementTarget alone: the menu's own PlacementTarget is not set when it is opened
        ///  from its owner's ContextMenu property, which is what used to leave Tag empty and make
        ///  "Group icon..." do nothing at all.
        /// </remarks>
        private void GroupMenuOpening(object? sender, CancelEventArgs e)
        {
            if (sender is not ContextMenu menu || menu.Tag is RequestGroup)
            {
                return;
            }

            menu.Tag = menu.DataContext as RequestGroup
                ?? (menu.PlacementTarget ?? menu.Parent as Control)?
                    .GetSelfAndVisualAncestors()
                    .OfType<Button>()
                    .Select(button => button.Tag)
                    .OfType<RequestGroup>()
                    .FirstOrDefault();
        }

        private async void GroupIconClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not HttpViewModel model || sender is not MenuItem item)
            {
                return;
            }

            // The menu's Tag first, then whatever the item itself inherited: the menu hangs off
            // the group's header button, so its DataContext is the group too.
            RequestGroup? group = (item.Parent as ContextMenu)?.Tag as RequestGroup
                ?? item.DataContext as RequestGroup;

            if (group is null)
            {
                return;
            }

            await model.ChooseGroupIcon(group);

            // Regrouping rebuilds every group object, so the list in front of the user is now a
            // different set of items to the one the selection was made on.
            Sync(model);
        }

        private async void EditEnvironments(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not HttpViewModel model || TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            model.TakeEnvironments(await EnvironmentEditor.Edit(owner, [.. model.Environments]));
        }

        private async Task Copy(string text)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
        }

        private async Task<string?> Paste() =>
            TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard
                ? await clipboard.TryGetTextAsync()
                : null;

        /// <summary>Asks for a new name for the selected request.</summary>
        /// <remarks>
        ///  Returns null when there is no window to be modal to, which is the state a panel is in
        ///  while it is being torn down - and asking then would throw rather than ask.
        /// </remarks>
        private async Task<string?> AskForName(string current) =>
            TopLevel.GetTopLevel(this) is Window owner
                ? await NamePrompt.Ask(owner, Strings.Text("HttpAskName"), current)
                : null;

        /// <summary>Asks which group to file the request under.</summary>
        private async Task<string?> AskForGroup(string current) =>
            TopLevel.GetTopLevel(this) is Window owner
                ? await NamePrompt.Ask(owner, Strings.Text("HttpAskGroup"), current)
                : null;

        /// <summary>Asks which icon to put on a request or a group.</summary>
        /// <remarks>
        ///  Null comes back both when the user cancelled and when there is no window to be modal
        ///  to. They mean the same thing to the caller - leave it as it was - so they do not need
        ///  telling apart.
        /// </remarks>
        private async Task<string?> AskForIcon(string heading, string? current) =>
            TopLevel.GetTopLevel(this) is Window owner
                ? await IconPicker.Ask(owner, heading, current)
                : null;

        /// <summary>
        ///  Asks where to write the response, with a name already filled in.
        /// </summary>
        /// <remarks>
        ///  No file-type filter, for the same reason the attachment picker has none: the extension
        ///  is already correct because it was worked out from the content type, and a filter would
        ///  only stop the user renaming the file to what they actually want it called.
        ///
        ///  The path is asked for rather than the stream written through, because the view model is
        ///  what knows whether it is writing bytes or text and the storage API's stream would make
        ///  that this method's problem instead. The one thing given up by taking a path is saving
        ///  to a location that has no path at all - a sandboxed picker on a phone - which this
        ///  desktop app does not have to care about.
        /// </remarks>
        private async Task<string?> AskWhereToSave(string suggested)
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            {
                return null;
            }

            IStorageFile? chosen = await storage.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = Strings.Text("HttpSaveResponse"),
                    SuggestedFileName = suggested,

                    // Suggested rather than enforced: without it the picker offers no extension at
                    // all on the platforms that do not read one out of the suggested name, and a
                    // file saved with none is one the desktop opens with the wrong application.
                    DefaultExtension = System.IO.Path.GetExtension(suggested).TrimStart('.'),
                });

            return chosen?.TryGetLocalPath();
        }

        /// <summary>
        ///  Ctrl+V in the address box: a curl command becomes the whole request, anything else is
        ///  pasted as text.
        /// </summary>
        /// <remarks>
        ///  Intercepted here rather than left to the view model's watch on the URL, because this is
        ///  the last point at which the pasted text still exists as it was copied. A curl out of a
        ///  browser's devtools is a dozen lines joined by backslashes, and this is a single-line
        ///  TextBox: it drops the newlines on the way in, which welds the end of one header onto
        ///  the start of the next flag and leaves the parser something it cannot read. Reading the
        ///  clipboard ourselves gets the text before that happens.
        ///
        ///  The paste is always cancelled and then redone by hand, which looks heavy-handed but is
        ///  the only correct order. Whether the text is a curl cannot be known without reading the
        ///  clipboard, reading the clipboard is asynchronous, and the event is not: letting the
        ///  default run while the answer is still being fetched means an ordinary paste lands
        ///  twice, and a curl leaves twenty lines of shell sitting in the box behind the request it
        ///  just imported. So nothing is pasted until it is known which of the two this is.
        ///
        ///  Doing it by hand also keeps undo honest, because replacing the selection is what the
        ///  default would have done.
        /// </remarks>
        private async void UrlPasting(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not HttpViewModel model || sender is not TextBox box)
            {
                return;
            }

            e.Handled = true;

            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                return;
            }

            string? text = await clipboard.TryGetTextAsync();

            if (text is null or { Length: 0 })
            {
                return;
            }

            if (model.TakeCurl(text))
            {
                return;
            }

            // Not a curl, so this is an ordinary paste that the default handler would have made.
            // Newlines are stripped rather than kept: the box is single-line, and a pasted URL that
            // came out of a wrapped email arrives with them in it.
            string flat = text.ReplaceLineEndings(string.Empty);

            string current = box.Text ?? string.Empty;
            int from = Math.Min(box.SelectionStart, box.SelectionEnd);
            int to = Math.Max(box.SelectionStart, box.SelectionEnd);

            // Clamped, because a selection can outlive the text it was made in - a binding that
            // wrote a shorter URL between the keypress and the clipboard coming back leaves the
            // offsets pointing past the end.
            from = Math.Clamp(from, 0, current.Length);
            to = Math.Clamp(to, from, current.Length);

            box.Text = string.Concat(current.AsSpan(0, from), flat, current.AsSpan(to));
            box.CaretIndex = from + flat.Length;
        }

        /// <summary>
        ///  Double-click on a saved request renames it.
        /// </summary>
        /// <remarks>
        ///  Guarded on the row rather than the list, because a double-click anywhere in a ListBox
        ///  raises this - including on the empty space below the last item, where renaming whatever
        ///  happened to be selected would be a rename nobody asked for.
        /// </remarks>
        private void RequestDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is not HttpViewModel model)
            {
                return;
            }

            if ((e.Source as Control)?.DataContext is not HttpRequest request)
            {
                return;
            }

            model.RenameAsCommand.Execute(request);
        }
    }
}
