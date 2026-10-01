using MadMax.World;

namespace MadMax.Game
{
    /// <summary>Lights block hooks: smashed and knocked-over world street lamps are kept in <c>SaveData.blockLights</c>
    /// (placed lamps, switches and cabin lights save as pieces).</summary>
    public partial class WastelandGame
    {
        partial void LightsSave(SaveData d) { if (d.blockLights == null) d.blockLights = new System.Collections.Generic.List<string>(); d.blockLights.Clear(); TownLights.Save(d.blockLights); }
        partial void LightsLoad(SaveData d) => TownLights.Load(d.blockLights);
        partial void LightsNewGame() => TownLights.NewGame();
    }
}
