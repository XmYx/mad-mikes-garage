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
        static partial void Tests_S04(List<Scenario> into) => into.Add(new QuestS04());
    }

    /// <summary>S04 THE HOUSE THAT WALKED in a sandbox world: the washed-out corner and the barrel that did it, the pantry
    /// emptied, a post that doesn't reach refused and one that does accepted, a ditch dug with the shovel, a ramp to the
    /// door, a walk into the kitchen; Della's porch plan lands in the build menu.</summary>
    class QuestS04 : Scenario
    {
        public override string Id => "story.s04";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var t = MadMax.World.DeformableTerrain.Instance;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("S04") == MadMax.Story.Story.State.Open, "S04 is on offer in a sandbox world");
            yield return H.Walk(c, "della", 2.5f);
            yield return H.Until(() => g.CastBody("della") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("della"), "EVERYTHING ALL RIGHT"), "Della Shaw's kitchen moved")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StateOf("S04") == MadMax.Story.Story.State.Active, 3f);
            var a = StoryAnchors.Get("s04_house"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("s04_house"), 0f);
            Vector3 W(float x, float z) { var p = a + r * new Vector3(x, 0f, z); p.y = t.Height(p.x, p.z); return p; }
            var corner = StoryAnchors.Get("s04_corner");
            var tile = Placeable.All.FirstOrDefault(p => p && p.id == "floor_wood" && g.IsStoryProp(p) && new Vector2(p.transform.position.x - corner.x, p.transform.position.z - corner.z).magnitude < 1.6f);
            if (!c.Check(tile, "the kitchen floor stands over the corner")) yield break;
            c.Check(!Placeable.All.Any(p => p && p.id == "post" && g.IsStoryProp(p) && new Vector2(p.transform.position.x - corner.x, p.transform.position.z - corner.z).magnitude < 0.3f && p.transform.up.y > 0.9f), "no post stands under the kitchen corner");
            c.Metric("gully_below_floor", tile.transform.position.y - t.Height(corner.x, corner.z), "m");
            c.Screenshot("house");
            yield return null;

            // ---- look it over
            foreach (var (x, z) in new[] { (-3.2f, -3.2f), (3.6f, -3.6f) })                    // outside, by the corner and by the barrel
            {
                var p = W(x, z); p.y += 0.3f;
                g.Player.Teleport(p, 0f);
                c.Fixture($"walked round the house to {x:0.#},{z:0.#} (teleport)");
                yield return new WaitForSeconds(1.2f);
            }
            yield return H.Until(() => MadMax.Story.Story.StepDone("S04", "survey"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S04", "gully") && MadMax.Story.Story.StepDone("S04", "spout"), "the washed-out corner and the barrel that did it");

            // ---- the pantry
            var pantry = Placeable.All.FirstOrDefault(p => p && p.id == "locker" && g.IsStoryProp(p));
            var box = pantry ? pantry.GetComponent<Container>() : null;
            if (!c.Check(box && box.Weight > 1f, "Della's pantry is full of jars and bricks")) yield break;
            foreach (var kv in box.inventory.Items.ToList()) if (kv.Value > 0) { g.Inventory.AddItem(kv.Key, kv.Value); box.inventory.TakeItem(kv.Key, kv.Value); }
            var res = box.inventory.ResourceArray;
            for (int i = 0; i < res.Length; i++) if (res[i] > 0) { g.Inventory.Add((ResourceType)i, res[i]); box.inventory.TrySpend((ResourceType)i, res[i]); }
            c.Fixture("emptied the pantry (its container page)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S04", "unload"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S04", "unload") && g.Inventory.Get(ResourceType.Stone) >= 12, "the pantry is empty; the bricks are yours");

            // ---- shoring: a post that doesn't reach the floor doesn't count, one under the corner does
            var wrong = FurnitureLibrary.Spawn("post", g.Build.Structures, W(-3.4f, -2.8f), r, g.propMaterial);
            c.Fixture("a post built beside the corner, clear of the floor");
            yield return new WaitForSeconds(1.5f);
            c.Check(wrong && !MadMax.Story.Story.StepDone("S04", "shore"), "a post that carries nothing isn't a repair");
            var prop = FurnitureLibrary.Spawn("post", g.Build.Structures, W(-1.85f, -1.85f), r, g.propMaterial);
            c.Fixture("a post built in the gully under the kitchen corner");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S04", "shore"), 4f);
            c.Check(prop && MadMax.Story.Story.StepDone("S04", "shore"), "the corner is shored: grounded and carrying the floor");

            // ---- the water: a ditch at the flag, dug with the shovel
            yield return H.Walk(c, "s04_ditch", 1.2f);
            var ditch = StoryAnchors.Get("s04_ditch");
            for (int i = 0; i < 6; i++) { g.ShovelDig(ditch); yield return new WaitForSeconds(0.1f); }
            c.Fixture("six shovel stabs at the flag");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S04", "drain"), 4f);
            c.Check(MadMax.Story.Story.Route("S04", "drain") == StoryLibrary.S04Ditch, "the overflow goes around the house: " + MadMax.Story.Story.Route("S04", "drain"));

            // ---- access: a ramp up to the door, then walk in
            FurnitureLibrary.Spawn("ramp_wood", g.Build.Structures, W(-5.05f, 1f), r * Quaternion.Euler(0f, 90f, 0f), g.propMaterial);
            c.Fixture("a timber ramp built up to the kitchen door");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S04", "access"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S04", "access"), "the door can be reached again");
            var inside = StoryAnchors.Get("s04_kitchen"); inside.y = tile.transform.position.y + 0.2f;
            g.Player.Teleport(inside, StoryAnchors.Yaw("s04_house") + 90f);
            c.Fixture("walked up the ramp into the kitchen (teleport)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S04", "walk"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S04", "walk"), "standing on the kitchen floor");
            c.Screenshot("kitchen");
            yield return null;

            // ---- Della
            int plans = StructurePlans.Count;
            yield return H.Walk(c, "della", 2.5f);
            yield return H.Until(() => g.CastBody("della") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("della"), "YOUR KITCHEN'S STAYING PUT"), "tell Della");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S04") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("S04") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("s04_done"), "S04 THE HOUSE THAT WALKED is done");
            c.Check(StructurePlans.Count == Mathf.Min(StructurePlans.Max, plans + 1) && StructurePlans.Get(StructurePlans.Count - 1).pieces.Any(e => e.id == "porch_awning"), "Della's porch is a structure plan");
            c.Check(g.Inventory.Get(ResourceType.Lime) >= 2, "foundation materials paid");
            c.Check(Journal.Entries.Any(e => e.text.Contains("RUNS AROUND THE HOUSE")), "the payoff remembers the ditch");
        }
    }
}
