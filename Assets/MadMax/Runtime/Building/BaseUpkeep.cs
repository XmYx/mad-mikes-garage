using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Base upkeep: built pieces standing in the open weather slowly. Rain rots wood and cloth and rusts metal,
    /// sand scours everything in the desert (much faster in a dust storm); stone, brick and concrete barely age. Now and
    /// then a piece loses a hit, never below one, so neglect leaves a base brittle for the next raid rather than
    /// falling down. Anything under a roof keeps. Worn pieces are marked in build mode and counted by the claim flag;
    /// R in build mode repairs. Checked every in-game hour, a batch of pieces per frame. Authority only.</summary>
    public static class BaseUpkeep
    {
        static float lastHour = -1f, pendingHours;
        static int cursor;
        static Object session;
        static readonly List<Placeable> batch = new List<Placeable>();
        static readonly RaycastHit[] hits = new RaycastHit[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { lastHour = -1f; pendingHours = 0f; cursor = 0; batch.Clear(); session = null; }

        public static void Tick()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (g != session) { session = g; lastHour = -1f; batch.Clear(); }                     // a new or loaded world
            if (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient) return;
            if (batch.Count == 0)
            {
                float hour = Mathf.Floor(DayNight.TotalDays * 24f);
                if (lastHour < 0f || hour < lastHour) { lastHour = hour; return; }                // first tick, or a load went back in time
                if (hour <= lastHour) return;
                pendingHours = Mathf.Min(72f, hour - lastHour);                                    // a night's sleep counts in full
                lastHour = hour;
                foreach (var p in Placeable.All) if (p && !p.Collapsing && !p.GetComponentInParent<Rigidbody>()) batch.Add(p);
                cursor = 0;
            }
            for (int n = 0; n < 48 && cursor < batch.Count; n++, cursor++) Age(batch[cursor], pendingHours);
            if (cursor >= batch.Count) batch.Clear();
        }

        /// <summary>Chance per hour in the open that a piece of this material loses a hit.</summary>
        static float Rate(ResourceType mat, bool desert)
        {
            bool wet = Weather.Raining;
            float r;
            switch (mat)
            {
                case ResourceType.Wood: case ResourceType.Cloth: case ResourceType.Leather: case ResourceType.Hide:
                    r = wet ? (Weather.Snowing ? 0.006f : 0.012f) : 0.0015f; break;
                case ResourceType.Scrap: case ResourceType.Iron: case ResourceType.Copper: case ResourceType.Bronze: case ResourceType.Aluminium: case ResourceType.Lead:
                    r = wet ? (Weather.Snowing ? 0.003f : 0.006f) : 0.001f; break;
                default:
                    r = wet ? 0.001f : 0.0003f; break;                                            // stone, brick, concrete, glass
            }
            if (desert) r += 0.002f * (1f + Storms.Dust * 15f);                                    // sand in everything
            return r;
        }

        static void Age(Placeable p, float hours)
        {
            if (!p || p.hits <= 1) return;
            var def = FurnitureLibrary.Get(p.id);
            if (def == null || def.category == BuildCategory.Hidden || def.cost == null || def.cost.Length == 0) return;
            var mat = def.cost[0].type; int most = def.cost[0].amount;
            foreach (var (type, amount) in def.cost) if (amount > most) { most = amount; mat = type; }
            var t = DeformableTerrain.Instance;
            var at = p.transform.position;
            bool desert = t && t.BiomeAt(at.x, at.z) == Biome.Desert;
            float chance = 1f - Mathf.Pow(1f - Rate(mat, desert), hours);
            if (Random.value >= chance || Roofed(p)) return;
            p.hits--;
        }

        /// <summary>Something solid overhead (a roof piece, a building, a tree).</summary>
        static bool Roofed(Placeable p)
        {
            var b = p.TryGetComponent<Renderer>(out var r) ? r.bounds : new Bounds(p.transform.position, Vector3.one * 0.5f);
            var from = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
            int n = Physics.RaycastNonAlloc(from, Vector3.up, hits, 25f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!hits[i].collider.transform.IsChildOf(p.transform)) return true;
            return false;
        }

        /// <summary>Pieces inside a claim below full condition.</summary>
        public static int WornIn(ClaimFlag claim)
        {
            int n = 0;
            foreach (var p in Placeable.All) if (p && p.hits < p.MaxHits && claim.Inside(p.transform.position) && !p.GetComponentInParent<Rigidbody>()) n++;
            return n;
        }
    }
}
