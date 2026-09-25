using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DevDeck.App.ViewModels
{
    /// <summary>Turns a stored icon name into the shape to draw.</summary>
    /// <remarks>
    ///  A converter rather than a property on the request, because a request is a Core type and
    ///  Core does not know what a <see cref="Geometry"/> is - the whole point of keeping the icon
    ///  as a name is that the model layer stays free of the drawing layer.
    ///
    ///  <para>
    ///   An unknown name converts to null, which is what hides the Path rather than drawing a
    ///   placeholder box. A settings file hand-edited to say "databse" should look like no icon
    ///   was chosen, not like something broke.
    ///  </para>
    /// </remarks>
    internal sealed class Glyph : IValueConverter
    {
        private Glyph()
        {
        }

        /// <summary>The single instance the XAML refers to.</summary>
        public static Glyph Shape { get; } = new();

        /// <summary>The companion test: is there anything to draw at all?</summary>
        public static Glyph Any { get; } = new();

        /// <summary>The icon's own colour, for its stroke.</summary>
        public static Glyph Ink { get; } = new();

        /// <summary>The faint wash inside it, or null for an open shape.</summary>
        public static Glyph Wash { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            Emblem? found = Icons.Pick(value as string);

            if (ReferenceEquals(this, Any))
            {
                return found is not null;
            }

            if (ReferenceEquals(this, Ink))
            {
                return found?.Ink;
            }

            return ReferenceEquals(this, Wash) ? found?.Fill : found?.Shape;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException("An icon is drawn from its name, never written back to it.");
    }
}
