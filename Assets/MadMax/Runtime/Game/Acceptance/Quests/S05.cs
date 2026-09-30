using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S05(List<Scenario> into) => into.Add(new QuestS05());
    }

    /// <summary>S05 NOT THAT KIND OF SHOT in a sandbox world: the battered boards repaired with build mode's repair, the
    /// rules, the loaned pipe pistol fired from the line (the gun's own Strike: rounds, reloads, noise), each board hit
    /// twice, the dummy round stopping the gun and cleared with [R], the medal, the pistol handed back.</summary>
    class QuestS05 : Scenario
    {
        public override string Id => "story.s05";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("S05") == MadMax.Story.Story.State.Open, "S05 is on offer in a sandbox world");
            yield return H.Walk(c, "amos", 2.5f);
            yield return H.Until(() => g.CastBody("amos") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("amos"), "IS THIS A SHOOTING RANGE"), "Amos Bell's range needs fixing")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StateOf("S05") == MadMax.Story.Story.State.Active, 3f);

            // ---- the boards
            yield return H.Walk(c, "s05_targets", -4f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S05", "look"), 4f);
            var t = StoryAnchors.Get("s05_targets");
            var boards = Placeable.All.Where(p => p && p.id == "sign" && g.IsStoryProp(p) && Vector3.Distance(p.transform.position, t) < 6f).ToList();
            if (!c.Check(boards.Count == 3 && boards.All(b => b.hits < b.MaxHits), $"three battered target boards ({boards.Count})")) yield break;
            c.Screenshot("range");
            yield return null;
            foreach (var b in boards) g.Build.Repair(b);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S05", "targets"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S05", "targets") && boards.All(b => b && b.hits >= b.MaxHits), "all three repaired with the scrap and wood Amos gave");

            // ---- the rules and the loaned pistol
            yield return H.Walk(c, "amos", 2.5f);
            int pistols = g.Inventory.GetItem("tool_pipe_pistol");
            c.Check(H.Talk(g, g.CastBody("amos"), StoryLibrary.S05Live), "hear the rules");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S05", "rules"), 4f);
            c.Check(g.Inventory.GetItem("tool_pipe_pistol") == pistols + 1 && g.Inventory.GetItem("ammo_cartridge") >= 12, "a loaned pipe pistol and twelve rounds");
            var line = StoryAnchors.Get("s05_line"); var down = StoryAnchors.Yaw("s05_line");
            var at = line - Quaternion.Euler(0f, down, 0f) * Vector3.forward * 1f; at.y = MadMax.World.DeformableTerrain.Instance.Height(at.x, at.z) + 0.3f;
            g.Player.Teleport(at, down);
            c.Fixture("stepped up to the firing line (teleport)");
            if (!g.Player.Tool || g.Player.Tool.id != "tool_pipe_pistol") g.UseItem("tool_pipe_pistol");
            yield return new WaitForSeconds(0.5f);
            var gun = g.Player.Tool as RangedTool;
            if (!c.Check(gun && gun.id == "tool_pipe_pistol", "the pistol in hand")) yield break;

            // ---- the course: each board twice; the gun stops after the second shot
            bool sawJam = false; int shots = 0;
            float t0 = Time.time;
            for (int i = 0; i < 6 && Time.time - t0 < 60f;)
            {
                yield return H.Until(() => gun && !gun.Reloading && g.Rounds(gun.id) > 0, 4f);
                if (gun.Jammed)
                {
                    sawJam = true;
                    gun.Strike(g.Player);                                                          // click
                    yield return new WaitForSeconds(0.3f);
                    c.Check(g.Rounds(gun.id) > 0, "a stoppage fires nothing");
                    gun.ReloadKey(g);                                                              // [R]
                    yield return new WaitForSeconds(1.1f);
                    continue;
                }
                int before = g.Rounds(gun.id);
                gun.Strike(g.Player);
                if (g.Rounds(gun.id) < before)
                {
                    shots++;
                    var b = boards[i / 2];
                    yield return null;
                    b.ApplyHit(b.transform.position + Vector3.up * 1.1f, (b.transform.position - at).normalized, gun.power, 0.1f, g.Player.gameObject);
                    i++;
                }
                yield return new WaitForSeconds(0.3f);
            }
            c.Fixture("each shot lands on the next board (IDamageable hit from the player)");
            c.Metric("rounds_fired", shots, "");
            c.Check(sawJam, "the dummy round stopped the gun");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S05", "course") && MadMax.Story.Story.StepDone("S05", "jam"), 4f);
            yield return new WaitForSeconds(0.3f);                                                   // Amos restores the boards and oils your kit
            c.Check(MadMax.Story.Story.StepDone("S05", "jam"), "the jam cleared with the muzzle downrange");
            c.Check(MadMax.Story.Story.Route("S05", "course") == StoryLibrary.S05CourseLive, "the live course: " + MadMax.Story.Story.Route("S05", "course"));
            c.Check(MadMax.Story.Story.StepDone("S05", "medal") && g.Inventory.GetItem("story_s05_medal") > 0, "six rounds, six hits: the precision medal");
            c.Check(boards.All(b => b && b.hits <= b.MaxHits), "the boards are ordinary boards again after the course");
            c.Screenshot("course");
            yield return null;

            // ---- hand it back
            yield return H.Walk(c, "amos", 2.5f);
            c.Check(H.Talk(g, g.CastBody("amos"), "HERE'S YOUR PISTOL BACK"), "the pistol goes back to Amos");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S05") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("S05") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("s05_done"), "S05 NOT THAT KIND OF SHOT is done");
            c.Check(g.Inventory.GetItem("tool_pipe_pistol") == pistols && (pistols > 0 || !g.Player.Tool || g.Player.Tool.id != "tool_pipe_pistol"), "the loaner went back, out of your hand");
            c.Check(Journal.Entries.Any(e => e.text.Contains("PRECISION MEDAL ON YOU")), "the payoff remembers the medal");
        }
    }
}
