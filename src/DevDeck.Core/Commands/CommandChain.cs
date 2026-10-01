namespace DevDeck.Core
{
    /// <summary>
    ///  Several commands, run one after another.
    /// </summary>
    /// <remarks>
    ///  The thing every deck grows into: pull, then build, then test, then deploy - four commands
    ///  that are really one action, currently run by clicking four times and watching each one
    ///  before starting the next.
    ///
    ///  A chain is stored as the names of its steps rather than copies of them, so editing a
    ///  command changes it everywhere it is used. The cost is that a renamed or deleted command
    ///  leaves a step pointing at nothing, which is why <see cref="Missing"/> exists and why the
    ///  panel shows a broken step rather than silently skipping it.
    /// </remarks>
    internal sealed class CommandChain
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>The names of the commands to run, in order.</summary>
        public List<string> Steps { get; set; } = [];

        /// <summary>
        ///  Whether a failing step stops the rest.
        /// </summary>
        /// <remarks>
        ///  On by default, because a chain is nearly always a pipeline: running the tests after the
        ///  build failed wastes a minute to report a second failure caused by the first. Off is for
        ///  the chains that are really a list of chores - clean three folders, and one of them not
        ///  existing is not a reason to skip the others.
        /// </remarks>
        public bool StopOnFailure { get; set; } = true;

        /// <summary>
        ///  The steps switched off, by position from zero: still in the chain, skipped when it runs.
        /// </summary>
        /// <remarks>
        ///  Positions rather than names, because a chain may run one command twice and only one of
        ///  the two be switched off. Empty for every chain saved before steps could be switched off,
        ///  so they all run as they did.
        /// </remarks>
        public List<int> SkippedSteps { get; set; } = [];

        /// <summary>Whether the step at <paramref name="position"/> runs.</summary>
        public bool IsOn(int position) => !SkippedSteps.Contains(position);

        public override string ToString() => Name;

        /// <summary>The steps that no longer name a command that exists.</summary>
        /// <param name="onlyOn">Only the steps that are switched on, which are the ones a run needs.</param>
        public IReadOnlyList<string> Missing(IEnumerable<CustomCommand> commands, bool onlyOn = false)
        {
            HashSet<string> have = new(commands.Select(command => command.Name), StringComparer.OrdinalIgnoreCase);

            return [.. Steps.Where((step, at) => !have.Contains(step) && (!onlyOn || IsOn(at)))];
        }

        /// <summary>The commands this chain runs, in order, skipping steps that no longer exist.</summary>
        public IReadOnlyList<CustomCommand> Resolve(IEnumerable<CustomCommand> commands)
        {
            List<CustomCommand> all = [.. commands];

            return
            [
                .. Steps
                    .Select(step => all.FirstOrDefault(command =>
                        command.Name.Equals(step, StringComparison.OrdinalIgnoreCase)))
                    .Where(command => command is not null)
                    .Select(command => command!)
            ];
        }

        /// <summary>Keeps a chain pointing at a command that has been renamed.</summary>
        public bool Rename(string from, string to)
        {
            bool changed = false;

            for (int at = 0; at < Steps.Count; at++)
            {
                if (Steps[at].Equals(from, StringComparison.OrdinalIgnoreCase))
                {
                    Steps[at] = to;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        ///  Whether this chain would eventually run itself.
        /// </summary>
        /// <remarks>
        ///  Chains only name commands today, so this cannot happen - but the check is here because
        ///  the obvious next feature is a chain that names another chain, and the failure mode of
        ///  that is an app that hangs rather than an error anyone can read.
        /// </remarks>
        public bool Cycles(IReadOnlyList<CommandChain> chains, HashSet<string>? seen = null)
        {
            seen ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!seen.Add(Name))
            {
                return true;
            }

            foreach (string step in Steps)
            {
                if (chains.FirstOrDefault(chain =>
                        chain.Name.Equals(step, StringComparison.OrdinalIgnoreCase)) is { } nested
                    && nested.Cycles(chains, seen))
                {
                    return true;
                }
            }

            seen.Remove(Name);

            return false;
        }
    }
}
