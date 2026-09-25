using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    /// <summary>The running panel. Opens URLs, and tells the model when it is being looked at.</summary>
    /// <remarks>
    ///  The polling is started on attach and stopped on detach rather than run for the life of the
    ///  app, because both listings spawn a process: a panel nobody is looking at should not be
    ///  running netstat every twelve seconds for the privilege.
    /// </remarks>
    public partial class RunningView : UserControl
    {
        public RunningView()
        {
            InitializeComponent();

            DataContextChanged += (_, _) =>
            {
                if (DataContext is RunningViewModel model)
                {
                    model.Open = Open;
                    model.Copy = Copy;

                    // A star row keeps its share even with nothing visible in it, so the
                    // containers row is folded to nothing by hand when there are none to show.
                    model.PropertyChanged += (_, changed) =>
                    {
                        if (changed.PropertyName == nameof(RunningViewModel.ShowContainers))
                        {
                            FoldContainers(model.ShowContainers);
                        }
                    };

                    FoldContainers(model.ShowContainers);
                }
            };
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            (DataContext as RunningViewModel)?.Start();
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            (DataContext as RunningViewModel)?.Stop();
        }

        private void FoldContainers(bool show) =>
            Lists.RowDefinitions[4].Height = show ? new Avalonia.Controls.GridLength(1, Avalonia.Controls.GridUnitType.Star) : new Avalonia.Controls.GridLength(0);

        private async Task Copy(string text)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
        }

        /// <summary>Enter in the URL box adds the check, as the button does.</summary>
        private void HealthUrlKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is RunningViewModel model)
            {
                e.Handled = true;
                model.AddHealthCheckCommand.Execute(null);
            }
        }

        /// <summary>Enter in the port box checks it.</summary>
        private void PortKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is RunningViewModel model)
            {
                e.Handled = true;
                model.CheckPortCommand.Execute(null);
            }
        }

        /// <summary>Hands a URL to whatever the desktop opens URLs with.</summary>
        /// <remarks>
        ///  UseShellExecute is what makes this one line rather than three platform branches - and
        ///  it is also why the failure has to be caught: a machine with no registered browser
        ///  throws here, which is not worth taking the app down for.
        /// </remarks>
        private static void Open(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception)
            {
                // Nothing useful to do about it: the panel keeps working, the browser did not open.
            }
        }
    }
}
