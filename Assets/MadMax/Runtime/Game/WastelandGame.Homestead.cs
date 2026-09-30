using System.Collections.Generic;
using MadMax.Building;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The homestead: a modest roadside rest stop on a claim, the player's first home. Ordinary placed pieces
    /// (editable, salvageable, saved); their ids are remembered so FIRST STEPS only counts what the player builds.
    /// The bed is the respawn point, and fleet cars parked near its workbench are looked after like at a garage.</summary>
    public partial class WastelandGame
    {
        readonly HashSet<uint> homestead = new HashSet<uint>();

        /// <summary>A piece that came with the homestead (not built by the player).</summary>
        public bool IsHomestead(Placeable p) => p && homestead.Contains(p.Id);

        void SpawnHomestead()
        {
            if (Rules.startingKit == 0 || !Build || !Build.Structures) return;
            World.Yard(out var origin, out var along, out var side);
            var center = origin + along * -7f + side * 30f;            // behind the machine row (side 11..~22 m): clear of arms and buckets
            var facing = Quaternion.LookRotation(-side);
            float floor = terrain.Height(center.x, center.z) + 0.04f;
            Placeable Put(string id, float x, float z, float y = 0f, float yaw = 0f)
            {
                var at = center + facing * new Vector3(x, 0f, z);
                at.y = floor + y;
                var p = FurnitureLibrary.Spawn(id, Build.Structures, at, facing * Quaternion.Euler(0, yaw, 0), propMaterial);
                if (p) { p.owner = Stats.name; homestead.Add(p.Id); }
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
            var bed = Put("bed", 1.2f, -3.2f, 0f, 90f);
            Put("claim_flag", -4.4f, 1.6f);
            if (bed) spawnPoint = bed.transform.position + bed.transform.forward * 1.2f + Vector3.up * 0.2f;   // wake up at home
        }

        /// <summary>The homestead workbench serves as the first home garage for cars parked within reach.</summary>
        bool IsHomesteadBench(CraftingStation st) => st && st.type == "workbench" && IsHomestead(st.GetComponentInParent<Placeable>());

        void SaveHomestead(SaveData d) { d.homestead = new List<uint>(homestead); }
        void LoadHomestead(SaveData d) { homestead.Clear(); if (d.homestead != null) foreach (var id in d.homestead) homestead.Add(id); }
    }
}
