namespace DevDeck.Core
{
    /// <summary>One saved prompt.</summary>
    /// <remarks>
    ///  <see cref="Body"/> may contain the same <c>{{placeholders}}</c> a command does, and for the
    ///  same reason: "review {{file}}" is a prompt worth keeping, "review Program.cs" is not.
    /// </remarks>
    internal sealed class SavedPrompt
    {
        public string Name { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        /// <summary>Whether this one came with the app, so a reset knows what it may replace.</summary>
        public bool IsStarter { get; set; }

        public override string ToString() => Name;
    }

    /// <summary>
    ///  The prompts worth typing twice.
    /// </summary>
    /// <remarks>
    ///  Everyone who uses a model for work ends up with four or five prompts they retype from
    ///  memory - review this diff, write a commit message, explain this error, turn this into a
    ///  script. Retyping them is both slow and lossy: the fifth version is never the one that
    ///  worked best, it is the one that was quickest to type.
    ///
    ///  The starters below are the ones this app is in a position to help with, phrased for a
    ///  developer looking at their own terminal rather than as general-purpose prompt engineering.
    /// </remarks>
    internal static class PromptLibrary
    {
        public static IReadOnlyList<SavedPrompt> Starters =>
        [
            new SavedPrompt
            {
                Name = "Explain this error",
                IsStarter = true,
                Body = "Explain this error and tell me the most likely cause. Be specific about "
                    + "which line is at fault. If the fix is one command, give me that command.\n\n"
                    + "```\n{{error}}\n```",
            },
            new SavedPrompt
            {
                Name = "Write a commit message",
                IsStarter = true,
                Body = "Write a commit message for this diff. One short subject line under 72 "
                    + "characters saying what changed and why, then a blank line, then bullets only "
                    + "if the change needs them. No preamble.\n\n```diff\n{{diff}}\n```",
            },
            new SavedPrompt
            {
                Name = "Review this change",
                IsStarter = true,
                Body = "Review this change. Look for bugs, unhandled failures and anything that "
                    + "will surprise the next person to read it. Say what is wrong, not what is "
                    + "fine. If you find nothing, say so in one line.\n\n```diff\n{{diff}}\n```",
            },
            new SavedPrompt
            {
                Name = "Turn this into a script",
                IsStarter = true,
                Body = "Turn this into a single script I can save as a command. Use {{shell}}. "
                    + "Make it fail loudly rather than continue on error, and do not add "
                    + "explanation around the code block.\n\n{{what}}",
            },
            new SavedPrompt
            {
                Name = "What does this command do",
                IsStarter = true,
                Body = "Explain what this does, flag by flag, and tell me anything about it that "
                    + "is destructive or hard to undo.\n\n```\n{{command}}\n```",
            },
            new SavedPrompt
            {
                Name = "Why is this slow",
                IsStarter = true,
                Body = "This is slower than it should be. Tell me the likeliest cause and how I "
                    + "would confirm it, before suggesting a fix.\n\n```\n{{code}}\n```",
            },
        ];

        /// <summary>Adds any starter the user does not already have, and says how many.</summary>
        /// <remarks>
        ///  By name, so a starter the user has edited is theirs and is left alone. A reset that
        ///  overwrote an improved prompt would be the app deciding it knows better.
        /// </remarks>
        public static int Seed(List<SavedPrompt> into)
        {
            HashSet<string> have = new(into.Select(prompt => prompt.Name), StringComparer.OrdinalIgnoreCase);

            int added = 0;

            foreach (SavedPrompt starter in Starters.Where(starter => !have.Contains(starter.Name)))
            {
                into.Add(starter);
                added++;
            }

            return added;
        }
    }
}
