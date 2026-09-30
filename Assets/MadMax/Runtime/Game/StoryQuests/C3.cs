using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>C3 ENOUGH TO GO AROUND in the world: the clinic's porch, the public generator in the square with its two
    /// street lamps and the notice board, Bo Tillman's irrigation pump at the village (its generator's fuel line weeping:
    /// a damaged piece to mend). While it runs: Sera's plan answers carry today's numbers (Allocation), the shares are
    /// fixed when posted, each hand-over takes that share of the cans and fuels the customer's generator, and once both
    /// places have signed the square gets the village's produce stall, the clinic a bed and a stocked cabinet for anyone,
    /// the lamps their share; what nobody needed is held for the convoy.</summary>
    public partial class WastelandGame
    {
        string c3Sig;

        partial void Scene_C3()
        {
            if (!Build || !Build.Structures) return;
            ArcCPut("c3_clinic", "porch_awning", new Vector3(0f, 0f, -1.6f), 0f);
            ArcCPut("c3_clinic", "bench", new Vector3(-1.8f, 0f, -1.4f), 0f);
            ArcCPut("c3_clinic", "flower_pot", new Vector3(2.2f, 0f, -2.2f), 0f);
            // the public generator, its lamps and the notice board
            var gen = ArcCPut("c3_generator", "generator", new Vector3(0f, 0f, -2.2f), 0f);
            if (gen && gen.TryGetComponent<Generator>(out var g0)) { g0.fuel = 1f; g0.on = false; gen.Dirty(); }
            var genNode = gen ? gen.GetComponent<UtilityNode>() : null;
            foreach (float x in new[] { -4.5f, 4.5f })
            {
                var lamp = ArcCPut("c3_generator", "lamppost", new Vector3(x, 0f, 1.5f), 0f);
                if (lamp && genNode && lamp.TryGetComponent<UtilityNode>(out var ln)) ln.Link(genNode, UtilityKind.Power);
            }
            ArcCPut("c3_generator", "sign", new Vector3(2.2f, 0f, -1.6f), 0f);
            // Bo's pump: a well pump, its generator (fuel line weeping), a tank and three thirsty beds
            ArcCPut("c3_farm", "pump", new Vector3(-1.5f, 0f, -2.5f), 0f);
            var pg = ArcCPut("c3_farm", "generator", new Vector3(1.2f, 0f, -2.6f), 0f);
            if (pg) { pg.hits = 5; if (pg.TryGetComponent<Generator>(out var g1)) { g1.fuel = 0f; g1.on = false; } pg.Dirty(); }
            ArcCPut("c3_farm", "water_tank", new Vector3(-4.5f, 0f, -3.5f), 0f);
            for (int i = 0; i < 3; i++)
            {
                var bed = ArcCPut("c3_farm", "field_bed", new Vector3(-2f + i * 2f, 0f, 4.5f), 0f);
                if (bed && bed.TryGetComponent<GardenPlot>(out var plot)) { plot.crop = "seed_corn"; plot.growth = 0.35f; plot.water = 0f; bed.Dirty(); }
            }
            Journal.Add("PLACE", "THE CLINIC, THE PUBLIC GENERATOR IN THE SQUARE AND BO TILLMAN'S PUMP AT THE VILLAGE ALL WANT SERA'S DIESEL");
        }

        partial void Tick_C3()
        {
            if (Time.frameCount % 20 != 0) return;
            // Bo's pump generator mended in build mode
            if (!Story.Story.StepDone("C3", "fix_farm") && !Story.Story.Flag("c3_pump_ok"))
            {
                var pg = ArcCProp("generator", "c3_farm", 6f);
                var def = FurnitureLibrary.Get("generator");
                if (pg && def != null && pg.hits >= def.hits) { Story.Story.SetFlag("c3_pump_ok"); Story.Story.Note("c3:pump_fixed"); Toast("BO'S PUMP GENERATOR HOLDS ITS FUEL NOW: IT NEEDS THREE CANS, NOT FIVE"); }
            }
            // Sera's answers carry the numbers she would post right now
            string sig = Story.Story.StepDone("C3", "fix_farm") + "|" + Story.Story.StepDone("C3", "fix_lamps");
            if (sig != c3Sig && !Story.Story.StepDone("C3", "plan"))
            {
                c3Sig = sig;
                foreach (var (topic, label) in new[] { ("c3_byneed", "BY NEED"), ("c3_even", "EVEN"), ("c3_clinicfirst", "THE CLINIC"), ("c3_farmfirst", "THE FARM") })
                    ArcCReply("C3", "plan", topic, StoryLibrary.C3PlanReply(label));
            }
            // the posted shares are fixed when posted
            string plan = Story.Story.Route("C3", "plan");
            if (plan != null && !ArcCRecord.Has("c3:share:clinic"))
            {
                var cs = StoryLibrary.C3Customers();
                var p = StoryLibrary.C3PlanOf(plan, out int fav);
                var sh = Allocation.Shares(cs, StoryLibrary.C3Supply, p, fav);
                for (int i = 0; i < cs.Count; i++) ArcCRecord.Put("c3:share:" + cs[i].key, sh[i]);
                Journal.Add("STORY", "POSTED IN THE SQUARE: " + Allocation.Posted(cs, sh, StoryLibrary.C3Supply, "CANS"));
                Toast("THE SHARES ARE POSTED ON THE BOARD IN THE SQUARE");
            }
            // hand-overs: the posted share of the cans, into the customer's generator
            foreach (var (step, key, anchor) in new[] { ("d_clinic", "clinic", "c3_clinic"), ("d_farm", "farm", "c3_farm"), ("d_lamps", "lamps", "c3_generator") })
            {
                if (!Story.Story.StepDone("C3", step) || ArcCRecord.Has("c3:given:" + key) || !ArcCRecord.Has("c3:share:" + key)) continue;
                int share = ArcCRecord.Get("c3:share:" + key), have = Inventory.GetItem(StoryLibrary.C3Can), give = Mathf.Min(share, have);
                using (Inventory.Source("DELIVERED", "HANDED OVER")) if (give > 0) Inventory.TakeItem(StoryLibrary.C3Can, give);
                ArcCRecord.Put("c3:given:" + key, give);
                if (give < share) Toast("SHORT: " + give + " OF " + share + " CANS HANDED OVER");
                var gen = key == "clinic" ? null : ArcCProp("generator", anchor, 6f);
                if (gen && gen.TryGetComponent<Generator>(out var gc)) { gc.fuel = gc.tankLitres; gc.on = true; gen.Dirty(); }
            }
            // both places signed: the square, the clinic and the lamps change
            if (Story.Story.StepDone("C3", "sign") && !Story.Story.Flag("c3_changed")) C3Settle();
        }

        void C3Settle()
        {
            Story.Story.SetFlag("c3_changed");
            int left = Inventory.GetItem(StoryLibrary.C3Can);
            if (left > 0) using (Inventory.Source("HELD FOR THE CONVOY")) Inventory.TakeItem(StoryLibrary.C3Can, left);
            ArcCRecord.Put("c3:reserve", left);
            // the clinic: a bed anyone may lie in and a stocked cabinet
            ArcCPut("c3_clinic", "clinic_bed", new Vector3(1.4f, 0f, -1.7f), 90f);
            var cab = ArcCPut("c3_clinic", MadMax.Building.MedSupply.CabinetId, new Vector3(-0.2f, 0f, -2.7f), 0f);
            if (cab && cab.TryGetComponent<Container>(out var box)) { box.inventory.AddItem("med_bandage", 3); box.inventory.AddItem("med_disinfectant", 2); }
            // the village's produce stall in the square
            ArcCPut("c3_generator", "porch_awning", new Vector3(-6f, 0f, -4f), 0f);
            ArcCPut("c3_generator", "table", new Vector3(-6f, 0f, -3.7f), 0f);
            ArcCPut("c3_generator", "crate", new Vector3(-7.4f, 0f, -4.2f), 20f);
            ArcCPut("c3_generator", "crate", new Vector3(-4.6f, 0f, -4.3f), 70f);
            var market = Market.Near(StoryAnchors.Get("town1")); var village = Market.Near(StoryAnchors.Get("c_village"));
            if (market != null) { Market.Sold(market, "food_potato", 30); Market.Sold(market, "food_carrot", 20); }
            if (village != null) Market.Sold(village, "res:" + (int)ResourceType.Diesel, 25);
            Factions.Shift(Faction.Settlers, 4);
            ArcCPayoff("C3", StoryLibrary.C3Payoff());
            Journal.Add("STORY", "PROVISIONAL PASSAGE AGREEMENT SIGNED: THE VILLAGE SELLS IN THE SQUARE, THE CLINIC KEEPS A BED FOR ANYONE, THE LAMPS BURN THEIR SHARE");
            Toast("THE AGREEMENT IS UP ON THE BOARD: PRODUCE IN THE SQUARE, A CLINIC BED FOR ANYONE");
            Story.Story.Note("c3:settled");
        }
    }
}
