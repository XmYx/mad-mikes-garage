using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Animals;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S12(List<Scenario> into) => into.Add(new StoryS12());
    }

    /// <summary>S12 THE DOG AT PLATFORM THREE in a sandbox world: Etta asks for help with the dog at the station; the
    /// player watches Penny's routine, feeds her by hand ([E]), treats the lame paw with a bandage (the animal's own
    /// treatment), learns the owner's name from Etta, finds Walt at his niece's porch, asks whether he can keep her (his
    /// blessing: the leash) and adopts her with his agreement; Penny becomes a kept dog that follows the player, and the
    /// route and closing line remember it.</summary>
    class StoryS12 : Scenario
    {
        public override string Id => "story.s12";
        public override float Timeout => 200f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("S12") == MadMax.Story.Story.State.Open, "S12 is on offer in a sandbox world");
            yield return H.Walk(c, "etta", 3f);
            yield return H.Until(() => g.CastBody("etta") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("etta"), "WHOSE DOG IS THAT"), "Etta Pike asks about the dog")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StateOf("S12") == MadMax.Story.Story.State.Active, 3f);
            Animal dog = null;
            yield return H.Until(() => (dog = Animal.All.FirstOrDefault(a => a && a.key == "s12:penny")) != null, 4f);
            if (!c.Check(dog && dog.Alive && !dog.owned, "a dog waits at Platform Three")) yield break;
            c.Check(dog.limp > 0.3f, $"she is lame on a forepaw (limp {dog.limp:0.00})");
            c.Screenshot("platform");
            yield return null;

            // ---- watch her routine a while (on foot, nearby)
            var near = dog.transform.position + (StoryAnchors.Get("etta") - dog.transform.position).normalized * 4f;
            near.y = MadMax.World.DeformableTerrain.Instance.Height(near.x, near.z) + 0.3f;
            g.Player.Teleport(near, 0f); c.Fixture("stood a few metres from the dog (teleport)");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S12", "watch"), 25f);
            c.Check(MadMax.Story.Story.StepDone("S12", "watch"), "watched her routine");

            // ---- a meal from the hand, then the paw
            g.Inventory.AddItem("food_meat_cooked", 1); c.Fixture("one cooked meat in the pack");
            var care = dog.GetComponent<S12DogCare>();
            if (!c.Check(care && care.Prompt(g) != null && care.Prompt(g).StartsWith("[E] OFFER"), "the dog can be offered food ([E])")) yield break;
            care.Use(g, false);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S12", "feed"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S12", "feed") && g.Inventory.GetItem("food_meat_cooked") == 0, "she eats from your hand");
            g.Inventory.AddItem("med_bandage", 1); c.Fixture("one bandage in the pack");
            dog.Use(g, false);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S12", "paw"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S12", "paw") && dog.limp < 0.05f, "the paw is treated ([E] on the dog: the animal's own treatment)");

            // ---- who is W. Ober
            yield return H.Walk(c, "etta", 3f);
            c.Check(H.Talk(g, g.CastBody("etta"), "HER TAG SAYS W. OBER"), "Etta knows the name");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S12", "clue"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S12", "clue"), "Walt Ober, the old signalman");
            yield return H.Walk(c, "s12_home", 3f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S12", "visit") && g.CastBody("s12_walt") != null, 6f);
            var walt = g.CastBody("s12_walt");
            if (!c.Check(walt, "Walt is at his niece's porch")) yield break;
            c.Screenshot("walt");
            yield return null;
            c.Check(H.Talk(g, walt, "YOUR DOG IS STILL WAITING"), "tell Walt about Penny");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S12", "walt"), 4f);
            c.Check(!H.Talk(g, walt, "THEN SHE COMES WITH ME"), "no adopting her before asking him");
            c.Check(H.Talk(g, walt, "CAN YOU STILL LOOK AFTER HER"), "ask whether he can keep her");
            yield return H.Until(() => g.Inventory.GetItem("story_penny_leash") > 0, 4f);
            c.Check(g.Inventory.GetItem("story_penny_leash") > 0, "his blessing: her old leash");
            c.Check(H.Talk(g, walt, "THEN SHE COMES WITH ME"), "adopt her, with his agreement");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S12") == MadMax.Story.Story.State.Done, 6f);
            c.Check(MadMax.Story.Story.StateOf("S12") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("penny_settled"), "S12 THE DOG AT PLATFORM THREE is done");
            c.Check(MadMax.Story.Story.Route("S12", "settle") == "PENNY GOES WITH YOU" && Q5Test.Route("S12", "decide", "THEN SHE COMES"), "the choice is remembered: " + MadMax.Story.Story.Route("S12", "settle"));
            c.Check(dog.owned && dog.order == 1 && AnimalDirector.Instance && AnimalDirector.Instance.kept.Contains(dog), "Penny is a kept dog now and follows you (saved with your animals)");
            c.Check(MadMax.Story.Story.StepDone("S12", "r_adopted") && !MadMax.Story.Story.StepDone("S12", "r_home") && g.Inventory.GetItem("story_penny_leash") == 0, "only the adoption route's hand-over was paid");
            c.Check(StoryLibrary.Get("S12").payoff.Contains("BLESSING"), "the closing line follows the choice");
        }
    }
}
