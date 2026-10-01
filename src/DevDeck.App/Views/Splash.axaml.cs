using Avalonia.Controls;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  The window on screen while the deck is built.
    /// </summary>
    /// <remarks>
    ///  Building the deck - settings, the command list, every panel's view model, the first probe of
    ///  the assistant's endpoint - happens on the UI thread before the main window can be drawn, and
    ///  on a cold start that is a second or two of nothing at all, which reads as the app not having
    ///  started. This is up first, so there is something there from the first moment.
    /// </remarks>
    public partial class Splash : Window
    {
        /// <summary>
        ///  The least time the splash stays up, even when the deck is ready sooner.
        /// </summary>
        /// <remarks>
        ///  Long enough to be seen rather than flashed - a window that appears and vanishes in a
        ///  tenth of a second looks like a glitch - and short enough not to be in the way.
        /// </remarks>
        public static readonly TimeSpan Showing = TimeSpan.FromMilliseconds(900);

        public Splash()
        {
            InitializeComponent();

            VersionText.Text = Strings.Format("SplashVersion", AppVersion.Number);
        }
    }
}
