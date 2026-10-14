using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A gun's report seen from outside: the pooled muzzle flash light and a spent casing tumbling out to the
    /// side (roof guns, NPC guns; the player's guns do the same in <c>RangedTool</c>).</summary>
    public static class GunFx
    {
        static readonly List<DebrisSystem.Chunk> casing = new List<DebrisSystem.Chunk>();

        public static void Report(Vector3 muzzle, Vector3 aim, Vector3 right, bool brass = true, float size = 1f)
        {
            Fx.Flash(muzzle + aim * 0.3f, new Color(1f, 0.78f, 0.4f), 7f * size, 5f * size, 0.05f);
            var fx = DebrisSystem.Instance;
            if (!brass || !fx) return;
            casing.Clear();
            casing.Add(new DebrisSystem.Chunk { position = muzzle - aim * 0.35f + right * 0.1f, color = new Color32(196, 150, 60, 255) });
            fx.Emit(casing, 0.025f, right * 2.2f + Vector3.up * 2.5f);
        }
    }
}
