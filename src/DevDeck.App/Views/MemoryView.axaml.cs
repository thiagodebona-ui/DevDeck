using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    public partial class MemoryView : UserControl
    {
        public MemoryView()
        {
            InitializeComponent();

            // The view model asks for a question to be put to the user, which needs a window to
            // own it. The widget and the ways back and out are the app's: see WidgetHost.
            DataContextChanged += (_, _) =>
            {
                if (DataContext is MemoryViewModel model)
                {
                    model.Confirm = Confirm;

                    if (IsAttachedToVisualTree())
                    {
                        model.ScanSpaceIfStale();
                    }
                }
            };

            // The page is attached when it is navigated to, which is when the disk sizes are worth
            // measuring - not at start-up, behind a page nobody has opened.
            AttachedToVisualTree += (_, _) => (DataContext as MemoryViewModel)?.ScanSpaceIfStale();
        }

        private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

        /// <summary>
        ///  Opens or closes a group from anywhere on its row: the chevron, the name, the bar.
        /// </summary>
        /// <remarks>
        ///  The chevron, Spare and End sit on the same row and keep their own clicks, so a click on
        ///  one of them is not counted twice.
        /// </remarks>
        private void OnRowTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control { DataContext: ProcessRow { IsGroup: true } row }
                || DataContext is not MemoryViewModel model)
            {
                return;
            }

            for (Visual? at = e.Source as Visual; at is not null && at != sender; at = at.GetVisualParent())
            {
                if (at is Button)
                {
                    return;
                }
            }

            model.ToggleGroupCommand.Execute(row);
            e.Handled = true;
        }

        /// <summary>The same modal question the settings page asks; false when there is no window.</summary>
        private async Task<bool> Confirm(string heading, string detail, string proceed) =>
            TopLevel.GetTopLevel(this) is Window owner
                && await ConfirmPrompt.Ask(owner, heading, detail, proceed);
    }
}
