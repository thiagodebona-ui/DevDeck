using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Owns the floating memory widget, and the ways back to the deck and out of the app.
    /// </summary>
    /// <remarks>
    ///  Attached by the app rather than by the Memory page, which only exists once it has been
    ///  navigated to. A widget left open at sign-out has to come back at the next sign-in, long
    ///  before anyone opens that page.
    /// </remarks>
    internal sealed class WidgetHost
    {
        private readonly MemoryViewModel model;

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
        private bool deckClosed;

        private bool watchingDeck;

        private WidgetHost(MemoryViewModel model) => this.model = model;

        /// <summary>Hands the view model the widget, the way back and the way out.</summary>
        public static void Attach(MemoryViewModel model)
        {
            WidgetHost host = new(model);

            model.ToggleWidget = host.Toggle;
            model.ShowApp = host.ShowApp;
            model.QuitApp = host.QuitApp;
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

            widget = new MemoryWidget { DataContext = model };

            // Clearing the field on close covers the widget's own menu as well as this one.
            widget.Closed += (_, _) =>
            {
                widget = null;
                model.IsWidgetOpen = false;

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
        private void ShowApp()
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
