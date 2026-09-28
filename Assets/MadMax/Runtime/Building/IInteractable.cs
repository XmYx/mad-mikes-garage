namespace MadMax.Building
{
    /// <summary>Something on a placed piece the player can use on foot: [E] primary, [T] secondary.
    /// Prompt returns null when there is nothing to do.</summary>
    public interface IInteractable
    {
        string Prompt(MadMax.Game.WastelandGame game);
        void Use(MadMax.Game.WastelandGame game, bool secondary);
    }
}
