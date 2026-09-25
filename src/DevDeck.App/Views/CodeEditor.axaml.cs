using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  A small code editor: line numbers, and syntax colouring for the language the command is
    ///  set to run with.
    /// </summary>
    /// <remarks>
    ///  Two layers, registered on top of each other. The lower one is a TextBlock holding coloured
    ///  inlines and is what you read; the upper one is a TextBox with transparent text, and is what
    ///  you type into. They share the font, the size, the line height and the padding, so a glyph
    ///  in one sits exactly over the same glyph in the other.
    ///
    ///  This is here rather than a package because AvaloniaEdit - the obvious choice - has no
    ///  version on the NuGet feed this machine can reach. The trade is honest: no folding, no
    ///  bracket matching, no undo grouping beyond what TextBox gives, and colouring by lexer rather
    ///  than by grammar. For command bodies, which are tens of lines rather than thousands, that
    ///  buys most of the value for none of the dependency.
    ///
    ///  Everything measures unconstrained inside one shared ScrollViewer. That is what keeps the
    ///  two layers together when the body is wider than the box: neither scrolls on its own, so
    ///  there are no two offsets to keep in step.
    ///
    ///  Internal, not public, because <see cref="CommandKind"/> is: the Core types came across
    ///  from V2 internal and are kept that way, so anything exposing one has to match.
    /// </remarks>
    internal partial class CodeEditor : UserControl
    {
        /// <summary>The body being edited. Two-way by default, as an editor's text should be.</summary>
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<CodeEditor, string>(
                nameof(Text), defaultValue: string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        /// <summary>Which language to colour it as. Follows the command's Run with.</summary>
        public static readonly StyledProperty<CommandKind> LanguageProperty =
            AvaloniaProperty.Register<CodeEditor, CommandKind>(nameof(Language));

        public static readonly StyledProperty<double> EditorFontSizeProperty =
            AvaloniaProperty.Register<CodeEditor, double>(nameof(EditorFontSize), defaultValue: 12.5);

        /// <summary>Set while the control is writing to its own TextBox, to break the echo.</summary>
        private bool syncing;

        public CodeEditor()
        {
            InitializeComponent();

            Entry.TextChanged += (_, _) =>
            {
                if (syncing)
                {
                    return;
                }

                syncing = true;
                Text = Entry.Text ?? string.Empty;
                syncing = false;

                Repaint();
            };

            // A click anywhere in the editor - the gutter, the space below the last line - puts
            // the caret in the body, as a real editor does. Handled-too, because the TextBox
            // marks its own presses handled and those must still reach here to be ignored.
            Scroller.AddHandler(PointerPressedEvent, (_, e) =>
            {
                if (e.Source is Visual source && Entry.IsVisualAncestorOf(source))
                {
                    return;
                }

                Entry.Focus();
                Entry.CaretIndex = Entry.Text?.Length ?? 0;
            }, RoutingStrategies.Bubble, handledEventsToo: true);

            Repaint();
        }

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public CommandKind Language
        {
            get => GetValue(LanguageProperty);
            set => SetValue(LanguageProperty, value);
        }

        /// <summary>
        ///  Named so it does not collide with <see cref="TemplatedControl.FontSize"/>.
        /// </summary>
        /// <remarks>
        ///  Inheriting FontSize would set it on the control and every layer inside it, which sounds
        ///  right and is not: the gutter, the painted layer and the entry layer have to be set from
        ///  one value together, and an inherited one arrives at each of them separately.
        /// </remarks>
        public double EditorFontSize
        {
            get => GetValue(EditorFontSizeProperty);
            set => SetValue(EditorFontSizeProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty && !syncing)
            {
                syncing = true;
                Entry.Text = Text;
                syncing = false;

                Repaint();
            }
            else if (change.Property == LanguageProperty)
            {
                // The same body reads differently as PowerShell and as bash, so switching Run with
                // recolours rather than waiting for the next keystroke.
                Repaint();
            }
            else if (change.Property == EditorFontSizeProperty)
            {
                Repaint();
            }
        }

        /// <summary>Rebuilds the coloured layer, the gutter and the shared metrics.</summary>
        private void Repaint()
        {
            string source = Text ?? string.Empty;

            // Line height is pinned rather than left to the font, because the two layers have to
            // agree on it exactly and a default computed per control can differ by a fraction.
            double size = EditorFontSize;
            double line = Math.Ceiling(size * 1.45);

            Painted.FontSize = size;
            Painted.LineHeight = line;

            Entry.FontSize = size;
            Entry.LineHeight = line;

            Gutter.FontSize = size;
            Gutter.LineHeight = line;

            Painted.Inlines?.Clear();

            InlineCollection inlines = Painted.Inlines ??= [];

            foreach (Token token in Syntax.Tokenize(source, Language))
            {
                inlines.Add(new Run(source.Substring(token.Start, token.Length))
                {
                    Foreground = BrushFor(token.Kind),
                });
            }

            // A trailing newline has no token of its own, but the caret can still be on the line
            // after it - so the gutter counts from the text rather than from the tokens.
            int lines = source.Length == 0 ? 1 : source.Count(character => character == '\n') + 1;

            Gutter.Text = string.Join('\n', Enumerable.Range(1, lines));
        }

        /// <summary>
        ///  The colour for a token kind, taken from the palette so it follows the theme.
        /// </summary>
        /// <remarks>
        ///  Looked up rather than bound: these are set on Runs, which are not visuals and so never
        ///  receive a DynamicResource. Repaint runs on every theme change through the same path as
        ///  a keystroke, which is what keeps the colours current.
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
