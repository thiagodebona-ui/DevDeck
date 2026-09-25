using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  The brush for a request's colour flag.
    /// </summary>
    /// <remarks>
    ///  The flag is stored as a name rather than as a colour, so that what a flag looks like stays
    ///  a decision of the app rather than of the settings file - a hex value written into a saved
    ///  request in one theme is a hex value that is invisible in another, and the user would have
    ///  no way of knowing which.
    ///
    ///  These seven are fixed rather than taken from the palette, and that is deliberate. A flag
    ///  means whatever the person using it decided it means - red for the one that deletes things,
    ///  green for the one that is safe - and a colour that shifted with the theme would break that
    ///  agreement every time the theme changed. They are mid-tones, which is what makes them
    ///  legible on both a dark and a light surface: a swatch is a solid fill rather than text, so
    ///  it needs to differ from the background rather than have contrast against it.
    ///
    ///  An unflagged request resolves to transparent instead of to nothing, so the bar still takes
    ///  its width and every row's text starts at the same place. Rows that shuffle sideways as
    ///  flags are added and removed are far more distracting than the flags are useful.
    /// </remarks>
    internal sealed class Swatch : IValueConverter
    {
        private static readonly Dictionary<string, IBrush> Brushes = new(StringComparer.Ordinal)
        {
            ["red"] = new SolidColorBrush(Color.Parse("#D2564B")),
            ["amber"] = new SolidColorBrush(Color.Parse("#D79A34")),
            ["green"] = new SolidColorBrush(Color.Parse("#4E9E6A")),
            ["blue"] = new SolidColorBrush(Color.Parse("#4A86C8")),
            ["purple"] = new SolidColorBrush(Color.Parse("#9A6FBF")),
            ["grey"] = new SolidColorBrush(Color.Parse("#8A97A6")),
        };

        private Swatch()
        {
        }

        /// <summary>The single instance the XAML refers to.</summary>
        public static Swatch Brush { get; } = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is string name && Brushes.TryGetValue(name, out IBrush? brush)
                ? brush
                : Avalonia.Media.Brushes.Transparent;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException("A swatch is drawn from the flag, never written back to it.");
    }
}
