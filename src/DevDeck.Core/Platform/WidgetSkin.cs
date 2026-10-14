namespace DevDeck.Core
{
    /// <summary>
    ///  What the floating memory widget looks like: the dial, or one of the scenes drawn in its place.
    /// </summary>
    /// <remarks>
    ///  Stored by name, so the order here is only the order of the menu. Every scene shows the same
    ///  four readings and keeps the same click, drag, wheel and menu; only the picture changes.
    /// </remarks>
    internal enum WidgetSkin
    {
        /// <summary>The round dial: memory as the outer ring, processor, graphics and disk inside it.</summary>
        Gauge,

        /// <summary>A rocket between the Earth and the Moon, as high as memory is full.</summary>
        MoonShot,

        /// <summary>A fish bowl filled to memory in use, one fish per heavy process.</summary>
        Aquarium,

        /// <summary>A glowing core in spinning rings, hotter as memory fills.</summary>
        Reactor,

        /// <summary>Sand running from free memory into used.</summary>
        Hourglass,

        /// <summary>The heaviest processes as planets round a sun.</summary>
        SolarSystem,

        /// <summary>A small creature whose mood follows the machine.</summary>
        Pet,

        /// <summary>Two analogue needles over a segment display.</summary>
        Retro,

        /// <summary>A glass globe whose weather follows the load.</summary>
        Globe,

        /// <summary>Wax rising and falling, more of it as memory fills.</summary>
        LavaLamp,

        /// <summary>A sweep finding one blip per heavy process.</summary>
        Radar,
    }

    internal static class WidgetSkins
    {
        public static IReadOnlyList<WidgetSkin> All { get; } = Enum.GetValues<WidgetSkin>();

        /// <summary>
        ///  The stored name back as a skin; anything unknown is the dial.
        /// </summary>
        /// <remarks>
        ///  By name only. Enum.TryParse also takes a number, and "3" would quietly mean whichever
        ///  skin happens to be third - a different one each time the list changes.
        /// </remarks>
        public static WidgetSkin Parse(string? name) =>
            All.FirstOrDefault(skin => string.Equals(skin.ToString(), name?.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>The string-table key for the name the menu shows.</summary>
        public static string NameKey(WidgetSkin skin) => "MemoryWidgetSkin" + skin;
    }
}
