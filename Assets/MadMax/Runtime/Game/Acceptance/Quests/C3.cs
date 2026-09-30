using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;
using StoryState = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_C3(List<Scenario> into) { into.Add(new QuestC3Allocation()); into.Add(new StoryAllocationMath()); }
    }

    /// <summary>C3 ENOUGH TO GO AROUND in a story world: twelve cans from Sera; the clinic, Bo's pump and the lamplighter
    /// asked what they really burn; Bo's pump generator mended in build mode and a power timer built by the public
    /// generator; the shares posted by need (Sera's answer carries the numbers); each share handed over; the village's
    /// mark and the town's signature. Checks the shares, the cans handed over and held back, the public generator lit, the
    /// clinic bed and the produce stall, the agreement paper and the closing line.</summary>
    class QuestC3Allocation : Scenario
    {
        public override string Id => "story.c3";
        public override float Timeout => 200f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            ArcCT.Skip(c, "A1", "A2", "C1", "C2");
            yield return new WaitForSeconds(1.5f);
            ArcCT.Open(c, "C3", "allocation");
            if (StoryState.StateOf("C3") == StoryState.State.Open)
            {
                yield return ArcCT.Meet(c, "sera");
                c.Check(ArcCT.Say(g, "sera", "WHAT DID THE TOLL BOOK EVER BUY"), "Sera's shipment");
            }
            yield return H.Until(() => StoryState.StateOf("C3") == StoryState.State.Active, 3f);
            if (!c.Check(StoryState.StateOf("C3") == StoryState.State.Active, "C3 runs")) yield break;
            yield return new WaitForSeconds(0.5f);

            // ---- the cans and the three claims
            yield return ArcCT.Meet(c, "sera");
            c.Check(ArcCT.Say(g, "sera", "HAND ME THE CANS"), "take the shipment");
            yield return ArcCT.Done("C3", "shipment");
            c.Check(g.Inventory.GetItem(StoryLibrary.C3Can) == StoryLibrary.C3Supply, "twelve cans of diesel");
            yield return ArcCT.Meet(c, "c3_doctor", -2.5f);
            c.Check(ArcCT.Say(g, "c3_doctor", "HOW MUCH DOES THE CLINIC"), "the clinic: four, not six");
            yield return ArcCT.Meet(c, "c3_warden");
            c.Check(ArcCT.Say(g, "c3_warden", "WHAT DOES THE PUBLIC GENERATOR"), "the lamps: four, all night");
            var pubGen = ArcCT.Prop(g, "generator", "c3_generator", 3f);
            c.Check(pubGen && !pubGen.GetComponent<Generator>().on, "the public generator stands dry in the square");
            yield return ArcCT.Meet(c, "c3_farmer");
            c.Screenshot("scene");
            yield return null;
            c.Check(ArcCT.Say(g, "c3_farmer", "SHOW ME WHAT THE PUMP"), "Bo's pump: five, half of it in the dirt");
            yield return ArcCT.Done("C3", "inspect");
            c.Check(StoryState.StepDone("C3", "inspect"), "all three needs inspected");

            // ---- mend the waste
            var pump = ArcCT.Prop(g, "generator", "c3_farm", 6f);
            if (!c.Check(pump && pump.hits < FurnitureLibrary.Get("generator").hits, "Bo's pump generator is weeping (damaged)")) yield break;
            g.Inventory.Add(ResourceType.Iron, 3); g.Inventory.Add(ResourceType.Copper, 2); g.Inventory.Add(ResourceType.Aluminium, 1);
            c.Fixture("iron, copper and aluminium for the repair");
            g.Build.Repair(pump);
            yield return ArcCT.Done("C3", "fix_farm");
            c.Check(StoryState.Route("C3", "fix_farm") == "MENDED THE PUMP'S GENERATOR", "the pump generator is mended: " + StoryState.Route("C3", "fix_farm"));
            var gp = StoryAnchors.Get("c3_generator") + Quaternion.Euler(0f, StoryAnchors.Yaw("c3_generator"), 0f) * new Vector3(1.5f, 0f, 1.5f);
            gp.y = MadMax.World.DeformableTerrain.Instance.Height(gp.x, gp.z);
            FurnitureLibrary.Spawn("power_timer", g.Build.Structures, gp, Quaternion.identity, g.propMaterial);
            c.Fixture("a power timer built beside the public generator");
            yield return ArcCT.Done("C3", "fix_lamps");
            c.Check(StoryState.Route("C3", "fix_lamps") == "A TIMER ON THE LAMPS", "the lamps go on a timer: " + StoryState.Route("C3", "fix_lamps"));

            // ---- the plan, by need
            yield return new WaitForSeconds(1f);
            var byNeed = StoryLibrary.Get("C3").steps.First(s => s.id == "plan").any.First(a => a.topic == "c3_byneed").reply;
            c.Note("Sera: " + byNeed);
            c.Check(byNeed.Contains("CLINIC 4 OF 4") && byNeed.Contains("BO'S PUMP 3 OF 3") && byNeed.Contains("LAMPS 2 OF 2") && byNeed.Contains("3 CANS IN RESERVE"),
                    "Sera's answer carries the numbers the fixes made possible");
            yield return ArcCT.Meet(c, "sera");
            c.Check(ArcCT.Say(g, "sera", "BY NEED, POSTED"), "post the shares by need");
            yield return H.Until(() => ArcCRecord.Has("c3:share:clinic"), 4f);
            c.Check(ArcCRecord.Get("c3:share:clinic") == 4 && ArcCRecord.Get("c3:share:farm") == 3 && ArcCRecord.Get("c3:share:lamps") == 2, "posted: clinic 4, pump 3, lamps 2");
            c.Check(g.Inventory.GetItem(StoryLibrary.C3Posted) == 1, "a copy of the posted shares");

            // ---- hand the shares over
            foreach (var (key, anchorKey, left) in new[] { ("c3_doctor", "clinic", 8), ("c3_farmer", "farm", 5), ("c3_warden", "lamps", 3) })
            {
                yield return ArcCT.Meet(c, key, key == "c3_doctor" ? -2.5f : 3f);
                c.Check(ArcCT.Say(g, key, "YOUR SHARE, AS POSTED"), "hand over the " + anchorKey + "'s share");
                yield return H.Until(() => ArcCRecord.Has("c3:given:" + anchorKey), 4f);
                c.Check(g.Inventory.GetItem(StoryLibrary.C3Can) == left, $"{anchorKey}: {ArcCRecord.Get("c3:given:" + anchorKey)} cans handed over, {g.Inventory.GetItem(StoryLibrary.C3Can)} left");
            }
            yield return ArcCT.Done("C3", "deliver");
            c.Check(pubGen && pubGen.GetComponent<Generator>().on && pubGen.GetComponent<Generator>().fuel > 10f, "the public generator runs on its share");
            c.Check(g.Inventory.GetItem(StoryLibrary.C3Mark) == 1, "Bo gives the village's mark");

            // ---- both sign
            yield return ArcCT.Meet(c, "sera");
            c.Check(ArcCT.Say(g, "sera", "THE VILLAGE HAS PUT ITS MARK"), "the town signs");
            yield return H.Until(() => StoryState.StateOf("C3") == StoryState.State.Done, 5f);
            c.Check(StoryState.StateOf("C3") == StoryState.State.Done && StoryState.Flag("c3_done"), "C3 ENOUGH TO GO AROUND is done");
            c.Check(g.Inventory.GetItem(StoryLibrary.C3Agreement) == 1 && g.Inventory.GetItem(StoryLibrary.C3Can) == 0 && ArcCRecord.Get("c3:reserve") == 3,
                    "the agreement paper; three cans held for the convoy");
            c.Check(ArcCT.Prop(g, "clinic_bed", "c3_clinic", 6f) && ArcCT.Prop(g, "table", "c3_generator", 10f), "a clinic bed for anyone and the village's stall in the square");
            var payoff = StoryLibrary.Get("C3").payoff;
            c.Check(payoff.Contains("BY NEED") && payoff.Contains("3 HELD FOR THE CONVOY") && Journal.Entries.Any(e => e.text == payoff), "the closing line: " + payoff);
        }
    }

    /// <summary>The allocation arithmetic (story system "allocation") without a world: shares always add up to the supply
    /// or leave a reserve, essential needs come first, a favoured customer gets its claim openly, even shares are even.</summary>
    class StoryAllocationMath : Scenario
    {
        public override string Id => "story.allocation";
        public override bool NeedsWorld => false;

        public override IEnumerator Run(ScenarioContext c)
        {
            var cs = new List<Allocation.Customer> { new Allocation.Customer("clinic", "CLINIC", 6, 4, true), new Allocation.Customer("farm", "FARM", 5, 5), new Allocation.Customer("lamps", "LAMPS", 5, 4) };
            var need = Allocation.Shares(cs, 12, Allocation.Plan.ByNeed);
            c.Check(need[0] == 4 && need.Sum() == 12, "by need: the clinic's need in full, the rest in proportion (" + string.Join("/", need) + ")");
            c.Check(Allocation.Shortfall(cs, need) == 1, "one can short across the others");
            var even = Allocation.Shares(cs, 12, Allocation.Plan.Even);
            c.Check(even.All(x => x == 4), "even thirds");
            var fav = Allocation.Shares(cs, 12, Allocation.Plan.Favour, 1);
            c.Check(fav[1] == 5 && fav[0] == 4 && fav.Sum() == 12, "the farm first, openly: its claim, the clinic's need, the rest to the lamps (" + string.Join("/", fav) + ")");
            cs[1] = new Allocation.Customer("farm", "FARM", 5, 3); cs[2] = new Allocation.Customer("lamps", "LAMPS", 5, 2);
            var mended = Allocation.Shares(cs, 12, Allocation.Plan.ByNeed);
            c.Check(mended.Sum() == 9 && Allocation.Reserve(mended, 12) == 3 && Allocation.Shortfall(cs, mended) == 0, "waste mended: every need met, three in reserve");
            c.Note(Allocation.Posted(cs, mended, 12, "CANS"));
            yield break;
        }
    }
}
