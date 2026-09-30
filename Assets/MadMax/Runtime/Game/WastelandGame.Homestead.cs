using MadMax.Building;
using UnityEngine;

namespace MadMax.Game
{
    public partial class WastelandGame
    {
        /// <summary>A modest roadside rest stop. Ordinary placed pieces: editable, salvageable and saved once.</summary>
        void SpawnHomestead()
        {
            if (Rules.startingKit == 0 || !Build || !Build.Structures) return;
            World.Yard(out var origin, out var along, out var side);
            var center = origin + along * -7f + side * 7.3f;
            var facing = Quaternion.LookRotation(-side);
            float floor = terrain.Height(center.x, center.z) + 0.04f;
            Placeable Put(string id, float x, float z, float y = 0f, float yaw = 0f)
            {
                var at = center + facing * new Vector3(x, 0f, z);
                at.y = floor + y;
                var p = FurnitureLibrary.Spawn(id, Build.Structures, at, facing * Quaternion.Euler(0, yaw, 0), propMaterial);
                if (p) { p.owner = Stats.name; _ = p.Id; }
                return p;
            }
            Put("porch_awning", 0, 0);
            Put("porch_lights", 0, 1.52f);
            Put("workbench", -0.9f, -0.75f);
            Put("chest", -2.8f, -0.9f);
            Put("rug", 0.5f, 0.15f, 0.02f);
            Put("chair", 1.05f, -0.45f, 0, -20);
            Put("table", 2.8f, 0);
            Put("radio", 2.8f, 0, 0.83f);
            Put("lamp", 2.55f, 0.2f, 0.83f);
            Put("herb_planter", -2.55f, 1.15f);
            Put("herb_planter", 2.6f, 1.25f);
        }
    }
}
