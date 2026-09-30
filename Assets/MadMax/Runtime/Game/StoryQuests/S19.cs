using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game
{
    /// <summary>S19 THE BELL BENEATH THE WATER: Ester's boatyard on the lake shore (a running generator feeding an air
    /// compressor, her skiff), the drowned chapel on the lake bed with its bell. The bell is a campaign prop the hooks
    /// move: it rises on the lift bags (a fast plan fouls halfway until cleared), is towed behind a boat or hauled in on
    /// the shore line, and is hung again in town or by the water.</summary>
    public partial class WastelandGame
    {
        Placeable s19Bell;
        bool s19Tow, s19AirWarned;
        float s19Check, s19Search;

        partial void Scene_S19()
        {
            Q3ResetPayoff("S19");
            if (!StoryAnchors.Has("ester") || !Build || !Build.Structures) return;
            PutAt("ester", "porch_awning", new Vector3(0f, 0f, -1.8f), 0f);
            PutAt("ester", "table", new Vector3(-2.4f, 0f, 0.6f), 0f);
            PutAt("ester", "barrel", new Vector3(-3.6f, 0f, -2.2f), 0f);
            PutAt("ester", "tyres", new Vector3(3.9f, 0f, 1.8f), 20f);
            var gen = PutAt("ester", "generator", new Vector3(3.6f, 0f, -2.6f), 0f);
            var comp = PutAt("ester", "air_compressor", new Vector3(1.8f, 0f, -2.9f), 0f);
            if (gen && gen.TryGetComponent<Generator>(out var gc)) { gc.fuel = gc.tankLitres; gc.on = true; gen.Dirty(); }
            if (gen && comp && gen.TryGetComponent<UtilityNode>(out var gn) && comp.TryGetComponent<UtilityNode>(out var cn)) cn.Link(gn, UtilityKind.Power);
            PutAt("s19_shore", "post", new Vector3(1.8f, 0f, 0f), 0f);                              // the shore line's post
            // the drowned chapel: a corner of wall, a window, a doorway, and the bell still in its frame
            PutAt("s19_ruin", "wall_brick", new Vector3(-2f, 0f, 1.2f), 0f);
            PutAt("s19_ruin", "wall_brick_window", new Vector3(0f, 0f, 1.2f), 0f);
            PutAt("s19_ruin", "doorway_brick", new Vector3(-3f, 0f, 0.2f), 90f);
            PutAt("s19_ruin", "wall_brick", new Vector3(1.4f, 0f, -1.4f), 70f);
            var bell = PutAt("s19_bell", "alarm_bell", Vector3.zero, 0f);
            if (bell) S19Tag(bell, true);
            // Ester's skiff, afloat between the yard and the ruin
            if (StoryAnchors.Has("s19_skiff"))
            {
                var sp = StoryAnchors.Get("s19_skiff");
                var skiff = Q3Vehicle("Skiff", sp, StoryAnchors.Yaw("s19_skiff"), "s19_skiff", 0.2f);
                float lvl = World.WaterLevel(sp.x, sp.z);
                if (skiff && !float.IsNaN(lvl))
                {
                    skiff.transform.position = new Vector3(sp.x, lvl + 0.25f, sp.z);
                    if (skiff.Body) skiff.Body.position = skiff.transform.position;
                }
                if (skiff) { skiff.name = "Ester's Skiff"; if (skiff.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = Mathf.Min(sys.fuelCapacity, 12f); }
            }
        }

        void S19Tag(Placeable b, bool quiet)
        {
            StoryTag.Set(b.gameObject, "s19_bell");
            if (quiet && b.TryGetComponent<AlarmBell>(out var ab)) ab.watch = 0f;                  // a drowned bell doesn't ring at raiders
        }

        Placeable S19FindBell()
        {
            bool hung = Plot.Flag("s19_hung");
            var at = !hung ? StoryAnchors.Get("s19_ruin") : Q3Route("S19", "hang", "IN THE TOWN") ? StoryAnchors.Get("s19_townbell") : StoryAnchors.Get("s19_shore");
            var b = Q3Prop("alarm_bell", at, hung ? 14f : 400f);
            if (!b && !hung && StoryAnchors.Has("s19_bell"))                                    // lost (blown up, say): Ester's village had a spare
                b = Q3Put("alarm_bell", StoryAnchors.Get("s19_bell"), StoryAnchors.Yaw("s19_bell"));
            if (b) S19Tag(b, !hung);
            return b;
        }

        partial void Tick_S19()
        {
            if (!Player) return;
            if (!s19Bell)
            {
                if (Time.time < s19Search) return;
                s19Search = Time.time + 1f;
                s19Bell = S19FindBell();
                if (!s19Bell) return;
            }
            var bt = s19Bell.transform;
            float dt = Time.deltaTime;
            bool rigged = Plot.StepDone("S19", "rig"), up = Plot.StepDone("S19", "raise"), landed = Plot.StepDone("S19", "landed");
            bool fast = Q3Route("S19", "plan", "ONE BIG");
            if (rigged && !up) S19Rise(bt, dt, fast);
            else if (up && !landed) S19Ashore(bt, dt);
            if (Plot.StepDone("S19", "hang") && !Plot.Flag("s19_hung")) { S19Hang(); return; }

            if (Time.time < s19Check) return;
            s19Check = Time.time + 0.25f;
            var me = Player.transform.position;
            // survey: however you get over the ruin
            if (!Plot.StepDone("S19", "survey"))
            {
                var ruin = StoryAnchors.Get("s19_ruin");
                if (!Current && Q3Flat(me, ruin) < 9f) { if (Player.HeadUnder) Plot.Note("s19:survey_dive"); else if (Player.Swimming) Plot.Note("s19:survey_swim"); }
                if (Current && Current.GetComponent<BoatModel>() && Q3Flat(Current.transform.position, ruin) < 12f) Plot.Note("s19:survey_boat");
            }
            // rigging it yourself: under water beside the bell with the bags and the air to fill them
            if (Plot.StepDone("S19", "plan") && !rigged && !Current && Player.Diving && Player.HeadUnder && Inventory.GetItem("story_lift_bags") > 0
                && Vector3.Distance(me, bt.position) < 3.6f)
            {
                float need = fast ? 45f : 90f;
                if (TankAir >= need + 8f)
                {
                    TankAir -= need;
                    Inventory.TakeItem("story_lift_bags");
                    Plot.Note("s19:rigged");
                    Toast("SLING ROUND THE YOKE, BAGS ON THE SHACKLES, AIR IN: SHE'S RIGGED. NOW GET OUT OF HER WAY");
                    MadMax.Audio.Sfx.Play("chain", bt.position, 0.8f);
                }
                else if (!s19AirWarned)
                {
                    s19AirWarned = true;
                    Toast("NOT ENOUGH AIR TO FILL THE BAGS (" + Mathf.RoundToInt(need) + " S OF TANK): TOP UP AT ESTER'S COMPRESSOR");
                }
            }
            if (rigged && Q3Route("S19", "rig", "BRAM") && !Plot.Flag("s19_bram_bags"))
            {
                Plot.SetFlag("s19_bram_bags");
                if (Inventory.GetItem("story_lift_bags") > 0) Inventory.TakeItem("story_lift_bags");
                Toast("BRAM TAKES THE BAGS AND GOES DOWN, GRUMBLING. MIND THE LINE.");
            }
            // a fouled sling: dive to it, come alongside in a boat, or Bram minds the lines
            if (Plot.Flag("s19_fouled") && !Plot.Flag("s19_cleared"))
            {
                bool diver = !Current && Player.HeadUnder && Vector3.Distance(me, bt.position) < 4f;
                bool boat = Current && Current.GetComponent<BoatModel>() && Q3Flat(Current.transform.position, bt.position) < 6f;
                bool bram = Plot.StepDone("S19", "lines");
                if (diver || boat || bram)
                {
                    Plot.SetFlag("s19_cleared");
                    Toast(bram ? "BRAM GOES DOWN, SWEARS, COMES UP: SHE'S CLEAR" : boat ? "YOU HOOK THE SLING OFF THE BEAM WITH A BOATHOOK. UP SHE COMES" : "YOU WORK THE SLING OFF THE BEAM. UP SHE COMES");
                }
            }
            if (Plot.Flag("s19_hung") && Vector3.Distance(me, bt.position) < 4.5f) Plot.Note("s19:heard");
            S19Payoff(fast);
        }

        /// <summary>The bags lift her: slow and level, or quick and (unless Bram minds the lines) fouled halfway.</summary>
        void S19Rise(Transform bt, float dt, bool fast)
        {
            var p = bt.position;
            float level = terrain.WaterLevel(p.x, p.z), bed = terrain.Height(p.x, p.z);
            if (float.IsNaN(level)) level = bed + 1.6f;
            float top = level - 1.5f;                                                            // afloat: the crown and the yoke above the water
            if (fast && !Plot.StepDone("S19", "lines") && !Plot.Flag("s19_cleared"))
            {
                float mid = Mathf.Lerp(bed, top, 0.5f);
                if (Plot.Flag("s19_fouled")) return;
                p.y = Mathf.MoveTowards(p.y, mid, 0.5f * dt);
                bt.position = p;
                if (p.y >= mid - 0.01f)
                {
                    Plot.SetFlag("s19_fouled");
                    Toast("THE SLING FOULED ON A ROOF BEAM: DIVE DOWN AND CLEAR IT, OR COME ALONGSIDE IN A BOAT");
                    Journal.Add("JOB", "THE BELL BENEATH THE WATER: SHE HANGS FOULED HALFWAY UP. CLEAR THE SLING.");
                    MadMax.Audio.Sfx.Play("chain", p, 0.9f, 0.8f);
                }
                return;
            }
            p.y = Mathf.MoveTowards(p.y, top, (fast ? 0.45f : 0.18f) * dt);
            bt.position = p;
            if (p.y >= top - 0.01f && !Plot.Flag("s19_afloat"))
            {
                Plot.SetFlag("s19_afloat");
                Plot.Note("s19:surfaced");
                Toast("SHE'S UP! THE BELL RIDES ON THE BAGS");
                MadMax.Audio.Sfx.Play("splash", p, 1f);
            }
        }

        /// <summary>Afloat: a boat that comes alongside takes her in tow; once ashore (towed close, or hauled on the shore
        /// line) she is drawn up onto the shingle.</summary>
        void S19Ashore(Transform bt, float dt)
        {
            var p = bt.position;
            bool ashore = Plot.StepDone("S19", "ashore");
            var boat = Current && Current.GetComponent<BoatModel>() ? Current : null;
            if (!ashore && !s19Tow && boat && Q3Flat(boat.transform.position, p) < 6f)
            {
                s19Tow = true;
                Toast("TOWLINE FAST: BRING HER IN TO ESTER'S YARD");
                MadMax.Audio.Sfx.Play("chain", p, 0.7f);
            }
            if (!boat || ashore) s19Tow = false;
            Vector3 target; float speed;
            if (s19Tow) { target = boat.transform.position - boat.transform.forward * 4.5f; speed = 9f; }
            else if (ashore) { target = StoryAnchors.Get("s19_shore"); speed = 1.6f; }
            else return;
            var flat = new Vector3(target.x - p.x, 0f, target.z - p.z);
            p += flat.normalized * Mathf.Min(flat.magnitude, speed * dt);
            float lvl = terrain.WaterLevel(p.x, p.z), ground = terrain.Height(p.x, p.z);
            float y = float.IsNaN(lvl) ? ground : Mathf.Max(ground, lvl - 1.5f);
            p.y = Mathf.MoveTowards(p.y, y, 2.5f * dt);
            bt.position = p;
            if (ashore && flat.magnitude < 0.3f && !Plot.Flag("s19_shingle"))
            {
                Plot.SetFlag("s19_shingle");
                MadMax.Audio.Sfx.Play("bell", p + Vector3.up * 2f, 0.7f, 0.95f, 80f);
            }
        }

        /// <summary>Ester's choice: the bell goes up at the edge of town (it warns of raiders there, [E] rings it) or on the
        /// shore over the drowned village.</summary>
        void S19Hang()
        {
            Plot.SetFlag("s19_hung");
            bool town = Q3Route("S19", "hang", "IN THE TOWN");
            var at = town ? StoryAnchors.Get("s19_townbell") : StoryAnchors.Get("s19_shore");
            float face = town ? StoryAnchors.Yaw("s19_townbell") : StoryAnchors.Yaw("s19_shore");
            if (s19Bell) Destroy(s19Bell.gameObject);
            s19Bell = Q3Put("alarm_bell", at, face);
            if (s19Bell) S19Tag(s19Bell, false);
            SetWaypoint(at, "THE BELL", true);
            Journal.Add("PLACE", town ? "ESTER'S BELL HANGS AT THE EDGE OF TOWN NOW. [E] RINGS IT; IT RINGS BY ITSELF WHEN RAIDERS COME."
                                      : "ESTER'S BELL HANGS BY THE LAKE, OVER THE DROWNED VILLAGE. [E] RINGS IT.");
            S19Payoff(Q3Route("S19", "plan", "ONE BIG"));
        }

        static void S19Payoff(bool fast)
        {
            string how = fast ? (Plot.Flag("s19_fouled") ? "ONE BIG BAG, ONE FOULED SLING, ONE LESSON" : "ONE BIG BAG AND SOME LUCK") : "SLOW AND LEVEL, THE WAY BELLS LIKE IT";
            string who = Q3Route("S19", "rig", "BRAM") ? "BRAM RIGGED HER" : "YOU RIGGED HER YOURSELF";
            string land = Q3Route("S19", "ashore", "TOWED") ? ", TOWED IN BY BOAT" : Q3Route("S19", "ashore", "HAUL") ? ", HAULED IN ON THE SHORE LINE" : "";
            string where = !Plot.StepDone("S19", "hang") ? "" : Q3Route("S19", "hang", "IN THE TOWN") ? " SHE HANGS AT THE EDGE OF TOWN, AND THE TOWN PRETENDS TO MIND THE NOISE."
                         : " SHE HANGS BY THE LAKE, FOR THE VILLAGE UNDER IT.";
            Q3Payoff("S19", "ESTER'S BELL IS UP: " + how + "; " + who + land + "." + where + " ESTER'S SLIPWAY SERVICES YOUR BOATS AT COST NOW.");
        }
    }
}
