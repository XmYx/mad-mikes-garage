using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A walking beat (town plans): a resident strolls the sidewalk loop round their block corner to corner
    /// instead of wandering about at random.</summary>
    public partial class Npc
    {
        [System.NonSerialized] public Vector3[] beat;
        int beatAt;

        /// <summary>Next corner of the beat as the wander goal (false: no beat, wander as usual).</summary>
        bool NextOnBeat()
        {
            if (beat == null || beat.Length < 2 || companion || leaving) return false;
            beatAt = (beatAt + 1) % beat.Length;
            var p = beat[beatAt];
            var t = MadMax.World.DeformableTerrain.Instance;
            if (t) p.y = t.HeightNoLoad(p.x, p.z);
            goal = p; hasGoal = true; repath = Time.time + 60f;
            return true;
        }
    }
}
