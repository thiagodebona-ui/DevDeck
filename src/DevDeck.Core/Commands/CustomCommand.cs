namespace DevDeck.Core
{
    /// <summary>How the body of a command should be run.</summary>
    internal enum CommandKind
    {
        /// <summary>A command line, run by cmd.exe or /bin/sh. What every command was before scripts existed.</summary>
        Shell,

        /// <summary>A PowerShell script, written to a temporary .ps1 and run.</summary>
        PowerShell,

        /// <summary>A batch script, written to a temporary .bat and run. Windows only.</summary>
        Batch,

        /// <summary>
        ///  A shell script, written to a temporary .sh and run by /bin/sh. macOS and Linux only.
        /// </summary>
        /// <remarks>
        ///  Added in V3. It sits last so that the three kinds saved before it keep their numbers
        ///  and an older settings file still deserialises.
        /// </remarks>
        Bash
    }

    /// <summary>
    ///  One parameter handed to a command when it runs: a name, a value, and whether it is on.
    /// </summary>
    /// <remarks>
    ///  Unlike a <c>{{placeholder}}</c>, which is text substituted into the body before it runs,
    ///  these reach the script the way arguments always do - so a script written to take
    ///  parameters works unchanged, and a value never has to be spliced into its source. See
    ///  <see cref="ScriptFile.ArgumentsFor"/> for how each kind receives them.
    /// </remarks>
    internal sealed class CommandArgument
    {
        /// <summary>The parameter's name. Optional: an unnamed one is passed by position only.</summary>
        public string Name { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        /// <summary>Off leaves the row in the list without passing it, for switching a flag off and on.</summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>A named command or script the user can run against the current workspace.</summary>
    internal sealed class CustomCommand
    {
        public string Name { get; set; } = string.Empty;

        public string Command { get; set; } = string.Empty;

        /// <summary>
        ///  What <see cref="Command"/> holds.
        /// </summary>
        /// <remarks>
        ///  Defaults to <see cref="CommandKind.Shell"/>, which is what every command saved before
        ///  this existed was, so an older settings file keeps working untouched.
        /// </remarks>
        public CommandKind Kind { get; set; } = CommandKind.Shell;

        /// <summary>
        ///  Start it and forget it: no output is captured, no exit code is reported, and no run tab
        ///  is opened.
        /// </summary>
        /// <remarks>
        ///  For commands that open something rather than finish something - <c>start cmd /k</c>
        ///  around an interactive session, an editor, a dev server in its own window. Watching one
        ///  of those makes the app look hung: it holds the output pipes and has nothing to report
        ///  until they close, and a session the user is meant to type into never closes them.
        ///  <para>
        ///   Defaults to false, so every command saved before this existed still runs watched.
        ///  </para>
        /// </remarks>
        public bool Detached { get; set; }

        /// <summary>
        ///  The parameters passed to it on every run, in order.
        /// </summary>
        /// <remarks>
        ///  Empty for every command saved before these existed, so they run exactly as they did.
        /// </remarks>
        public List<CommandArgument> Arguments { get; set; } = [];

        public override string ToString() => Name;
    }
}
