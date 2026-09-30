using System.Globalization;
using System.Text;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Sluice box (depth stage H, the workshop rung of mining): set in a running river (current 0.15 m/s+) and fed
    /// soil from the pack ([T]: sand, gravel, rubble, clay, laterite, slag; hopper 60), the water washes it over the
    /// riffles — one load every 5 s at a good current — and the heavy grains stay behind: ores by soil kind, plus the
    /// river's gold (<see cref="MadMax.Game.WastelandGame.GoldRichness"/>). No power, no dirty water; slower and leaner
    /// than the wash plant but the only one that catches gold. [E] takes the catch.</summary>
    public class SluiceBox : MonoBehaviour, IInteractable, IPlaceState
    {
        public const int Capacity = 60;
        public const float SecondsPerLoad = 5f;
        public readonly Inventory hopper = new Inventory();
        public readonly Inventory caught = new Inventory();
        readonly float[] acc = new float[ResourceInfo.Count];
        float work, sense, flow, rich;
        int loads;

        static readonly ResourceType[] Soils = { ResourceType.Sand, ResourceType.Gravel, ResourceType.Rubble, ResourceType.Clay, ResourceType.Laterite, ResourceType.Slag };

        /// <summary>What one load of a soil leaves in the riffles (fractions of a unit; gold is scaled by the river).</summary>
        public static (ResourceType ore, float per)[] Yield(ResourceType soil) => soil switch
        {
            ResourceType.Sand => new[] { (ResourceType.Silica, 0.3f), (ResourceType.IronOre, 0.06f), (ResourceType.GoldOre, 0.05f) },
            ResourceType.Gravel => new[] { (ResourceType.IronOre, 0.08f), (ResourceType.Silica, 0.1f), (ResourceType.GoldOre, 0.06f) },
            ResourceType.Rubble => new[] { (ResourceType.IronOre, 0.12f), (ResourceType.CopperOre, 0.06f), (ResourceType.Stone, 0.2f), (ResourceType.GoldOre, 0.02f) },
            ResourceType.Clay => new[] { (ResourceType.CopperOre, 0.12f), (ResourceType.TinOre, 0.1f), (ResourceType.GoldOre, 0.02f) },
            ResourceType.Laterite => new[] { (ResourceType.Bauxite, 0.2f), (ResourceType.IronOre, 0.1f), (ResourceType.GoldOre, 0.03f) },
            _ => new[] { (ResourceType.CopperOre, 0.1f), (ResourceType.TinOre, 0.05f), (ResourceType.GoldOre, 0.01f) },
        };

        public float Flow => flow;
        public float Richness => rich;
        public bool Running => flow > 0.15f;

        public int Load
        {
            get { int n = 0; foreach (var s in Soils) n += hopper.Get(s); return n; }
        }

        public int Caught
        {
            get { int n = 0; for (int t = 1; t < ResourceInfo.Count; t++) n += caught.Get((ResourceType)t); return n; }
        }

        void Awake() => sense = Random.value;

        void Sense()
        {
            var t = DeformableTerrain.Instance;
            var p = transform.position;
            flow = t && t.World != null && t.WaterDepthNoLoad(p.x, p.z) > 0.08f ? t.World.RiverFlow(p.x, p.z).magnitude : 0f;
            var g = MadMax.Game.WastelandGame.Instance;
            rich = g && flow > 0f ? g.GoldRichness(p) : 0f;
        }

        void Update()
        {
            if ((sense -= Time.deltaTime) <= 0f) { sense = 2f; Sense(); }
            if (Running && Load > 0) Process(Time.deltaTime);
        }

        /// <summary>Run the water over the riffles for <paramref name="seconds"/> (tests fast-forward with it).</summary>
        public void Advance(float seconds) { Sense(); if (Running) Process(seconds); }

        void Process(float dt)
        {
            work += dt * Mathf.Clamp(flow / 0.8f, 0.4f, 1.6f) / SecondsPerLoad;
            bool changed = false;
            while (work >= 1f && Load > 0)
            {
                work -= 1f;
                ResourceType soil = ResourceType.None;
                foreach (var s in Soils) if (hopper.Get(s) > 0) { soil = s; break; }
                hopper.TrySpend(soil, 1);
                foreach (var (ore, per) in Yield(soil)) acc[(int)ore] += ore == ResourceType.GoldOre ? per * rich : per;
                for (int i = 1; i < acc.Length; i++)
                    if (acc[i] >= 1f) { int n = Mathf.FloorToInt(acc[i]); acc[i] -= n; caught.Add((ResourceType)i, n); }
                if (++loads % 4 == 0) MadMax.Game.WastelandGame.Instance?.WorkGold(transform.position, 1);
                changed = true;
            }
            if (Load == 0) work = 0f;
            if (changed) GetComponent<Placeable>()?.Dirty();
        }

        /// <summary>Tip soil from the pack into the hopper (up to its capacity). Returns the units taken.</summary>
        public int Feed(Inventory from)
        {
            int room = Capacity - Load, took = 0;
            foreach (var s in Soils)
            {
                int n = Mathf.Min(room - took, from.Get(s));
                if (n > 0 && from.TrySpend(s, n)) { hopper.Add(s, n); took += n; }
            }
            if (took > 0) GetComponent<Placeable>()?.Dirty();
            return took;
        }

        static bool HasSoil(Inventory inv) { foreach (var s in Soils) if (inv.Get(s) > 0) return true; return false; }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (!Running && Load == 0 && Caught == 0) return "SLUICE BOX: SET IT IN A RUNNING RIVER";
            var sb = new StringBuilder("SLUICE BOX");
            sb.Append(Running ? (Load > 0 ? "  WASHING " + Load + " SOIL" : "  EMPTY") : "  NO CURRENT");
            if (Caught > 0) sb.Append("  [E] TAKE ").Append(Caught).Append(" FROM THE RIFFLES");
            if (Load < Capacity && HasSoil(g.Inventory)) sb.Append("  [T] FEED SOIL");
            return sb.ToString();
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                int n = Feed(g.Inventory);
                g.Toast(n > 0 ? "FED " + n + " SOIL INTO THE SLUICE" : Load >= Capacity ? "THE HOPPER IS FULL" : "NO SOIL IN THE PACK (DIG WITH A SHOVEL)");
                if (n > 0) MadMax.Audio.Sfx.Play("dig", transform.position, 0.5f, 1.1f, 15f);
                return;
            }
            int got = TakeCatch(g.Inventory, out string what);
            if (got == 0) { g.Toast(Running ? "NOTHING IN THE RIFFLES YET" : "SLUICE BOX: SET IT IN A RUNNING RIVER"); return; }
            g.Stats.Practice(MadMax.RPG.Skill.Salvaging, 0.5f * got);
            MadMax.Audio.Sfx.Play("splash", transform.position, 0.5f, 1.2f, 15f);
            g.Toast("FROM THE RIFFLES:" + what);
        }

        /// <summary>Move the catch into <paramref name="to"/>. Returns the units moved.</summary>
        public int TakeCatch(Inventory to, out string what)
        {
            var sb = new StringBuilder();
            int got = 0;
            for (int t = 1; t < ResourceInfo.Count; t++)
            {
                int n = caught.Get((ResourceType)t);
                if (n <= 0) continue;
                caught.TrySpend((ResourceType)t, n);
                to.Add((ResourceType)t, n);
                got += n;
                sb.Append(' ').Append(n).Append(' ').Append(ResourceInfo.Name((ResourceType)t)).Append(',');
            }
            if (sb.Length > 0) sb.Length--;
            what = sb.ToString();
            if (got > 0) GetComponent<Placeable>()?.Dirty();
            return got;
        }

        // state: hopper \u001d catch \u001d fractional yields ("index=value,...")
        public string SaveState()
        {
            var sb = new StringBuilder(InventoryCodec.Encode(hopper)).Append('\u001d').Append(InventoryCodec.Encode(caught)).Append('\u001d');
            for (int i = 1; i < acc.Length; i++) if (acc[i] > 0.0001f) sb.Append(i).Append('=').Append(acc[i].ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            return sb.ToString();
        }

        public void LoadState(string s)
        {
            System.Array.Clear(acc, 0, acc.Length);
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split('\u001d');
            InventoryCodec.Decode(hopper, p[0]);
            if (p.Length > 1) InventoryCodec.Decode(caught, p[1]);
            if (p.Length > 2)
                foreach (var e in p[2].Split(','))
                {
                    var kv = e.Split('=');
                    if (kv.Length == 2 && int.TryParse(kv[0], out int i) && i > 0 && i < acc.Length)
                        float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out acc[i]);
                }
        }
    }
}
