using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // S15 THE ORGAN RUNS ON DIESEL: the open-air stage (boards, two lamps, benches at the sides), the organ (S15Organ on
    // the lamp over its music stand), Hollis's cracked and empty generator beside the stage, cabled to the lamps and the
    // audience heater (on); the chapel ruin with two pipes on the ground and one in a crate. The load is balanced once
    // the organ has run steadily for ten seconds; the recital (a PerformanceScene) sounds as quiet or as noisy as the
    // supply the player arranged.
    public partial class WastelandGame
    {
        static readonly Vector3 S15OrganAt = new Vector3(0f, 0f, -4.4f), S15GenAt = new Vector3(5.6f, 0f, -6.4f);
        float s15T, s15Steady;

        partial void Scene_S15()
        {
            Q5ResetPayoff("S15");
            if (!StoryAnchors.Has("hollis") || !Build || !Build.Structures) return;
            foreach (float x in new[] { -2f, 0f, 2f }) Q5Put("hollis", "floor_wood", new Vector3(x, 0f, -4.2f), 0f);
            var organ = Q5Put("hollis", "light_ceiling", S15OrganAt, 0f, S15Organ.Drop);
            var gen = Q5Put("hollis", "generator", S15GenAt, -90f);
            var lampL = Q5Put("hollis", "lamppost", new Vector3(-4.2f, 0f, -3.6f), 0f);
            var lampR = Q5Put("hollis", "lamppost", new Vector3(4.2f, 0f, -3.6f), 0f);
            var heater = Q5Put("hollis", "heater", new Vector3(-6.4f, 0f, 3.4f), 90f);
            Q5Put("hollis", "bench", new Vector3(-7.2f, 0f, 5.6f), 90f);
            Q5Put("hollis", "bench", new Vector3(7.2f, 0f, 5.6f), -90f);
            Q5Put("hollis", "barrel", new Vector3(6.8f, 0f, -4.4f), 0f);
            if (gen)
            {
                if (gen.TryGetComponent<Generator>(out var g0)) { g0.fuel = 0f; g0.on = false; }
                gen.hits = Mathf.Max(1, gen.MaxHits - 2);
                gen.Dirty();
                var gn = gen.GetComponent<UtilityNode>();
                foreach (var p in new[] { lampL, lampR, heater }) if (p && p.TryGetComponent<UtilityNode>(out var n)) n.Link(gn, UtilityKind.Power);
            }
            if (heater && heater.TryGetComponent<Climate>(out var cl)) { cl.on = true; heater.Dirty(); }
            if (organ) organ.gameObject.AddComponent<S15Organ>();
            // the chapel ruin: broken walls, rubble, two pipes on the ground and one in a crate
            if (StoryAnchors.Has("s15_ruin"))
            {
                foreach (var (x, z, turn) in new[] { (-2f, -2f, 0f), (0f, -2f, 0f), (-3f, -1f, 90f), (-3f, 1f, 90f) })
                {
                    var w = Q5Put("s15_ruin", "wall_brick", new Vector3(x, 0f, z), turn);
                    if (w) { w.hits = Mathf.Max(1, w.MaxHits / 3); w.Dirty(); }
                }
                Q5Put("s15_ruin", "tyres", new Vector3(2.4f, 0f, 1.2f), 10f);
                var crate = Q5Put("s15_ruin", "crate", new Vector3(1.8f, 0f, -1.6f), 20f);
                if (crate && crate.TryGetComponent<Container>(out var box)) { box.inventory.AddItem("story_organ_pipe", 1); crate.Dirty(); }
                foreach (var off in new[] { new Vector3(-1.2f, 0.2f, 0.4f), new Vector3(0.9f, 0.2f, 1.6f) })
                    SpawnWorldItem("story_organ_pipe", 1, -1f, Q5At("s15_ruin", off) + Vector3.up * 0.2f, Quaternion.Euler(0f, StoryAnchors.Yaw("s15_ruin") + off.x * 40f, 90f), null);
            }
        }

        /// <summary>The runtime organ on its lamp (added again after a reload; see Q5Dressing).</summary>
        S15Organ S15OrganNow()
        {
            if (!StoryAnchors.Has("hollis") || MadMax.Story.Story.StateOf("S15") == MadMax.Story.Story.State.Locked) return null;
            var lamp = Q5Prop("light_ceiling", Q5At("hollis", S15OrganAt), 1.5f);
            if (!lamp) return null;
            var o = lamp.GetComponent<S15Organ>();
            return o ? o : lamp.gameObject.AddComponent<S15Organ>();
        }

        void S15Dress() => S15OrganNow();

        /// <summary>The organ's node shares a power network with a supply (a generator, battery, panel...), whether or not
        /// that supply can carry it yet.</summary>
        static bool S15Cabled(UtilityNode node)
        {
            if (node.powerNet < 0) return false;
            foreach (var n in UtilityNode.All) if (n && n != node && n.powerNet == node.powerNet && n.IsSource) return true;
            return false;
        }

        /// <summary>A running generator within earshot of the organ.</summary>
        bool S15Noisy(Vector3 organ)
        {
            foreach (var n in UtilityNode.All)
                if (n && n.TryGetComponent<Generator>(out var gen) && gen.on && gen.fuel > 0f && n.produce > 0f && Q5Flat(n.transform.position, organ) < 22f) return true;
            return false;
        }

        /// <summary>The recital: the beats follow the supply the player arranged.</summary>
        Performance S15Recital(bool quiet, S15Organ organ)
        {
            var p = new Performance { key = "s15:recital", title = "HOLLIS REED'S RECITAL", stage = "s15_stage", extras = 10, crowdAt = 6.5f };
            p.performers.Add("hollis");
            if (quiet)
                p.Line(null, "HOLLIS SITS. THE BLOWER BREATHES IN. THE LAMP OVER THE MUSIC STAND FLICKERS AND HOLDS.", 4f)
                 .Line("hollis", "THIS IS 'THE LONG ROAD HOME'. I WROTE IT FOR A TOWN THAT WASN'T HERE YET.", 5f)
                 .Line(null, "THE FIRST CHORD FILLS THE SQUARE. SOMEONE AT THE BACK TAKES THEIR HAT OFF.", 6f)
                 .Line(Performance.Crowd, "OOH.", 3f)
                 .Line(null, "THE SLOW PART. THE THREE NEW PIPES SING A LITTLE BRIGHTER THAN THE OLD ONES. NOBODY MINDS.", 6f)
                 .Line(null, "THE LAST CHORD HANGS IN THE AIR UNTIL THE WIND TAKES IT.", 6f)
                 .Line(Performance.Crowd, "BRAVO!", 3f, "crowd_cheer")
                 .Line("hollis", "THANK YOU. THERE'S TEA, IF ANYONE CAN FIND THE KETTLE.", 4f);
            else
                p.Line(null, "HOLLIS SITS. THE BLOWER BREATHES IN, AND THE GENERATOR BESIDE THE STAGE CLEARS ITS THROAT.", 4f)
                 .Line("hollis", "THIS IS 'THE LONG ROAD HOME'. IN D. THE GENERATOR IS IN SOMETHING ELSE.", 5f)
                 .Line(null, "THE FIRST CHORD FILLS THE SQUARE, WITH A DIESEL THROB UNDER IT LIKE A SECOND, RUDER ORGAN.", 6f)
                 .Line(Performance.Crowd, "WHAT?", 3f)
                 .Line(null, "IN THE SLOW PART THE GENERATOR IS THE LOUDEST THING THERE. A CHILD HUMS ALONG WITH IT, IN TUNE WITH NEITHER.", 6f)
                 .Line(null, "THE LAST CHORD AND THE GENERATOR STOP AT DIFFERENT TIMES.", 5f)
                 .Line(Performance.Crowd, "BRAVO! ...PROBABLY!", 3f, "crowd_cheer")
                 .Line("hollis", "THANK YOU. NEXT YEAR, BATTERIES.", 4f);
            int[][] chords = { new[] { 50, 62, 66, 69 }, new[] { 55, 62, 67, 71 }, new[] { 57, 61, 64, 69 }, new[] { 50, 62, 66, 74 } };
            p.onBeat = i => { if (i >= 2 && i <= 5 && organ) organ.Chord(p.beats[i].seconds, chords[(i - 2) % chords.Length]); };
            p.hold = () => organ && organ.Powered ? null : "THE ORGAN HAS NO WIND: ITS BLOWER HAS NO POWER";
            p.onDone = () =>
            {
                Q5Payoff("S15", quiet
                    ? "HOLLIS REED GAVE HIS RECITAL IN THE OPEN AIR ON QUIET POWER. THE SQUARE HEARD EVERY PIPE, THE THREE NEW ONES INCLUDED."
                    : "HOLLIS REED GAVE HIS RECITAL IN THE OPEN AIR, WITH A GENERATOR THUMPING ALONG BESIDE THE STAGE. PEOPLE STILL HUM THE GENERATOR'S PART.");
                MadMax.Story.Story.Note(quiet ? "s15:quiet" : "s15:noisy");
            };
            return p;
        }

        partial void Tick_S15()
        {
            if ((s15T += Time.deltaTime) < 0.25f) return;
            float step = s15T; s15T = 0f;
            var organ = S15OrganNow();
            var gen = StoryAnchors.Has("hollis") ? Q5Prop("generator", Q5At("hollis", S15GenAt), 2f) : null;
            if (gen && gen.hits >= gen.MaxHits && gen.TryGetComponent<Generator>(out var g0) && g0.fuel > 0f) Q5Note("s15:gen");
            var node = organ ? organ.GetComponent<UtilityNode>() : null;
            if (node && S15Cabled(node)) Q5Note("s15:cabled");
            // balanced: the organ runs steadily for ten seconds with the supply not overloaded
            if (MadMax.Story.Story.StepDone("S15", "power") && !MadMax.Story.Story.StepDone("S15", "balance") && node)
            {
                s15Steady = node.Powered && !node.Overloaded ? s15Steady + step : 0f;
                if (s15Steady >= 10f)
                {
                    var heater = StoryAnchors.Has("hollis") ? Q5Prop("heater", Q5At("hollis", new Vector3(-6.4f, 0f, 3.4f)), 2f) : null;
                    var hn = heater ? heater.GetComponent<UtilityNode>() : null;
                    var hc = heater ? heater.GetComponent<Climate>() : null;
                    Q5Note(hn && hn.Shed ? "s15:balanced_breaker" : hc && !hc.on ? "s15:balanced_off" : "s15:balanced_power");
                }
            }
            // the recital, on the player's word: quiet or noisy as the supply stands when it begins
            if (MadMax.Story.Story.StepDone("S15", "start") && !MadMax.Story.Story.StepDone("S15", "recital") && organ
                && (!PerformanceScene.Current || PerformanceScene.Current.Show.key != "s15:recital"))
            {
                bool quiet = !S15Noisy(organ.transform.position);
                PerformanceScene.Begin(this, S15Recital(quiet, organ));
            }
        }
    }
}
