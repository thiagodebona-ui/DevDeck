using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using DevDeck.Core;

namespace DevDeck.App
{
    /// <summary>
    ///  Puts a palette on the running application.
    /// </summary>
    /// <remarks>
    ///  This is what V3 was missing, and why the light theme did nothing before.
    ///
    ///  The palette used to be a flat set of brushes declared straight in App.axaml, with every
    ///  view reaching them through <c>StaticResource</c>. A StaticResource is resolved once when
    ///  the control is built and never looked at again, and the brushes were dark whatever the
    ///  variant was - so switching to Light only restyled the parts Fluent draws for us
    ///  (scrollbars, popups, the caret), and left the app itself dark with light scrollbars.
    ///
    ///  Now a palette is a dictionary of its own, merged in at slot zero and swapped whole, and the
    ///  views ask for those keys through <c>DynamicResource</c>, which re-reads on every swap. The
    ///  variant is set alongside it so Fluent's own chrome agrees with the palette it is sitting
    ///  in - a light palette with dark scrollbars is the same bug in miniature.
    /// </remarks>
    internal static class ThemeManager
    {
        /// <summary>Where a <see cref="ResourceInclude"/> resolves its relative paths from.</summary>
        private static readonly Uri BaseUri = new("avares://DevDeck/Themes/");

        /// <summary>
        ///  The palette the app is currently wearing, after System has been resolved.
        /// </summary>
        public static ThemePalette Current { get; private set; } = AppTheme.Dark;

        /// <summary>
        ///  Applies <paramref name="palette"/> to <paramref name="app"/>, resolving System first.
        /// </summary>
        public static void Apply(Application app, ThemePalette palette)
        {
            ThemePalette wanted = AppTheme.Resolve(palette, DesktopIsDark(app));

            // The variant goes on before the dictionary. Fluent rebuilds its own resources when the
            // variant changes, and doing it in the other order makes that rebuild overwrite the
            // palette on the way past.
            app.RequestedThemeVariant = wanted.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

            ResourceInclude include = new(BaseUri) { Source = new Uri(wanted.Source) };

            if (app.Resources.MergedDictionaries.Count == 0)
            {
                app.Resources.MergedDictionaries.Add(include);
            }
            else
            {
                // Slot zero is the palette by convention; anything merged after it is free to
                // override, which is how a view could ship a one-off brush without a theme edit.
                app.Resources.MergedDictionaries[0] = include;
            }

            Dress(app);

            Current = wanted;
        }

        /// <summary>
        ///  The Fluent resource keys that decide how a ComboBox and its drop-down are drawn,
        ///  pointed at the palette's own brushes.
        /// </summary>
        /// <remarks>
        ///  Fluent draws a ComboBox from about forty named brushes of its own, and none of them are
        ///  in the palette - which is why every other surface in the deck followed the theme and the
        ///  one drop-down did not. It opened as a pale Fluent card with Fluent's blue selection in
        ///  it, over a steel panel.
        ///
        ///  Aliased rather than re-templated on purpose. Writing a ControlTheme means owning the
        ///  popup, its placement, the light-dismiss behaviour, the editable text box and the
        ///  keyboard handling forever, for a change that is entirely about colour. Pointing
        ///  Fluent's keys at the palette gets the whole of that behaviour for nothing and cannot
        ///  break the control - the worst case for a key a future Fluent stops reading is that the
        ///  drop-down goes back to looking as it did.
        ///
        ///  In order: the closed face, its border, the text on it, the glyph, the panel that opens,
        ///  and the rows in it.
        /// </remarks>
        private static readonly (string Fluent, string Palette)[] Aliases =
        [
            ("ComboBoxBackground", "Steel"),
            ("ComboBoxBackgroundUnfocused", "Steel"),
            ("ComboBoxBackgroundPointerOver", "SteelHover"),
            ("ComboBoxBackgroundPressed", "SteelRaised"),
            ("ComboBoxBackgroundDisabled", "Steel"),

            ("ComboBoxBorderBrush", "Line"),
            ("ComboBoxBorderBrushPointerOver", "Line"),
            ("ComboBoxBorderBrushPressed", "Accent"),
            ("ComboBoxBorderBrushDisabled", "Line"),
            ("ComboBoxBackgroundBorderBrushFocused", "Accent"),
            ("ComboBoxBackgroundBorderBrushUnfocused", "Line"),

            ("ComboBoxForeground", "Text"),
            ("ComboBoxForegroundFocused", "Text"),
            ("ComboBoxForegroundFocusedPressed", "Text"),
            ("ComboBoxForegroundDisabled", "TextFaint"),
            ("ComboBoxPlaceHolderForeground", "TextFaint"),
            ("ComboBoxPlaceHolderForegroundFocusedPressed", "TextFaint"),

            ("ComboBoxDropDownGlyphForeground", "TextMuted"),
            ("ComboBoxDropDownGlyphForegroundFocused", "Accent"),
            ("ComboBoxDropDownGlyphForegroundFocusedPressed", "Accent"),
            ("ComboBoxDropDownGlyphForegroundDisabled", "TextFaint"),

            // The panel that opens. A step lighter than the page rather than the same shade, so a
            // list dropped over a panel of its own colour still has an edge.
            ("ComboBoxDropDownBackground", "SteelRaised"),
            ("ComboBoxDropDownBorderBrush", "Line"),

            // Rows. Unselected ones are the panel itself - a row with its own background is a stack
            // of boxes, and a list is easier to read as a column of text.
            ("ComboBoxItemBackground", "Transparent"),
            ("ComboBoxItemBackgroundDisabled", "Transparent"),
            ("ComboBoxItemBackgroundPointerOver", "SteelHover"),
            ("ComboBoxItemBackgroundPressed", "Steel"),
            ("ComboBoxItemBackgroundSelected", "Steel"),
            ("ComboBoxItemBackgroundSelectedPointerOver", "SteelHover"),
            ("ComboBoxItemBackgroundSelectedPressed", "Steel"),
            ("ComboBoxItemBackgroundSelectedDisabled", "Steel"),

            // The selected row is marked in brass rather than by a filled bar. A full accent fill
            // across a row of a list that is mostly command names and file paths is a great deal of
            // colour for one line, and the text changing is enough to find it.
            ("ComboBoxItemForeground", "Text"),
            ("ComboBoxItemForegroundPointerOver", "Text"),
            ("ComboBoxItemForegroundPressed", "Text"),
            ("ComboBoxItemForegroundSelected", "Accent"),
            ("ComboBoxItemForegroundSelectedPointerOver", "Accent"),
            ("ComboBoxItemForegroundSelectedPressed", "Accent"),
            ("ComboBoxItemForegroundDisabled", "TextFaint"),
            ("ComboBoxItemForegroundSelectedDisabled", "TextFaint"),

            // Fluent outlines the row under the pointer on top of filling it. Two marks for one
            // state reads as a box being dragged down the list.
            ("ComboBoxItemBorderBrushPointerOver", "Transparent"),
            ("ComboBoxItemBorderBrushPressed", "Transparent"),
            ("ComboBoxItemBorderBrushSelected", "Transparent"),
            ("ComboBoxItemBorderBrushSelectedPointerOver", "Transparent"),
            ("ComboBoxItemBorderBrushSelectedPressed", "Transparent"),
            ("ComboBoxItemBorderBrushDisabled", "Transparent"),
            ("ComboBoxItemBorderBrushSelectedDisabled", "Transparent"),
        ];

        /// <summary>
        ///  Re-points Fluent's own control resources at the palette that has just been applied.
        /// </summary>
        /// <remarks>
        ///  In code rather than in the theme files because the answer is the same for all nine of
        ///  them - it is "whatever this palette calls Steel" - and writing it out per theme would be
        ///  four hundred lines that all say the same thing and drift the first time one is edited.
        ///
        ///  Written into <c>app.Resources</c> directly rather than into the merged palette, so that
        ///  the next theme swap - which replaces slot zero whole - leaves them in place to be
        ///  rewritten here against the new palette.
        /// </remarks>
        private static void Dress(Application app)
        {
            foreach ((string fluent, string palette) in Aliases)
            {
                if (palette == "Transparent")
                {
                    app.Resources[fluent] = Brushes.Transparent;

                    continue;
                }

                if (app.TryGetResource(palette, app.ActualThemeVariant, out object? brush)
                    && brush is not null)
                {
                    app.Resources[fluent] = brush;
                }
            }

            // Metrics, which have no palette equivalent and are the same in every theme. The deck
            // draws its own buttons at a 2px radius with 11,5 of padding, and a picker sitting in a
            // row with them should not be the one control wearing Fluent's rounder, taller shape.
            app.Resources["ComboBoxPadding"] = new Thickness(11, 5);
            app.Resources["ComboBoxMinHeight"] = 30d;
            app.Resources["ComboBoxItemThemePadding"] = new Thickness(11, 7);
            app.Resources["ComboBoxDropdownBorderPadding"] = new Thickness(3);
            app.Resources["ComboBoxDropdownBorderThickness"] = new Thickness(1);
            app.Resources["ComboBoxDropdownContentMargin"] = new Thickness(0);

            // Shared with flyouts and tooltips, deliberately: the drop-down should not be the only
            // thing in the app that opens with a different corner on it.
            app.Resources["ControlCornerRadius"] = new CornerRadius(2);
            app.Resources["OverlayCornerRadius"] = new CornerRadius(3);
        }

        /// <summary>
        ///  What the desktop is set to, for the System palette.
        /// </summary>
        /// <remarks>
        ///  PlatformSettings is the only thing that knows; ActualThemeVariant would just hand back
        ///  whatever we last asked for, which would pin System to its own previous answer.
        /// </remarks>
        private static bool DesktopIsDark(Application app)
        {
            try
            {
                return app.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
            }
            catch (Exception)
            {
                // No platform settings on this backend - headless, or a very old desktop session.
                return true;
            }
        }
    }
}
