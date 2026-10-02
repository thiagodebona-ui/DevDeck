using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  A run's output lines, with a batch going in and the oldest lines coming out as one change
    ///  each rather than one per line.
    /// </summary>
    /// <remarks>
    ///  Once a log reaches its ceiling, every batch has to push as many lines out of the front as
    ///  it brings in. Done with RemoveAt(0), that was a shift of the whole list and a change event
    ///  per line, and the list on screen answered each event on its own - so a build that kept
    ///  printing at the ceiling spent the UI thread on thousands of removals per batch, and the
    ///  window stopped answering clicks until the build went quiet.
    ///
    ///  One Remove and one Add carrying the whole range is the same result for a fraction of the
    ///  work. Avalonia's lists take ranged changes as they are; a Reset would also be one event,
    ///  but the panels treat a Reset as a new run and start the list over.
    /// </remarks>
    internal sealed class OutputLog : ObservableCollection<OutputLine>
    {
        private static readonly PropertyChangedEventArgs CountChanged = new(nameof(Count));

        private static readonly PropertyChangedEventArgs IndexerChanged = new("Item[]");

        /// <summary>
        ///  Adds a batch, first taking out as many of the oldest lines as it needs to stay within
        ///  <paramref name="most"/>.
        /// </summary>
        public void Write(IReadOnlyList<OutputLine> lines, int most)
        {
            CheckReentrancy();

            List<OutputLine> items = (List<OutputLine>)Items;

            // A batch bigger than the whole ceiling keeps only its own tail.
            IReadOnlyList<OutputLine> added = lines.Count > most ? lines.Skip(lines.Count - most).ToList() : lines;

            int over = Math.Min(items.Count, items.Count + added.Count - most);

            if (over > 0)
            {
                IList removed = items.GetRange(0, over);
                items.RemoveRange(0, over);

                Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, 0));
            }

            if (added.Count > 0)
            {
                int start = items.Count;
                items.AddRange(added);

                Raise(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, added as IList ?? added.ToList(), start));
            }
        }

        private void Raise(NotifyCollectionChangedEventArgs change)
        {
            OnPropertyChanged(CountChanged);
            OnPropertyChanged(IndexerChanged);
            OnCollectionChanged(change);
        }
    }
}
