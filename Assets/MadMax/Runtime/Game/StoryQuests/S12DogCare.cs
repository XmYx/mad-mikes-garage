using MadMax.Building;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S12: offering food to Penny, the dog at Platform Three, while she still waits for her owner (a village-kept
    /// animal otherwise takes nothing from strangers): [E] with meat, jerky or a bone in the pack. The first meal lets you
    /// read her collar tag.</summary>
    public class S12DogCare : MonoBehaviour, IInteractable
    {
        MadMax.Animals.Animal dog;

        void Awake() => dog = GetComponent<MadMax.Animals.Animal>();
        void OnEnable() => MadMax.Vehicles.PartFunctions.Interactables.Add(this);
        void OnDisable() => MadMax.Vehicles.PartFunctions.Interactables.Remove(this);

        static bool Fed => MadMax.Story.Story.Flag("s12:fed");

        string Food(WastelandGame g)
        {
            if (!dog || dog.Def == null) return null;
            foreach (var f in dog.Def.likes) if (g.Inventory.GetItem(f) > 0) return f;
            return null;
        }

        public string Prompt(WastelandGame g)
        {
            if (!dog || !dog.Alive || dog.owned || g.Current || MadMax.Story.Story.StateOf("S12") != MadMax.Story.Story.State.Active || Fed) return null;
            var food = Food(g);
            return food != null ? "[E] OFFER " + ItemCatalog.Name(food) + " TO THE DOG" : "THE DOG WATCHES YOU. SHE MIGHT TAKE MEAT, JERKY OR A BONE";
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (secondary || !dog || !dog.Alive || Fed || MadMax.Story.Story.StateOf("S12") != MadMax.Story.Story.State.Active) return;
            var food = Food(g);
            if (food == null || !g.Inventory.TakeItem(food)) return;
            MadMax.Story.Story.SetFlag("s12:fed");
            dog.trust = Mathf.Max(dog.trust, 0.5f);
            dog.fedDay = MadMax.World.DayNight.Day;
            g.Stats.Practice(MadMax.RPG.Skill.Survival, 2f);
            MadMax.Audio.Sfx.Play("chest", transform.position, 0.3f, 1.5f);
            g.Toast("SHE TAKES IT FROM YOUR HAND. HER COLLAR TAG: 'PENNY. W. OBER, PLATFORM 3'");
            Journal.Add("JOB", "THE DOG IS CALLED PENNY. HER TAG SAYS 'W. OBER, PLATFORM 3'.");
            MadMax.Story.Story.Note("s12:fed");
        }
    }
}
