using Avalonia.Controls;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    public partial class ChangelogView : UserControl
    {
        public ChangelogView()
        {
            InitializeComponent();

            // On the data context rather than in the constructor, as in SettingsView: the view is
            // built before its model is attached.
            DataContextChanged += (_, _) =>
            {
                if (DataContext is ChangelogViewModel model)
                {
                    model.OpenUrl = Open;
                }
            };
        }

        /// <summary>Hands the release page to the system's browser, as SettingsView does.</summary>
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
                DevDeck.Core.AppLog.Instance.Failure("changelog", $"Could not open {url}", exception);
            }
        }
    }
}
