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
        static partial void Tests_S01(List<Scenario> into) => into.Add(new StoryS01());
    }

    /// <summary>S01 A FRIDGE FULL OF FLOWERS in a sandbox world: Orla's cold cabinet has three faults (crates on the grille,
    /// no cable, a dead compressor); the crates are broken, a cable is run from her generator, the coil and copper come out
    /// of the chest freezer at the appliance dump and are fitted with [T]; the cabinet runs below 8 °C and goes behind an
    /// ESSENTIAL breaker; Orla sets out the first bunch and the keepsake is taken (remembered in the closing line).</summary>
    class StoryS01 : Scenario
    {
        public override string Id => "story.s01";
        public override float Timeout => 200f;

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("S01") == MadMax.Story.Story.State.Open, "S01 is on offer in a sandbox world");
            yield return H.Walk(c, "orla", 2.5f);
            yield return H.Until(() => g.CastBody("orla") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("orla"), "THAT CABINET SOUNDS"), "Orla Finch asks for a look at her cabinet")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StateOf("S01") == MadMax.Story.Story.State.Active, 3f);
            var a = StoryAnchors.Get("orla");
            var fridge = Placeable.All.FirstOrDefault(p => p && p.id == "fridge" && g.IsStoryProp(p) && Flat(p.transform.position, a) < 7f);
            var cs = fridge ? fridge.GetComponent<ColdStore>() : null;
            if (!c.Check(cs && cs.broken, "the cold cabinet's compressor is dead")) yield break;
            var node = fridge.GetComponent<UtilityNode>();
            var gen = Placeable.All.FirstOrDefault(p => p && p.id == "generator" && g.IsStoryProp(p) && Flat(p.transform.position, a) < 10f);
            var genNode = gen ? gen.GetComponent<UtilityNode>() : null;
            if (!c.Check(genNode && gen.GetComponent<Generator>().on, "Orla's generator runs")) yield break;
            yield return new WaitForSeconds(0.6f);
            c.Check(!node.Powered, "no cable reaches the cabinet");
            var crates = Placeable.All.Where(p => p && p.id == "crate" && g.IsStoryProp(p) && Flat(p.transform.position, fridge.transform.position) < 1.7f).ToList();
            c.Check(crates.Count == 2, $"flower crates smother its back grille ({crates.Count})");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S01", "look"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S01", "look"), "looked the cabinet over");
            c.Screenshot("stall");
            yield return null;

            // ---- the grille: break the crates
            foreach (var cr in crates) for (int i = 0; i < 10 && cr; i++) { cr.ApplyHit(cr.transform.position + Vector3.up * 0.3f, fridge.transform.forward, 1f, 0.3f, g.Player.gameObject); yield return null; }
            c.Fixture("the crates broken with blows (as a sledgehammer would)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S01", "vent"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S01", "vent"), "the grille can breathe");
            // ---- the cable
            node.Link(genNode, UtilityKind.Power);
            c.Fixture("a cable run from the generator to the cabinet (as the build tool's cable does)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S01", "cable"), 5f);
            c.Check(node.Powered && MadMax.Story.Story.StepDone("S01", "cable"), "the cabinet has power again");
            c.Check(!cs.Running && !MadMax.Story.Story.StepDone("S01", "test"), "but the dead compressor doesn't run");
            // ---- the coil from the appliance dump
            yield return H.Walk(c, "s01_dump", 3.5f);
            var dump = StoryAnchors.Get("s01_dump");
            var chest = Placeable.All.FirstOrDefault(p => p && p.id == "freezer" && g.IsStoryProp(p) && Flat(p.transform.position, dump) < 6f);
            var box = chest ? chest.GetComponent<Container>() : null;
            if (!c.Check(box && box.inventory.GetItem(ItemIds.Coil) > 0 && box.inventory.Get(ResourceType.Copper) > 0, "a generator coil and a copper wire in the dead chest freezer")) yield break;
            c.Screenshot("dump");
            yield return null;
            box.inventory.TakeItem(ItemIds.Coil); g.Inventory.AddItem(ItemIds.Coil);
            box.inventory.TrySpend(ResourceType.Copper, 1); g.Inventory.Add(ResourceType.Copper, 1);
            c.Fixture("coil and copper moved from the freezer to the pack (its container page)");
            var fp = fridge.transform.position + fridge.transform.forward * 1.3f; fp.y = MadMax.World.DeformableTerrain.Instance.Height(fp.x, fp.z) + 0.3f;
            g.Player.Teleport(fp, fridge.transform.eulerAngles.y + 180f);
            c.Fixture("walked back to the cabinet (teleport)");
            yield return new WaitForSeconds(0.8f);
            cs.Use(g, true);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S01", "fix"), 4f);
            c.Check(!cs.broken && MadMax.Story.Story.StepDone("S01", "part") && MadMax.Story.Story.StepDone("S01", "fix"), "the compressor is fitted ([T]): all three faults fixed");
            // ---- the test: it pulls down below 8 °C
            yield return H.Until(() => MadMax.Story.Story.StepDone("S01", "test"), 45f);
            c.Metric("cabinet_c", cs.temp, "C");
            c.Check(MadMax.Story.Story.StepDone("S01", "test") && cs.Chilled, $"the cabinet runs down to {cs.temp:0.0} °C");
            // ---- optional: an essential breaker
            var bp = fridge.transform.position + fridge.transform.right * 1.4f; bp.y = MadMax.World.DeformableTerrain.Instance.Height(bp.x, bp.z);
            var breaker = FurnitureLibrary.Spawn("breaker", g.Build.Structures, bp, fridge.transform.rotation, g.propMaterial);
            yield return null;
            var bn = breaker ? breaker.GetComponent<UtilityNode>() : null;
            if (c.Check(bn, "a load breaker"))
            {
                node.Unlink(); bn.Link(genNode, UtilityKind.Power); node.Link(bn, UtilityKind.Power);
                breaker.GetComponent<PowerBreaker>().SetPriority(0);
                c.Fixture("a load breaker built between the generator and the cabinet, set ESSENTIAL ([E])");
                yield return H.Until(() => MadMax.Story.Story.StepDone("S01", "priority"), 5f);
                c.Check(MadMax.Story.Story.StepDone("S01", "priority") && node.priority == 0, "the cabinet is an essential load");
            }
            // ---- Orla, Sam's flowers, the keepsake
            yield return H.Walk(c, "orla", 2.5f);
            yield return H.Until(() => g.CastBody("orla") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("orla"), "IT HOLDS THE COLD"), "tell Orla");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S01", "tell"), 4f);
            c.Check(H.Talk(g, g.CastBody("orla"), "I'D BE GLAD TO KEEP ONE"), "take one of Sam's pressed flowers");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S01") == MadMax.Story.Story.State.Done, 6f);
            c.Check(MadMax.Story.Story.StateOf("S01") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("orla_flowers"), "S01 A FRIDGE FULL OF FLOWERS is done");
            c.Check(g.Inventory.GetItem("story_pressed_flower") == 1 && g.Inventory.GetItem("seed_flower") >= 4, "the keepsake and the pay (seeds, flowers, ice-box makings)");
            c.Check(Q5Test.Route("S01", "keepsake", "I'D BE GLAD"), "the keepsake choice is remembered: " + MadMax.Story.Story.Route("S01", "keepsake"));
            c.Check(StoryLibrary.Get("S01").payoff.Contains("POCKET"), "the closing line follows the choice");
            c.Screenshot("first_bunch");
            yield return null;
        }
    }
}
