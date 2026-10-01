using System.Globalization;
using Avalonia.Data.Converters;

namespace DevDeck.App.Views
{
    /// <summary>A chain step switched off is drawn faded, so the ones that will run stand out.</summary>
    internal sealed class StepOpacity : IValueConverter
    {
        public static StepOpacity Instance { get; } = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is false ? 0.4 : 1.0;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
