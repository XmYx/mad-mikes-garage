using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A pump with a hose (intake at its front). IN draws water from what the hose reaches — a store not on
    /// its own pipes, a puddle or pond on the ground, a lake or the sea — into its water network; OUT pushes network water
    /// out of the hose into a store or onto the ground (empty a tank, flood a field, fill a lined pond). Powered it moves
    /// 1 L/s; without power [E] cranks it by hand (3 L a turn). [T] cycles IN / OUT / OFF.</summary>
    public class TransferPump : MonoBehaviour, IInteractable, IPlaceState
    {
        public enum Mode { In, Out, Off }
        public Mode mode = Mode.In;
        public float rate = 1f, watts = 300f;
        public Vector3 hose = new Vector3(0f, 0.1f, 0.9f);
        /// <summary>Litres moved since built (tests, prompt).</summary>
        public float moved;
        public string lastSource;
        UtilityNode node;
        float acc;

        void Awake() => node = GetComponent<UtilityNode>();

        public Vector3 HoseEnd => transform.TransformPoint(hose);

        void Update()
        {
            if (!node) return;
            node.demand = mode == Mode.Off ? 0f : watts;
            if (mode == Mode.Off || !node.Powered) return;
            acc += rate * Time.deltaTime;
            if (acc < 0.5f) return;
            float l = acc; acc = 0f;
            Move(l);
        }

        /// <summary>Move up to <paramref name="litres"/> the current way; returns what moved.</summary>
        public float Move(float litres)
        {
            if (!node || mode == Mode.Off) return 0f;
            var at = HoseEnd;
            if (mode == Mode.In)
            {
                float room = UtilityGrid.NetCapacity(node) - UtilityGrid.NetWater(node, out _);
                float want = Mathf.Min(litres, room);
                if (want <= 0.01f) { lastSource = "PIPES FULL"; return 0f; }
                var got = new FluidMix();
                float n = Source(at, want, got);
                if (n <= 0f) return 0f;
                if (FluidStore.WaterOnly(got) && got[ResourceType.Water] > 0.99f) node.clean += n;
                else
                {
                    node.dirty += n;
                    node.taint |= got[ResourceType.SeaWater] > 0.05f ? WaterTaint.Salt : got.Of(FluidFamily.Fuel) + got.Of(FluidFamily.Lube) > 0.02f ? WaterTaint.Oil : WaterTaint.Silt;
                }
                moved += n;
                return n;
            }
            float drawn = UtilityGrid.Draw(node, litres, out bool clean);
            if (drawn <= 0.01f) { lastSource = "PIPES EMPTY"; return 0f; }
            var m = new FluidMix(clean ? ResourceType.Water : WaterQuality.Carried(node));
            float left = drawn;
            var store = FluidStore.Near(at, 1.2f);
            if (store && (!store.OnNet || store.Node.waterNet != node.waterNet) && store.Refuse(m) == null) { left -= store.Pour(m, left); lastSource = "INTO THE " + store.title; }
            else lastSource = "ONTO THE GROUND";
            if (left > 0.01f) Spills.Pour(at, m, left);
            moved += drawn;
            return drawn;
        }

        /// <summary>Water within the hose's reach: a separate store, a ground pool, open water.</summary>
        float Source(Vector3 at, float want, FluidMix into)
        {
            var store = FluidStore.Near(at, 1.2f);
            if (store && (!store.OnNet || store.Node.waterNet != node.waterNet) && FluidStore.WaterOnly(store.Mix) && store.Contents > 0.05f)
            { lastSource = "FROM THE " + store.title; return store.Draw(want, into); }
            if (Spills.PoolNear(at, 1.5f, out var pm) > 0.05f && pm != null && pm.Of(FluidFamily.Aqueous) > 0.8f)
            { lastSource = "FROM THE PUDDLE"; return Spills.Take(at, 1.5f, want, into); }
            var t = DeformableTerrain.Instance;
            if (t && (t.WaterDepth(at.x, at.z) > 0.1f || t.WaterDepth(transform.position.x, transform.position.z) > 0.1f))
            {
                bool sea = WaterQuality.IsSea(at.x, at.z);
                into.Set(sea ? ResourceType.SeaWater : ResourceType.DirtyWater);
                lastSource = sea ? "FROM THE SEA" : "FROM THE LAKE";
                return want;
            }
            lastSource = "NOTHING AT THE HOSE";
            return 0f;
        }

        string Way => mode == Mode.In ? "IN" : mode == Mode.Out ? "OUT" : "OFF";

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            string use = MadMax.Game.Controls.Name(MadMax.Game.Controls.Act.Use), second = MadMax.Game.Controls.Name(MadMax.Game.Controls.Act.Second);
            string power = mode == Mode.Off ? "" : node && node.Powered ? " POWERED" : " NO POWER";
            return "TRANSFER PUMP: " + Way + power + (lastSource != null && mode != Mode.Off ? " (" + lastSource + ")" : "")
                + (mode != Mode.Off && !(node && node.Powered) ? "  [" + use + "] CRANK" : "") + "  [" + second + "] " + (mode == Mode.In ? "PUMP OUT" : mode == Mode.Out ? "SWITCH OFF" : "PUMP IN");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                mode = mode == Mode.In ? Mode.Out : mode == Mode.Out ? Mode.Off : Mode.In;
                GetComponent<Placeable>()?.Dirty();
                g.Toast("TRANSFER PUMP: " + Way);
                return;
            }
            if (mode == Mode.Off) { g.Toast("THE PUMP IS OFF"); return; }
            float n = Move(3f);
            MadMax.Audio.Sfx.Play("creak", transform.position, 0.5f, 1.3f, 15f, 0.2f);
            g.Stats.Practice(MadMax.RPG.Skill.Athletics, 0.2f);
            g.Toast(n > 0.01f ? "PUMPED " + n.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " L " + lastSource : lastSource ?? "NOTHING MOVES");
        }

        public string SaveState() => ((int)mode).ToString();
        public void LoadState(string s) { if (int.TryParse(s, out int m)) mode = (Mode)Mathf.Clamp(m, 0, 2); }
    }
}
