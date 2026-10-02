using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  A response body, drawn with colour and still selectable.
    /// </summary>
    /// <remarks>
    ///  The read-only counterpart to <see cref="CodeEditor"/>. A read-only TextBox was what this
    ///  replaced: it could show the text and could not show what the text was - and a JSON body is
    ///  mostly punctuation, so flat monospace is where the eye has to do the work that a colour
    ///  would have done for it.
    ///
    ///  It was then one SelectableTextBlock holding the whole body as coloured runs, and that is
    ///  what made a large JSON response freeze the panel: text layout is not virtualised, so every
    ///  show of the pane measured every run of the body. Now the body is cut into lines off the UI
    ///  thread (see <see cref="BodyLines"/>) and drawn as a virtualised list, so the cost is the
    ///  rows on screen whatever the size. The price is that a selection stays within one line;
    ///  Copy beside the pane takes the whole body, which is what copying a response nearly always
    ///  means.
    ///
    ///  Internal, because <see cref="BodyKind"/> is: the Core types are internal and anything with
    ///  one in its signature has to match.
    /// </remarks>
    internal partial class BodyView : UserControl
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<BodyView, string>(nameof(Text), defaultValue: string.Empty);

        /// <summary>What the body turned out to be, which decides how it is coloured.</summary>
        public static readonly StyledProperty<BodyKind> KindProperty =
            AvaloniaProperty.Register<BodyView, BodyKind>(nameof(Kind));

        public static readonly StyledProperty<double> BodyFontSizeProperty =
            AvaloniaProperty.Register<BodyView, double>(nameof(BodyFontSize), defaultValue: 12.5);

        /// <summary>Shown in place of the body while there is not one.</summary>
        public static readonly StyledProperty<string> PlaceholderProperty =
            AvaloniaProperty.Register<BodyView, string>(nameof(Placeholder), defaultValue: string.Empty);

        /// <summary>Counts repaints, so a slow split of an older body is thrown away when it lands.</summary>
        private int painting;

        public BodyView()
        {
            InitializeComponent();

            Repaint();
        }

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public BodyKind Kind
        {
            get => GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        /// <summary>Named apart from FontSize, for the reason given on the editor's.</summary>
        public double BodyFontSize
        {
            get => GetValue(BodyFontSizeProperty);
            set => SetValue(BodyFontSizeProperty, value);
        }

        public string Placeholder
        {
            get => GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty || change.Property == KindProperty)
            {
                Repaint();
            }
            else if (change.Property == PlaceholderProperty)
            {
                Empty.Text = Placeholder;
            }
        }

        /// <summary>Cuts the body into coloured lines, away from the UI thread, and shows them.</summary>
        private async void Repaint()
        {
            int mine = ++painting;
            string source = Text ?? string.Empty;
            BodyKind kind = Kind;

            Empty.Text = Placeholder;
            Empty.IsVisible = source.Length == 0;
            Scroller.IsVisible = source.Length > 0;

            if (source.Length == 0)
            {
                Lines.ItemsSource = null;
                return;
            }

            List<BodyLine> lines;

            try
            {
                lines = await Task.Run(() => BodyLines.Split(source, kind));
            }
            catch (Exception)
            {
                // A highlighter that trips over something odd still leaves the text readable.
                lines = BodyLines.Split(source, BodyKind.Plain);
            }

            if (mine != painting)
            {
                return;
            }

            Lines.ItemsSource = lines;

            // A new response starts at the top. Without this a short reply after a long one is
            // scrolled past its own end and reads as empty.
            Scroller.Offset = default;
        }
    }

    /// <summary>
    ///  Paints one body line's coloured runs into a text block.
    /// </summary>
    /// <remarks>
    ///  An attached property for the reason <see cref="AnsiText"/> is one: an InlineCollection is
    ///  built, not bound, and the rows are recycled as the list scrolls, so it is rebuilt each time
    ///  a row is handed a different line.
    ///
    ///  The colours are looked up from the palette as the row is painted, so they follow the
    ///  theme - a Run is not a visual and never receives a DynamicResource.
    /// </remarks>
    internal static class BodyLineText
    {
        public static readonly AttachedProperty<BodyLine?> LineProperty =
            AvaloniaProperty.RegisterAttached<Control, BodyLine?>("Line", typeof(BodyLineText));

        public static void SetLine(Control control, BodyLine? value) => control.SetValue(LineProperty, value);

        public static BodyLine? GetLine(Control control) => control.GetValue(LineProperty);

        static BodyLineText()
        {
            LineProperty.Changed.AddClassHandler<Control>(Paint);
        }

        private static void Paint(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            if (control is not TextBlock block)
            {
                return;
            }

            InlineCollection inlines = block.Inlines ??= [];
            inlines.Clear();

            if (args.NewValue is not BodyLine line)
            {
                return;
            }

            foreach (BodyRun run in line.Runs)
            {
                inlines.Add(new Run(run.Text) { Foreground = BrushFor(control, run.Kind) });
            }
        }

        /// <summary>The same keys as the editor, so a JSON body and a script body agree about what a string looks like.</summary>
        private static IBrush BrushFor(Control control, TokenKind kind)
        {
            string key = kind switch
            {
                TokenKind.Keyword => "SynKeyword",
                TokenKind.Text => "SynString",
                TokenKind.Comment => "SynComment",
                TokenKind.Number => "SynNumber",
                TokenKind.Variable => "SynVariable",
                _ => "Text",
            };

            // The row may be painted before it is in the window, when only the application can
            // answer for the theme.
            if (control.TryFindResource(key, out object? found) && found is IBrush brush)
            {
                return brush;
            }

            return Application.Current is { } app
                && app.TryGetResource(key, app.ActualThemeVariant, out object? themed)
                && themed is IBrush fallback
                ? fallback
                : Brushes.Gray;
        }
    }
}
