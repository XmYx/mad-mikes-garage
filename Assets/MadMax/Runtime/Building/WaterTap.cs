using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A standpipe tap on the water network. [E] fills the container in hand or drinks; [T] opens it to run
    /// (0.3 L/s) into whatever is under the spout: a store, a garden bed, or the ground (a puddle, a lined pond, a
    /// flooded furrow).</summary>
    public class WaterTap : MonoBehaviour, IInteractable, IPlaceState
    {
        public Vector3 spout = new Vector3(0f, 0.62f, 0.22f);
        public float flow = 0.3f;
        public bool running;
        UtilityNode node;
        float acc, fx;

        void Awake() => node = GetComponent<UtilityNode>();

        void Update()
        {
            if (!running || !node) return;
            float dt = Time.deltaTime;
            acc += flow * dt;
            var at = transform.TransformPoint(spout);
            bool wet = UtilityGrid.NetWater(node, out _) > 0.2f;
            if ((fx -= dt) <= 0f && wet)
            {
                fx = 0.12f;
                Fx.Smoke(at, Vector3.down * 2f + Random.insideUnitSphere * 0.1f, 0.03f, new Color(0.7f, 0.8f, 0.9f, 0.6f), 0.3f);
            }
            MadMax.Audio.Sfx.Loop(this, "pour", wet ? 0.25f : 0f, 1.2f, 12f);
            if (acc < 0.5f) return;
            float want = acc; acc = 0f;
            float got = UtilityGrid.Draw(node, want, out bool clean);
            if (got <= 0.01f) return;
            var m = new FluidMix(clean ? ResourceType.Water : WaterQuality.Carried(node));
            // where the water lands: a store, a bed, else the ground below the spout
            var below = FluidStore.Below(at);
            if (below && below.Refuse(m) == null) got -= below.Pour(m, got);
            if (got <= 0.01f) return;
            if (Physics.Raycast(at, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Collide))
            {
                var plot = hit.collider.GetComponentInParent<GardenPlot>();
                if (plot && plot.water < 0.95f) { plot.Water(got * 0.3f); return; }
            }
            Spills.Pour(at, m, got);
        }

        void OnDisable() => MadMax.Audio.Sfx.Loop(this, "pour", 0f, 1f, 12f);

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            float w = UtilityGrid.NetWater(node, out float clean);
            string use = MadMax.Game.Controls.Name(MadMax.Game.Controls.Act.Use), second = MadMax.Game.Controls.Name(MadMax.Game.Controls.Act.Second);
            string q = w < 0.5f ? "NO WATER" : Mathf.RoundToInt(w) + " L IN THE PIPES" + (clean < 0.99f ? " " + WaterQuality.Word(WaterQuality.TaintOf(node)) : "");
            return "TAP  " + q + (w >= 0.5f ? "  [" + use + "] " + (g.HeldCanDef != null ? "FILL" : "DRINK") : "") + "  [" + second + "] " + (running ? "TURN OFF" : "TURN ON");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) { running = !running; GetComponent<Placeable>()?.Dirty(); g.Toast(running ? "TAP RUNNING" : "TAP OFF"); return; }
            if (g.HeldCanDef != null) { g.FillHeldFromNetwork(node, "THE TAP"); return; }
            var taint = WaterQuality.TaintOf(node);
            float got = UtilityGrid.Draw(node, 0.5f, out bool clean);
            if (got < 0.1f) { g.Toast("NO WATER"); return; }
            g.DrinkTainted(got * 60f, clean ? WaterTaint.None : taint);
        }

        public string SaveState() => running ? "1" : "0";
        public void LoadState(string s) => running = s == "1";
    }
}
