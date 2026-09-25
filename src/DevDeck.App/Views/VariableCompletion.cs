using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Types the closing braces of a <c>{{variable}}</c> and offers the names that could go
    ///  between them.
    /// </summary>
    /// <remarks>
    ///  Environment variables are only useful if people remember they exist and spell them the way
    ///  the environment does. Both halves of that were being left to the user: a name had to be
    ///  typed exactly, from memory, with two braces at each end, and getting it wrong produced a
    ///  request that sent the literal text <c>{{baseUrI}}</c> to the server. That is deliberate
    ///  behaviour elsewhere in the code - an unresolved name is left as written rather than
    ///  silently becoming empty - but it means a typo is only discovered as a confusing 404.
    ///
    ///  So: the second <c>{</c> closes its own braces and opens the list. Picking from a list
    ///  cannot produce a name that does not exist, which removes the failure entirely rather than
    ///  reporting it.
    ///
    ///  <para>
    ///   An attached behaviour rather than a control, because the four places that need it - URL,
    ///   header name, header value, body - are ordinary TextBoxes that already have bindings,
    ///   watermarks and paste handlers on them. Replacing them with a custom control to add one
    ///   keyboard shortcut would mean owning all of that too.
    ///  </para>
    ///  <para>
    ///   The popup is deliberately not a ComboBox. A ComboBox owns a selection, and there is no
    ///   selection here: the text box owns the text, and the list is a transient offer that
    ///   disappears whether or not it was used.
    ///  </para>
    /// </remarks>
    internal static class VariableCompletion
    {
        /// <summary>Supplies the names to offer, read at the moment the list opens.</summary>
        /// <remarks>
        ///  A function rather than a list, because the active environment changes underneath the
        ///  view and a list captured at attach time would go stale on the first switch.
        /// </remarks>
        public static readonly AttachedProperty<Func<IEnumerable<string>>?> NamesProperty =
            AvaloniaProperty.RegisterAttached<TextBox, Func<IEnumerable<string>>?>(
                "Names", typeof(VariableCompletion));

        /// <summary>The list currently open, if any. One at a time, app-wide.</summary>
        private static Popup? open;

        /// <summary>The box it belongs to, so a pick knows where to write.</summary>
        private static TextBox? host;

        /// <summary>Where the <c>{{</c> started, so a pick knows what to replace.</summary>
        private static int anchor;

        public static void SetNames(TextBox box, Func<IEnumerable<string>>? value) =>
            box.SetValue(NamesProperty, value);

        public static Func<IEnumerable<string>>? GetNames(TextBox box) =>
            box.GetValue(NamesProperty);

        /// <summary>Starts watching <paramref name="box"/>.</summary>
        public static void Attach(TextBox box, Func<IEnumerable<string>> names)
        {
            SetNames(box, names);

            // Tunnelling, because the TextBox handles a printable key itself and a bubbling
            // handler would run after the character was already in the text.
            box.AddHandler(InputElement.TextInputEvent, Typed, RoutingStrategies.Tunnel);
            box.AddHandler(InputElement.KeyDownEvent, Pressed, RoutingStrategies.Tunnel);
            box.LostFocus += (_, _) => Shut();
        }

        /// <summary>
        ///  The second brace of a pair writes <c>{{}}</c> and puts the caret in the middle.
        /// </summary>
        private static void Typed(object? sender, TextInputEventArgs e)
        {
            if (sender is not TextBox box || e.Text != "{")
            {
                return;
            }

            string text = box.Text ?? string.Empty;
            int caret = box.CaretIndex;

            // Only when this brace closes a pair, and only at the end of what is already there.
            // Auto-closing every single brace would fight anyone typing JSON in the body pane,
            // which is the same text box.
            if (caret <= 0 || caret > text.Length || text[caret - 1] != '{')
            {
                return;
            }

            e.Handled = true;

            box.Text = string.Concat(text.AsSpan(0, caret), "{}}", text.AsSpan(caret));
            box.CaretIndex = caret + 1;

            anchor = caret - 1;
            Offer(box, string.Empty);
        }

        /// <summary>
        ///  Escape shuts the list, and a pick can be made with the keyboard alone.
        /// </summary>
        private static void Pressed(object? sender, KeyEventArgs e)
        {
            if (open is null || sender is not TextBox box)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Escape:
                    Shut();
                    e.Handled = true;
                    return;

                case Key.Down when open.Child is ListBox down:
                    down.SelectedIndex = Math.Min(down.SelectedIndex + 1, down.ItemCount - 1);
                    e.Handled = true;
                    return;

                case Key.Up when open.Child is ListBox up:
                    up.SelectedIndex = Math.Max(up.SelectedIndex - 1, 0);
                    e.Handled = true;
                    return;

                case Key.Enter or Key.Tab when open.Child is ListBox chosen:
                    if (chosen.SelectedItem is string name)
                    {
                        Take(box, name);
                        e.Handled = true;
                    }

                    return;

                default:
                    // Any other key means the user is typing a name; narrow the list to it. The
                    // text has not been updated yet at tunnel time, so this runs a beat later.
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Narrow(box));
                    return;
            }
        }

        /// <summary>Re-filters the open list against whatever is now between the braces.</summary>
        private static void Narrow(TextBox box)
        {
            if (open is null)
            {
                return;
            }

            string text = box.Text ?? string.Empty;

            // The caret has left the braces, or the text was rewritten underneath us.
            if (anchor + 2 > text.Length || box.CaretIndex < anchor + 2)
            {
                Shut();
                return;
            }

            string typed = text[(anchor + 2)..box.CaretIndex];

            if (typed.Contains('}', StringComparison.Ordinal))
            {
                Shut();
                return;
            }

            Offer(box, typed);
        }

        /// <summary>Opens, or re-fills, the list under the caret.</summary>
        private static void Offer(TextBox box, string typed)
        {
            Func<IEnumerable<string>>? source = GetNames(box);

            if (source is null)
            {
                return;
            }

            List<string> names =
            [
                .. source()
                    .Where(name => name.Length > 0)
                    .Where(name => typed.Length == 0
                        || name.Contains(typed, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
            ];

            // Nothing to offer is not a reason to show an empty box. The braces are already
            // typed, which was the other half of the job.
            if (names.Count == 0)
            {
                Shut();
                return;
            }

            if (open?.Child is ListBox already)
            {
                already.ItemsSource = names;
                already.SelectedIndex = 0;
                return;
            }

            ListBox list = new()
            {
                ItemsSource = names,
                SelectedIndex = 0,
                MaxHeight = 190,
                MinWidth = 170,
            };

            list.PointerReleased += (_, _) =>
            {
                if (list.SelectedItem is string picked && host is not null)
                {
                    Take(host, picked);
                }
            };

            open = new Popup
            {
                Child = new Border
                {
                    Child = list,
                    Background = box.FindResource("SteelRaised") as Avalonia.Media.IBrush,
                    BorderBrush = box.FindResource("Line") as Avalonia.Media.IBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(2),
                },
                PlacementTarget = box,
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                HorizontalOffset = 0,
                VerticalOffset = 2,
                // Light dismiss off: focus stays in the text box, so the user can keep typing to
                // narrow the list. Shut() is called from LostFocus and from Escape instead.
                IsLightDismissEnabled = false,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            ((ISetLogicalParent)open).SetParent(box);
            host = box;
            open.IsOpen = true;
        }

        /// <summary>Writes the chosen name between the braces and closes the list.</summary>
        private static void Take(TextBox box, string name)
        {
            string text = box.Text ?? string.Empty;

            if (anchor + 2 > text.Length)
            {
                Shut();
                return;
            }

            int caret = Math.Max(box.CaretIndex, anchor + 2);

            // Everything typed since the braces opened is replaced, so narrowing the list with a
            // few letters and then picking does not leave those letters behind.
            box.Text = string.Concat(text.AsSpan(0, anchor + 2), name, text.AsSpan(caret));
            box.CaretIndex = anchor + 2 + name.Length + 2;

            Shut();
        }

        private static void Shut()
        {
            if (open is null)
            {
                return;
            }

            open.IsOpen = false;
            ((ISetLogicalParent)open).SetParent(null);
            open = null;
            host = null;
        }
    }
}
