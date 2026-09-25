using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One box in the parameter prompt.</summary>
    /// <remarks>
    ///  Wraps the immutable <see cref="CommandParameter"/> with the thing the dialog needs and it
    ///  does not have: somewhere to put the answer, and a warning that appears as the answer is
    ///  typed rather than after the command has already run.
    /// </remarks>
    internal sealed partial class ParameterEntry : ObservableObject
    {
        public ParameterEntry(CommandParameter parameter)
        {
            Parameter = parameter;
            value = parameter.Default;
        }

        public CommandParameter Parameter { get; }

        public string Name => Parameter.Name;

        public string Label => Parameter.Label;

        public bool IsSecret => Parameter.IsSecret;

        /// <summary>Shown under the box when a default exists, so it is clear what blank means.</summary>
        public string? Hint => Parameter.Default.Length > 0 ? Strings.Format("ParamDefaultsTo", Parameter.Default) : null;

        public bool HasHint => Hint is not null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Warning))]
        [NotifyPropertyChangedFor(nameof(HasWarning))]
        private string value;

        /// <summary>
        ///  Names a character in the value that would end the command and begin another.
        /// </summary>
        /// <remarks>
        ///  Not a block, and it is not pretending to be a sandbox - the user can already type any
        ///  command they like into the body. It exists for the likelier accident: a value copied
        ///  from somewhere with a trailing fragment on it, which would otherwise run silently as a
        ///  second command. Saying so before Run is pressed costs nothing and occasionally saves
        ///  a repository.
        /// </remarks>
        public string? Warning => CommandParameters.Unsafe(Value)
            ? Strings.Format("ParamOffending", CommandParameters.Offending(Value))
            : null;

        public bool HasWarning => Warning is not null;
    }

    /// <summary>
    ///  The values a parameterised command is asking for, before it runs.
    /// </summary>
    /// <remarks>
    ///  Secrets are absent from this list by design: <c>{{secret:NAME}}</c> is resolved from the
    ///  vault at substitution time and never typed into a dialog, so a token cannot end up in the
    ///  prompt's history, in a screenshot, or in the saved command body.
    /// </remarks>
    internal sealed partial class ParameterPromptViewModel : ObservableObject
    {
        public ParameterPromptViewModel(string command, IEnumerable<CommandParameter> parameters)
        {
            Command = command;
            Entries = new ObservableCollection<ParameterEntry>(
                parameters.Where(parameter => !parameter.IsSecret).Select(parameter => new ParameterEntry(parameter)));
        }

        /// <summary>The command being run, for the dialog's title.</summary>
        public string Command { get; }

        public ObservableCollection<ParameterEntry> Entries { get; }

        /// <summary>The answers, keyed by name, in the form <see cref="CommandParameters.Fill"/> wants.</summary>
        public IReadOnlyDictionary<string, string> Values =>
            Entries.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }
}
