using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    public partial class WastelandGame
    {
        struct BoatPlan { public string key, design; public Vector3 pos; public float yaw; }
        List<BoatPlan> boatPlans;
        /// <summary>Found boats already put in the world (saved; after that they save as ordinary vehicles).</summary>
        public readonly HashSet<string> BoatsFound = new HashSet<string>();
        float boatScan;

        /// <summary>Boats to find (user additions): a skiff pulled up on the shore nearest the start, and at every
        /// coastal settlement a boat moored a few metres off the beach (skiffs and rafts, now and then a trawler or a
        /// shanty boat). Put in the world when the player comes within 250 m.</summary>
        void PlanBoats()
        {
            boatPlans = new List<BoatPlan>();
            var w = World;
            if (w == null) return;
            Moor(w, Vector2.zero, "start", "Skiff", 600f);
            foreach (var st in w.settlements)
            {
                var r = new System.Random(seed * 131 + st.index * 7919);
                double roll = r.NextDouble();
                string design = roll < 0.5 ? "Skiff" : roll < 0.78 ? "Raft" : roll < 0.9 ? "Trawler" : "Houseboat";
                Moor(w, st.pos, "s" + st.index, design, st.radius + 220f);
            }
        }

        void Moor(WorldGen w, Vector2 c, string key, string design, float reach)
        {
            // the nearest clean water 1.4-3.5 m deep, bow pointing out to sea
            for (float r = 20f; r <= reach; r += 12f)
            for (int a = 0; a < 36; a++)
            {
                float ang = a * Mathf.PI * 2f / 36f;
                var p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (!w.Ocean(p.x, p.y) || w.NaturalBiome(p.x, p.y) == Biome.Nuclear) continue;   // never moored in fallout
                float depth = WorldGen.SeaLevel - w.BaseHeight(p.x, p.y);
                if (depth < 1.4f || depth > 3.5f) continue;
                boatPlans.Add(new BoatPlan { key = key, design = design, pos = new Vector3(p.x, WorldGen.SeaLevel + 0.4f, p.y), yaw = ang * Mathf.Rad2Deg * -1f + 90f });
                return;
            }
        }

        void UpdateBoats()
        {
            if ((boatScan -= Time.deltaTime) > 0f) return;
            boatScan = 1f;
            if (boatPlans == null) PlanBoats();
            var f = FocusPos;
            foreach (var b in boatPlans)
            {
                if (BoatsFound.Contains(b.key) || (new Vector2(b.pos.x - f.x, b.pos.z - f.z)).sqrMagnitude > 250f * 250f) continue;
                GameObject prefab = null;
                foreach (var p in boatPrefabs) if (p && p.name == b.design) prefab = p;
                BoatsFound.Add(b.key);
                if (!prefab) continue;
                var v = Instantiate(prefab, b.pos, Quaternion.Euler(0f, b.yaw, 0f)).GetComponent<MadMax.Vehicles.VehicleDriver>();
                Register(v, null);
                if (v.TryGetComponent<MadMax.Vehicles.VehicleSystems>(out var vs)) vs.fuel = vs.fuelCapacity * 0.3f;
            }
        }
    }
}
