using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    public partial class MemoryView : UserControl
    {
        private MemoryWidget? widget;

        /// <summary>
        ///  Set once the main window has really closed, as opposed to hidden.
        /// </summary>
        /// <remarks>
        ///  At sign-out or shutdown Windows closes every window the app has, the hidden main window
        ///  included, and then the widget. The widget closing used to bring the main window back -
        ///  which had just been closed, and a closed Avalonia window throws when shown, so the app
        ///  died with an error on the shutdown screen. There is nothing to come back to by then.
        /// </remarks>
        private static bool deckClosed;

        private static bool watchingDeck;

        public MemoryView()
        {
            InitializeComponent();

            // The view model asks for the widget, for the app to come back, and for the app to
            // end; only the view knows how to do any of those.
            DataContextChanged += (_, _) =>
            {
                if (DataContext is MemoryViewModel model)
                {
                    model.ToggleWidget = Toggle;
                    model.ShowApp = ShowApp;
                    model.QuitApp = QuitApp;
                }
            };
        }

        /// <summary>Opens or closes the floating readout, and reports which it did.</summary>
        private bool Toggle()
        {
            if (widget is not null)
            {
                widget.Close();
                widget = null;
                return false;
            }

            if (!watchingDeck && Deck() is { } deck)
            {
                watchingDeck = true;
                deck.Closed += (_, _) => deckClosed = true;
            }

            widget = new MemoryWidget { DataContext = DataContext };

            // Clearing the field on close covers the widget's own menu as well as this one.
            widget.Closed += (_, _) =>
            {
                widget = null;

                if (DataContext is MemoryViewModel model)
                {
                    model.IsWidgetOpen = false;
                }

                // The widget was the only thing on screen if the main window had been closed behind
                // it. Closing it as well would leave the app running with nothing to click, so the
                // window comes back instead.
                if (Deck() is { IsVisible: false })
                {
                    ShowApp();
                }
            };

            widget.Show();
            return true;
        }

        /// <summary>
        ///  Brings the main window back from hiding.
        /// </summary>
        /// <remarks>
        ///  Hidden rather than closed, which is why this can simply show it again: a closed
        ///  Avalonia window cannot be reopened, so <see cref="MainWindow"/> hides itself instead
        ///  whenever the widget is floating.
        /// </remarks>
        private static void ShowApp()
        {
            if (deckClosed || Deck() is not { } main)
            {
                return;
            }

            main.Show();

            if (main.WindowState == WindowState.Minimized)
            {
                main.WindowState = WindowState.Normal;
            }

            main.Activate();
        }

        /// <summary>Ends the app outright, widget and all.</summary>
        private void QuitApp()
        {
            widget?.Close();
            widget = null;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        private static Window? Deck() =>
            Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
    }
}
