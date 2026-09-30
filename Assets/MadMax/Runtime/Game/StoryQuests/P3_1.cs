using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    // P3.1 THE SEA DOES NOT KEEP RECEIPTS: Halvard's trawler (tagged "p3_trawler") moored off the harbour with a split
    // seam (frame damage 0.45): while it stays split the hold loses a fish every 90 s. Looking in the hold explains it;
    // welding the hull under 0.2 counts as the welded route; a repair kit handed over or the boatyard's fee closes it
    // outright. After the fix a fresh catch of three that stays in the hold proves it.
    public partial class WastelandGame
    {
        float p3Check, p3Ensure, p3Leak;

        /// <summary>Halvard's trawler (null when it isn't in the world).</summary>
        public VehicleDriver P3Boat() { var t = StoryTag.Find("p3_trawler"); return t ? t.GetComponent<VehicleDriver>() : null; }

        partial void Scene_P3_1()
        {
            P3SpawnTrawler();
            if (!Build || !Build.Structures || !StoryAnchors.Has("p3_harbour")) return;
            PutAt("p3_harbour", "crate", new Vector3(-2.6f, 0f, -1.6f), 15f);
            PutAt("p3_harbour", "barrel", new Vector3(2.5f, 0f, -1.2f), 0f);
            PutAt("p3_harbour", "tyres", new Vector3(3.6f, 0f, 1f), 0f);
            PutAt("p3_harbour", "bench", new Vector3(0f, 0f, -3.2f), 180f);
        }

        void P3SpawnTrawler()
        {
            if (P3Boat() || !StoryAnchors.Has("p3_trawler")) return;
            var pf = PrefabFor("Trawler");
            if (!pf) return;
            var p = StoryAnchors.Get("p3_trawler"); p.y = WorldGen.SeaLevel + 0.4f;
            var v = Instantiate(pf, p, Quaternion.Euler(0f, StoryAnchors.Yaw("p3_trawler"), 0f)).GetComponent<VehicleDriver>();
            v.name = "Halvard's Trawler";
            Register(v, null);
            StoryTag.Set(v.gameObject, "p3_trawler");
            if (v.TryGetComponent<VehicleSystems>(out var vs)) vs.fuel = vs.fuelCapacity * 0.4f;
            if (!Story.Story.Flag("p3_seam_fixed") && v.TryGetComponent<VehicleDamage>(out var d)) d.AddFrameDamage(0.45f, 1f);
            if (v.TryGetComponent<Container>(out var hold)) hold.inventory.AddItem("food_fish_raw", 2);
        }

        partial void Tick_P3_1()
        {
            if (Time.time < p3Check) return;
            p3Check = Time.time + 0.5f;
            var boat = P3Boat();
            if (!boat) { if (Time.time > p3Ensure) { p3Ensure = Time.time + 3f; P3SpawnTrawler(); } return; }
            var dmg = boat.GetComponent<VehicleDamage>();
            var hold = boat.GetComponent<Container>();
            bool fixedUp = Story.Story.Flag("p3_seam_fixed");
            if (!fixedUp && dmg && dmg.FrameDamage >= 0.2f && hold && Time.time > p3Leak)                   // the catch goes home by itself
            {
                p3Leak = Time.time + 90f;
                if (hold.inventory.GetItem("food_fish_raw") > 0) hold.inventory.TakeItem("food_fish_raw");
            }
            if (Story.Story.StepDone("P3.1", "look") && !Story.Story.Flag("p3_diagnosed"))
            {
                Story.Story.SetFlag("p3_diagnosed");
                Toast("THE HOLD'S BILGE SEAM IS SPLIT BELOW THE WATERLINE. NO TEETH MARKS. NO ARMS.");
                Journal.Add("CLUE", "HALVARD'S TRAWLER: A SPLIT SEAM IN THE FISH HOLD. EVERY TIDE THE CATCH WASHES OUT THROUGH THE BILGE.");
            }
            if (!fixedUp && !Story.Story.StepDone("P3.1", "repair") && dmg && dmg.FrameDamage < 0.2f) Story.Story.Note("p3_1:welded");
            string how = Story.Story.Route("P3.1", "repair");
            if (how != null && !fixedUp)
            {
                Story.Story.SetFlag("p3_seam_fixed");
                if (how.StartsWith("HERE'S A REPAIR KIT")) Inventory.TakeItem("use_repair_kit");
                if (dmg) dmg.StraightenFrame(1f);
                if (hold) foreach (var fish in new[] { "food_fish_raw", "food_fish_glow" }) { int n = hold.inventory.GetItem(fish); if (n > 0) hold.inventory.TakeItem(fish, n); }   // the old catch went over the side
                Journal.Add("STORY", how.StartsWith("WELDED") ? "YOU WELDED THE TRAWLER'S SEAM SHUT" : how.StartsWith("HERE'S") ? "HALVARD PATCHED THE SEAM WITH YOUR REPAIR KIT" : "THE BOATYARD CLOSED THE SEAM ON YOUR SCRAP");
                fixedUp = true;
            }
            if (how != null)
                StoryLibrary.Get("P3.1").payoff = (how.StartsWith("WELDED") ? "YOU WELDED THE SEAM YOURSELF. " : how.StartsWith("HERE'S") ? "A REPAIR KIT CLOSED THE SEAM. " : "THE BOATYARD CLOSED THE SEAM ON YOUR SCRAP. ")
                    + "NO MONSTER: A SPLIT SEAM AND A LOT OF FISH GOING HOME BY THEMSELVES. PEG IS SULKING.";
            if (fixedUp && hold && !Story.Story.StepDone("P3.1", "proof") && hold.inventory.GetItem("food_fish_raw") + hold.inventory.GetItem("food_fish_glow") >= 3)
                Story.Story.Note("p3_1:catch");
        }
    }
}
