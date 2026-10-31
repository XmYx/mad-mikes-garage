using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-31: frost on parked cars' glass (heater and scraper clear it), the scrapyard's
    /// working crane, the Remnant guard at a checkpoint, chalk timetable boards on the platforms, gang marks on the road.</summary>
    public static class ScheduledScenarios1031
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new GlassFrostScenario();
            yield return new ScrapyardCraneScenario();
            yield return new CheckpointGuardScenario();
            yield return new TimetableBoardScenario();
            yield return new GangMarksScenario();
        }

        /// <summary>Put the player on foot at <paramref name="p"/> and wait for the ground there.</summary>
        internal static IEnumerator GoTo(ScenarioContext c, Vector3 p, float yaw = 0f)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var t = DeformableTerrain.Instance;
            g.Player.Teleport(new Vector3(p.x, t.Height(p.x, p.z) + 0.3f, p.z), yaw);
            yield return SurvivalKit.Until(() => Physics.Raycast(g.Player.transform.position + Vector3.up * 2f, Vector3.down, 8f, 1 << Layers.Terrain, QueryTriggerInteraction.Ignore), 30f);   // the chunk's collider is in
            yield return SurvivalKit.GameSeconds(1.5f, 10);
            c.Fixture($"player on foot at {p.x:0},{p.z:0}");
        }

        /// <summary>Air of <paramref name="deg"/> °C where <paramref name="at"/> is (the base weather less the latitude's offset).</summary>
        internal static void AirHere(ScenarioContext c, Vector3 at, float deg)
        {
            float offset = Weather.TemperatureAt(at.z) - Weather.BaseTemperature;
            Weather.Restore(false, 0f, 0f, deg - offset);
            c.Fixture($"weather set dry, {Weather.TemperatureAt(at.z):0} °C at the pad (latitude offset {offset:+0;-0} °C)");
        }

        internal static float ShaderFrost(GlassFrost f)
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var r in f.GetComponentsInChildren<MeshRenderer>())
                if (r.name == "Glass") { r.GetPropertyBlock(mpb); return mpb.GetFloat("_Frost"); }
            return -1f;
        }
    }

    /// <summary>A sedan parked at −6 °C at night ices over (the glass renderers get <c>_Frost</c>); SCRAPE THE ICE clears it
    /// by hand; frosted again and driven with a warm engine the heater clears it from the bottom up; in warm air it melts.</summary>
    class GlassFrostScenario : Scenario
    {
        public override string Id => "vehicle.glass_frost";
        public override float Timeout => 320f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            if (!TestWorld.Pad(5f, out var pad)) { c.Block("no pad"); yield break; }
            ScheduledScenarios1031.AirHere(c, pad, -6f);
            DayNight.SetHours(2f);
            var v = TestWorld.Vehicle("Sedan");
            if (!c.Check(v, "a sedan")) yield break;
            yield return TestWorld.Place(c, v, pad, Vector3.forward);
            var f = v.GetComponent<GlassFrost>();
            if (!c.Check(f, "the sedan carries GlassFrost")) { Object.Destroy(v.gameObject); yield break; }
            yield return SurvivalKit.GameSeconds(1f, 8);
            c.Metric("frost_parked", f.Frost, "");
            c.Check(f.Frost > 0.85f, $"a car parked at −6 °C on a clear night is frosted over ({f.Frost:0.00})");
            c.Check(f.GlassRenderers >= 1, $"glass renderers found ({f.GlassRenderers})");
            float shader = ScheduledScenarios1031.ShaderFrost(f);
            c.Check(shader > 0.8f, $"the glass shows it (_Frost {shader:0.00})");
            g.Player.Teleport(pad + Vector3.right * 3.5f + Vector3.up * 0.2f, -90f);
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward((v.transform.position - g.Player.transform.position).normalized + Vector3.up * 0.15f); }
            DayNight.SetHours(9f);
            yield return SurvivalKit.GameSeconds(0.8f, 6);
            c.Screenshot("glass_frost");
            yield return null;
            // scrape it by hand
            var opts = new List<ContextOption>();
            var m = typeof(WastelandGame).GetMethod("ScrapeOptions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            m.Invoke(g, new object[] { new ContextTarget { point = v.transform.position }, opts });
            var scrape = opts.Find(o => o.label.StartsWith("SCRAPE"));
            if (c.Check(scrape != null && scrape.blocked == null, "SCRAPE THE ICE is offered beside the frosted car"))
            {
                scrape.run();
                yield return SurvivalKit.Until(() => f.Frost <= 0.01f, 25f);
                c.Check(f.Frost <= 0.01f, $"scraped clear ({f.Frost:0.00}, {GlassFrost.Scrapes} scrapes)");
            }
            // frosted again; the heater clears it from the bottom up
            ScheduledScenarios1031.AirHere(c, pad, -6f);
            f.Force(1f);
            g.Enter(v);
            yield return SurvivalKit.GameSeconds(1f, 6);
            var sys = v.GetComponent<VehicleSystems>();
            var started = typeof(VehicleSystems).GetProperty("Started");
            var temp = typeof(VehicleSystems).GetProperty("Temperature");
            started.SetValue(sys, true);
            temp.SetValue(sys, 80f);
            if (sys.fuel < 5f) sys.fuel = 20f;
            c.Fixture("in the seat, engine running warm (80 °C)");
            yield return SurvivalKit.GameSeconds(6f, 20);
            c.Metric("cleared_6s", f.Cleared, "");
            c.Check(f.Cleared > 0.15f && f.Frost > 0f, $"the heater clears it from the bottom up ({f.Cleared:0.00} of the glass after 6 s)");
            int d0 = GlassFrost.Defrosted;
            yield return SurvivalKit.Until(() => { if (sys.Temperature < 70f) temp.SetValue(sys, 80f); if (!sys.Started) started.SetValue(sys, true); return GlassFrost.Defrosted > d0; }, 180f);   // ~36 game s; the test player runs slow
            c.Check(GlassFrost.Defrosted > d0 && f.Frost <= 0f, "the windows are clear after the heater's run");
            g.Exit();
            yield return SurvivalKit.GameSeconds(0.5f, 4);
            // warm air melts it
            f.Force(1f);
            ScheduledScenarios1031.AirHere(c, pad, 22f);
            yield return SurvivalKit.GameSeconds(4f, 12);
            c.Check(f.Frost < 0.95f, $"melting in warm air ({f.Frost:0.00} after 4 s at 22 °C)");
            Object.Destroy(v.gameObject);
        }
    }

    /// <summary>At a scrapyard the crane works: it picks the crushed slab up with the magnet and sets it down on the other
    /// heap; it holds while the player stands under the hook.</summary>
    class ScrapyardCraneScenario : Scenario
    {
        /// <summary>Wild dogs roam some yards: keep the test player standing through the long waits.</summary>
        static void Heal(WastelandGame g) { g.Stats.health = g.Stats.MaxHealth; g.Stats.injuries.Clear(); }

        public override string Id => "landmarks.scrapyard_crane";
        public override float Timeout => 520f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var yards = new List<(Vector3 pos, float yaw)>();
            BiomeProps.Scrapyards(g.World, yards);
            if (yards.Count == 0) { c.Block("no scrapyard in this world"); yield break; }
            var me = g.Player.transform.position;
            var (yard, yaw) = yards[0];
            for (int i = 1; i < yards.Count; i++) if ((yards[i].pos - me).sqrMagnitude < (yard - me).sqrMagnitude) { yard = yards[i].pos; yaw = yards[i].yaw; }
            DayNight.SetHours(11f);
            ScheduledScenarios1029.Warm(c);
            var stand = yard + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 9f);     // inside the gate
            yield return ScheduledScenarios1031.GoTo(c, stand, yaw + 180f);
            yield return SurvivalKit.Until(() => ScrapyardCrane.All.Count > 0, 150f);   // the yard's chunks build in turn after the teleport
            if (!c.Check(ScrapyardCrane.All.Count > 0, "the yard's crane is built with its chunk")) yield break;
            ScrapyardCrane crane = ScrapyardCrane.All[0];
            foreach (var k in ScrapyardCrane.All) if ((k.transform.position - yard).sqrMagnitude < (crane.transform.position - yard).sqrMagnitude) crane = k;
            var slab0 = crane.SlabPosition;
            int l0 = ScrapyardCrane.Lifts, d0 = ScrapyardCrane.Drops;
            Time.timeScale = 4f;
            crane.Hurry();
            yield return SurvivalKit.Until(() => { Heal(g); return ScrapyardCrane.Lifts > l0; }, 60f);
            Time.timeScale = 1f;
            c.Check(ScrapyardCrane.Lifts > l0 && crane.Carrying, $"the magnet picked up the slab ({crane.step})");
            // stand under the hook: it holds
            var under = crane.MagnetPosition; under.y = DeformableTerrain.Instance.Height(under.x, under.z) + 0.2f;
            g.Player.Teleport(under, 0f);
            int h0 = ScrapyardCrane.Holds;
            yield return SurvivalKit.Until(() => { Heal(g); return crane.Holding; }, 20f);
            c.Check(ScrapyardCrane.Holds > h0 && crane.Holding, $"it holds while someone stands under the hook ({ScrapyardCrane.Holds - h0} frames)");
            g.Player.Teleport(new Vector3(stand.x, DeformableTerrain.Instance.Height(stand.x, stand.z) + 0.3f, stand.z), yaw + 180f);
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward((crane.transform.position + Vector3.up * 6f - g.Player.transform.position).normalized); }
            yield return SurvivalKit.GameSeconds(1f, 6);
            c.Screenshot("scrapyard_crane");
            yield return null;
            Time.timeScale = 4f;
            yield return SurvivalKit.Until(() => { Heal(g); return ScrapyardCrane.Drops > d0; }, 220f);
            Time.timeScale = 1f;
            float moved = Vector3.Distance(slab0, crane.SlabPosition);
            c.Metric("slab_moved", moved, "m");
            c.Check(ScrapyardCrane.Drops > d0 && moved > 8f, $"the slab set down on the other heap ({moved:0.0} m away)");
        }
    }

    /// <summary>At a checkpoint a Remnant guard stands by the boom, holds up a palm to a car coming at the lowered boom and
    /// waves it through once the toll is paid.</summary>
    class CheckpointGuardScenario : Scenario
    {
        public override string Id => "landmarks.checkpoint_guard";
        public override float Timeout => 300f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            BiomeProps.Mark best = default; bool any = false;
            var me = new Vector2(g.Player.transform.position.x, g.Player.transform.position.z);
            foreach (var m in BiomeProps.Landmarks(g.World))
                if (m.kind == BiomeProps.MarkKind.Checkpoint && (!any || (m.pos - me).sqrMagnitude < (best.pos - me).sqrMagnitude)) { best = m; any = true; }
            if (!any) { c.Block("no checkpoint in this world"); yield break; }
            DayNight.SetHours(11f);
            ScheduledScenarios1029.Warm(c);
            var along = Quaternion.Euler(0f, best.yaw, 0f) * Vector3.forward;
            var centre = new Vector3(best.pos.x, 0f, best.pos.y);
            yield return ScheduledScenarios1031.GoTo(c, centre + along * 14f, best.yaw + 180f);
            yield return SurvivalKit.Until(() => Checkpoint.All.Count > 0, 150f);   // the hut's chunk builds in turn after the teleport
            if (!c.Check(Checkpoint.All.Count > 0, "the checkpoint is built")) yield break;
            var cp = Checkpoint.All[0];
            foreach (var k in Checkpoint.All) if ((k.transform.position - centre).sqrMagnitude < (cp.transform.position - centre).sqrMagnitude) cp = k;
            yield return SurvivalKit.Until(() => cp.Guard, 10f);
            if (!c.Check(cp.Guard, "a Remnant guard is on duty at the hut")) yield break;
            var guard = cp.Guard.GetComponent<CheckpointGuard>();
            var v = TestWorld.Vehicle("Sedan");
            if (!c.Check(v, "a sedan")) yield break;
            var t = DeformableTerrain.Instance;
            var at = centre + along * 22f; at.y = t.Height(at.x, at.z);
            yield return TestWorld.Place(c, v, at, -along);
            g.Enter(v);
            int s0 = CheckpointGuard.Stops;
            yield return SurvivalKit.GameSeconds(2f, 12);
            c.Check(CheckpointGuard.Stops > s0 && guard.Showing == CheckpointGuard.Gesture.Stop, $"the guard holds up a palm at the car ({guard.Showing})");
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward((cp.Guard.transform.position - v.transform.position).normalized + Vector3.up * 0.1f); }
            yield return SurvivalKit.GameSeconds(0.6f, 5);
            c.Screenshot("checkpoint_guard");
            yield return null;
            g.Inventory.Add(MadMax.Items.ResourceType.Scrap, Checkpoint.Toll);
            int w0 = CheckpointGuard.Waves;
            Checkpoint.Horn(v.transform.position, v);
            yield return SurvivalKit.GameSeconds(1f, 6);
            c.Check(cp.IsOpen, "the toll paid, the boom is up");
            c.Check(CheckpointGuard.Waves > w0 && guard.Showing == CheckpointGuard.Gesture.Wave, $"and the guard waves the car through ({guard.Showing})");
            g.Exit();
            Object.Destroy(v.gameObject);
        }
    }

    /// <summary>A passenger station's platform has a chalk board: the station's name and the next trains, each due at the
    /// time the service's timetable says (checked against <see cref="TrainService.Evaluate"/>).</summary>
    class TimetableBoardScenario : Scenario
    {
        public override string Id => "rail.timetable_board";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ScheduledScenarios1030.Director(c);
            var dir = TrainDirector.Instance;
            var net = g.World.rail;
            if (!dir || net == null) { c.Block("no railway"); yield break; }
            TrainService svc = null; int stop = -1; RailStation st = null;
            foreach (var s in dir.services)
            {
                if (s.armoured || s.ended) continue;
                for (int k = 0; k < s.stopStation.Count && st == null; k++)
                {
                    var cand = net.stations[s.stopStation[k]];
                    if (cand.Passenger && cand.platforms.Count > 0) { svc = s; stop = k; st = cand; }
                }
                if (st != null) break;
            }
            if (st == null) { c.Block("no Guild service calls at a passenger platform"); yield break; }
            // the timetable maths: the train stands at that stop when the board says
            float secs = svc.SecondsUntil(stop, out int next);
            c.Check(secs >= 0f && next >= 0, $"{svc.name} calls at {st.Name(g.World)} in {secs:0} s, then stop {next}");
            svc.Evaluate(svc.clock + secs + 0.5, out _, out int standing, out _, out _);
            c.Check(standing == stop, $"the timetable has it standing there then (stop {standing}, wanted {stop})");
            var pf = st.platforms[0];
            var at = st.World(pf.x + 6f, (pf.z + pf.w) * 0.5f, RailNetwork.PlatformTop + 0.1f);
            yield return ScheduledScenarios1031.GoTo(c, at, st.Yaw + 180f);
            yield return SurvivalKit.Until(() => TimetableBoard.Shown.ContainsKey(st.id), 6f);
            if (!c.Check(TimetableBoard.Shown.TryGetValue(st.id, out var board) && board, "a board stands on the platform")) yield break;
            board.Draw();
            c.Check(board.Lines.Count >= 2 && st.Name(g.World).StartsWith(board.Lines[0]), "the board names the station: " + string.Join(" / ", board.Lines));
            bool timed = board.Lines.Exists(l => (l.Length > 5 && l[2] == ':') || l.StartsWith("NOW "));
            c.Check(timed, "and lists a train with its time");
            var side = board.transform.position + board.transform.forward * 3.2f + board.transform.right * 1.8f;
            g.Player.Teleport(side + Vector3.up * 0.2f, board.transform.eulerAngles.y + 180f);
            yield return SurvivalKit.GameSeconds(0.5f, 4);
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward((board.transform.position + Vector3.up * 1.3f - g.Player.transform.position).normalized); }
            yield return SurvivalKit.GameSeconds(0.8f, 6);
            c.Screenshot("timetable_board");
            yield return null;
        }
    }

    /// <summary>Each raider gang's road has a tyre cairn in its colours at both ends (outside the towns); passing one
    /// gives the notice; a beaten gang's cairn is shown shot up.</summary>
    class GangMarksScenario : Scenario
    {
        public override string Id => "world.gang_marks";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var gm = GangMarks.Ensure();
            gm.Tend(g.Player.transform.position, g.propMaterial, g);
            c.Metric("gang_marks", GangMarks.Marks.Count, "");
            if (!c.Check(GangMarks.Marks.Count >= 2, $"marks planned at the gangs' road ends ({GangMarks.Marks.Count})")) yield break;
            var w = g.World;
            bool inTown = false;
            foreach (var m in GangMarks.Marks) if (w.SettlementAt(m.at.x, m.at.z) != null) inTown = true;
            c.Check(!inTown, "none stands inside a town");
            var me = g.Player.transform.position;
            var mark = GangMarks.Marks[0];
            foreach (var m in GangMarks.Marks) if ((m.at - me).sqrMagnitude < (mark.at - me).sqrMagnitude) mark = m;
            var face = Quaternion.Euler(0f, mark.yaw, 0f) * Vector3.back;
            DayNight.SetHours(11f);
            GangMarks.LastNotice = null;
            var aside = Quaternion.Euler(0f, mark.yaw, 0f) * Vector3.left;
            yield return ScheduledScenarios1031.GoTo(c, mark.at + face * 6f + aside * 3f, mark.yaw);   // the 1 Hz hook notices it on the way
            gm.Tend(g.Player.transform.position, g.propMaterial, g);
            c.Check(GangMarks.Shown.TryGetValue(mark.key, out var shown) && shown && shown.name.EndsWith(":up"), "the cairn stands there in the gang's colours");
            c.Check(GangMarks.LastNotice != null && GangMarks.LastNotice.Contains(MadMax.Npc.NpcLore.Gangs[mark.gang]), "noticed: " + GangMarks.LastNotice);
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward((mark.at + Vector3.up * 1.2f - g.Player.transform.position).normalized); }
            yield return SurvivalKit.GameSeconds(0.8f, 6);
            c.Screenshot("gang_mark");
            yield return null;
            int gen = mark.convoy.save.generation;
            mark.convoy.save.generation = gen + 1;
            gm.Tend(g.Player.transform.position, g.propMaterial, g);
            c.Check(GangMarks.Shown.TryGetValue(mark.key, out shown) && shown && shown.name.EndsWith(":down"), "a beaten gang's cairn is shot up");
            yield return SurvivalKit.GameSeconds(0.6f, 5);
            c.Screenshot("gang_mark_beaten");
            yield return null;
            mark.convoy.save.generation = gen;
            gm.Tend(g.Player.transform.position, g.propMaterial, g);
        }
    }
}
