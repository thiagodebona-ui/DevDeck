using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  One entry in the background-effect picker: what is stored, and what is shown.
    /// </summary>
    /// <remarks>
    ///  The label is read from the table each time it is asked for, rather than once, so the
    ///  picker follows a change of language the next time it is opened.
    /// </remarks>
    internal sealed record EffectChoice(string Id)
    {
        public string Label => Strings.Text("Effect" + Id);

        public override string ToString() => Label;
    }
}
