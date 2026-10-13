using Avalonia.Controls;
using Avalonia.Input.Platform;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  The toolbox, which is mostly bindings.
    /// </summary>
    /// <remarks>
    ///  The only thing the code-behind is here for is the clipboard: it hangs off the top level,
    ///  which a view model has no business knowing about, so the view hands the two operations down
    ///  as functions.
    /// </remarks>
    public partial class ToolboxView : UserControl
    {
        public ToolboxView()
        {
            InitializeComponent();

            DataContextChanged += (_, _) =>
            {
                if (DataContext is ToolboxViewModel model)
                {
                    model.Copy = Copy;
                    model.Paste = Paste;

                    model.PropertyChanged += (_, changed) =>
                    {
                        if (changed.PropertyName == nameof(ToolboxViewModel.Selected))
                        {
                            Fit(model);
                        }
                    };

                    Fit(model);
                }
            };
        }

        /// <summary>
        ///  A generator has no input, so its output panel takes the input's place as well rather
        ///  than sitting under an empty gap.
        /// </summary>
        private void Fit(ToolboxViewModel model)
        {
            bool alone = model.Selected.Generates;

            Grid.SetRow(OutputPane, alone ? 0 : 2);
            Grid.SetRowSpan(OutputPane, alone ? 3 : 1);
        }

        /// <summary>Enter in the search box opens the first tool it found; Escape clears it.</summary>
        private void SearchKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (DataContext is not ToolboxViewModel model)
            {
                return;
            }

            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                model.PickFirst();
            }
            else if (e.Key == Avalonia.Input.Key.Escape)
            {
                e.Handled = true;
                model.Search = string.Empty;
            }
        }

        private async Task Copy(string text)
        {
            if (Clipboard() is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
        }

        private async Task<string?> Paste() =>
            Clipboard() is { } clipboard ? await clipboard.TryGetTextAsync() : null;

        /// <summary>
        ///  The clipboard for the window this control is in.
        /// </summary>
        /// <remarks>
        ///  Reached through the top level rather than through the application, because a control
        ///  that is not attached to a window has no clipboard - and during a template swap, this is
        ///  one of those.
        /// </remarks>
        private IClipboard? Clipboard() => TopLevel.GetTopLevel(this)?.Clipboard;
    }
}
