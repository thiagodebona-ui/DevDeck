using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace DevDeck.App.Localisation
{
    /// <summary>
    ///  Writes a string into a view: <c>Text="{loc:T HttpRename}"</c>.
    /// </summary>
    /// <remarks>
    ///  It returns a binding rather than the text itself, which is the whole difference between a
    ///  language setting that works and one that needs the app restarted. A markup extension that
    ///  returned a string would be evaluated once, when the view was built, and the window would
    ///  keep whatever language it was born in.
    ///
    ///  Deliberately a reflection binding against <see cref="Tr.Current"/> rather than a compiled
    ///  one: the source is fixed and global, so there is nothing for the compiler's DataType to
    ///  help with, and binding to the view model's own DataType - which is what a compiled binding
    ///  here would try - is exactly the wrong target.
    /// </remarks>
    public sealed class TExtension : MarkupExtension
    {
        public TExtension()
        {
        }

        public TExtension(string key) => Key = key;

        /// <summary>The name of the property on <see cref="Tr"/>, which is the string's id.</summary>
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider) =>
            new Binding
            {
                Source = Tr.Current,
                Path = Key,
                Mode = BindingMode.OneWay,
            };
    }
}
