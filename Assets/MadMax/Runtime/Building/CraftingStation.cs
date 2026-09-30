using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A crafting station (workbench, stove, oven, furnace, kiln, wash plant, mixer, still, composter, garage,
    /// sewing table, chemistry lab, tanning rack, smokehouse, loom, gunsmith bench): E opens its recipes. Jobs queue up
    /// (inputs are paid when queued) and are worked off over time, also while the player is away; finished items wait
    /// in the output tray ([T] collect) unless the crafter stands nearby. Parts and vehicles appear at the output point.</summary>
    public class CraftingStation : MonoBehaviour, IInteractable, IPlaceState
    {
        public static readonly List<CraftingStation> All = new List<CraftingStation>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string type = "workbench";
        public string title = "WORKBENCH";
        public float watts;                       // > 0: needs power
        public Vector3 output = new Vector3(0, 1.1f, 0);
        /// <summary>Quality bonus of the station's tools (0 basic, 1 good, 2 excellent).</summary>
        public float tier;
        public const int MaxQueue = 8;

        public class Job { public string recipe; public float progress, speed = 1f; public ResourceType paidFuel; }
        public readonly List<Job> queue = new List<Job>();
        /// <summary>Finished items waiting for the crafter.</summary>
        public readonly Inventory tray = new Inventory();
        UtilityNode node;
        float fxT;

        void Awake() => node = GetComponent<UtilityNode>();

        public bool Powered => watts <= 0f || (node && node.Powered);
        public Vector3 OutputPoint => transform.TransformPoint(output);
        public bool Busy => queue.Count > 0;
        public Job Current => queue.Count > 0 ? queue[0] : null;

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (!Powered) return title + ": NO POWER";
            var sb = new StringBuilder("[E] ").Append(title);
            if (queue.Count > 0)
            {
                var r = RecipeLibrary.Get(queue[0].recipe);
                sb.Append("  (").Append(r != null ? r.name : "?").Append(' ').Append(Mathf.RoundToInt(queue[0].progress * 100f)).Append('%');
                if (queue.Count > 1) sb.Append(" +").Append(queue.Count - 1);
                sb.Append(')');
            }
            int n = TrayCount;
            if (n > 0) sb.Append("  [T] COLLECT (").Append(n).Append(')');
            return sb.ToString();
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) { g.CollectTray(this); return; }
            if (Powered) g.Menus.OpenCrafting(this);
        }

        public int TrayCount
        {
            get
            {
                int n = 0;
                foreach (var kv in tray.Items) n += kv.Value;
                for (int t = 1; t < ResourceInfo.Count; t++) if (tray.Get((ResourceType)t) > 0) n++;
                return n;
            }
        }

        public void Enqueue(Recipe r, float speed, ResourceType paidFuel = ResourceType.None) { queue.Add(new Job { recipe = r.id, speed = speed, paidFuel = paidFuel == ResourceType.None ? r.fuel : paidFuel }); Dirty(); }

        void Update()
        {
            if (node && watts > 0f) node.demand = watts * (Busy ? 1f : 0.1f);
            if (queue.Count == 0 || !Powered) return;
            var job = queue[0];
            var r = RecipeLibrary.Get(job.recipe);
            if (r == null) { queue.RemoveAt(0); return; }
            job.progress += Time.deltaTime * job.speed / RecipeLibrary.Seconds(r);
            // a little life while it works: sparks at benches, smoke at fires
            if ((fxT -= Time.deltaTime) <= 0f)
            {
                fxT = Random.Range(0.6f, 1.4f);
                var game = MadMax.Game.WastelandGame.Instance;
                var listener = game && game.Current ? game.Current.transform : game && game.Player ? game.Player.transform : null;
                if (listener && (listener.position - transform.position).sqrMagnitude < 28f * 28f)
                {
                    bool cooking = type == "stove" || type == "oven" || type == "still" || type == "smokehouse" || type == "campfire" || type == "range" || type == "cannery";
                    bool hot = type == "furnace" || type == "arc_furnace" || type == "kiln";
                    if (cooking || hot)
                    {
                        Color steam = cooking ? MadMax.Voxel.Pal.Steam : MadMax.Voxel.Pal.WorkshopDust; steam.a = 0.28f;
                        MadMax.World.Fx.Smoke(OutputPoint + Vector3.up * 0.15f, Vector3.up * 0.45f + MadMax.World.Fx.Wind * 0.08f, 0.18f, steam, 1.6f);
                    }
                    else if (type == "garage" || type == "workbench" || type == "gunsmith")
                    {
                        MadMax.World.Fx.Sparks(OutputPoint, Vector3.up, 2, MadMax.Voxel.Pal.Accent);
                        MadMax.Audio.Sfx.Play("ratchet", OutputPoint, 0.14f, 0.9f, 7f, 0.8f);
                    }
                }
            }
            if (job.progress < 1f) return;
            queue.RemoveAt(0);
            MadMax.Game.WastelandGame.Instance?.FinishJob(r, this);
            Dirty();
        }

        /// <summary>Cancel the last queued job; its inputs come back to the crafter.</summary>
        public Recipe CancelLast()
        {
            if (queue.Count == 0) return null;
            var job = queue[queue.Count - 1];
            queue.RemoveAt(queue.Count - 1);
            Dirty();
            return RecipeLibrary.Get(job.recipe);
        }

        void Dirty() => GetComponent<Placeable>()?.Dirty();

        // state: "recipe:progress:speed:paidFuel;...|tray inventory" (older jobs omit paidFuel)
        public string SaveState()
        {
            var sb = new StringBuilder();
            foreach (var j in queue) sb.Append(j.recipe).Append(':').Append(j.progress.ToString("0.###", CultureInfo.InvariantCulture)).Append(':').Append(j.speed.ToString("0.##", CultureInfo.InvariantCulture)).Append(':').Append((int)j.paidFuel).Append(';');
            sb.Append('\u001d').Append(InventoryCodec.Encode(tray));
            return sb.ToString();
        }

        public void LoadState(string s)
        {
            queue.Clear();
            if (string.IsNullOrEmpty(s)) return;
            var halves = s.Split('\u001d');
            foreach (var e in halves[0].Split(';'))
            {
                var p = e.Split(':');
                if (p.Length < 3 || RecipeLibrary.Get(p[0]) == null) continue;
                float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var prog);
                float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var sp);
                var fuel = RecipeLibrary.Get(p[0]).fuel;
                if (p.Length > 3 && int.TryParse(p[3], out int paid) && paid >= 0 && paid < ResourceInfo.Count) fuel = (ResourceType)paid;
                queue.Add(new Job { recipe = p[0], progress = prog, speed = sp > 0f ? sp : 1f, paidFuel = fuel });
            }
            if (halves.Length > 1) InventoryCodec.Decode(tray, halves[1]);
        }
    }
}
