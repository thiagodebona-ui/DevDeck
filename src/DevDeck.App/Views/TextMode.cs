using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Swaps an output list for plain text that can be selected across lines, and back.
    /// </summary>
    /// <remarks>
    ///  The output panes are virtualised lists with one text block per line, which is what keeps a
    ///  fifty-thousand-line build from stalling the page - and also why a selection could never
    ///  cross from one line into the next. Each line is its own control, and a selection belongs to
    ///  one control. Copying a stack trace meant copying it a line at a time.
    ///
    ///  A single read-only text box holding the same lines is the one control that selects the
    ///  way everyone expects: drag, Shift, Ctrl+A, Ctrl+C. It costs the colour and the live tail,
    ///  so it is a mode the user turns on to copy something rather than how output is shown.
    ///
    ///  A snapshot of the lines at the moment it is turned on. Text appended to a text box resets
    ///  whatever is selected in it, which would make selecting from a running build impossible.
    ///
    ///  The list is hidden by opacity rather than by IsVisible, so it keeps its size: the chain
    ///  output is sized from its own bounds, and a collapsed list would measure as nothing.
    /// </remarks>
    internal static class TextMode
    {
        /// <summary>
        ///  Builds the text box over <paramref name="list"/>, inside the panel that holds it, and
        ///  has <paramref name="toggle"/> switch between the two.
        /// </summary>
        /// <param name="read">The text to show, read each time the mode is turned on.</param>
        public static void Wire(ToggleButton toggle, ListBox list, Func<string> read)
        {
            if (list.Parent is not Panel panel)
            {
                return;
            }

            TextBox text = new()
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                IsVisible = false,
                BorderThickness = new Avalonia.Thickness(0),
                Padding = list.Padding,
                FontSize = 12,
            };

            text.Bind(TemplatedControl.FontFamilyProperty, text.GetResourceObservable("MonoFont"));
            text.Bind(TemplatedControl.BackgroundProperty, text.GetResourceObservable("CodeBg"));
            text.Bind(TemplatedControl.ForegroundProperty, text.GetResourceObservable("Text"));

            panel.Children.Insert(panel.Children.IndexOf(list) + 1, text);

            toggle.IsCheckedChanged += (_, _) =>
            {
                bool on = toggle.IsChecked == true;

                text.Text = on ? read() : string.Empty;
                text.IsVisible = on;
                list.Opacity = on ? 0 : 1;
                list.IsHitTestVisible = !on;

                if (on)
                {
                    text.CaretIndex = 0;
                    text.Focus();
                }
            };

            // A different command or chain is different output, and a snapshot of the last one
            // left on screen over it would be read as this one's.
            list.PropertyChanged += (_, change) =>
            {
                if (change.Property == ItemsControl.ItemsSourceProperty)
                {
                    toggle.IsChecked = false;
                }
            };
        }
    }
}
