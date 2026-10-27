using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-27: the pumpjack's rod strokes with the beam and its sight glass shows the crude,
    /// the oil press screws down and drips, the wet wash drips and its water goes on the ground, breakfast from the yard,
    /// raider lookouts flash on the skyline, the scarecrow trade.</summary>
    public static class ScheduledScenarios1027
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new PumpjackRod();
            yield return new OilPressAtWork();
            yield return new WashDrips();
            yield return new YardBreakfast();
            yield return new LookoutGlint();
            yield return new ScarecrowTrade();
        }
    }

    /// <summary>On an oil field with power the beam nods and the polished rod rides up and down with the horse head;
    /// the sight glass rises with the crude; a full tank stops it and the beam settles level.</summary>
    class PumpjackRod : Scenario
    {
        public override string Id => "refining.pumpjack_rod";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var p = PH.Piece(g, "pumpjack", me + fwd * 6f, -fwd);
            if (!c.Check(p, "a pumpjack")) yield break;
            var jack = p.GetComponent<Pumpjack>();
            yield return null;
            jack.Field = 0.8f; jack.forcePower = true; jack.stored = 0f;
            c.Fixture("a rich field under it and power without a net");
            yield return PH.Until(() => p.transform.Find("Rod"), 3f);
            c.Check(p.transform.Find("Rod") && p.transform.Find("CrudeGlass"), "it has a rod on its bridle and a sight glass");
            float lo = float.MaxValue, hi = float.MinValue, aLo = float.MaxValue, aHi = float.MinValue;
            int s0 = jack.Strokes;
            for (float t = 0f; t < 9f; t += Time.deltaTime)
            {
                lo = Mathf.Min(lo, jack.RodLift); hi = Mathf.Max(hi, jack.RodLift);
                aLo = Mathf.Min(aLo, jack.BeamAngle); aHi = Mathf.Max(aHi, jack.BeamAngle);
                yield return null;
            }
            c.Check(aHi - aLo > 20f, $"the beam nods ({aLo:0}..{aHi:0} deg)");
            c.Check(hi - lo > 0.3f, $"the rod strokes with it ({lo:0.00}..{hi:0.00} m)");
            c.Check(jack.Strokes > s0, $"strokes counted ({jack.Strokes - s0})");
            c.Check(jack.stored > 0.5f && jack.GlassLevel > 0f, $"crude in the tank shows in the glass ({jack.stored:0.0} L, {jack.GlassLevel * 100f:0.0} cm)");
            float low = jack.GlassLevel;
            jack.stored = jack.capacity * 0.75f;
            yield return null;
            c.Check(jack.GlassLevel > low + 0.2f, $"three quarters full stands high in the glass ({jack.GlassLevel:0.00} m)");
            yield return ScheduledScenarios1015.Shoot(c, g, p.transform.position + Vector3.up * 1.2f, ScheduledScenarios1026Audit.Side(g), 5f, "pumpjack_rod");
            jack.stored = jack.capacity;
            yield return PH.Until(() => Mathf.Abs(jack.BeamAngle) < 0.5f && !jack.Working, 10f);
            c.Check(!jack.Working && Mathf.Abs(jack.BeamAngle) < 0.5f, $"full: it winds down level ({jack.BeamAngle:0.0} deg)");
            c.Check(jack.Prompt(g).Contains("TANK FULL"), "the prompt says the tank is full");
            Object.Destroy(p.gameObject);
        }
    }

    /// <summary>A pressing screws the plate down and turns the bar, oil drips into the cup once the seeds give, the
    /// screw winds back up when the job is done.</summary>
    class OilPressAtWork : Scenario
    {
        public override string Id => "refining.oil_press";
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var p = PH.Piece(g, "oil_press", me + fwd * 2.5f, -fwd);
            if (!c.Check(p, "an oil press")) yield break;
            var work = p.GetComponent<OilPressWork>();
            var st = p.GetComponent<CraftingStation>();
            var r = RecipeLibrary.Get("press_food_sunseeds");
            if (!c.Check(work && st && r != null, "it has its screw work and a sunflower pressing")) { Object.Destroy(p.gameObject); yield break; }
            yield return null;
            float secs = RecipeLibrary.Seconds(r);
            st.Enqueue(r, secs / 8f);
            c.Fixture("a sunflower pressing sped up to take 8 s");
            yield return PH.Until(() => work.Press > 0.4f, 6f);
            c.Check(work.Press > 0.4f && work.BarAngle > 360f, $"the plate goes down, the bar turning ({work.Press:0.00}, {work.BarAngle:0} deg)");
            var screw = p.transform.Find("Screw");
            if (!PieceBits.HD(p.gameObject)) c.Check(screw && screw.localPosition.y < 9f * 0.08f - 0.1f, "the screw is down in the barrel");
            c.Check(work.Dripping, "oil drips from the spout");
            yield return PH.Until(() => work.Drops > 0, 3f);
            c.Check(work.Drops > 0, $"into the cup ({work.Drops} drops)");
            yield return ScheduledScenarios1015.Shoot(c, g, p.transform.position + Vector3.up * 0.8f, ScheduledScenarios1026Audit.Side(g), 2.4f, "oil_press");
            yield return PH.Until(() => !st.Busy && work.Press < 0.02f, 10f);
            c.Check(!st.Busy && work.Press < 0.02f && !work.Dripping, $"done: the screw winds back up ({work.Press:0.00})");
            Object.Destroy(p.gameObject);
        }
    }

    /// <summary>Washing pours the used water out beside the line; the wet wash drips (drops falling, the ground under it
    /// wet); dry, it stops.</summary>
    class WashDrips : Scenario
    {
        public override string Id => "living.wash_drips";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var lp = PH.Piece(g, "clothes_line", me + fwd * 4f, -fwd);
            var dp = PH.Piece(g, "storage_drum", me + fwd * 4f + right * 3f, -fwd);
            if (!c.Check(lp && dp, "a clothes line by a drum")) yield break;
            var line = lp.GetComponent<ClothesLine>();
            bool rain = Weather.Raining; Weather.Raining = false;
            DayNight.SetHours(11f);
            var drum = dp.GetComponent<FluidStore>();
            drum.litres = 60f; drum.mix.Set(ResourceType.Water);
            var tub = lp.transform.position + lp.transform.forward * 1.2f;
            float before = Spills.PoolNear(tub, 2.5f, out _) + Spills.WetAt(tub.x, tub.z);
            c.Check(g.WashClothes(line) && line.Hung, "washed and hung out");
            yield return null;
            float after = Spills.PoolNear(tub, 2.5f, out _) + Spills.WetAt(tub.x, tub.z);
            c.Check(after > before, $"the wash water went on the ground beside it ({before:0.00} -> {after:0.00})");
            line.dryness = 0f;
            yield return PH.Until(() => line.DripsLanded > 2, 5f);
            c.Check(line.Dripping && line.DripsLanded > 2, $"the wet wash drips ({line.DripsLanded} drops)");
            yield return ScheduledScenarios1015.Shoot(c, g, lp.transform.position + Vector3.up * 1.1f, right, 3f, "wash_drips");
            line.dryness = 0.9f;
            yield return null; yield return null;
            c.Check(!line.Dripping, "nearly dry: no more drips");
            line.dryness = 1f; g.TakeInWash(line);
            Weather.Raining = rain;
            Object.Destroy(lp.gameObject); Object.Destroy(dp.gameObject);
        }
    }

    /// <summary>Eggs and milk make a yard breakfast (stove, fire); eaten in the morning it steadies the aim till noon
    /// (late it doesn't); at a table the crew sits down to it.</summary>
    class YardBreakfast : Scenario
    {
        public override string Id => "food.yard_breakfast";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            c.Check(RecipeLibrary.Get("fire_breakfast") != null && RecipeLibrary.Get("stove_breakfast") != null || RecipeLibrary.All.Any(r => r.output == WastelandGame.Breakfast && r.station == "stove"),
                "a breakfast recipe at the fire and the stove");
            c.Check(RecipeLibrary.All.Any(r => r.output == WastelandGame.Breakfast && r.items != null && r.items.Any(i => i.Item1 == "food_egg") && r.items.Any(i => i.Item1 == "drink_milk")), "made from eggs and milk");
            float pen = g.AimPenalty;
            DayNight.SetHours(8f);
            g.Inventory.AddItem(WastelandGame.Breakfast);
            g.Eat(WastelandGame.Breakfast);
            c.Check(WastelandGame.SteadyHands, "eaten at 8:00: steady hands (" + WastelandGame.LastBreakfast + ")");
            c.Check(g.AimPenalty < pen - 0.1f, $"less aim spread ({pen:0.00} -> {g.AimPenalty:0.00})");
            DayNight.SetHours(12.5f);
            c.Check(!WastelandGame.SteadyHands, "noon: it wears off");
            DayNight.SetHours(16f);
            g.Inventory.AddItem(WastelandGame.Breakfast);
            g.Eat(WastelandGame.Breakfast);
            c.Check(!WastelandGame.SteadyHands, "eaten in the afternoon: just food (" + WastelandGame.LastBreakfast + ")");
            // at the table with a companion
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var tp = PH.Piece(g, "dining_table", me + fwd * 2f, -fwd);
            if (!tp) { c.Note("no dining table piece: the crew part is skipped"); yield break; }
            MadMax.Npc.Npc mate = MadMax.Npc.Companions.Live.FirstOrDefault(n => n && n.Alive);
            if (!mate) { c.Note("no companion in this world: the crew part is skipped"); Object.Destroy(tp.gameObject); yield break; }
            mate.transform.position = PH.Ground(me + Vector3.Cross(Vector3.up, fwd) * 5f);
            DayNight.SetHours(7.5f);
            g.Inventory.AddItem(WastelandGame.Breakfast);
            g.Eat(WastelandGame.Breakfast);
            c.Check(WastelandGame.BreakfastCrew >= 1 && mate.Visiting, $"the crew comes to the table ({WastelandGame.BreakfastCrew})");
            yield return PH.Until(() => mate.VisitSettled, 10f);
            c.Check(mate.VisitSettled, "and sits down to it");
            yield return ScheduledScenarios1015.Shoot(c, g, tp.transform.position + Vector3.up * 0.9f, fwd, 3f, "yard_breakfast");
            mate.EndVisit(false);
            yield return null; yield return null;
            c.Check(!mate.leaving, "afterwards they carry on with the crew");
            Object.Destroy(tp.gameObject);
        }
    }

    /// <summary>A lookout's spot is the high ground on the column's side; with the sun ahead of their look a closing
    /// raider column's lookout flashes on the skyline (a word to the player); with the sun behind it doesn't.</summary>
    class LookoutGlint : Scenario
    {
        public override string Id => "npc.lookout_glint";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var me = g.Player.transform.position;
            var t = DeformableTerrain.Instance;
            var col = me + new Vector3(250f, 0f, 0f);
            var spot = WastelandGame.LookoutSpot(col, me);
            float ground = t ? t.HeightNoLoad(col.x, col.z) : 0f;
            c.Check(spot.y >= ground - 0.01f && PH.Flat(spot, col) < 120f, $"the lookout lies on the highest ground near the column ({spot.y - ground:+0.0;-0.0} m)");
            c.Check(WastelandGame.GlintFor(me - spot, (me - spot).normalized + Vector3.up * 0.3f, 0f, 0f) > 0.5f, "facing a low sun, a lens flashes");
            c.Check(WastelandGame.GlintFor(me - spot, -(me - spot).normalized + Vector3.up * 0.3f, 0f, 0f) < 0.05f, "sun behind them: no flash");
            var d = MadMax.Npc.NpcDirector.Instance;
            var raider = d ? d.Convoys.FirstOrDefault(cv => cv != null && cv.raiders) : null;
            if (!c.Check(raider != null, "a raider column exists")) yield break;
            int n = WastelandGame.LookoutFlashes;
            g.LookoutFlash(raider, spot, 0.9f);
            c.Check(WastelandGame.LookoutFlashes == n + 1 && WastelandGame.LastLookout == raider.Gang, "a flash on the skyline from " + raider.Gang);
            yield return null;
        }
    }

    /// <summary>A farmer takes an old garment for the field scarecrow (it wears its colour) and pays a little; a gang's
    /// scarecrow can be torn down (the gang cools, the town warms); both are saved.</summary>
    class ScarecrowTrade : Scenario
    {
        public override string Id => "towns.scarecrow_trade";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            ScheduledScenarios1023.Call(g, "UpdateNightly1026");
            var ts = Object.FindAnyObjectByType<TownScarecrows>();
            if (!c.Check(ts, "the town scarecrows run")) yield break;
            TownScarecrows.Spot field = default, gang = default; bool hasField = false, hasGang = false;
            for (int i = 0; i < g.World.settlements.Count && !(hasField && hasGang); i++)
            {
                var st = g.World.settlements[i];
                if (st.plan == null || st.kind == Biome.City) continue;
                foreach (var s in ts.Plan(i, st, g))
                {
                    if (s.gang < 0 && !hasField) { field = s; hasField = true; }
                    if (s.gang >= 0 && !hasGang) { gang = s; hasGang = true; }
                }
            }
            if (!c.Check(hasField, "a field scarecrow somewhere")) yield break;
            var back = g.Player.transform.position;
            g.Player.Teleport(PH.Ground(field.at + new Vector3(1.6f, 0f, 1.6f)) + Vector3.up * 0.2f, 0f);
            yield return PH.Until(() => TownScarecrows.Shown.ContainsKey(field.key), 8f);
            g.Inventory.AddItem("cloth_poncho");
            int scrap = PH.Scrap(g);
            var opts = ContextActions.Gather(g, new ContextTarget());
            c.Check(opts.Any(o => o.label.StartsWith("GIVE YOUR")), "beside it: GIVE YOUR ... FOR THE SCARECROW");
            c.Check(g.DressScarecrow(field.key, "cloth_poncho"), WastelandGame.LastScarecrow ?? "dressed");
            c.Check(PH.Scrap(g) >= scrap + WastelandGame.ScarecrowPayMin, $"paid ({PH.Scrap(g) - scrap} scrap)");
            c.Check(TownScarecrows.Dressed.ContainsKey(field.key) && g.Inventory.GetItem("cloth_poncho") == 0, "the scarecrow keeps the garment");
            yield return PH.Until(() => TownScarecrows.Shown.ContainsKey(field.key), 4f);
            if (TownScarecrows.Shown.TryGetValue(field.key, out var tr) && tr) yield return ScheduledScenarios1015.Shoot(c, g, tr.position + Vector3.up * 1.2f, Vector3.right, 4f, "scarecrow_dressed");
            var lines = new List<string>();
            ScheduledScenarios1023.Call(g, "Save1023", lines);
            c.Check(lines.Any(l => l.StartsWith("w|" + field.key + "|")), "saved");
            if (hasGang)
            {
                g.Player.Teleport(PH.Ground(gang.at + new Vector3(1.6f, 0f, 1.6f)) + Vector3.up * 0.2f, 0f);
                yield return PH.Until(() => TownScarecrows.Shown.ContainsKey(gang.key), 8f);
                var f = MadMax.Npc.Factions.OfGang(MadMax.Npc.NpcLore.Gangs[gang.gang]);
                int before = MadMax.Npc.Factions.Rank(f);
                c.Check(ContextActions.Gather(g, new ContextTarget()).Any(o => o.label == "TEAR DOWN THE GANG SCARECROW"), "TEAR DOWN at the gang's scarecrow");
                c.Check(g.TearDownScarecrow(gang.key) && TownScarecrows.Torn.Contains(gang.key), WastelandGame.LastScarecrow ?? "torn down");
                yield return null;
                c.Check(!TownScarecrows.Shown.ContainsKey(gang.key), "it is gone");
                c.Note($"{MadMax.Npc.NpcLore.Gangs[gang.gang]} standing {before} -> {MadMax.Npc.Factions.Rank(f)}");
            }
            else c.Note("no gang scarecrow in this world: tear-down skipped");
            ScheduledScenarios1023.Call(g, "Clear1027");
            g.Player.Teleport(back + Vector3.up * 0.2f, 0f);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
