using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Asks once, before something that cannot be undone.
    /// </summary>
    /// <remarks>
    ///  Deliberately not a general message box. It answers true or false, it has no third button,
    ///  and the affirmative one is phrased by the caller - "Reset everything" rather than "OK" -
    ///  because a dialog whose buttons say OK and Cancel forces the user to reconstruct the
    ///  question from the body text before either one means anything.
    ///
    ///  Cancel is the default button and the escape route both, so that Enter, Escape and the close
    ///  box all land on the safe answer. Every way of dismissing a window without reading it should
    ///  mean no.
    /// </remarks>
    public partial class ConfirmPrompt : Window
    {
        public ConfirmPrompt() => InitializeComponent();

        private void AcceptClick(object? sender, RoutedEventArgs e) => Close(true);

        private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close(false);
            }

            base.OnKeyDown(e);
        }

        /// <summary>
        ///  Shows the question over <paramref name="owner"/> and waits for an answer.
        /// </summary>
        /// <remarks>
        ///  <paramref name="heading"/> is the question, <paramref name="detail"/> is what will
        ///  actually happen, and <paramref name="proceed"/> goes on the button. Splitting the first
        ///  two matters: the consequence is the part people skip, and it is the part that stops
        ///  them.
        /// </remarks>
        internal static async Task<bool> Ask(
            Window owner, string heading, string detail, string proceed)
        {
            ConfirmPrompt prompt = new();

            prompt.Heading.Text = heading;
            prompt.Detail.Text = detail;
            prompt.Go.Content = proceed;

            return await prompt.ShowDialog<bool>(owner);
        }
    }
}
