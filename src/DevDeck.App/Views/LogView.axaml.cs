using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using DevDeck.App.ViewModels;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>The log panel. Here for the clipboard, and to keep the newest line in sight.</summary>
    public partial class LogView : UserControl
    {
        public LogView()
        {
            InitializeComponent();

            // What the list is showing, filter and all, rather than the whole log: the reader
            // narrowed it down to find the lines they want to copy.
            TextMode.Wire(SelectLines, Lines, () => string.Join(
                Environment.NewLine,
                Lines.Items.OfType<LogEntry>().Select(entry => $"{entry.Stamp}  {entry.Source,-12}  {entry.Text}")));

            DataContextChanged += (_, _) =>
            {
                if (DataContext is LogViewModel model)
                {
                    model.Copy = Copy;

                    // A log that does not follow is a log the reader has to chase with the scroll
                    // bar every time something happens, which is the opposite of watching one.
                    model.Items.CollectionChanged += Follow;
                }
            };
        }

        private void Follow(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add)
            {
                return;
            }

            if (Lines.ItemCount > 0)
            {
                Lines.ScrollIntoView(Lines.ItemCount - 1);
            }
        }

        private async Task Copy(string text)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
        }
    }
}
