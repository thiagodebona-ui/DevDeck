using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using DevDeck.App.ViewModels;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Asks for a command's placeholder values, and returns them or nothing.
    /// </summary>
    /// <remarks>
    ///  Closed with the dictionary on Run and with null on Cancel, so the caller can tell "the
    ///  user did not want to run this" from "the user ran it with everything left at its default" -
    ///  two outcomes that would otherwise look identical.
    /// </remarks>
    public partial class ParameterPrompt : Window
    {
        public ParameterPrompt()
        {
            InitializeComponent();

            // Escape closes as a cancel, which is what every dialog on every desktop does and what
            // a user who has thought better of the run will reach for first.
            KeyDown += (_, key) =>
            {
                if (key.Key == Key.Escape)
                {
                    Close(null);
                }
            };

            // The first box takes focus so the common case - one parameter, type it, press Enter -
            // needs no mouse at all.
            Opened += (_, _) => this.FindDescendantOfType<TextBox>()?.Focus();
        }

        private void RunClick(object? sender, RoutedEventArgs e)
        {
            Close((DataContext as ParameterPromptViewModel)?.Values);
        }

        private void CancelClick(object? sender, RoutedEventArgs e) => Close(null);

        /// <summary>
        ///  Shows the prompt over <paramref name="owner"/> and waits for an answer.
        /// </summary>
        /// <remarks>
        ///  A command whose every placeholder is a secret has nothing to ask - the vault answers
        ///  all of them - so it runs without a dialog rather than showing an empty one.
        /// </remarks>
        internal static async Task<IReadOnlyDictionary<string, string>?> Ask(
            Window owner,
            string command,
            IReadOnlyList<CommandParameter> parameters)
        {
            ParameterPromptViewModel model = new(command, parameters);

            if (model.Entries.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            ParameterPrompt prompt = new() { DataContext = model };

            return await prompt.ShowDialog<IReadOnlyDictionary<string, string>?>(owner);
        }
    }
}
