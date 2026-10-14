using UnityEngine;

namespace MadMax.World
{
    /// <summary>The railway in the world (<see cref="RailNetwork"/>): built last in the constructor (after the town plans,
    /// rivers and the cities it keeps clear of), graded last in <see cref="Sample"/>.</summary>
    public partial class WorldGen
    {
        /// <summary>Stations, lines and points (null: none could be laid).</summary>
        public RailNetwork rail;

        void BuildRail()
        {
            try { rail = new RailNetwork(this); }
            catch (System.Exception e) { Debug.LogException(e); rail = null; }
        }
    }
}
