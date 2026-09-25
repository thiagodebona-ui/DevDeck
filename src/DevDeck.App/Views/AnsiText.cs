using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Paints a line of parsed ANSI spans into a text block.
    /// </summary>
    /// <remarks>
    ///  An attached property because <c>InlineCollection</c> is not something a binding can fill -
    ///  it has to be built, and the natural place to build it from a data template is a property
    ///  setter that fires when the row is recycled onto a new line.
    ///
    ///  That recycling is why the collection is rebuilt rather than appended to: the output list
    ///  virtualises, so one text block will be handed hundreds of different lines as the user
    ///  scrolls a long build, and anything left behind from the previous one would show up
    ///  prepended to the next.
    /// </remarks>
    internal static class AnsiText
    {
        public static readonly AttachedProperty<IReadOnlyList<AnsiSpan>?> SpansProperty =
            AvaloniaProperty.RegisterAttached<Avalonia.Controls.Control, IReadOnlyList<AnsiSpan>?>(
                "Spans", typeof(AnsiText));

        public static void SetSpans(Avalonia.Controls.Control control, IReadOnlyList<AnsiSpan>? value) =>
            control.SetValue(SpansProperty, value);

        public static IReadOnlyList<AnsiSpan>? GetSpans(Avalonia.Controls.Control control) =>
            control.GetValue(SpansProperty);

        static AnsiText()
        {
            SpansProperty.Changed.AddClassHandler<Avalonia.Controls.Control>(Paint);
        }

        private static void Paint(Avalonia.Controls.Control control, AvaloniaPropertyChangedEventArgs args)
        {
            InlineCollection? inlines = control switch
            {
                Avalonia.Controls.SelectableTextBlock selectable => selectable.Inlines,
                Avalonia.Controls.TextBlock block => block.Inlines,
                _ => null,
            };

            if (inlines is null)
            {
                return;
            }

            inlines.Clear();

            if (args.NewValue is not IReadOnlyList<AnsiSpan> spans)
            {
                return;
            }

            foreach (AnsiSpan span in spans)
            {
                if (span.Text.Length == 0)
                {
                    continue;
                }

                Run run = new(span.Text);

                if (AnsiBrushConverter.Instance.Convert(span.Foreground, typeof(IBrush), null,
                    System.Globalization.CultureInfo.InvariantCulture) is IBrush brush)
                {
                    run.Foreground = brush;
                }

                if (span.Bold)
                {
                    run.FontWeight = FontWeight.SemiBold;
                }

                // Faint is drawn as a dimmer brush rather than as opacity: a Run has no opacity of
                // its own, and setting it on the block would fade the whole line.
                if (span.Faint && run.Foreground is null)
                {
                    run.Foreground = Avalonia.Media.Brushes.Gray;
                }

                inlines.Add(run);
            }
        }
    }
}
