using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One visible row of a JSON tree: a node, how deep it sits, and whether it is open.</summary>
    internal sealed partial class JsonRow : ObservableObject
    {
        public JsonRow(JsonNode node, int depth, JsonRow? parent)
        {
            Node = node;
            Depth = depth;
            Parent = parent;
        }

        public JsonNode Node { get; }

        public int Depth { get; }

        public JsonRow? Parent { get; }

        /// <summary>The indent, as a margin the row template can bind to.</summary>
        public Avalonia.Thickness Indent => new(Depth * 16, 0, 0, 0);

        public bool CanOpen => Node.Children.Count > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Glyph))]
        private bool isOpen;

        /// <summary>▸ closed, ▾ open, nothing for a leaf.</summary>
        public string Glyph => !CanOpen ? string.Empty : IsOpen ? "▾" : "▸";
    }

    /// <summary>
    ///  A JSON tree flattened into the rows that are showing, for a virtualised list.
    /// </summary>
    /// <remarks>
    ///  A TreeView builds a container for every row of every open node and lays them all out;
    ///  opening a 500-element array cost over a second of the UI thread, and a large reply has
    ///  many of those. Flattened, the open rows are one list, and the list draws only the ones in
    ///  view - so opening a node is an insert into a list, however many children it has.
    ///
    ///  Opening and closing raise one change for the whole run of rows rather than one per row,
    ///  for the reason <see cref="OutputLog"/> does: a list answers each change on its own.
    /// </remarks>
    internal sealed class JsonRows : ObservableCollection<JsonRow>
    {
        private static readonly PropertyChangedEventArgs CountChanged = new(nameof(Count));

        private static readonly PropertyChangedEventArgs IndexerChanged = new("Item[]");

        /// <summary>Starts again from a new document, with its top level open.</summary>
        public void Load(JsonNode? root)
        {
            Items.Clear();

            if (root is not null)
            {
                JsonRow top = new(root, 0, null);
                Items.Add(top);

                top.IsOpen = true;
                ((List<JsonRow>)Items).AddRange(Children(top));
            }

            OnPropertyChanged(CountChanged);
            OnPropertyChanged(IndexerChanged);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Toggle(JsonRow row)
        {
            if (row.IsOpen)
            {
                Close(row);
            }
            else
            {
                Open(row);
            }
        }

        public void Open(JsonRow row)
        {
            if (row.IsOpen || !row.CanOpen || IndexOf(row) is not (var at and >= 0))
            {
                return;
            }

            CheckReentrancy();
            row.IsOpen = true;

            List<JsonRow> added = Children(row);
            ((List<JsonRow>)Items).InsertRange(at + 1, added);

            OnPropertyChanged(CountChanged);
            OnPropertyChanged(IndexerChanged);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)added, at + 1));
        }

        public void Close(JsonRow row)
        {
            if (!row.IsOpen || IndexOf(row) is not (var at and >= 0))
            {
                return;
            }

            CheckReentrancy();
            row.IsOpen = false;

            // Everything deeper than the row, up to the next row at its own depth or shallower.
            int end = at + 1;

            while (end < Count && this[end].Depth > row.Depth)
            {
                this[end].IsOpen = false;
                end++;
            }

            if (end == at + 1)
            {
                return;
            }

            List<JsonRow> items = (List<JsonRow>)Items;
            List<JsonRow> removed = items.GetRange(at + 1, end - at - 1);
            items.RemoveRange(at + 1, end - at - 1);

            OnPropertyChanged(CountChanged);
            OnPropertyChanged(IndexerChanged);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (IList)removed, at + 1));
        }

        private static List<JsonRow> Children(JsonRow row) =>
            [.. row.Node.Children.Select(child => new JsonRow(child, row.Depth + 1, row))];
    }
}
