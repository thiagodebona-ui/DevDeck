using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  A grid of every icon that can be put on a request or a group.
    /// </summary>
    /// <remarks>
    ///  The tiles are built in code rather than bound to a collection, because the thing being
    ///  drawn is a <see cref="Geometry"/> held in a static field. Binding to it would mean a
    ///  view-model wrapper per icon whose only job is to carry a value that never changes, and a
    ///  converter to get at it - fifty objects and a converter to draw fifty shapes
    ///  that are known at compile time.
    ///
    ///  Returns the chosen name rather than the geometry, because a name is what gets stored. See
    ///  <see cref="Icons.Pickable"/> for why.
    ///
    ///  <para>
    ///   Cancel, escape and the close box all mean "leave it as it was", and clearing it is a
    ///   separate button. Dismissing a dialog by accident should never change anything.
    ///  </para>
    /// </remarks>
    public partial class IconPicker : Window
    {
        /// <summary>What the caller gets back: a name, "" for none, or null for cancelled.</summary>
        private string? chosen;

        public IconPicker() => InitializeComponent();

        private void NoneClick(object? sender, RoutedEventArgs e) => Close(string.Empty);

        private void CancelClick(object? sender, RoutedEventArgs e) => Close(null);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
            }

            base.OnKeyDown(e);
        }

        /// <summary>Fills the grid, marking <paramref name="current"/> if it is one of them.</summary>
        private void Fill(string? current)
        {
            List<Control> tiles = [];

            foreach (KeyValuePair<string, Emblem> icon in Icons.Pickable)
            {
                Button tile = new()
                {
                    Classes = { "tile" },
                    Tag = icon.Key,
                    // Classed for the weight and size in App.axaml, but stroked in the icon's own
                    // colour: the picker should show exactly what the row will wear. Drawn a
                    // little larger than on a row, so the colours are easy to tell apart.
                    Content = new Avalonia.Controls.Shapes.Path
                    {
                        Data = icon.Value.Shape,
                        Classes = { "icon" },
                        Stroke = icon.Value.Ink,
                        Fill = icon.Value.Fill,
                        RenderTransform = new ScaleTransform(1.25, 1.25),
                    },
                    // The name is what is stored, so it is worth being able to read it - for
                    // anyone who wants to type it into settings.json by hand later.
                    [ToolTip.TipProperty] = icon.Key,
                };

                if (current is { Length: > 0 }
                    && string.Equals(current, icon.Key, System.StringComparison.OrdinalIgnoreCase))
                {
                    tile.Classes.Add("chosen");
                }

                tile.Click += (s, _) =>
                {
                    chosen = (s as Button)?.Tag as string;
                    Close(chosen);
                };

                tiles.Add(tile);
            }

            Tiles.ItemsSource = tiles;
        }

        /// <summary>
        ///  Asks for an icon. Returns the name, "" to clear it, or null if the user backed out.
        /// </summary>
        internal static async Task<string?> Ask(Window owner, string heading, string? current)
        {
            IconPicker picker = new();
            picker.Title = heading;
            picker.Fill(current);

            return await picker.ShowDialog<string?>(owner);
        }
    }
}
