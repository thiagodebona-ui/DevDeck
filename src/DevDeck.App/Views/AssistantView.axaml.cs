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
    public partial class AssistantView : UserControl
    {
        /// <summary>A few pixels of slack: a wheel notch rarely lands on the last pixel exactly.</summary>
        private const double TailSlack = 24;

        /// <summary>
        ///  Whether the transcript is pinned to the newest turn.
        /// </summary>
        /// <remarks>
        ///  Starts true and is only given up when the user scrolls up themselves. The first cut of
        ///  this asked "are we at the bottom?" at the moment new text arrived, which is before the
        ///  new text has been measured - so the answer came from the old extent, went false the
        ///  moment the conversation outgrew the viewport, and never came back. The transcript then
        ///  sat still for the rest of the session.
        /// </remarks>
        private bool followTail = true;

        /// <summary>The offset last seen, to tell a user scrolling up from content growing.</summary>
        private double lastOffset;

        public AssistantView()
        {
            InitializeComponent();

            Transcript.ScrollChanged += TranscriptScrolled;

            // Tunnel, not bubble. A handler attached with KeyDown="..." in the XAML is a bubbling
            // one, and TextBox marks Enter handled in its own class handler when AcceptsReturn is
            // set - so the bubbling handler was never reached and Enter only ever inserted a line
            // break. Tunnelling puts this in front of the TextBox, which is the only place it can
            // decide whether the key is a send or a newline.
            Composer.AddHandler(KeyDownEvent, ComposerKeyDown, RoutingStrategies.Tunnel);

            // Scrolling, focus and the clipboard are the view's business; the view model only says
            // when, and hands over the text.
            DataContextChanged += (_, _) =>
            {
                if (DataContext is AssistantViewModel model)
                {
                    model.ScrollToEnd = ScrollToEnd;
                    model.Copy = Copy;
                    model.PickFiles = PickFiles;
                    model.FocusComposer = () => Composer.Focus();
                }
            };

            // Dropping a file on the transcript attaches it. This is how people expect to hand a
            // file to a chat window, and it saves the walk through a file dialog to a path they
            // already have open in another window.
            AddHandler(DragDrop.DragOverEvent, DragOver);
            AddHandler(DragDrop.DropEvent, Dropped);
        }

        /// <summary>
        ///  Asks for files to attach.
        /// </summary>
        /// <remarks>
        ///  No file-type filter. The check that matters is whether the bytes are text, and that is
        ///  made when the file is read - a filter here would only refuse the extensions nobody
        ///  thought of, which on a developer's machine is most of them.
        /// </remarks>
        private async Task<IReadOnlyList<string>> PickFiles()
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            {
                return [];
            }

            IReadOnlyList<IStorageFile> chosen = await storage.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = Strings.Text("PickAttachFiles"),
                    AllowMultiple = true,
                });

            return [.. chosen
                .Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => path!)];
        }

        /// <summary>
        ///  Closes the help panel once an example is picked.
        /// </summary>
        /// <remarks>
        ///  The command has already put the example in the box. Left open, the panel would sit on
        ///  top of the very box the text just went into.
        /// </remarks>
        private void ExampleClick(object? sender, RoutedEventArgs e) => HelpButton.Flyout?.Hide();

        private void DragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void Dropped(object? sender, DragEventArgs e)
        {
            if (DataContext is not AssistantViewModel model)
            {
                return;
            }

            foreach (IStorageItem item in e.DataTransfer.TryGetValues(DataFormat.File) ?? [])
            {
                // Folders are skipped rather than walked: attaching a directory is attaching an
                // unknown number of files, which is how a context window gets filled by accident.
                if (item is IStorageFile file && file.TryGetLocalPath() is { Length: > 0 } path)
                {
                    model.Add(path);
                }
            }
        }

        /// <summary>
        ///  Enter sends; Shift+Enter starts a new line.
        /// </summary>
        /// <remarks>
        ///  V2 did this in the composer's KeyDown for the reason it still holds: every chat box the
        ///  user already has open behaves this way, and a Send button you have to reach for is one
        ///  the hands never learn.
        ///
        ///  Attached on the tunnel route in the constructor. Marking the event handled here is what
        ///  stops the TextBox inserting a line break as well as sending; Shift+Enter falls straight
        ///  through to it untouched, which is exactly what should happen.
        /// </remarks>
        private void ComposerKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                return;
            }

            if (DataContext is not AssistantViewModel model)
            {
                return;
            }

            // Handled either way: Enter is a send, and a send that cannot run right now - no
            // endpoint, or an answer still arriving - must not quietly become a newline instead.
            e.Handled = true;

            if (model.SendCommand.CanExecute(null))
            {
                model.SendCommand.Execute(null);
            }
        }

        /// <summary>
        ///  Notices the user scrolling away from the bottom, and back to it.
        /// </summary>
        /// <remarks>
        ///  A scroll that moves the offset up while the content is the same size was the user's
        ///  doing, and is the one thing that stops the transcript following. Content growing raises
        ///  the extent instead, which is not a reason to stop.
        /// </remarks>
        private void TranscriptScrolled(object? sender, ScrollChangedEventArgs e)
        {
            double offset = Transcript.Offset.Y;

            if (offset < lastOffset - 1 && e.ExtentDelta.Y == 0)
            {
                followTail = false;
            }

            // Back at the bottom by any means - dragged there, wheeled there, or put there by us.
            if (offset >= Transcript.Extent.Height - Transcript.Viewport.Height - TailSlack)
            {
                followTail = true;
            }

            lastOffset = offset;
        }

        /// <summary>
        ///  Scrolls the transcript to the newest turn, unless the user has scrolled up.
        /// </summary>
        /// <remarks>
        ///  V2 called this following the tail, and it is what makes a streaming reply readable:
        ///  scroll up to re-read something while an answer is still arriving and an unconditional
        ///  scroll drags you back down on every token. Scrolling back to the bottom starts it
        ///  following again.
        ///
        ///  Posted rather than called straight: the turn that triggered this has not been measured
        ///  when the collection changes, so scrolling now would stop one turn short. At Background
        ///  priority it runs after layout, which is when "the end" means what the user thinks.
        /// </remarks>
        private void ScrollToEnd()
        {
            if (!followTail)
            {
                return;
            }

            Dispatcher.UIThread.Post(() => Transcript.ScrollToEnd(), DispatcherPriority.Background);
        }

        /// <summary>
        ///  Puts text on the clipboard.
        /// </summary>
        /// <remarks>
        ///  Here rather than in the view model because the clipboard hangs off the top level, which
        ///  a view model has no business knowing about.
        /// </remarks>
        private async void Copy(string text)
        {
            if (string.IsNullOrEmpty(text) || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                return;
            }

            try
            {
                await clipboard.SetTextAsync(text);
            }
            catch (Exception)
            {
                // The clipboard is shared and another process may hold it - not worth interrupting.
            }
        }
    }
}
