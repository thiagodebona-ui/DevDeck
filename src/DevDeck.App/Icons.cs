using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace DevDeck.App
{
    /// <summary>
    ///  Every icon the app draws, as geometry ready to bind to a Path.
    /// </summary>
    /// <remarks>
    ///  Parsed path data in a C# field rather than an icon font or a folder of SVGs, for three
    ///  reasons that all point the same way.
    ///
    ///  An icon font would have to ship, be installed, and be found - and when it is not found the
    ///  app draws a column of empty rectangles rather than nothing, which is worse than no icons at
    ///  all. Emoji, the other tempting shortcut, are drawn by the system: the same character is a
    ///  flat glyph on one desktop and a full-colour cartoon on the next, and neither follows the
    ///  theme. These are strokes in the current foreground colour, so they go dark with the theme
    ///  and brass with the selection, like everything else here.
    ///
    ///  They are deliberately one weight and one size: sixteen points square, drawn on a sixteen
    ///  point grid, stroked rather than filled, with no detail that survives being shrunk. A set
    ///  that is consistent matters more than any single icon being clever - the rail is read as a
    ///  column, and one icon heavier than its neighbours pulls the eye to the wrong row.
    ///
    ///  Parsed once here rather than left as strings for the binding to convert: a Geometry is
    ///  immutable in use and safe to share between every Path that draws it, and parsing at startup
    ///  turns a typo in this file into an exception on the first frame rather than into an icon
    ///  that silently draws nothing.
    /// </remarks>
    internal static class Icons
    {
        /// <summary>A terminal: a window with a prompt chevron and a typed line.</summary>
        public static readonly Geometry Commands = Geometry.Parse(
            "M2 3.5 H14 V12.5 H2 Z M4.8 6.4 L7 8 L4.8 9.6 M8.6 10.2 H11.4");

        /// <summary>A speech bubble with a tail, for the thing you talk to.</summary>
        public static readonly Geometry Assistant = Geometry.Parse(
            "M2 3.5 H14 V10.5 H7.5 L4.5 13.2 V10.5 H2 Z M5 6 H11 M5 8.2 H9");

        /// <summary>A globe: the wire frame, not the continents, which do not survive 16 points.</summary>
        public static readonly Geometry Http = Geometry.Parse(
            "M8 2 A6 6 0 1 1 7.99 2 Z M2 8 H14 M8 2 A5 6 0 0 1 8 14 A5 6 0 0 1 8 2");

        /// <summary>A spanner, held at the angle every toolbox icon has held one since 1995.</summary>
        public static readonly Geometry Toolbox = Geometry.Parse(
            "M10.6 2.2 A3.6 3.6 0 0 0 7.3 7.3 L2.6 12 A1.6 1.6 0 0 0 4.9 14.2 L9.5 9.5 "
            + "A3.6 3.6 0 0 0 14 6.2 L11.6 8.2 L9.3 5.9 Z");

        /// <summary>Stacked layers with a play mark: things that are running, not things you start.</summary>
        public static readonly Geometry Running = Geometry.Parse(
            "M2.5 4.2 L8 2 L13.5 4.2 L8 6.4 Z M2.5 8 L8 10.2 L13.5 8 M2.5 11.4 L8 13.6 L13.5 11.4");

        /// <summary>A bolt. Automation is the panel where something happens without you.</summary>
        public static readonly Geometry Automation = Geometry.Parse(
            "M9.2 1.8 L3.4 8.8 H7.4 L6.8 14.2 L12.6 7.2 H8.6 Z");

        /// <summary>A clipboard, with the clip drawn as its own shape so it reads at this size.</summary>
        public static readonly Geometry Clipboard = Geometry.Parse(
            "M6 2.6 H10 V4.6 H6 Z M6 3.6 H3.8 V13.6 H12.2 V3.6 H10 M5.8 7.4 H10.2 M5.8 10 H9");

        /// <summary>Lines of text, getting shorter: a log, rather than a document.</summary>
        public static readonly Geometry Log = Geometry.Parse(
            "M3 3.6 H13 M3 6.4 H13 M3 9.2 H10.5 M3 12 H8");

        /// <summary>A chip with its legs. The one icon everybody reads as "the machine itself".</summary>
        public static readonly Geometry Memory = Geometry.Parse(
            "M5 5 H11 V11 H5 Z M6.6 2.4 V5 M9.4 2.4 V5 M6.6 11 V13.6 M9.4 11 V13.6 "
            + "M2.4 6.6 H5 M2.4 9.4 H5 M11 6.6 H13.6 M11 9.4 H13.6");

        /// <summary>Two sliders. Not a cog: a cog says "machinery", sliders say "your choices".</summary>
        public static readonly Geometry Settings = Geometry.Parse(
            "M2.5 5.2 H13.5 M2.5 10.8 H13.5 "
            + "M6.6 5.2 A1.5 1.5 0 1 1 6.59 5.2 Z M10.4 10.8 A1.5 1.5 0 1 1 10.39 10.8 Z");

        /// <summary>A framed page of notes: what each version changed.</summary>
        public static readonly Geometry Changelog = Geometry.Parse(
            "M3.4 2.2 H12.6 V13.8 H3.4 Z M5.6 5 H10.4 M5.6 7.6 H10.4 M5.6 10.2 H8.4");

        /// <summary>A triangle pointing right, for every button that starts something.</summary>
        public static readonly Geometry Run = Geometry.Parse(
            "M4.5 2.8 L12.5 8 L4.5 13.2 Z");

        /// <summary>A filled square, for every button that ends something.</summary>
        public static readonly Geometry Stop = Geometry.Parse(
            "M4 4 H12 V12 H4 Z");

        /// <summary>A plus, for every button that makes one more of something.</summary>
        public static readonly Geometry Add = Geometry.Parse(
            "M8 3 V13 M3 8 H13");

        /// <summary>A circling arrow, for anything that goes and asks again.</summary>
        public static readonly Geometry Refresh = Geometry.Parse(
            "M13 8 A5 5 0 1 1 11.2 4.2 M13.4 1.8 V4.6 H10.6");

        /// <summary>Two sheets, one behind the other: copy.</summary>
        public static readonly Geometry Copy = Geometry.Parse(
            "M5.5 2.6 H13 V10.2 H5.5 Z M10.5 10.2 V13.4 H3 V5.8 H5.5");

        /// <summary>A magnifier, for every box you type a filter into.</summary>
        public static readonly Geometry Search = Geometry.Parse(
            "M7.2 2.4 A4.8 4.8 0 1 1 7.19 2.4 Z M10.7 10.7 L13.8 13.8");

        /// <summary>A folder, for anything that opens or picks one.</summary>
        public static readonly Geometry Folder = Geometry.Parse(
            "M2.4 12.6 V4 H6.6 L8.2 5.8 H13.6 V12.6 Z");

        /// <summary>A bin, for delete. Lidded, so it is not mistaken for a cup.</summary>
        public static readonly Geometry Delete = Geometry.Parse(
            "M3.4 4.6 H12.6 M6.4 4.6 V3 H9.6 V4.6 M4.6 4.6 V13 H11.4 V4.6 M6.8 7 V10.8 M9.2 7 V10.8");

        #region Movement

        /// <summary>A chevron, for moving a row up a list.</summary>
        public static readonly Geometry Up = Geometry.Parse("M4 10 L8 6 L12 10");

        /// <summary>A chevron, for moving a row down a list.</summary>
        public static readonly Geometry Down = Geometry.Parse("M4 6 L8 10 L12 6");

        public static readonly Geometry Left = Geometry.Parse("M10 4 L6 8 L10 12");

        public static readonly Geometry Right = Geometry.Parse("M6 4 L10 8 L6 12");

        /// <summary>A cross, for closing and for removing one row of several.</summary>
        public static readonly Geometry Close = Geometry.Parse("M4 4 L12 12 M12 4 L4 12");

        /// <summary>A tick, for anything that has passed or is switched on.</summary>
        public static readonly Geometry Check = Geometry.Parse("M3.4 8.4 L6.4 11.4 L12.6 4.8");

        #endregion

        #region The picker set

        // Everything below exists to be chosen rather than to be placed by hand: a request or a
        // group is given one, and the name is what settles in settings.json. They are drawn to the
        // same rules as the rail icons above - one weight, sixteen points, no detail that dies when
        // shrunk - because the two sets sit in the same window, and a picker full of finer artwork
        // than the rail would make the rail look like a mistake.
        //
        // They are not a complete iconography of anything. They are the shapes somebody reaches for
        // when labelling their own work: the environments, the parts of a system, the states a
        // thing can be in. Every one has to be recognisable as a silhouette at sixteen points,
        // which rules out most cleverness and all text.

        /// <summary>A floppy disk. Still the only shape everyone reads as "save".</summary>
        public static readonly Geometry Save = Geometry.Parse(
            "M3 3 H11.4 L13 4.6 V13 H3 Z M5.4 3 V6.6 H10.6 V3 M5.4 13 V9.4 H10.6 V13");

        /// <summary>A pencil, for edit.</summary>
        public static readonly Geometry Edit = Geometry.Parse(
            "M11.2 2.6 L13.4 4.8 L5.6 12.6 L2.6 13.4 L3.4 10.4 Z M9.8 4 L12 6.2");

        public static readonly Geometry Star = Geometry.Parse(
            "M8 2.4 L9.85 6.15 L14 6.75 L11 9.65 L11.7 13.7 L8 11.8 L4.3 13.7 L5 9.65 L2 6.75 L6.15 6.15 Z");

        public static readonly Geometry Heart = Geometry.Parse(
            "M8 13.4 C8 13.4 2 9.8 2 6.2 A3 3 0 0 1 8 5 A3 3 0 0 1 14 6.2 C14 9.8 8 13.4 8 13.4 Z");

        public static readonly Geometry Bookmark = Geometry.Parse(
            "M4 2.6 H12 V13.6 L8 10.4 L4 13.6 Z");

        /// <summary>A luggage tag, for labelling and for anything versioned.</summary>
        public static readonly Geometry Tag = Geometry.Parse(
            "M2.6 2.6 H8 L13.4 8 L8 13.4 L2.6 8 Z M5.2 5.2 H5.21");

        public static readonly Geometry Lock = Geometry.Parse(
            "M3.6 7.2 H12.4 V13.4 H3.6 Z M5.6 7.2 V5 A2.4 2.4 0 0 1 10.4 5 V7.2");

        /// <summary>The same padlock with its shackle swung open.</summary>
        public static readonly Geometry Unlock = Geometry.Parse(
            "M3.6 7.2 H12.4 V13.4 H3.6 Z M5.6 7.2 V5 A2.4 2.4 0 0 1 10.4 5");

        public static readonly Geometry Key = Geometry.Parse(
            "M6.4 6.4 A2.6 2.6 0 1 1 6.39 6.4 Z M8.2 8.2 L13.4 13.4 M11.2 11.2 L12.8 9.6");

        public static readonly Geometry Shield = Geometry.Parse(
            "M8 2.2 L13.2 4.2 V8 C13.2 11 10.8 13.2 8 14 C5.2 13.2 2.8 11 2.8 8 V4.2 Z");

        /// <summary>One person, head and shoulders.</summary>
        public static readonly Geometry User = Geometry.Parse(
            "M8 3 A2.6 2.6 0 1 1 7.99 3 Z M3 13.6 A5 5 0 0 1 13 13.6");

        /// <summary>Two of them, for anything shared or belonging to a team.</summary>
        public static readonly Geometry Users = Geometry.Parse(
            "M6.4 3.4 A2.2 2.2 0 1 1 6.39 3.4 Z M1.8 13.2 A4.6 4.6 0 0 1 11 13.2 M10.8 3.6 A2.2 2.2 0 0 1 10.8 7.8 M12 9.2 A4.4 4.4 0 0 1 14.2 13.2");

        public static readonly Geometry Mail = Geometry.Parse(
            "M2.2 4 H13.8 V12 H2.2 Z M2.2 4.4 L8 9 L13.8 4.4");

        public static readonly Geometry Bell = Geometry.Parse(
            "M4.4 11.4 V7.4 A3.6 3.6 0 0 1 11.6 7.4 V11.4 L12.8 12.6 H3.2 Z M6.8 12.6 A1.4 1.4 0 0 0 9.2 12.6");

        public static readonly Geometry Calendar = Geometry.Parse(
            "M2.6 3.8 H13.4 V13.4 H2.6 Z M2.6 6.8 H13.4 M5.6 2.4 V5 M10.4 2.4 V5");

        public static readonly Geometry Clock = Geometry.Parse(
            "M8 2.4 A5.6 5.6 0 1 1 7.99 2.4 Z M8 4.6 V8 L10.4 9.8");

        /// <summary>A meridian and a parallel: the web, and anything public.</summary>
        public static readonly Geometry Globe = Geometry.Parse(
            "M8 2.4 A5.6 5.6 0 1 1 7.99 2.4 Z M2.4 8 H13.6 M8 2.4 A7.4 7.4 0 0 1 8 13.6 A7.4 7.4 0 0 1 8 2.4");

        public static readonly Geometry Cloud = Geometry.Parse(
            "M4.6 12 A2.8 2.8 0 0 1 4.9 6.5 A3.8 3.8 0 0 1 12 6.9 A2.6 2.6 0 0 1 11.6 12 Z");

        public static readonly Geometry Download = Geometry.Parse(
            "M8 2.6 V10.2 M4.8 7 L8 10.2 L11.2 7 M3 13.2 H13");

        public static readonly Geometry Upload = Geometry.Parse(
            "M8 10.2 V2.6 M4.8 5.8 L8 2.6 L11.2 5.8 M3 13.2 H13");

        /// <summary>Two links of a chain, for URLs and for anything that points elsewhere.</summary>
        public static readonly Geometry Link = Geometry.Parse(
            "M6.6 9.4 L9.4 6.6 M6.9 4.6 L8.4 3.1 A2.6 2.6 0 0 1 12.9 7.6 L11.4 9.1 M9.1 11.4 L7.6 12.9 A2.6 2.6 0 0 1 3.1 8.4 L4.6 6.9");

        public static readonly Geometry Database = Geometry.Parse(
            "M3 4 A5 2 0 1 1 13 4 A5 2 0 1 1 3 4 M3 4 V12 A5 2 0 0 0 13 12 V4 M3 8 A5 2 0 0 0 13 8");

        /// <summary>Two rack units with a status lamp each.</summary>
        public static readonly Geometry Server = Geometry.Parse(
            "M2.6 3 H13.4 V7 H2.6 Z M2.6 9 H13.4 V13 H2.6 Z M4.8 5 H4.81 M4.8 11 H4.81");

        /// <summary>Angle brackets: anything that is source.</summary>
        public static readonly Geometry Code = Geometry.Parse(
            "M5.4 5 L2.2 8 L5.4 11 M10.6 5 L13.8 8 L10.6 11 M9.4 3.2 L6.6 12.8");

        /// <summary>A commit line with a branch coming off it.</summary>
        public static readonly Geometry Branch = Geometry.Parse(
            "M4.6 4.4 A1.6 1.6 0 1 1 4.59 4.4 Z M4.6 11.6 A1.6 1.6 0 1 1 4.59 11.6 Z M11.4 4.4 A1.6 1.6 0 1 1 11.39 4.4 Z M4.6 6 V10 M11.4 6 V7 A2.6 2.6 0 0 1 8.8 9.6 H6.2");

        public static readonly Geometry Bug = Geometry.Parse(
            "M5.2 6 A2.8 2.8 0 0 1 10.8 6 V10.4 A2.8 2.8 0 0 1 5.2 10.4 Z M5.2 8.2 H2.6 M10.8 8.2 H13.4 M5.6 5 L4 3.4 M10.4 5 L12 3.4 M5.6 11.8 L4 13.4 M10.4 11.8 L12 13.4");

        /// <summary>A conical flask, for experiments and for anything staging.</summary>
        public static readonly Geometry Flask = Geometry.Parse(
            "M6.4 2.4 V6.4 L2.8 12.4 A1 1 0 0 0 3.7 13.8 H12.3 A1 1 0 0 0 13.2 12.4 L9.6 6.4 V2.4 M5.6 2.4 H10.4 M4.6 9.6 H11.4");

        public static readonly Geometry Rocket = Geometry.Parse(
            "M8 1.8 C10.4 4 11.4 6.6 11.4 9.4 L8 12.2 L4.6 9.4 C4.6 6.6 5.6 4 8 1.8 Z M8 6.4 A1.2 1.2 0 1 1 7.99 6.4 Z M4.6 9.4 L2.8 13.6 L5.6 12.2 M11.4 9.4 L13.2 13.6 L10.4 12.2");

        public static readonly Geometry Flame = Geometry.Parse(
            "M8 1.8 C8 5 11 5.4 11 9 A3 3 0 0 1 5 9 C5 7.6 5.8 6.8 5.8 6.8 C5.8 8.6 7 8.8 7 7.4 C7 5.6 8 4.4 8 1.8 Z");

        /// <summary>A lightning bolt, for anything fast or automatic.</summary>
        public static readonly Geometry Bolt = Geometry.Parse("M9.4 1.8 L4 8.8 H8 L6.6 14.2 L12 7.2 H8 Z");

        public static readonly Geometry Eye = Geometry.Parse(
            "M1.6 8 C3.4 5 5.6 3.6 8 3.6 C10.4 3.6 12.6 5 14.4 8 C12.6 11 10.4 12.4 8 12.4 C5.6 12.4 3.4 11 1.6 8 Z M8 6 A2 2 0 1 1 7.99 6 Z");

        public static readonly Geometry Filter = Geometry.Parse("M2.2 3.4 H13.8 L9.2 8.6 V13 L6.8 11.6 V8.6 Z");

        /// <summary>Three bars. Anything measured, reported or timed.</summary>
        public static readonly Geometry Chart = Geometry.Parse(
            "M2.6 13.4 H13.4 M5 13.4 V8.4 M8 13.4 V4.6 M11 13.4 V10");

        public static readonly Geometry Grid = Geometry.Parse(
            "M2.6 2.6 H7 V7 H2.6 Z M9 2.6 H13.4 V7 H9 Z M2.6 9 H7 V13.4 H2.6 Z M9 9 H13.4 V13.4 H9 Z");

        public static readonly Geometry List = Geometry.Parse(
            "M5.6 4.2 H13.4 M5.6 8 H13.4 M5.6 11.8 H13.4 M2.8 4.2 H2.81 M2.8 8 H2.81 M2.8 11.8 H2.81");

        /// <summary>A sheet with a folded corner.</summary>
        public static readonly Geometry File = Geometry.Parse(
            "M3.4 2.2 H9.4 L12.6 5.4 V13.8 H3.4 Z M9.4 2.2 V5.4 H12.6");

        /// <summary>A frame with a sun and a hill in it.</summary>
        public static readonly Geometry Image = Geometry.Parse(
            "M2.6 3.4 H13.4 V12.6 H2.6 Z M5.6 6.6 A0.9 0.9 0 1 1 5.59 6.6 Z M2.6 11 L6.2 7.8 L9 10 L11 8.4 L13.4 10.4");

        public static readonly Geometry Video = Geometry.Parse(
            "M2.2 4.2 H10 V11.8 H2.2 Z M10 7 L13.8 4.6 V11.4 L10 9 Z");

        public static readonly Geometry Music = Geometry.Parse(
            "M4.6 12.2 A1.7 1.7 0 1 1 4.59 12.2 Z M11.6 10.8 A1.7 1.7 0 1 1 11.59 10.8 Z M6.3 12.2 V4.2 L13.3 2.8 V10.8");

        public static readonly Geometry Phone = Geometry.Parse(
            "M4.6 2.6 H11.4 V13.4 H4.6 Z M6.8 11.6 H9.2");

        /// <summary>A map pin, for places and for anything local.</summary>
        public static readonly Geometry Pin = Geometry.Parse(
            "M8 1.8 A4.2 4.2 0 0 1 12.2 6 C12.2 9 8 14.2 8 14.2 C8 14.2 3.8 9 3.8 6 A4.2 4.2 0 0 1 8 1.8 Z M8 4.6 A1.5 1.5 0 1 1 7.99 4.6 Z");

        public static readonly Geometry Flag = Geometry.Parse(
            "M3.6 14 V2.4 M3.6 3 H12.4 L10.6 6 L12.4 9 H3.6");

        public static readonly Geometry Gift = Geometry.Parse(
            "M2.6 6.6 H13.4 V9 H2.6 Z M3.8 9 H12.2 V13.4 H3.8 Z M8 6.6 V13.4 M8 6.6 C6.8 4.2 4 3.4 4 5.2 C4 6.2 5.6 6.6 8 6.6 C10.4 6.6 12 6.2 12 5.2 C12 3.4 9.2 4.2 8 6.6 Z");

        /// <summary>A carton, for builds and for anything shipped.</summary>
        public static readonly Geometry Package = Geometry.Parse(
            "M8 2 L13.6 4.8 V11.2 L8 14 L2.4 11.2 V4.8 Z M2.4 4.8 L8 7.6 L13.6 4.8 M8 7.6 V14");

        public static readonly Geometry Cart = Geometry.Parse(
            "M1.8 2.8 H3.8 L5.6 10.2 H12.2 L13.8 5.2 H4.4 M6.2 13 A0.9 0.9 0 1 1 6.19 13 Z M11.6 13 A0.9 0.9 0 1 1 11.59 13 Z");

        public static readonly Geometry Money = Geometry.Parse(
            "M1.8 4 H14.2 V12 H1.8 Z M8 6 A2 2 0 1 1 7.99 6 Z M4.4 6.4 H4.41 M11.6 9.6 H11.61");

        public static readonly Geometry Home = Geometry.Parse(
            "M1.8 7.6 L8 2 L14.2 7.6 M3.6 6.4 V13.6 H12.4 V6.4 M6.6 13.6 V9.4 H9.4 V13.6");

        public static readonly Geometry Map = Geometry.Parse(
            "M1.8 3.8 L5.8 2.4 L10.2 4.2 L14.2 2.8 V12.2 L10.2 13.6 L5.8 11.8 L1.8 13.2 Z M5.8 2.4 V11.8 M10.2 4.2 V13.6");

        public static readonly Geometry Compass = Geometry.Parse(
            "M8 2.4 A5.6 5.6 0 1 1 7.99 2.4 Z M10.6 5.4 L9.2 9.2 L5.4 10.6 L6.8 6.8 Z");

        public static readonly Geometry Wrench = Geometry.Parse(
            "M13 3.2 L10.6 5.6 L8.8 5.4 L8.6 3.6 L11 1.2 A4.2 4.2 0 0 0 5.6 6.6 L2 10.2 A1.8 1.8 0 0 0 4.6 12.8 L8.2 9.2 A4.2 4.2 0 0 0 13 3.2 Z");

        public static readonly Geometry Hammer = Geometry.Parse(
            "M9 2.6 L13.4 7 L11.6 8.8 L10.2 7.4 L4.4 13.2 A1.6 1.6 0 0 1 2.2 11 L8 5.2 L6.6 3.8 Z");

        public static readonly Geometry Puzzle = Geometry.Parse(
            "M6 2.4 H10 V4.4 A1.4 1.4 0 1 1 10 7.2 V9.2 H7.8 A1.4 1.4 0 1 0 5 9.2 H2.8 V5.2 H4.8 A1.4 1.4 0 1 1 6 3.2 Z");

        /// <summary>A head with a seam down it. The assistant, and anything it does.</summary>
        public static readonly Geometry Brain = Geometry.Parse(
            "M8 3 A3 3 0 0 0 3.4 6.4 A2.4 2.4 0 0 0 4.4 11 A2.8 2.8 0 0 0 8 13.4 A2.8 2.8 0 0 0 11.6 11 A2.4 2.4 0 0 0 12.6 6.4 A3 3 0 0 0 8 3 Z M8 3 V13.4");

        /// <summary>Three arcs and a dot.</summary>
        public static readonly Geometry Wifi = Geometry.Parse(
            "M1.8 6.2 A9 9 0 0 1 14.2 6.2 M4.2 8.8 A5.6 5.6 0 0 1 11.8 8.8 M6.4 11.2 A2.4 2.4 0 0 1 9.6 11.2 M8 13.6 H8.01");

        /// <summary>A power symbol, for environments and for anything that is on or off.</summary>
        public static readonly Geometry Power = Geometry.Parse(
            "M8 2.4 V7.6 M4.8 4.4 A5 5 0 1 0 11.2 4.4");

        /// <summary>A gauge with its needle over, for load and for production.</summary>
        public static readonly Geometry Gauge = Geometry.Parse(
            "M2.4 11.4 A5.6 5.6 0 1 1 13.6 11.4 M8 11.4 L11 6.6");

        /// <summary>A sun, for a light theme and for anything daily.</summary>
        public static readonly Geometry Sun = Geometry.Parse(
            "M5.6 8 A2.4 2.4 0 1 1 5.59 8 Z M8 1.6 V3.2 M8 12.8 V14.4 M1.6 8 H3.2 M12.8 8 H14.4 M3.5 3.5 L4.6 4.6 M11.4 11.4 L12.5 12.5 M12.5 3.5 L11.4 4.6 M4.6 11.4 L3.5 12.5");

        /// <summary>A crescent moon, for a dark theme and for anything nightly.</summary>
        public static readonly Geometry Moon = Geometry.Parse(
            "M13 9.8 A5.6 5.6 0 1 1 6.6 3 A4.4 4.4 0 0 0 13 9.8 Z");

        /// <summary>A four-pointed glint with a small one beside it, for anything new or clever.</summary>
        public static readonly Geometry Sparkle = Geometry.Parse(
            "M7 2.4 C7.5 6 8.6 7.3 12 7.8 C8.6 8.3 7.5 9.6 7 13.2 C6.5 9.6 5.4 8.3 2 7.8 C5.4 7.3 6.5 6 7 2.4 Z M12.6 1.8 V4.6 M11.2 3.2 H14");

        /// <summary>Two rings and a bullseye, for goals and for whatever is being aimed at.</summary>
        public static readonly Geometry Target = Geometry.Parse(
            "M8 2.2 A5.8 5.8 0 1 1 7.99 2.2 Z M8 5 A3 3 0 1 1 7.99 5 Z M8 8 H8.01");

        public static readonly Geometry Trophy = Geometry.Parse(
            "M4.6 2.4 H11.4 V6 A3.4 3.4 0 0 1 4.6 6 Z M4.6 3.6 H2.4 C2.4 6 3.4 7 4.8 7.2 M11.4 3.6 H13.6 C13.6 6 12.6 7 11.2 7.2 M8 9.4 V11.6 M5.2 13.6 H10.8 L10.2 11.6 H5.8 Z");

        /// <summary>A chip with its pins, for hardware and for anything compute-bound.</summary>
        public static readonly Geometry Cpu = Geometry.Parse(
            "M4.4 4.4 H11.6 V11.6 H4.4 Z M6.6 6.6 H9.4 V9.4 H6.6 Z M6.4 2 V4.4 M9.6 2 V4.4 M6.4 11.6 V14 M9.6 11.6 V14 M2 6.4 H4.4 M2 9.6 H4.4 M11.6 6.4 H14 M11.6 9.6 H14");

        /// <summary>A window with a prompt in it, for shells and scripts.</summary>
        public static readonly Geometry Terminal = Geometry.Parse(
            "M1.8 2.8 H14.2 V13.2 H1.8 Z M4.4 6 L6.6 8 L4.4 10 M8 10.4 H11.4");

        /// <summary>A pair of curly braces, for JSON and for APIs.</summary>
        public static readonly Geometry Braces = Geometry.Parse(
            "M5.6 2.4 C3.8 2.4 4.4 5.6 4.4 6.6 C4.4 7.6 3.8 8 2.6 8 C3.8 8 4.4 8.4 4.4 9.4 C4.4 10.4 3.8 13.6 5.6 13.6 M10.4 2.4 C12.2 2.4 11.6 5.6 11.6 6.6 C11.6 7.6 12.2 8 13.4 8 C12.2 8 11.6 8.4 11.6 9.4 C11.6 10.4 12.2 13.6 10.4 13.6");

        /// <summary>Three stacked sheets, for staging, tiers and anything with levels.</summary>
        public static readonly Geometry Layers = Geometry.Parse(
            "M8 2 L14 5 L8 8 L2 5 Z M2 8 L8 11 L14 8 M2 11 L8 14 L14 11");

        /// <summary>A paper plane, for anything that posts or notifies.</summary>
        public static readonly Geometry Send = Geometry.Parse(
            "M14 2 L1.8 7 L6.6 9.4 L9 14.2 Z M14 2 L6.6 9.4");

        /// <summary>A heartbeat trace, for health checks and monitoring.</summary>
        public static readonly Geometry Pulse = Geometry.Parse(
            "M1.6 8.4 H4.6 L6.4 4 L9.2 12.4 L11 8.4 H14.4");

        /// <summary>A light bulb, for ideas and experiments.</summary>
        public static readonly Geometry Bulb = Geometry.Parse(
            "M8 1.8 A4.4 4.4 0 0 1 10.6 9.8 V11.4 H5.4 V9.8 A4.4 4.4 0 0 1 8 1.8 Z M6 13.6 H10");

        /// <summary>A medical cross, for clinical systems and patient data.</summary>
        public static readonly Geometry Medical = Geometry.Parse(
            "M6 2.4 H10 V6 H13.6 V10 H10 V13.6 H6 V10 H2.4 V6 H6 Z");

        /// <summary>A cup with steam over it, for the calls that are only for fun.</summary>
        public static readonly Geometry Coffee = Geometry.Parse(
            "M2.8 6 H11 V10 A3.2 3.2 0 0 1 7.8 13.2 H6 A3.2 3.2 0 0 1 2.8 10 Z M11 7 H12.2 A1.8 1.8 0 0 1 12.2 10.6 H11 M5.4 2 V3.8 M8.4 2 V3.8");

        #endregion

        #region Choosing one

        /// <summary>
        ///  Every icon a request or a group may be given, by the name that is stored, with the
        ///  colour it is drawn in.
        /// </summary>
        /// <remarks>
        ///  The name is what goes into settings.json rather than the path data, so that an icon can
        ///  be redrawn here without touching anybody's saved requests, and so that a hand-edited
        ///  settings file can say <c>"icon": "database"</c> and mean it.
        ///
        ///  Insertion order is the order the picker shows: marks and states, then people and time,
        ///  then the parts of a system, then the tools, then files and the rest. Alphabetical
        ///  order would scatter every one of those groups.
        ///
        ///  <para>
        ///   Unlike the rail, these are coloured. A label is there to be found in a long list, and
        ///   colour is found before shape is. Each one's colour is fixed to the icon rather than
        ///   chosen, so a bug is always red and a database always blue wherever it appears. The
        ///   colours are mid-tones that read on both the dark and the light theme. A closed shape
        ///   also gets a faint wash of its own colour inside it; open strokes such as a tick or a
        ///   link get none, because filling them would only fill the gap between the lines.
        ///  </para>
        ///
        ///  The rail icons are deliberately absent. A request wearing the same glyph as the panel
        ///  it lives in reads as a duplicate of the panel rather than as a label on a row.
        /// </remarks>
        public static readonly IReadOnlyDictionary<string, Emblem> Pickable =
            new Dictionary<string, Emblem>(StringComparer.OrdinalIgnoreCase)
            {
                ["star"] = new(Star, "#F5B400"),
                ["heart"] = new(Heart, "#E5484D"),
                ["bookmark"] = new(Bookmark, "#3E8EF7"),
                ["flag"] = new(Flag, "#E5484D"),
                ["tag"] = new(Tag, "#12A594"),
                ["pin"] = new(Pin, "#E5484D"),
                ["check"] = new(Check, "#30A46C", wash: false),
                ["bolt"] = new(Bolt, "#F5B400"),
                ["flame"] = new(Flame, "#F76B15"),
                ["bug"] = new(Bug, "#E5484D"),
                ["bell"] = new(Bell, "#F5B400"),
                ["sparkle"] = new(Sparkle, "#AB4ABA"),
                ["target"] = new(Target, "#E5484D"),
                ["trophy"] = new(Trophy, "#D4A017"),

                ["user"] = new(User, "#3E8EF7"),
                ["users"] = new(Users, "#6E56CF"),
                ["home"] = new(Home, "#F76B15"),
                ["globe"] = new(Globe, "#0EA5C6"),
                ["mail"] = new(Mail, "#3E8EF7"),
                ["calendar"] = new(Calendar, "#E5484D"),
                ["clock"] = new(Clock, "#6E56CF"),

                ["server"] = new(Server, "#5B6B8C"),
                ["database"] = new(Database, "#3E8EF7"),
                ["cloud"] = new(Cloud, "#0EA5C6"),
                ["link"] = new(Link, "#12A594", wash: false),
                ["gauge"] = new(Gauge, "#F76B15", wash: false),
                ["shield"] = new(Shield, "#30A46C"),
                ["lock"] = new(Lock, "#D4A017"),
                ["key"] = new(Key, "#D4A017"),
                ["cpu"] = new(Cpu, "#6E56CF"),
                ["terminal"] = new(Terminal, "#30A46C"),
                ["braces"] = new(Braces, "#F76B15", wash: false),
                ["layers"] = new(Layers, "#3E63DD"),
                ["send"] = new(Send, "#0EA5C6"),
                ["pulse"] = new(Pulse, "#E5484D", wash: false),

                ["code"] = new(Code, "#6E56CF", wash: false),
                ["branch"] = new(Branch, "#F76B15", wash: false),
                ["package"] = new(Package, "#A0764B"),
                ["rocket"] = new(Rocket, "#D6409F"),
                ["flask"] = new(Flask, "#30A46C"),
                ["brain"] = new(Brain, "#D6409F"),
                ["puzzle"] = new(Puzzle, "#AB4ABA"),
                ["wrench"] = new(Wrench, "#5B6B8C"),
                ["bulb"] = new(Bulb, "#F5B400"),

                ["medical"] = new(Medical, "#E5484D"),
                ["file"] = new(File, "#5B6B8C"),
                ["folder"] = new(Folder, "#F5B400"),
                ["image"] = new(Image, "#12A594"),
                ["chart"] = new(Chart, "#3E63DD", wash: false),
                ["coffee"] = new(Coffee, "#A0764B"),
            };

        /// <summary>
        ///  Icons that used to be in the picker and are no longer offered.
        /// </summary>
        /// <remarks>
        ///  Kept so that a request saved with one still draws it. Taking an icon out of the picker
        ///  should stop people choosing it, not quietly strip it from the rows that already have
        ///  it.
        /// </remarks>
        private static readonly IReadOnlyDictionary<string, Emblem> Retired =
            new Dictionary<string, Emblem>(StringComparer.OrdinalIgnoreCase)
            {
                ["eye"] = new(Eye, "#3E8EF7"),
                ["map"] = new(Map, "#30A46C"),
                ["compass"] = new(Compass, "#0EA5C6"),
                ["phone"] = new(Phone, "#5B6B8C"),
                ["wifi"] = new(Wifi, "#0EA5C6", wash: false),
                ["power"] = new(Power, "#E5484D", wash: false),
                ["unlock"] = new(Unlock, "#30A46C"),
                ["hammer"] = new(Hammer, "#A0764B"),
                ["video"] = new(Video, "#D6409F"),
                ["music"] = new(Music, "#AB4ABA"),
                ["list"] = new(List, "#5B6B8C", wash: false),
                ["grid"] = new(Grid, "#5B6B8C"),
                ["filter"] = new(Filter, "#6E56CF"),
                ["download"] = new(Download, "#30A46C", wash: false),
                ["upload"] = new(Upload, "#3E8EF7", wash: false),
                ["cart"] = new(Cart, "#F76B15"),
                ["money"] = new(Money, "#30A46C"),
                ["gift"] = new(Gift, "#D6409F"),
                ["sun"] = new(Sun, "#F5B400"),
                ["moon"] = new(Moon, "#6E56CF"),
            };

        /// <summary>
        ///  The icon of that name, or null when there is none.
        /// </summary>
        /// <remarks>
        ///  Null rather than a fallback glyph, so a caller can draw nothing at all. A row with no
        ///  icon should have no icon: a question mark in its place is a defect being reported to
        ///  the user, and the only thing it can mean here is that a settings file names an icon
        ///  which has since been removed - not their problem to look at.
        /// </remarks>
        public static Emblem? Pick(string? name)
        {
            if (name is not { Length: > 0 })
            {
                return null;
            }

            string wanted = name.Trim();

            return Pickable.TryGetValue(wanted, out Emblem? found)
                || Retired.TryGetValue(wanted, out found)
                    ? found
                    : null;
        }

        #endregion
    }

    /// <summary>
    ///  A pickable icon: its shape, the colour it is stroked in, and the paler wash inside it.
    /// </summary>
    /// <remarks>
    ///  The brushes are built once and frozen, for the same reason the geometry is: every row
    ///  that wears the icon shares them, and an immutable brush is safe to share.
    /// </remarks>
    internal sealed class Emblem
    {
        public Emblem(Geometry shape, string colour, bool wash = true)
        {
            Shape = shape;

            Color parsed = Color.Parse(colour);
            Ink = new ImmutableSolidColorBrush(parsed);
            Fill = wash ? new ImmutableSolidColorBrush(parsed, 0.2) : null;
        }

        public Geometry Shape { get; }

        /// <summary>The stroke.</summary>
        public IBrush Ink { get; }

        /// <summary>The faint fill inside a closed shape, or null for an open one.</summary>
        public IBrush? Fill { get; }
    }
}
