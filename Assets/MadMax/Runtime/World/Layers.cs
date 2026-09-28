using UnityEngine;

namespace MadMax.World
{
    /// <summary>Physics layers used by code (named in ProjectSettings/TagManager): terrain chunk colliders and machine
    /// tools (buckets, blades, arms) never collide, so a blade or bucket in the ground does not lift or pin the machine.</summary>
    public static class Layers
    {
        public const int Terrain = 8, MachineTool = 9;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Setup() => Physics.IgnoreLayerCollision(Terrain, MachineTool, true);
    }
}
