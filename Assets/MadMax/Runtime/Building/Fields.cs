using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Open-ground farming (depth stage B): 2 m field cells on a world grid. The hoe tills one by hand, a
    /// tractor's plough tills a strip as it drives; seeders sow, sprayers water and harvesters reap the beds they pass.
    /// A field bed is an ordinary <see cref="GardenPlot"/> piece ("field_bed", saved like any placed piece).</summary>
    public static class Fields
    {
        public const float Cell = 2f;

        /// <summary>Centre of the field cell under <paramref name="p"/> (x, z; y left as is).</summary>
        public static Vector3 Snap(Vector3 p) => new Vector3((Mathf.Floor(p.x / Cell) + 0.5f) * Cell, p.y, (Mathf.Floor(p.z / Cell) + 0.5f) * Cell);

        /// <summary>The garden bed (field bed or plot) covering the cell of <paramref name="p"/>.</summary>
        public static GardenPlot BedAt(Vector3 p)
        {
            var c = Snap(p);
            foreach (var g in GardenPlot.All)
                if (g && Mathf.Abs(g.transform.position.x - c.x) < 0.95f && Mathf.Abs(g.transform.position.z - c.z) < 0.95f) return g;
            return null;
        }

        /// <summary>Whether the cell of <paramref name="p"/> can be tilled; <paramref name="why"/> says why not.</summary>
        public static bool CanTill(Vector3 p, out string why)
        {
            var t = MadMax.World.DeformableTerrain.Instance;
            why = null;
            if (!t || t.World == null) { why = "NO GROUND"; return false; }
            var c = Snap(p);
            var s = t.World.Sample(c.x, c.z);
            if (s.roadDist < 2.5f) { why = "ROAD"; return false; }
            if (Paved(t, c)) { why = "PAVED"; return false; }
            if (t.WaterDepth(c.x, c.z) > 0.05f || s.shore > 0.3f) { why = "WATER"; return false; }
            if (s.feature != 0) { why = "ROCK OR CONCRETE"; return false; }
            if (t.Normal(c.x, c.z).y < 0.93f) { why = "TOO STEEP"; return false; }
            if (BedAt(c)) { why = "ALREADY TILLED"; return false; }
            c.y = t.Height(c.x, c.z);
            foreach (var col in Physics.OverlapBox(c + Vector3.up * 0.7f, new Vector3(0.9f, 0.6f, 0.9f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (col.GetComponentInParent<Placeable>() || col.GetComponentInParent<MadMax.World.DestructibleVoxels>()) { why = "IN THE WAY"; return false; }
            return true;
        }

        /// <summary>Any paving (asphalt, concrete, gravel, cobbles, paint, potholes), a paved highway or a structure deck on
        /// the cell: its centre and four corners (the plough and the hoe only work open, unpaved ground).</summary>
        static bool Paved(MadMax.World.DeformableTerrain t, Vector3 c)
        {
            const float h = Cell * 0.5f - 0.1f;
            for (int i = 0; i < 5; i++)
            {
                float x = c.x + (i == 0 ? 0f : (i & 1) == 0 ? -h : h), z = c.z + (i == 0 ? 0f : i < 3 ? -h : h);
                if (t.PaveAt(x, z) != 0 || t.HardRoadAt(x, z)) return true;
                float y = t.Height(x, z), top = y;
                if (StructureGround.Top(new Vector3(x, y + 0.6f, z), ref top, out _) ) return true;
            }
            return false;
        }

        /// <summary>Till the cell of <paramref name="p"/> into a field bed (null when it can't be).</summary>
        public static GardenPlot Till(MadMax.Game.WastelandGame g, Vector3 p)
        {
            if (!g || !g.Build || !CanTill(p, out _)) return null;
            var t = MadMax.World.DeformableTerrain.Instance;
            var c = Snap(p); c.y = t.Height(c.x, c.z) - 0.03f;
            var piece = FurnitureLibrary.Spawn("field_bed", g.Build.Structures, c, Quaternion.identity, g.propMaterial);
            if (!piece) return null;
            piece.Dirty();
            MadMax.Net.NetSession.Instance?.SendPlaced(piece);
            MadMax.Story.Story.Note("tilled");
            return piece.GetComponent<GardenPlot>();
        }

        /// <summary>Sow the bed under <paramref name="p"/> with the first seed in <paramref name="from"/> (then the pack).</summary>
        public static bool Sow(MadMax.Game.WastelandGame g, Vector3 p, Inventory from)
        {
            var bed = BedAt(p);
            if (!bed || bed.crop != null) return false;
            string seed = null;
            if (from != null) foreach (var kv in from.Items) if (kv.Value > 0 && kv.Key.StartsWith("seed_")) { seed = kv.Key; break; }
            var src = seed != null ? from : g.Inventory;
            if (seed == null) seed = g.FirstSeed(false);
            if (seed == null || !src.TakeItem(seed)) return false;
            bed.Sow(seed, g.QualityOf(seed));
            return true;
        }

        /// <summary>Water the bed under <paramref name="p"/> from <paramref name="tank"/> (then the pack): 1 L a bed.</summary>
        public static bool Spray(MadMax.Game.WastelandGame g, Vector3 p, Inventory tank)
        {
            var bed = BedAt(p);
            if (!bed || bed.water > 0.7f) return false;
            foreach (var inv in new[] { tank, g.Inventory })
                foreach (var t in new[] { ResourceType.Water, ResourceType.DirtyWater })
                    if (inv != null && inv.TrySpend(t, 1)) { bed.Water(1f); return true; }
            return false;
        }
    }
}
