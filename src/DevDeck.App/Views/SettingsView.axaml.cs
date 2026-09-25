using Avalonia.Controls;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();

            // Hooked on the data context rather than in the constructor, because the view is built
            // before the view model is attached and a callback set on nothing is a button that
            // silently does nothing - which is exactly the failure that is hardest to notice.
            DataContextChanged += (_, _) =>
            {
                if (DataContext is SettingsViewModel model)
                {
                    model.OpenUrl = Open;
                    model.Confirm = Confirm;
                }
            };
        }

        /// <summary>
        ///  Puts a question the user cannot take back in front of the window.
        /// </summary>
        /// <remarks>
        ///  Answers false when there is no window to be modal to, which happens only if the panel
        ///  is hosted somewhere that is not a window at all. Refusing is the right answer there:
        ///  the alternative is carrying out something irreversible having failed to ask.
        /// </remarks>
        private async Task<bool> Confirm(string heading, string detail, string proceed)
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return false;
            }

            return await ConfirmPrompt.Ask(owner, heading, detail, proceed);
        }

        /// <summary>
        ///  Hands a link to whatever the system opens links with.
        /// </summary>
        /// <remarks>
        ///  Through the top level's launcher rather than <c>Process.Start</c>: the launcher is the
        ///  one route that works on all three platforms without the app having to know whether the
        ///  answer is <c>explorer</c>, <c>open</c> or <c>xdg-open</c>.
        ///
        ///  A failure here is swallowed on purpose. The worst case is a desktop with no registered
        ///  browser, and an unhandled exception from a settings button is a far worse outcome than
        ///  a link that did not open.
        /// </remarks>
        private void Open(string url)
        {
            try
            {
                if (url.Length > 0 && Uri.TryCreate(url, UriKind.Absolute, out Uri? address))
                {
                    TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(address);
                }
            }
            catch (Exception exception)
            {
                DevDeck.Core.AppLog.Instance.Failure("settings", $"Could not open {url}", exception);
            }
        }
    }
}
