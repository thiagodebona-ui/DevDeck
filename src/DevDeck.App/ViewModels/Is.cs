using System.Globalization;
using Avalonia.Data.Converters;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  Whether a value equals a particular string, for driving a style class.
    /// </summary>
    /// <remarks>
    ///  Avalonia can bind a class to a boolean but not to "does this property equal that value", and
    ///  the alternative is three booleans on the view model that exist only so XAML can read them -
    ///  which puts presentation decisions in the model to work around a syntax gap.
    ///
    ///  Comparing as strings keeps the tone vocabulary in one place: the Core type decides what
    ///  "good" means, the stylesheet decides what colour good is, and nothing in between has to
    ///  agree on an enum.
    /// </remarks>
    internal sealed class Is : IValueConverter
    {
        private readonly string wanted;

        private Is(string wanted) => this.wanted = wanted;

        public static Is Good { get; } = new("good");

        public static Is Warn { get; } = new("warn");

        public static Is Bad { get; } = new("bad");

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is string text && string.Equals(text, wanted, StringComparison.Ordinal);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException("A style class is never written back.");
    }
}
