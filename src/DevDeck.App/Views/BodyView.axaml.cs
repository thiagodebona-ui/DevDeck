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
    ///  The read-only counterpart to <see cref="CodeEditor"/>, and simpler than one by exactly the
    ///  part that was difficult: with nothing to type into there is no transparent entry layer to
    ///  keep registered over the painted one, so this is a single SelectableTextBlock holding
    ///  coloured inlines. Selection and copying come from the control; only the colouring is here.
    ///
    ///  A read-only TextBox was what this replaced. It could show the text and could not show what
    ///  the text was - and a JSON body is mostly punctuation, so flat monospace is where the eye
    ///  has to do the work that a colour would have done for it.
    ///
    ///  Bounded before it paints. A response can be megabytes - a page of HTML with an inlined
    ///  data URI in it, say - and laying that out as tens of thousands of separate inlines freezes
    ///  the window for long enough to look like a crash. Past the ceiling it paints the head of the
    ///  body in one colour and says so, which is still readable and still copyable.
    ///
    ///  Internal, because <see cref="BodyKind"/> is: the Core types are internal and anything with
    ///  one in its signature has to match.
    /// </remarks>
    internal partial class BodyView : UserControl
    {
        /// <summary>
        ///  How much is coloured before it is shown flat instead.
        /// </summary>
        /// <remarks>
        ///  Roughly a megabyte. Chosen against what the layout costs rather than what a response
        ///  might contain: the tokenising is linear and cheap, and the expense is the inline run
        ///  count, which this bounds by falling back to one run.
        /// </remarks>
        private const int ColourCeiling = 1_000_000;

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

            if (change.Property == TextProperty
                || change.Property == KindProperty
                || change.Property == BodyFontSizeProperty
                || change.Property == PlaceholderProperty)
            {
                Repaint();
            }
        }

        /// <summary>Rebuilds the coloured runs from the current body.</summary>
        private void Repaint()
        {
            string source = Text ?? string.Empty;

            Painted.FontSize = BodyFontSize;
            Painted.LineHeight = Math.Ceiling(BodyFontSize * 1.45);

            Empty.Text = Placeholder;
            Empty.IsVisible = source.Length == 0;
            Scroller.IsVisible = source.Length > 0;

            Painted.Inlines?.Clear();

            if (source.Length == 0)
            {
                return;
            }

            // A new response starts at the top. Without this a short reply after a long one is
            // scrolled past its own end and reads as empty.
            Scroller.Offset = default;

            InlineCollection inlines = Painted.Inlines ??= [];

            if (source.Length > ColourCeiling || !Markup.Handles(Kind))
            {
                inlines.Add(new Run(source) { Foreground = BrushFor(TokenKind.Plain) });

                return;
            }

            foreach (Token token in Markup.Tokenize(source, Kind))
            {
                inlines.Add(new Run(source.Substring(token.Start, token.Length))
                {
                    Foreground = BrushFor(token.Kind),
                });
            }
        }

        /// <summary>
        ///  The colour for a token kind, from the palette so it follows the theme.
        /// </summary>
        /// <remarks>
        ///  Looked up rather than bound, for the reason the editor's is: a Run is not a visual and
        ///  never receives a DynamicResource. The same keys as the editor, so a JSON body and a
        ///  script body agree about what a string looks like.
        /// </remarks>
        private IBrush BrushFor(TokenKind kind)
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

            return this.TryFindResource(key, out object? found) && found is IBrush brush
                ? brush
                : Brushes.Gray;
        }
    }
}
