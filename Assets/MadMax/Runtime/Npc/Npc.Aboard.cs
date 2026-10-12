namespace MadMax.Npc
{
    public partial class Npc
    {
        /// <summary>Lives aboard a generated city (<see cref="CityFolk"/>): parented to its hull, steered along the city's
        /// own routes (no terrain pathing or cliff checks), its post moved with the deck every frame.</summary>
        [System.NonSerialized] public MadMax.World.CityHost aboard;
        /// <summary>The city's housing clerk (rents flats: Dialogue.Quarters).</summary>
        [System.NonSerialized] public bool clerk;
    }
}
