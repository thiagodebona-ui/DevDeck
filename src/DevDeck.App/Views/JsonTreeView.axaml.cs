using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DevDeck.App.ViewModels;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  A JSON document as a tree to walk with the mouse or the arrow keys.
    /// </summary>
    /// <remarks>
    ///  Shared by the toolbox and the HTTP panel, so a parsed document looks and behaves the same
    ///  wherever one appears. It takes the root already built - building is the caller's, and is
    ///  done off the UI thread for a large reply - and shows a note in place of the tree when
    ///  there is none.
    ///
    ///  Drawn as a flat, virtualised list of the open rows (see <see cref="JsonRows"/>), with the
    ///  tree's keys put back on top of it here.
    /// </remarks>
    internal partial class JsonTreeView : UserControl
    {
        public static readonly StyledProperty<JsonNode?> DocumentProperty =
            AvaloniaProperty.Register<JsonTreeView, JsonNode?>(nameof(Document));

        /// <summary>Said where the tree would be when there is no document.</summary>
        public static readonly StyledProperty<string> EmptyTextProperty =
            AvaloniaProperty.Register<JsonTreeView, string>(nameof(EmptyText), defaultValue: string.Empty);

        private readonly JsonRows rows = [];

        public JsonTreeView()
        {
            InitializeComponent();

            Rows.ItemsSource = rows;

            // Tunnelled: a ListBox would otherwise take Left and Right for itself, and Enter too.
            Rows.AddHandler(KeyDownEvent, RowKeyDown, RoutingStrategies.Tunnel);
            Rows.DoubleTapped += (_, _) => Toggle(Rows.SelectedItem as JsonRow);

            Show();
        }

        public JsonNode? Document
        {
            get => GetValue(DocumentProperty);
            set => SetValue(DocumentProperty, value);
        }

        public string EmptyText
        {
            get => GetValue(EmptyTextProperty);
            set => SetValue(EmptyTextProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DocumentProperty || change.Property == EmptyTextProperty)
            {
                Show();
            }
        }

        private void Show()
        {
            if (rows.Count > 0 && rows[0].Node == Document)
            {
                // Only the note changed; the tree the user may have opened up stays as it is.
                ShowNote();
                return;
            }

            rows.Load(Document);
            Rows.IsVisible = Document is not null;

            // The root selected, so the arrow keys work the moment the tree is clicked into or
            // tabbed to, rather than after a row has been picked by hand.
            Rows.SelectedIndex = rows.Count > 0 ? 0 : -1;

            ShowNote();
        }

        private void ShowNote()
        {
            Note.Text = EmptyText;
            Note.IsVisible = Document is null && EmptyText.Length > 0;
        }

        private void RowKeyDown(object? sender, KeyEventArgs e)
        {
            if (Rows.SelectedItem is not JsonRow row || e.KeyModifiers != KeyModifiers.None)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Right when row.CanOpen && !row.IsOpen:
                    rows.Open(row);
                    break;

                // Already open: step into it, as a file tree does.
                case Key.Right when row.IsOpen:
                    Select(rows.IndexOf(row) + 1);
                    break;

                case Key.Left when row.IsOpen:
                    rows.Close(row);
                    break;

                case Key.Left when row.Parent is { } parent:
                    Select(rows.IndexOf(parent));
                    break;

                case Key.Enter or Key.Space:
                    Toggle(row);
                    break;

                default:
                    return;
            }

            e.Handled = true;
        }

        private void GlyphTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as Control)?.DataContext is JsonRow row)
            {
                Toggle(row);
                e.Handled = true;
            }
        }

        private void Toggle(JsonRow? row)
        {
            if (row is not null && row.CanOpen)
            {
                rows.Toggle(row);
            }
        }

        private void Select(int index)
        {
            if (index < 0 || index >= rows.Count)
            {
                return;
            }

            Rows.SelectedIndex = index;
            Rows.ScrollIntoView(index);

            if (Rows.ContainerFromIndex(index) is { } container)
            {
                container.Focus(NavigationMethod.Directional);
            }
        }
    }
}
