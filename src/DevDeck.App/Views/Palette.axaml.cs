using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  One key, three letters, Enter.
    /// </summary>
    /// <remarks>
    ///  The keyboard handling is on the window rather than on the search box because the box must
    ///  keep focus the entire time: the arrows move the list while what the user is typing goes on
    ///  filtering it, and a palette that makes you tab between the two is one nobody uses twice.
    ///
    ///  It closes as soon as an action is picked, and the action runs afterwards. A dialog still on
    ///  screen while the thing it launched is starting looks like it has not registered the press.
    /// </remarks>
    public partial class Palette : Window
    {
        public Palette()
        {
            InitializeComponent();

            Opened += (_, _) => Search.Focus();

            // A palette that stays open when the user clicks past it has become a window they now
            // have to close, which is not what a transient launcher should ever turn into.
            Deactivated += (_, _) => Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            PaletteViewModel? model = DataContext as PaletteViewModel;

            switch (e.Key)
            {
                case Key.Escape:
                    Close();
                    e.Handled = true;

                    return;

                case Key.Down:
                    model?.Move(1);
                    Reveal();
                    e.Handled = true;

                    return;

                case Key.Up:
                    model?.Move(-1);
                    Reveal();
                    e.Handled = true;

                    return;

                case Key.Enter:
                    Accept();
                    e.Handled = true;

                    return;
            }

            base.OnKeyDown(e);
        }

        private void HitTapped(object? sender, RoutedEventArgs e) => Accept();

        /// <summary>Closes first, then runs - see the note on the class.</summary>
        private void Accept()
        {
            if ((DataContext as PaletteViewModel)?.Selected is not { } action)
            {
                return;
            }

            Close();
            action.Run();
        }

        /// <summary>Keeps the highlight on screen when the arrows walk past the visible rows.</summary>
        private void Reveal()
        {
            if ((DataContext as PaletteViewModel)?.Selected is { } action)
            {
                Hits.ScrollIntoView(action);
            }
        }

        /// <summary>Shows the palette over its owner.</summary>
        internal static void Show(Window owner, PaletteViewModel model)
        {
            Palette palette = new() { DataContext = model };

            palette.ShowDialog(owner);
        }
    }
}
