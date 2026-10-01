using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Sets a line of changelog markdown into a text block: <c>code</c> in the mono face on its own
    ///  tint, <b>bold</b> in bold, everything else as it is.
    /// </summary>
    /// <remarks>
    ///  An attached property for the same reason as <see cref="AnsiText"/>: inlines have to be
    ///  built, not bound. The code tint and colour are bound to the theme's resources rather than
    ///  read once, so a theme change recolours them along with everything else.
    /// </remarks>
    internal static class MarkdownText
    {
        public static readonly AttachedProperty<string?> MarkdownProperty =
            AvaloniaProperty.RegisterAttached<Control, string?>("Markdown", typeof(MarkdownText));

        public static void SetMarkdown(Control control, string? value) => control.SetValue(MarkdownProperty, value);

        public static string? GetMarkdown(Control control) => control.GetValue(MarkdownProperty);

        static MarkdownText()
        {
            MarkdownProperty.Changed.AddClassHandler<Control>(Paint);
        }

        private static void Paint(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            if (control is not TextBlock block)
            {
                return;
            }

            InlineCollection inlines = block.Inlines ??= [];
            inlines.Clear();

            if (args.NewValue is not string { Length: > 0 } text)
            {
                return;
            }

            foreach (InlineRun part in Changelog.Runs(text))
            {
                Run run = new(part.Text);

                if (part.IsBold)
                {
                    run.FontWeight = FontWeight.SemiBold;
                }

                if (part.IsCode)
                {
                    run.Bind(TextElement.FontFamilyProperty, control.GetResourceObservable("MonoFont"));
                    run.Bind(TextElement.BackgroundProperty, control.GetResourceObservable("CodeBg"));
                    run.Bind(TextElement.ForegroundProperty, control.GetResourceObservable("Accent"));
                }

                inlines.Add(run);
            }
        }
    }
}
