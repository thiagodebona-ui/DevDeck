using DevDeck.Core;

namespace DevDeck.App.Views.Skins
{
    internal static class SkinScenes
    {
        /// <summary>The scene for a skin, or null for the dial, which is laid out in XAML.</summary>
        public static SkinScene? Create(WidgetSkin skin) => skin switch
        {
            WidgetSkin.MoonShot => new MoonShotScene(),
            WidgetSkin.Aquarium => new AquariumScene(),
            WidgetSkin.Reactor => new ReactorScene(),
            WidgetSkin.Hourglass => new HourglassScene(),
            WidgetSkin.SolarSystem => new SolarSystemScene(),
            WidgetSkin.Pet => new PetScene(),
            WidgetSkin.Retro => new RetroScene(),
            WidgetSkin.Globe => new GlobeScene(),
            WidgetSkin.LavaLamp => new LavaLampScene(),
            WidgetSkin.Radar => new RadarScene(),
            _ => null,
        };
    }
}
