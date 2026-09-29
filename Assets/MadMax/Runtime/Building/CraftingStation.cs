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

        public class Job { public string recipe; public float progress, speed = 1f; }
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

        public void Enqueue(Recipe r, float speed) { queue.Add(new Job { recipe = r.id, speed = speed }); Dirty(); }

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
                bool fire = type == "stove" || type == "furnace" || type == "kiln" || type == "smokehouse" || type == "still";
                if (fire) MadMax.World.Fx.Smoke(OutputPoint + Vector3.up * 0.3f, Vector3.up * 0.8f, 0.3f, new Color(0.35f, 0.33f, 0.3f, 0.5f), 2f);
                else if (MadMax.World.DebrisSystem.Instance) MadMax.World.DebrisSystem.Instance.EmitPuff(OutputPoint, new Color32(255, 220, 140, 255), 0.02f, Random.insideUnitSphere + Vector3.up, 0.2f);
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

        // state: "recipe:progress:speed;...|tray inventory"
        public string SaveState()
        {
            var sb = new StringBuilder();
            foreach (var j in queue) sb.Append(j.recipe).Append(':').Append(j.progress.ToString("0.###", CultureInfo.InvariantCulture)).Append(':').Append(j.speed.ToString("0.##", CultureInfo.InvariantCulture)).Append(';');
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
                queue.Add(new Job { recipe = p[0], progress = prog, speed = sp > 0f ? sp : 1f });
            }
            if (halves.Length > 1) InventoryCodec.Decode(tray, halves[1]);
        }
    }
}
