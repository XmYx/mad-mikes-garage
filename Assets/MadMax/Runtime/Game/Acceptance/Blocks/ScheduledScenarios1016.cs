using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Animals;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-16: the sprinkler's arc, sawdust at the saws, bees working the flowering beds,
    /// flies following rot (carcasses, food left out, an unwashed player), a programme per tape and people watching
    /// the TV, the neighbour who keeps watch on the base.</summary>
    public static class ScheduledScenarios1016
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new SprinklerArc();
            yield return new SawdustPile();
            yield return new BedBees();
            yield return new FliesFollowRot();
            yield return new TvProgrammes();
            yield return new NeighbourWatch();
        }

        internal static Vector3 Fwd(WastelandGame g) { var f = g.Player.transform.forward; f.y = 0f; return f.normalized; }
    }

    /// <summary>The sprinkler throws a proper arc (over a metre up, out to its reach) at any frame rate, and its head
    /// ticks round the arc and springs back.</summary>
    class SprinklerArc : Scenario
    {
        public override string Id => "living.sprinkler_arc";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            // one long step integrates exactly (the 2026-10-15 jet lost its climb to a single big step)
            Vector3 p = Vector3.zero, v = new Vector3(0f, 3f, 0f);
            Droplets.Step(ref p, ref v, new Vector3(0f, -9.81f, 0f), 0f, 0.3f);
            c.Check(Mathf.Abs(p.y - (0.9f - 0.5f * 9.81f * 0.09f)) < 0.002f, $"a 0.3 s step lands where the parabola does ({p.y:0.000} m)");
            var fwd = ScheduledScenarios1016.Fwd(g);
            var sp = PH.Piece(g, "sprinkler", g.Player.transform.position + fwd * 5f, -fwd);
            if (!c.Check(sp, "a sprinkler")) yield break;
            var s = sp.GetComponent<Sprinkler>();
            s.Spray(10f);
            yield return new WaitForSeconds(0.3f);
            var jet = sp.GetComponentInChildren<Droplets>();
            if (!c.Check(jet, "the jet")) { Object.Destroy(sp.gameObject); yield break; }
            float hi = 0f, far = 0f, h0 = s.Heading;
            var seen = new HashSet<int>();
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                var sr = jet.Spread();   // "n drops, y a..b m, out to c m"
                int yi = sr.IndexOf(".."), mi = sr.IndexOf(" m,", yi), oi = sr.IndexOf("out to ");
                if (yi > 0 && mi > yi && oi > 0)
                {
                    float.TryParse(sr.Substring(yi + 2, mi - yi - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y1);
                    float.TryParse(sr.Substring(oi + 7).Replace(" m", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f1);
                    hi = Mathf.Max(hi, y1); far = Mathf.Max(far, f1);
                }
                seen.Add(Mathf.RoundToInt(s.Heading));
                yield return null;
            }
            c.Metric("jet_peak_m", hi, "m");
            c.Metric("jet_reach_m", far, "m");
            c.Check(hi > 0.9f, $"the jet rises over 0.9 m ({hi:0.00} m)");
            c.Check(far > 2.5f, $"and carries out past 2.5 m ({far:0.00} m)");
            c.Check(!Mathf.Approximately(h0, s.Heading) && seen.All(h => Mathf.Abs(h) <= Sprinkler.Arc + 1f), $"the head works round its arc ({seen.Count} headings)");
            yield return ScheduledScenarios1015.Shoot(c, g, sp.transform.position + Vector3.up * 0.6f, sp.transform.right, 5.5f, "sprinkler_arc");
            Object.Destroy(sp.gameObject);
        }
    }

    /// <summary>A saw at work throws dust; each finished job leaves a pile that sweeps up as wood.</summary>
    class SawdustPile : Scenario
    {
        public override string Id => "living.sawdust";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var fwd = ScheduledScenarios1016.Fwd(g);
            var bench = PH.Piece(g, "saw_bench", g.Player.transform.position + fwd * 3f, -fwd);
            if (!c.Check(bench, "a saw bench")) yield break;
            var st = bench.GetComponent<CraftingStation>();
            var r = RecipeLibrary.Get("saw_planks");
            if (!c.Check(st && r != null, "the hand-saw recipe")) { Object.Destroy(bench.gameObject); yield break; }
            st.queue.Add(new CraftingStation.Job { recipe = r.id, progress = 0.6f, speed = 0.05f });
            yield return new WaitForSeconds(1.2f);
            var dust = bench.GetComponent<Sawdust>();
            c.Check(dust && dust.Cutting && dust.Airborne > 0, $"dust off the blade while it cuts ({(dust ? dust.Airborne : -1)} specks)");
            yield return ScheduledScenarios1015.Shoot(c, g, bench.transform.position + Vector3.up * 0.7f, bench.transform.forward + bench.transform.right * 0.6f, 2f, "sawdust_cut");
            st.queue[0].progress = 0.999f; st.queue[0].speed = 5f;
            yield return PH.Until(() => st.queue.Count == 0, 5f);
            c.Check(st.queue.Count == 0, "the job finished");
            float share = dust ? dust.amount : 0f;
            c.Check(dust && share > 0.5f && dust.PileShown, $"a pile in front of the cut ({share:0.00} wood)");
            if (dust)
            {
                dust.amount = 2.4f;
                int before = g.Inventory.Get(ResourceType.Wood);
                int n = dust.Sweep(g.Inventory);
                c.Check(n == 2 && g.Inventory.Get(ResourceType.Wood) == before + 2 && dust.amount < 0.5f, $"swept up as 2 wood (left {dust.amount:0.0})");
            }
            yield return new WaitForSeconds(1.8f);
            c.Check(!dust || dust.Airborne == 0, "no dust once the saw stops");
            Object.Destroy(bench.gameObject);
        }
    }

    /// <summary>Bees from a hive work the flowering beds within reach; none without a hive, none on a bare bed.</summary>
    class BedBees : Scenario
    {
        public override string Id => "living.bed_bees";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(11f);
            bool wasRain = Weather.Raining; Weather.Raining = false;
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var bed = PH.Piece(g, "garden_plot", me + fwd * 3f, -fwd);
            var bare = PH.Piece(g, "garden_plot", me + fwd * 3f + right * 3f, -fwd);
            if (!c.Check(bed && bare, "two beds")) yield break;
            var plot = bed.GetComponent<GardenPlot>();
            plot.Sow("seed_sunflower", 1f); plot.growth = 0.7f;
            c.Check(Beehive.BedBees(plot) == 0f, "no hive, no bees");
            var hive = PH.Piece(g, "beehive", me + fwd * 9f, -fwd);
            yield return new WaitForSeconds(4.5f);
            float busy = hive.GetComponent<Beehive>().Busy();
            c.Note($"hive busy {busy:0.00}, temp {Weather.TemperatureAt(me.z):0} C");
            var sw = bed.GetComponent<Swarm>();
            c.Check(sw && sw.kind == Swarm.Kind.Forage, "the flowering bed gets foragers");
            c.Check(sw && (sw.Flying > 0 || busy <= 0.1f), $"working over the flowers ({(sw ? sw.Flying : -1)})");
            c.Check(!bare.GetComponent<Swarm>(), "the bare bed none");
            yield return ScheduledScenarios1015.Shoot(c, g, bed.transform.position + Vector3.up * 0.4f, bed.transform.forward + bed.transform.right * 0.3f, 1.8f, "bed_bees");
            Weather.Raining = wasRain;
            foreach (var p in new[] { bed, bare, hive }) if (p) Object.Destroy(p.gameObject);
        }
    }

    /// <summary>Flies gather over a carcass past its first hours, over rotten food, around an unwashed player; food left
    /// out on the ground rots where it lies.</summary>
    class FliesFollowRot : Scenario
    {
        public override string Id => "living.flies_rot";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(12f);
            bool wasRain = Weather.Raining; Weather.Raining = false;
            var me = g.Player.transform.position;
            float temp = Weather.TemperatureAt(me.z);
            if (temp < 6f) { c.Block($"too cold for flies here ({temp:0} C)"); Weather.Raining = wasRain; yield break; }
            var fwd = ScheduledScenarios1016.Fwd(g);
            var at = me + fwd * 3f; at.y = DeformableTerrain.Instance.Height(at.x, at.z) + 0.2f;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.position = at; body.transform.localScale = new Vector3(0.9f, 0.4f, 0.5f);
            var carcass = body.AddComponent<Carcass>();
            carcass.def = AnimalLibrary.Get("goat") ?? AnimalLibrary.All.FirstOrDefault();
            carcass.size = 1f; carcass.diedAt = DayNight.TotalDays - 1.2f;
            c.Check(WastelandGame.FlyLevel(0.05f, at) == 0f, "a fresh kill draws none yet");
            // food left out: an apple with a very short life rots on the ground
            var f = FoodLibrary.Get("food_apple");
            float keep = f != null ? f.spoilMinutes : 0f;
            if (f != null) f.spoilMinutes = 0.05f;
            var apple = WorldItem.Create("food_apple", 1, -1f, g.propMaterial, me + fwd * 2f + Vector3.up * 0.3f, Quaternion.identity);
            int rot0 = WastelandGame.RottedOnGround;
            float hy = g.Stats.hygiene; g.Stats.hygiene = 8f;
            yield return new WaitForSeconds(7f);
            if (f != null) f.spoilMinutes = keep;
            var flies = body.GetComponent<Swarm>();
            c.Check(flies && flies.kind == Swarm.Kind.Flies && flies.Flying > 0, $"flies over the day-old carcass ({(flies ? flies.Flying : -1)})");
            c.Check(apple && apple.key == "food_rotten" && WastelandGame.RottedOnGround > rot0, "the apple left out rotted where it lay");
            yield return new WaitForSeconds(2.5f);
            var onApple = apple ? apple.GetComponent<Swarm>() : null;
            c.Check(onApple && onApple.Flying > 0, "and draws its own flies");
            var pf = g.PlayerFlySwarm;
            c.Check(pf && pf.Flying > 0, $"an unwashed player draws flies ({(pf ? pf.Flying : -1)})");
            yield return ScheduledScenarios1015.Shoot(c, g, at, fwd, 1.6f, "flies_carcass");
            g.Stats.hygiene = 95f;
            yield return new WaitForSeconds(1.5f);
            c.Check(!pf || pf.Flying == 0, "washed: they leave");
            g.Stats.hygiene = hy;
            Weather.Raining = wasRain;
            Object.Destroy(body);
            if (apple) Object.Destroy(apple.gameObject);
        }
    }

    /// <summary>Each tape plays its own programme; someone idle nearby sits down in front of a playing TV and watches.</summary>
    class TvProgrammes : Scenario
    {
        public override string Id => "living.tv_programmes";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(11f);
            var tapes = new[] { "vhs_driving", "vhs_garage", "vhs_survival", "vhs_karate", "vhs_demolition", "vhs_salesman", "vhs_garden" };
            var shows = tapes.Select(TvSet.ShowFor).ToList();
            c.Check(shows.Distinct().Count() == tapes.Length, "every tape its own programme (" + string.Join(", ", shows.Select(k => TvSet.ShowNames[k])) + ")");
            c.Check(tapes.All(t => MadMax.RPG.MediaLibrary.Get(t) != null), "all of them are real tapes");
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g);
            var tvp = PH.Piece(g, "tv", me + fwd * 6f, -fwd);
            var chair = PH.Piece(g, "chair", me + fwd * 3.6f, fwd);       // facing the screen
            if (!c.Check(tvp && chair, "a TV and a chair in front of it")) yield break;
            var tv = tvp.GetComponent<TvSet>();
            foreach (var t in tapes)
            {
                tv.Play(t);
                tv.Draw(1.1f); float a = tv.Brightness;
                tv.Draw(2.9f); float b = tv.Brightness;
                c.Check(a > 0.03f && !Mathf.Approximately(a, b), $"{TvSet.ShowNames[tv.Show]}: a moving picture ({a:0.00} / {b:0.00})");
            }
            tv.Play("vhs_karate");
            var n = ScheduledScenarios1012.Person(g, "tv:watcher", NpcRole.Resident, 4711, me + fwd * 2f + Vector3.Cross(Vector3.up, fwd) * 3f);
            n.home = n.transform.position;
            yield return PH.Until(() => n.WatchingSeated, 20f);
            c.Check(n.Watching == tv, "a resident nearby comes to watch");
            c.Check(n.WatchingSeated, "from the chair in front of it");
            yield return ScheduledScenarios1015.Shoot(c, g, tvp.transform.position + Vector3.up * 0.5f, Vector3.Cross(Vector3.up, fwd), 4f, "tv_watcher");
            tv.Stop();
            yield return PH.Until(() => !n.WatchingSeated && n.Watching == null, 6f);
            c.Check(n.Watching == null && !n.WatchingSeated, "and gets up when it goes off");
            Object.Destroy(n.gameObject);
            Object.Destroy(tvp.gameObject); Object.Destroy(chair.gameObject);
        }
    }

    /// <summary>A neighbour whose favour was done keeps an eye on the base: radios before a raid while the player is
    /// away, and turns out with the neighbours when it comes (defence in BaseRaid's resolution).</summary>
    class NeighbourWatch : Scenario
    {
        public override string Id => "npc.neighbour_watch";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var me = g.Player.transform.position;
            var town = g.NearestTown(me);
            if (town == null || Vector2.Distance(town.pos, new Vector2(me.x, me.z)) > WastelandGame.WatchReach - 200f) { c.Block("no town within watching reach"); yield break; }
            var fwd = ScheduledScenarios1016.Fwd(g);
            var flag = PH.Piece(g, "claim_flag", me + fwd * 4f, -fwd);
            if (!c.Check(flag, "a claim")) yield break;
            var claim = flag.GetComponent<ClaimFlag>();
            g.NeighbourErrands.Clear();
            c.Check(g.Watchers(claim).Count == 0, "no favour done, no watch");
            c.Check(g.WatchTick(claim, me + fwd * 400f) == null, "and no word");
            g.NeighbourErrands["r" + town.index + ",0"] = new WastelandGame.NeighbourErrand { kind = 0, state = 2, due = -1, town = town.index, name = "OLD MAE" };
            c.Check(g.Watchers(claim).Count == 1, "the favour done: they keep watch");
            c.Check(g.WatchTick(claim, me) == null, "nothing while the player is at home");
            float next = BaseRaid.NextDay;
            BaseRaid.NextDay = DayNight.TotalDays + 1f / 24f;
            string word = g.WatchTick(claim, me + fwd * 400f);
            var gang = NpcDirector.Instance ? NpcDirector.Instance.RaidersByRoad(claim.transform.position) : null;
            c.Note(word ?? "(no word)");
            if (gang != null && gang.SparesBases) c.Check(word == null, "the gang spares the base: nothing to warn of");
            else c.Check(word != null && word.Contains("OLD MAE") && word.Contains("SCOUTS"), "an hour or two ahead they radio a warning");
            string again = g.WatchTick(claim, me + fwd * 400f);
            c.Check(again == null || !again.Contains("SCOUTS"), "once per raid");
            int help = BaseRaid.NeighbourHelp != null ? BaseRaid.NeighbourHelp(claim, out string who) : 0;
            c.Check(help == WastelandGame.WatchDefence, $"they turn out when it comes (+{help} defence)");
            ScheduledScenarios1015.SaveRoundTrip(g);
            string after = g.WatchTick(claim, me + fwd * 400f);
            c.Check(g.NeighbourErrands.Count == 1 && (after == null || !after.Contains("SCOUTS")), "the favour and the warning are remembered over a save");
            BaseRaid.NextDay = next;
            g.NeighbourErrands.Clear();
            Object.Destroy(flag.gameObject);
            yield return null;
        }
    }
}
