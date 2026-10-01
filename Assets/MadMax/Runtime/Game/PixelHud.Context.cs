namespace MadMax.Game
{
    /// <summary>Context block HUD: the floating loot panels (<see cref="LootOverlay"/>), under any menu or row menu.</summary>
    public partial class PixelHud
    {
        partial void DrawContextUi()
        {
            if (game.ShowHelp) return;
            game.Loot.Draw(canvas);
        }
    }
}
