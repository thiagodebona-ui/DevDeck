using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using DevDeck.App.ViewModels;

namespace DevDeck.App.Views
{
    /// <summary>The clipboard panel. Here for the clipboard itself, and for the polling window.</summary>
    public partial class ClipsView : UserControl
    {
        public ClipsView()
        {
            InitializeComponent();

            DataContextChanged += (_, _) =>
            {
                if (DataContext is ClipsViewModel model)
                {
                    model.Copy = Copy;
                    model.Paste = Paste;
                    model.Open = Open;
                }
            };
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            (DataContext as ClipsViewModel)?.Start();
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            (DataContext as ClipsViewModel)?.Stop();
        }

        private IClipboard? Clipboard() => TopLevel.GetTopLevel(this)?.Clipboard;

        private async Task Copy(string text)
        {
            if (Clipboard() is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
        }

        private async Task<string?> Paste() =>
            Clipboard() is { } clipboard ? await clipboard.TryGetTextAsync() : null;

        /// <summary>Hands a link to whatever the desktop opens links with.</summary>
        /// <remarks>
        ///  The same shape as the running panel's, and caught for the same reason: a machine with
        ///  no registered browser throws here, and a click that does nothing is a better outcome
        ///  than a dialog about it. What may reach this is bounded by the model - only an http or
        ///  https address is ever offered - because this line is a shell execute.
        /// </remarks>
        private static void Open(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception)
            {
                // The panel keeps working, the browser did not open.
            }
        }
    }
}
