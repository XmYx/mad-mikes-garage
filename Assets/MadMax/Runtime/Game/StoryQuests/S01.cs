using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // S01 A FRIDGE FULL OF FLOWERS: Orla's stall (awning, table, flower pots, a lamp post and a running generator), the
    // cold cabinet (a fridge with a dead compressor, no cable, flower crates against its back grille) and the appliance
    // dump down the track (dead fridges, an oven, and a chest freezer with a generator coil and a copper wire in it).
    public partial class WastelandGame
    {
        Placeable s01Fridge;
        bool s01Hummed;
        float s01T;

        static readonly Vector3 S01FridgeAt = new Vector3(1.6f, 0f, -2.2f);

        partial void Scene_S01()
        {
            Q5ResetPayoff("S01");
            if (!StoryAnchors.Has("orla") || !Build || !Build.Structures) return;
            Q5Put("orla", "porch_awning", new Vector3(0f, 0f, -1.8f), 0f);
            Q5Put("orla", "table", new Vector3(-1.6f, 0f, -1.4f), 0f);
            foreach (var p in new[] { new Vector3(-3.6f, 0f, 0.4f), new Vector3(-3.9f, 0f, -0.7f), new Vector3(3.9f, 0f, 0.5f) }) Q5Put("orla", "flower_pot", p, 0f);
            Q5Put("orla", "bench", new Vector3(0f, 0f, 3.6f), 180f);
            // the cold cabinet: a dead compressor, no cable, crates stacked against the back grille
            var fridge = Q5Put("orla", "fridge", S01FridgeAt, 0f);
            if (fridge)
            {
                if (fridge.TryGetComponent<ColdStore>(out var cs)) { cs.broken = true; cs.temp = Mathf.Max(MadMax.World.Weather.Temperature, 22f); }
                if (fridge.TryGetComponent<Container>(out var box)) box.inventory.AddItem("crop_flower", 4);
                fridge.Dirty();
            }
            Q5Put("orla", "crate", S01FridgeAt + new Vector3(-0.35f, 0f, -0.95f), 8f);
            Q5Put("orla", "crate", S01FridgeAt + new Vector3(0.45f, 0f, -1.0f), -12f);
            // the supply: a running generator and the lamp it lights (the cabinet's cable has come away)
            var gen = Q5Put("orla", "generator", new Vector3(-4.6f, 0f, -4.6f), 90f);
            var lamp = Q5Put("orla", "lamppost", new Vector3(-3.3f, 0f, -2.0f), 0f);
            if (gen && gen.TryGetComponent<Generator>(out var g0)) { g0.fuel = 18f; g0.on = true; gen.Dirty(); }
            if (gen && lamp && gen.TryGetComponent<UtilityNode>(out var gn) && lamp.TryGetComponent<UtilityNode>(out var ln)) ln.Link(gn, UtilityKind.Power);
            // the appliance dump: dead fridges, an oven, a chest freezer with a coil in it
            if (StoryAnchors.Has("s01_dump"))
            {
                Q5Put("s01_dump", "fridge", new Vector3(0f, 0f, 0f), 20f, 0.36f, 90f);           // on its side
                Q5Put("s01_dump", "fridge", new Vector3(-1.4f, 0f, -1.8f), 160f);
                Q5Put("s01_dump", "oven", new Vector3(-2.2f, 0f, 0.9f), -30f);
                var chest = Q5Put("s01_dump", "freezer", new Vector3(2.2f, 0f, 1.0f), 25f);
                if (chest)
                {
                    if (chest.TryGetComponent<Container>(out var box)) { box.inventory.AddItem(ItemIds.Coil, 1); box.inventory.Add(ResourceType.Copper, 1); }
                    chest.Dirty();
                }
                Q5Put("s01_dump", "tyres", new Vector3(1.4f, 0f, -2.1f), 30f);
                Q5Put("s01_dump", "barrel", new Vector3(3.3f, 0f, -0.9f), 0f);
            }
        }

        /// <summary>The cold cabinet at Orla's stall.</summary>
        ColdStore S01Cabinet()
        {
            if (!s01Fridge && StoryAnchors.Has("orla")) s01Fridge = Q5Prop("fridge", Q5At("orla", S01FridgeAt), 1.5f);
            return s01Fridge ? s01Fridge.GetComponent<ColdStore>() : null;
        }

        partial void Tick_S01()
        {
            if ((s01T += Time.deltaTime) < 0.25f) return;
            float dt = s01T; s01T = 0f;
            var cs = S01Cabinet();
            if (!cs) return;
            var node = cs.GetComponent<UtilityNode>();
            if (MadMax.Story.Story.StepDone("S01", "look") && !MadMax.Story.Story.Flag("s01:diag"))
            {
                MadMax.Story.Story.SetFlag("s01:diag");
                Journal.Add("JOB", "ORLA'S CABINET: WARM INSIDE. FLOWER CRATES ARE STACKED AGAINST THE BACK GRILLE, THE SUPPLY CABLE HANGS LOOSE, AND THE COMPRESSOR DOESN'T EVEN CLICK.");
                Toast("CRATES ON THE GRILLE, A LOOSE CABLE, A DEAD COMPRESSOR");
            }
            bool vented = Q5Count("crate", cs.transform.position, 1.7f) == 0;
            if (vented) Q5Note("s01:vent");
            else if (cs.Running)
            {
                // the heat has nowhere to go: it hums and warms up instead
                cs.temp += (ColdStore.CoolRate + 0.15f) * dt;
                if (!s01Hummed && Q5Near(cs.transform.position, 8f)) { s01Hummed = true; Toast("THE CABINET HUMS BUT GETS NO COLDER: THE CRATES SMOTHER THE GRILLE"); }
            }
            if (node && node.Powered) Q5Note("s01:cable");
            if (!cs.broken) Q5Note("s01:compressor");
            if (vented && cs.Running && cs.Chilled) Q5Note("s01:cold");
            if (node && node.priority == 0 && node.Powered) Q5Note("s01:essential");
            // the last choice has landed: the first bunch goes out on the table, the closing line follows the keepsake
            if (MadMax.Story.Story.StepDone("S01", "keepsake") && !MadMax.Story.Story.Flag("s01:bunch"))
            {
                MadMax.Story.Story.SetFlag("s01:bunch");
                Q5Put("orla", "flower_pot", new Vector3(-1.6f, 0f, -0.4f), 0f);
                bool kept = Q5Route("S01", "keepsake", "I'D BE GLAD");
                Q5Payoff("S01", kept
                    ? "ORLA'S CABINET HOLDS FOUR DEGREES. THIS YEAR'S FIRST BUNCH SOLD BEFORE NINE, AND ONE OF SAM'S PRESSED FLOWERS RIDES IN YOUR POCKET."
                    : "ORLA'S CABINET HOLDS FOUR DEGREES. THIS YEAR'S FIRST BUNCH SOLD BEFORE NINE; SAM'S PRESSED FLOWERS STAY IN HER BOOK, WHERE THEY BELONG.");
                Q5Note("s01:bunch");
            }
        }
    }
}
