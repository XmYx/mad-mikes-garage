using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // S14 SOMETHING IN THE WELL: the inn (a porch, tables, a sign) with its water system (well → filter with a spent
    // cartridge → tank → sink, piped), the kettle barrel, and up the hill Dunmore's old pumpjack weeping crude (a tipped
    // barrel, dark smoke at its foot). While it leaks, oily water seeps into the inn's well; once it is stopped and the
    // filter has a cartridge, the network clears and the kit says CLEAN.
    public partial class WastelandGame
    {
        static readonly Vector3 S14WellAt = new Vector3(-4.6f, 0f, -3.2f), S14FilterAt = new Vector3(-2.7f, 0f, -4.6f), S14TankAt = new Vector3(-5.2f, 0f, -6.8f),
            S14SinkAt = new Vector3(-1.1f, 0f, -3.6f), S14BarrelAt = new Vector3(1.7f, 0f, -3.6f);
        UtilityNode s14Well, s14Filter, s14Tank, s14Sink;
        Placeable s14Pump;
        float s14T, s14Seep, s14Smoke;

        partial void Scene_S14()
        {
            Q5ResetPayoff("S14");
            if (!StoryAnchors.Has("oona") || !Build || !Build.Structures) return;
            // the inn's porch
            Q5Put("oona", "porch_awning", new Vector3(1.5f, 0f, -1.6f), 0f);
            Q5Put("oona", "dining_table", new Vector3(2.8f, 0f, -2.2f), 0f);
            Q5Put("oona", "bench", new Vector3(2.8f, 0f, -3.4f), 0f);
            Q5Put("oona", "bench", new Vector3(2.8f, 0f, -1.0f), 180f);
            Q5Put("oona", "lamppost", new Vector3(4.6f, 0f, 0.6f), 0f);
            Q5Put("oona", "flag", new Vector3(-0.6f, 0f, 1.4f), 0f);
            Q5Put("oona", "barrel", S14BarrelAt, 0f);
            // the water: well → filter (spent) → tank → sink, oily from the start
            var well = Q5Put("oona", "well", S14WellAt, 0f);
            var filter = Q5Put("oona", "filter", S14FilterAt, 90f);
            var tank = Q5Put("oona", "water_tank", S14TankAt, 0f);
            var sink = Q5Put("oona", "sink", S14SinkAt, 180f);
            if (well && filter && tank && sink)
            {
                var wn = well.GetComponent<UtilityNode>(); var fn = filter.GetComponent<UtilityNode>(); var tn = tank.GetComponent<UtilityNode>(); var sn = sink.GetComponent<UtilityNode>();
                fn.Link(wn, UtilityKind.Water); tn.Link(fn, UtilityKind.Water); sn.Link(tn, UtilityKind.Water);
                tn.clean = 30f; tn.dirty = 12f; tn.taint = WaterTaint.Oil;
                if (filter.TryGetComponent<FilterCartridge>(out var fc)) fc.life = 0f;
                foreach (var p in new[] { well, filter, tank, sink }) p.Dirty();
            }
            // up the hill: the old pumpjack, cracked, a tipped barrel, its owner's sign
            if (StoryAnchors.Has("s14_leak"))
            {
                var pump = Q5Put("s14_leak", "pumpjack", Vector3.zero, 0f);
                if (pump) { pump.hits = Mathf.Max(1, pump.MaxHits - 6); pump.Dirty(); }
                Q5Put("s14_leak", "barrel", new Vector3(2.4f, 0f, 1.6f), 40f, 0.32f, 90f);
                Q5Put("s14_leak", "barrel", new Vector3(3.2f, 0f, 0.4f), 0f);
                Q5Put("s14_leak", "sign", new Vector3(-2.8f, 0f, 2.6f), 0f);
            }
        }

        void S14Find()
        {
            if (!StoryAnchors.Has("oona")) return;
            UtilityNode N(string id, Vector3 at) { var p = Q5Prop(id, Q5At("oona", at), 1.6f); return p ? p.GetComponent<UtilityNode>() : null; }
            if (!s14Well) s14Well = N("well", S14WellAt);
            if (!s14Filter) s14Filter = N("filter", S14FilterAt);
            if (!s14Tank) s14Tank = N("water_tank", S14TankAt);
            if (!s14Sink) s14Sink = N("sink", S14SinkAt);
            if (!s14Pump && StoryAnchors.Has("s14_leak")) s14Pump = Q5Prop("pumpjack", StoryAnchors.Get("s14_leak"), 3f);
            var barrel = Q5Prop("barrel", Q5At("oona", S14BarrelAt), 1.2f);
            if (barrel && !barrel.GetComponent<S14KettleBarrel>()) barrel.gameObject.AddComponent<S14KettleBarrel>();
        }

        /// <summary>The inn's water was sampled with the kit and read as <paramref name="clean"/> (or anything but).</summary>
        bool S14Sampled(bool clean)
        {
            foreach (var n in new[] { s14Well, s14Filter, s14Tank, s14Sink })
                if (n && waterSamples.TryGetValue(n.Id, out var s) && (s.verdict == "CLEAN") == clean && s.verdict != "EMPTY") return true;
            return false;
        }

        bool S14Leaking => !MadMax.Story.Story.StepDone("S14", "stop");

        partial void Tick_S14()
        {
            float dt = Time.deltaTime;
            // the leak: oily seep into the well, dark puffs at the pump's foot
            if (S14Leaking && s14Pump && (s14Smoke -= dt) <= 0f && Q5Near(s14Pump.transform.position, 60f))
            {
                s14Smoke = 1.2f;
                var foot = s14Pump.transform.position + s14Pump.transform.right * 0.6f + Vector3.up * 0.2f;
                MadMax.World.Fx.Smoke(foot, Vector3.up * 0.25f, 0.25f, new Color(0.08f, 0.07f, 0.06f, 0.6f), 2.2f);
            }
            if ((s14T += dt) < 0.5f) return;
            float step = s14T; s14T = 0f;
            S14Find();
            if (S14Leaking && s14Well && (s14Seep += step) >= 2f)
            {
                s14Seep = 0f;
                float moved = Mathf.Min(0.4f, s14Well.clean);                                        // the ground water turns oily
                s14Well.clean -= moved; s14Well.dirty += 0.4f; s14Well.taint |= WaterTaint.Oil;
            }
            if (S14Sampled(false)) Q5Note("s14:tested");
            if (s14Pump && s14Pump.hits >= s14Pump.MaxHits) Q5Note("s14:repaired");
            if (!S14Leaking && s14Filter && s14Filter.TryGetComponent<FilterCartridge>(out var fc) && fc.life > 0.5f) Q5Note("s14:filter");
            if (MadMax.Story.Story.StepDone("S14", "filter") && S14Sampled(true)) Q5Note("s14:clean");
            if (MadMax.Story.Story.StepDone("S14", "test") && !MadMax.Story.Story.Flag("s14:advice"))
            {
                MadMax.Story.Story.SetFlag("s14:advice");
                Journal.Add("WATER", "OONA'S WELL IS OILY, BUT THERE'S NO OIL IN THE GROUND HERE: IT COMES IN FROM SOMEWHERE UPHILL. A NEW CARTRIDGE WOULD ONLY CLOG UNTIL THE LEAK STOPS.");
            }
            // the stop route is known: the inn gets a full tank once it runs clean, the closing line says how
            if (MadMax.Story.Story.StepDone("S14", "stop") && !MadMax.Story.Story.Flag("s14:payoff"))
            {
                MadMax.Story.Story.SetFlag("s14:payoff");
                string how = Q5Route("S14", "stop", "REPAIRED") ? "YOU RESEALED DUNMORE'S OLD PUMPJACK"
                    : Q5Route("S14", "stop", "TORE") ? "YOU TORE DUNMORE'S OLD PUMPJACK OUT OF THE HILL" : "DUNMORE CAPPED HIS OLD PUMPJACK, SHAMEFACED";
                Q5Payoff("S14", "OONA'S WELL RUNS CLEAN AGAIN: " + how + ", AND A NEW CARTRIDGE CAUGHT THE REST. HER SINK IS YOURS TO FILL UP AT, FREE.");
                Journal.Add("WATER", "THE LEAK HAS STOPPED. NOW THE INN'S FILTER NEEDS A NEW CARTRIDGE TO CATCH WHAT'S LEFT.");
            }
            if (MadMax.Story.Story.StepDone("S14", "clean") && !MadMax.Story.Story.Flag("s14:filled") && s14Tank)
            {
                MadMax.Story.Story.SetFlag("s14:filled");
                s14Tank.clean += 150f;                                                                 // Oona refills from the clean well
                s14Tank.GetComponent<Placeable>()?.Dirty();
            }
        }
    }
}
