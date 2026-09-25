using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Asks for one short piece of text.
    /// </summary>
    /// <remarks>
    ///  Small on purpose and deliberately not a view model: there is one field, it has no
    ///  validation beyond being non-empty, and wiring an observable object to a single string
    ///  would be more machinery than the thing it holds.
    ///
    ///  Returns null when cancelled, which is what tells a caller to leave the name alone. An empty
    ///  string would be a rename to nothing, and those are different answers.
    /// </remarks>
    public partial class NamePrompt : Window
    {
        public NamePrompt() => InitializeComponent();

        private void AcceptClick(object? sender, RoutedEventArgs e) => Accept();

        private void CancelClick(object? sender, RoutedEventArgs e) => Close(null);

        /// <summary>
        ///  Escape closes without renaming.
        /// </summary>
        /// <remarks>
        ///  Enter is handled by the accept button's <c>IsDefault</c>, but escape has no equivalent
        ///  on a window with no cancel affordance of its own, and a rename dialog that traps the
        ///  user is an unpleasant surprise.
        /// </remarks>
        private void EntryKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
            }
        }

        private void Accept()
        {
            string typed = Entry.Text?.Trim() ?? string.Empty;

            Close(typed.Length > 0 ? typed : null);
        }

        /// <summary>
        ///  Shows the prompt over <paramref name="owner"/> and waits for a name.
        /// </summary>
        /// <remarks>
        ///  The current name arrives selected rather than merely present, because a rename is far
        ///  more often a replacement than an edit - and when it is an edit, one arrow key undoes
        ///  the selection.
        /// </remarks>
        internal static async Task<string?> Ask(Window owner, string caption, string current)
        {
            NamePrompt prompt = new();

            prompt.Caption.Text = caption;
            prompt.Entry.Text = current;

            // After the window is up: focusing a control that is not yet attached does nothing,
            // and the user would be typing into a dialog with no cursor in it.
            prompt.Opened += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                prompt.Entry.Focus();
                prompt.Entry.SelectAll();
            });

            return await prompt.ShowDialog<string?>(owner);
        }
    }
}
