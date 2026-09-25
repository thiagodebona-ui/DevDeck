using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Turns a parsed ANSI colour into something the output panel can paint with.
    /// </summary>
    /// <remarks>
    ///  The shades are fixed here rather than read from the theme, and that is a deliberate
    ///  exception to how the rest of the app colours itself.
    ///
    ///  A theme's palette answers "what does this application's own warning look like". ANSI is a
    ///  different question: the process said red, and red carries meaning the tool chose - a
    ///  failing test, a diff removal, an error prefix. Remapping those onto the theme's accent
    ///  would make output disagree with the same command run in a terminal beside it, which is the
    ///  thing the user is comparing against.
    ///
    ///  What they are tuned for is legibility on both ends of the theme range. The base eight are
    ///  pulled off full saturation so they do not vibrate on a dark panel, and are kept dark enough
    ///  to stay readable on the light themes; the bright eight are separated far enough to be told
    ///  apart at twelve point rather than being the same hue a shade up.
    /// </remarks>
    internal sealed class AnsiBrushConverter : IValueConverter
    {
        public static AnsiBrushConverter Instance { get; } = new();

        private static readonly IReadOnlyDictionary<AnsiColor, IBrush> Brushes = new Dictionary<AnsiColor, IBrush>
        {
            [AnsiColor.Black] = Brush("#555F6B"),
            [AnsiColor.Red] = Brush("#D65C5C"),
            [AnsiColor.Green] = Brush("#5AA469"),
            [AnsiColor.Yellow] = Brush("#C79A3E"),
            [AnsiColor.Blue] = Brush("#4F86C6"),
            [AnsiColor.Magenta] = Brush("#A96BB5"),
            [AnsiColor.Cyan] = Brush("#3E9E9E"),
            [AnsiColor.White] = Brush("#A9B1BA"),
            [AnsiColor.BrightBlack] = Brush("#78838F"),
            [AnsiColor.BrightRed] = Brush("#F07178"),
            [AnsiColor.BrightGreen] = Brush("#77C68A"),
            [AnsiColor.BrightYellow] = Brush("#E3B341"),
            [AnsiColor.BrightBlue] = Brush("#6CA6E8"),
            [AnsiColor.BrightMagenta] = Brush("#C88CD4"),
            [AnsiColor.BrightCyan] = Brush("#56C3C3"),
            [AnsiColor.BrightWhite] = Brush("#E4E9EE"),
        };

        private static IBrush Brush(string hex)
        {
            SolidColorBrush brush = new(Color.Parse(hex));
            brush.ToImmutable();

            return brush;
        }

        /// <summary>
        ///  The brush for a colour, or null for "the process did not ask", which leaves the row at
        ///  whatever the level already painted it.
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is AnsiColor colour && Brushes.TryGetValue(colour, out IBrush? brush) ? brush : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException("Output colour is read from the process, never written back.");
    }

    /// <summary>Bold and faint, as the weight and opacity the panel binds to.</summary>
    internal sealed class AnsiWeightConverter : IValueConverter
    {
        public static AnsiWeightConverter Instance { get; } = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is true ? FontWeight.SemiBold : FontWeight.Normal;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
