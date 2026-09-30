using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S08(List<Scenario> into) => into.Add(new StoryS08());
    }

    /// <summary>S08 THE WEDDING AT THE WRONG END OF THE ROAD in a sandbox world: Fen hands over the torn groom's coat;
    /// a local names the landmark (the water tower at Bracken Top); the mix-up is explained to the bride's mother; the
    /// coat is mended at a sewing table; the band comes along (the fiddler rides in the car's passenger seat, the
    /// accordionist catches up); the groom is dressed, the radio switched on, and the ceremony started on the player's
    /// word plays as a performance scene (gathered guests, lines, the crowd) to its end; the radio dedication and the
    /// band's route are remembered.</summary>
    class StoryS08 : Scenario
    {
        public override string Id => "story.s08";
        public override float Timeout => 320f;

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            yield return Q5Test.Open(c, "S08", "fen", "fen", "YOU LOOK LIKE YOU'RE WAITING");
            if (!c.Check(MadMax.Story.Story.StateOf("S08") == MadMax.Story.Story.State.Active, "S08 is running")) yield break;
            var fen = g.CastBody("fen");
            if (!c.Check(fen, "Fen Locke waits at Bracken Bottom")) yield break;
            c.Check(H.Talk(g, fen, "LET'S SEE THE GROOM'S COAT"), "Fen hands over the groom's coat");
            yield return H.Until(() => MadMax.Story.Story.Flag("s08:torn"), 4f);
            c.Check(g.Inventory.GetItem("cloth_duster") > 0 && g.GarmentCondition("duster") < 0.5f, $"the coat is torn ({g.GarmentCondition("duster"):P0})");
            c.Screenshot("bracken_bottom");
            yield return null;

            // ---- the landmark and the mix-up
            yield return H.Until(() => g.CastBody("s08_local") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("s08_local"), "IS THERE A WEDDING"), "a local knows the other end of the road");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S08", "clue"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S08", "clue"), "the landmark: the water tower at Bracken Top");
            c.Metric("bracken_ends", Flat(StoryAnchors.Get("fen"), StoryAnchors.Get("s08_wedding")), "m");
            yield return H.Walk(c, "s08_wedding", 5f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S08", "right") && g.CastBody("s08_host") != null, 6f);
            c.Check(g.Build && Placeable.All.Any(p => p && p.id == "water_tower" && g.IsStoryProp(p) && Flat(p.transform.position, StoryAnchors.Get("s08_wedding")) < 16f), "the water tower stands over Bracken Top");
            c.Check(H.Talk(g, g.CastBody("s08_host"), "FEN LOCKE IS AT THE OTHER END"), "the mix-up, explained to the bride's mother");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S08", "mixup"), 4f);

            // ---- the coat at Fen's sewing table
            yield return H.Walk(c, "fen", 3f);
            var table = Placeable.All.FirstOrDefault(p => p && p.id == "sewing_table" && g.IsStoryProp(p));
            c.Check(table && table.GetComponentInChildren<CraftingStation>() && table.GetComponentInChildren<CraftingStation>().type == "sewing", "Fen's sewing table");
            c.Check(g.Mend("duster"), "the coat mended at the sewing table (the MEND CLOTHES page's action)");
            c.Fixture("MEND CLOTHES confirmed for the coat (menu action called directly)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S08", "mend"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S08", "mend") && g.GarmentCondition("duster") >= 0.95f, "the coat is whole again");

            // ---- the band: a ride in the passenger seat
            yield return H.Until(() => g.CastBody("s08_fiddler") != null && g.CastBody("s08_squeeze") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("s08_fiddler"), "COME ON, BOTH OF YOU"), "the band will come along");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S08", "go"), 4f);
            var car = g.Fleet.FirstOrDefault(v => v && v.driveable && !v.aiDriven && v.GetComponentInChildren<PassengerSeat>(true));
            var top = StoryAnchors.Get("s08_wedding");
            if (car)
            {
                var near = StoryAnchors.Get("fen") + Quaternion.Euler(0f, StoryAnchors.Yaw("fen"), 0f) * new Vector3(0f, 0f, 7f);
                yield return TestWorld.Place(c, car, near, Quaternion.Euler(0f, StoryAnchors.Yaw("fen"), 0f) * Vector3.right, 1f);
                g.Enter(car);
                yield return H.Until(() => g.CastBody("s08_fiddler") && (g.CastBody("s08_fiddler").Riding || (g.CastBody("s08_squeeze") && g.CastBody("s08_squeeze").Riding)), 14f);
                c.Check(g.CastBody("s08_fiddler").Riding || g.CastBody("s08_squeeze").Riding, "one of the band takes the passenger seat");
                yield return new WaitForSeconds(0.5f);
                yield return TestWorld.Place(c, car, top + Quaternion.Euler(0f, StoryAnchors.Yaw("s08_wedding"), 0f) * new Vector3(0f, 0f, 9f), Vector3.forward, 1f);
                c.Fixture("driven up to Bracken Top (the car placed there with the band aboard)");
                g.Exit();
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                c.Note("no fleet car with a passenger seat: the band walks");
                yield return H.Walk(c, "s08_wedding", 5f);
            }
            yield return H.Until(() => MadMax.Story.Story.StepDone("S08", "band"), 20f);
            c.Check(MadMax.Story.Story.StepDone("S08", "band"), "the band is at Bracken Top: " + MadMax.Story.Story.Route("S08", "band"));
            if (car) c.Check(MadMax.Story.Story.Route("S08", "band") == "RODE IN YOUR PASSENGER SEAT", "they came in the passenger seat (remembered)");

            // ---- dress the groom, the radio, the word
            yield return H.Walk(c, "s08_wedding", 4f);
            yield return H.Until(() => g.CastBody("s08_groom") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("s08_groom"), "YOUR COAT. MENDED"), "the groom gets his coat");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S08", "dress"), 4f);
            c.Check(g.Inventory.GetItem("cloth_duster") == 0, "the coat went to Tobias");
            var radio = Placeable.All.FirstOrDefault(p => p && p.id == "radio" && g.IsStoryProp(p) && Flat(p.transform.position, top) < 16f);
            var set = radio ? radio.GetComponent<RadioSet>() : null;
            if (!c.Check(set, "a radio by the tables")) yield break;
            set.Use(g, false);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S08", "radio"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S08", "radio"), "the radio is on ([E])");
            c.Check(H.Talk(g, g.CastBody("s08_host"), "EVERYONE'S HERE"), "start the ceremony: everyone is ready");
            yield return H.Until(() => PerformanceScene.Current != null && PerformanceScene.Current.Beat >= 3, 20f);
            var scene = PerformanceScene.Current;
            c.Check(scene && scene.Show.key == "s08:ceremony", "the ceremony plays");
            if (scene) c.Check(scene.Guests.Count >= 6, $"guests gathered to watch ({scene.Guests.Count})");
            c.Screenshot("ceremony");
            yield return null;
            yield return H.Until(() => MadMax.Story.Story.StepDone("S08", "ceremony"), 80f);
            c.Check(MadMax.Story.Story.StepDone("S08", "ceremony"), "the ceremony runs to its end: they're married");
            c.Check(MadMax.Audio.RadioNetwork.FlashText != null && MadMax.Audio.RadioNetwork.FlashText.Contains("DEDICATION"), "a dedication goes out on the radio");
            c.Check(H.Talk(g, g.CastBody("s08_host"), "CONGRATULATIONS"), "congratulate Maud");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S08") == MadMax.Story.Story.State.Done, 5f);
            c.Check(MadMax.Story.Story.StateOf("S08") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("kerry_dedication"), "S08 THE WEDDING AT THE WRONG END OF THE ROAD is done");
            c.Check(g.Inventory.GetItem("cloth_bomber") > 0, "Fen's spare wedding jacket");
            c.Check(StoryLibrary.Get("S08").payoff.Contains(car ? "PASSENGER SEAT" : "WALKED"), "the closing line remembers how the band came");
        }
    }
}
