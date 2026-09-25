using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One thing the palette can do.</summary>
    /// <remarks>
    ///  <paramref name="Group"/> is shown beside the name rather than as a heading, because the
    ///  list is re-ranked on every keystroke and headings that reorder themselves under the cursor
    ///  are harder to read than a quiet label on each row.
    /// </remarks>
    internal sealed record PaletteAction(string Name, string Group, Action Run, string? Detail = null)
    {
        /// <summary>Set by the search, and used by the view to draw the matched characters bold.</summary>
        public IReadOnlyList<int> Matched { get; init; } = [];
    }

    /// <summary>
    ///  Everything in the app, reachable by typing part of its name.
    /// </summary>
    /// <remarks>
    ///  The deck's whole premise is that a command is faster to reach than a terminal, and that
    ///  stopped being true somewhere around the twentieth command: finding the right one meant
    ///  scrolling a list with the mouse, which is slower than typing what you wanted. This is the
    ///  answer every editor and launcher arrived at - one key, type three letters, press Enter.
    ///
    ///  Ranking is subsequence matching rather than substring, so "gst" finds "git status" the way
    ///  it does everywhere else. The tie-breaks matter more than the score: a shorter name wins,
    ///  because a query that matches both "build" and "rebuild everything" almost always meant the
    ///  short one.
    /// </remarks>
    internal sealed partial class PaletteViewModel : ObservableObject
    {
        private readonly IReadOnlyList<PaletteAction> all;

        public PaletteViewModel(IReadOnlyList<PaletteAction> actions)
        {
            all = actions;
            Results = new ObservableCollection<PaletteAction>(actions.Take(Limit));
            selected = Results.FirstOrDefault();
        }

        /// <summary>Past this the list is longer than the window and nobody is reading the tail.</summary>
        private const int Limit = 40;

        public ObservableCollection<PaletteAction> Results { get; }

        [ObservableProperty]
        private string query = string.Empty;

        [ObservableProperty]
        private PaletteAction? selected;

        public bool HasResults => Results.Count > 0;

        partial void OnQueryChanged(string value) => Rank(value);

        /// <summary>
        ///  Refills the list for a query, best first.
        /// </summary>
        /// <remarks>
        ///  The collection is mutated in place rather than replaced so the bound list keeps its
        ///  scroll position and its selection machinery, which a wholesale swap resets on every
        ///  keystroke.
        /// </remarks>
        private void Rank(string value)
        {
            Results.Clear();

            IEnumerable<PaletteAction> matches = value.Trim().Length == 0
                ? all.Take(Limit)
                : all.Select(action => (Action: action, Score: Score(action, value.Trim(), out IReadOnlyList<int> hits), Hits: hits))
                    .Where(result => result.Score > 0)
                    .OrderByDescending(result => result.Score)
                    .ThenBy(result => result.Action.Name.Length)
                    .ThenBy(result => result.Action.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(Limit)
                    .Select(result => result.Action with { Matched = result.Hits });

            foreach (PaletteAction action in matches)
            {
                Results.Add(action);
            }

            Selected = Results.FirstOrDefault();
            OnPropertyChanged(nameof(HasResults));
        }

        /// <summary>
        ///  How well an action matches, and which of its characters were hit.
        /// </summary>
        /// <remarks>
        ///  Zero means no match at all, which is the only value the caller treats specially.
        ///  Everything above it is relative: a run of adjacent characters scores more than the same
        ///  characters scattered, and a hit at the start of a word scores more than one in the
        ///  middle, so "gs" prefers "git status" over "logs".
        /// </remarks>
        public static int Score(PaletteAction action, string query, out IReadOnlyList<int> matched)
        {
            string name = action.Name;
            List<int> hits = [];
            matched = hits;

            int score = 0;
            int at = 0;
            int streak = 0;

            foreach (char wanted in query)
            {
                if (wanted == ' ')
                {
                    continue;
                }

                int found = Find(name, wanted, at);

                if (found < 0)
                {
                    // Fall back to the group, so "set" still finds an action named "Theme" in the
                    // Settings group - but scored far lower, as a name match is what was meant.
                    return action.Group.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                }

                hits.Add(found);

                bool adjacent = found == at && at > 0;
                bool wordStart = found == 0 || name[found - 1] is ' ' or '-' or '_' or '.' or '/';

                streak = adjacent ? streak + 1 : 0;

                score += 10 + (streak * 6) + (wordStart ? 8 : 0);
                at = found + 1;
            }

            // A query that is the whole name is unambiguous and should never sit below a longer
            // name that happens to have scored more adjacency.
            if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
            else if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
            }

            return score;
        }

        /// <summary>
        ///  The next occurrence of a character, ignoring case.
        /// </summary>
        /// <remarks>
        ///  Written out rather than reached through a string overload because the query is compared
        ///  a character at a time, and allocating a one-character string per character per action
        ///  per keystroke is the one place in this class where that would be felt.
        /// </remarks>
        private static int Find(string name, char wanted, int from)
        {
            char lower = char.ToLowerInvariant(wanted);

            for (int at = from; at < name.Length; at++)
            {
                if (char.ToLowerInvariant(name[at]) == lower)
                {
                    return at;
                }
            }

            return -1;
        }

        /// <summary>Moves the highlight, wrapping at both ends as a menu does.</summary>
        public void Move(int by)
        {
            if (Results.Count == 0)
            {
                return;
            }

            int at = Selected is null ? -1 : Results.IndexOf(Selected);
            int next = (at + by + Results.Count) % Results.Count;

            Selected = Results[next];
        }
    }
}
