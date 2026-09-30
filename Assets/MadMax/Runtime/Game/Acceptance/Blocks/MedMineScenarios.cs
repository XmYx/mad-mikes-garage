using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the MedMine block (depth stages G and H).</summary>
    public static class MedMineScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new MedMineLadder();
            yield return new MedMineClinic();
            yield return new MedMineWorks();
        }

        // ------------------------------------------------------------------ shared fixtures

        /// <summary>Clean slate for the body: no wounds, fed, watered, clean, full health (disclosed).</summary>
        internal static void Healthy(ScenarioContext c, string why)
        {
            var s = c.Game.Stats;
            s.injuries.Clear();
            s.hunger = s.thirst = 100f; s.hygiene = 90f; s.sick = 0f;
            s.health = s.MaxHealth;
            c.Fixture("wounds cleared, fed, watered and clean (" + why + ")");
        }

        /// <summary>A piece spawned on the ground at <paramref name="at"/>, facing <paramref name="face"/>.</summary>
        internal static Placeable Piece(WastelandGame g, string id, Vector3 at, Vector3 face)
        {
            var t = DeformableTerrain.Instance;
            at.y = t.Height(at.x, at.z);
            return FurnitureLibrary.Spawn(id, g.Build.Structures, at, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : Vector3.forward), g.propMaterial);
        }

        /// <summary>Grant a recipe's inputs (optional), queue it, fast-forward the job to 95 % and wait for the goods.</summary>
        internal static IEnumerator Make(ScenarioContext c, CraftingStation st, Recipe r, bool grant)
        {
            var g = c.Game;
            if (grant)
            {
                foreach (var (t, n) in r.resources) if (t != ResourceType.None) g.Inventory.Add(t, RecipeLibrary.Amount(n));
                foreach (var (it, n) in r.items) g.Inventory.AddItem(it, n);
                if (r.fuel != ResourceType.None) g.Inventory.Add(r.fuel, r.fuelAmount);
                c.Fixture("granted the inputs of " + r.name);
            }
            string why = g.CraftBlockReason(r, st);
            if (!c.Check(why == null, r.name + " can be made at the " + st.title + (why != null ? ": " + why : ""))) yield break;
            g.Craft(r, st);
            if (st.queue.Count > 0) st.queue[0].progress = 0.95f;
            c.Fixture(r.name + ": job fast-forwarded to 95 %");
            float t0 = Time.time;
            while (st.queue.Count > 0 && Time.time - t0 < 30f) yield return null;
            yield return new WaitForSeconds(0.2f);
        }

        internal static CraftingStation Station(Placeable p) => p ? p.GetComponentInChildren<CraftingStation>() : null;

        /// <summary>A roll that always comes up <paramref name="v"/> (surgery outcomes on demand).</summary>
        internal sealed class FixedRoll : System.Random
        {
            readonly double v;
            public FixedRoll(double v) { this.v = v; }
            public override double NextDouble() => v;
        }

        /// <summary>A wading spot at a running river (current 0.5 m/s+ mid-channel) nearest to <paramref name="near"/>:
        /// where to stand (a hand's depth of water, not over the knees), which way the channel lies, and a point a third of
        /// the way out from the centre line for a sluice.</summary>
        internal static bool RiverSpot(WastelandGame g, Vector3 near, out Vector3 stand, out Vector3 face, out Vector3 channel)
        {
            var w = g.World; var t = DeformableTerrain.Instance;
            stand = face = channel = default;
            var from = new Vector2(near.x, near.z);
            // running reaches (not the dry wadis), nearest first
            var reaches = new List<(float d, River rv, int i)>();
            foreach (var rv in w.rivers)
                for (int i = 2; i + 2 < rv.pts.Length; i++)
                    if (w.RiverFlow(rv.pts[i].x, rv.pts[i].y).magnitude >= 0.5f) reaches.Add(((rv.pts[i] - from).sqrMagnitude, rv, i));
            reaches.Sort((a, b) => a.d.CompareTo(b.d));
            foreach (var (_, rv, i) in reaches)
            {
                var P = rv.pts[i];
                var dir = (rv.pts[i + 1] - P).normalized; var n = new Vector2(-dir.y, dir.x);
                float half = rv.half[i];
                // wade in from the bank: the first spot with a hand's depth of water
                for (float s = half + 2f; s >= 0.5f; s -= 0.25f)
                {
                    var q = P + n * s;
                    float depth = t.WaterDepthNoLoad(q.x, q.y);
                    if (depth < 0.12f) continue;
                    if (depth > 0.9f) break;
                    stand = new Vector3(q.x, 0f, q.y);
                    face = new Vector3(-n.x, 0f, -n.y);
                    var m = P + n * (half * 0.35f);
                    channel = new Vector3(m.x, 0f, m.y);
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Depth stages G and H end to end in a live seed-7 world. Medicine: a herbal poultice made at a campfire
    /// stops a bleeding wound; willow-bark tea dulls the pain; a first-aid kit made at a workbench treats three wounds in
    /// one use; lying in a clinic bed heals a dressed wound at least three times as fast as standing about for the same
    /// time, and sets a fracture in traction. Mining: a gold pan at the nearest running river wins a flake of gold ore
    /// within 30 swirls; a sluice box set in the current washes 60 sand into ores including gold; the gold ore smelts to
    /// gold at a furnace, and traders buy it dear.</summary>
    class MedMineLadder : Scenario
    {
        public override string Id => "medmine.ladder";
        public override float Timeout => 300f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            c.Fixture("on foot at a level pad near the start");
            yield return new WaitForSeconds(0.4f);
            MedMineScenarios.Healthy(c, "medicine rung");
            var fwd = g.Player.transform.forward; var right = g.Player.transform.right;

            // ---- herbal tier: campfire poultice on a bleeding wound, bark tea
            var fire = MedMineScenarios.Station(MedMineScenarios.Piece(g, "campfire", pad + fwd * 1.8f, -fwd));
            yield return null;
            var rPoultice = RecipeLibrary.Get("fire_poultice");
            if (c.Check(fire && rPoultice != null && rPoultice.station == "campfire", "the campfire offers the herbal poultice"))
            {
                int before = g.Inventory.GetItem("med_poultice");
                yield return MedMineScenarios.Make(c, fire, rPoultice, true);
                c.Check(g.Inventory.GetItem("med_poultice") == before + 1, "a herbal poultice comes off the fire");
            }
            InjuryRules.Apply(g.Stats.injuries, 15f, "MELEE", new System.Random(7));
            c.Fixture("a 15-point blade cut (InjuryRules.Apply MELEE, seed 7)");
            var cut = g.Stats.injuries.Count > 0 ? g.Stats.injuries[g.Stats.injuries.Count - 1] : null;
            if (c.Check(cut != null && cut.Bleeding, "the cut bleeds: " + (cut != null ? Injury.WoundNames[(int)cut.type] : "none")))
            {
                float sev = cut.severity; int poultices = g.Inventory.GetItem("med_poultice");
                g.UseItem("med_poultice");
                c.Check(!cut.Bleeding && cut.bandaged, "the poultice binds the wound: no longer bleeding");
                c.Check(cut.severity < sev && g.Inventory.GetItem("med_poultice") == poultices - 1, $"it heals a little ({sev:0.00} → {cut.severity:0.00}) and is used up");
            }
            var rTea = RecipeLibrary.Get("fire_bark_tea");
            if (fire && c.Check(rTea != null, "willow-bark tea is brewed at the campfire"))
            {
                yield return MedMineScenarios.Make(c, fire, rTea, true);
                g.Stats.painkilled = false; g.Stats.painkillerUntil = 0f;
                g.UseItem("drink_bark_tea");
                c.Check(g.Stats.painkilled && g.Inventory.GetItem("drink_bark_tea") == 0, "the tea dulls the pain (painkilled)");
            }

            // ---- first aid: a kit treats several wounds in one use
            MedMineScenarios.Healthy(c, "first aid");
            InjuryRules.Apply(g.Stats.injuries, 20f, "SHOT", new System.Random(11));
            InjuryRules.Apply(g.Stats.injuries, 25f, "FALL", new System.Random(12));
            c.Fixture("two gunshot wounds (SHOT 20) and a broken leg (FALL 25) through InjuryRules.Apply");
            int wounds = g.Stats.injuries.Count, bleeding = 0, broken = 0;
            foreach (var i in g.Stats.injuries) { if (i.Bleeding) bleeding++; if (i.type == Wound.Fracture && !i.splinted) broken++; }
            c.Check(wounds >= 3 && bleeding >= 2 && broken >= 1, $"{wounds} wounds, {bleeding} bleeding, {broken} fracture");
            var bench = MedMineScenarios.Station(MedMineScenarios.Piece(g, "workbench", pad + fwd * 1.8f + right * 2.6f, -fwd));
            yield return null;
            var rKit = RecipeLibrary.Get("firstaid");
            if (c.Check(bench && rKit != null, "the workbench makes first-aid kits"))
            {
                yield return MedMineScenarios.Make(c, bench, rKit, true);
                c.Check(g.Inventory.GetItem("med_firstaid") == 1, "a first-aid kit is made");
            }
            g.UseItem("med_firstaid");
            int treated = 0; bool stillBleeding = false, unset = false;
            foreach (var i in g.Stats.injuries)
            {
                bool open = i.type != Wound.Bruise && i.type != Wound.Fracture;
                if ((open && i.bandaged && i.disinfected) || (i.type == Wound.Fracture && i.splinted)) treated++;
                stillBleeding |= i.Bleeding; unset |= i.type == Wound.Fracture && !i.splinted;
            }
            c.Metric("wounds_treated_by_one_kit", treated, "");
            c.Check(treated == wounds && treated >= 3 && !stillBleeding && !unset, $"one kit treats all {wounds} wounds (dressed, disinfected, splinted)");
            c.Check(g.Inventory.GetItem("med_firstaid") == 0, "the kit is used up");

            // ---- clinic: the same dressed wound heals faster lying in the clinic bed than standing about
            MedMineScenarios.Healthy(c, "clinic comparison");
            InjuryRules.Apply(g.Stats.injuries, 15f, "MELEE", new System.Random(7));
            var probe = g.Stats.injuries.Count > 0 ? g.Stats.injuries[0] : null;
            if (!c.Check(probe != null, "a cut to compare healing on")) yield break;
            probe.bandaged = probe.disinfected = true; probe.bandageAge = 0f; probe.severity = 1f;
            c.Fixture("the cut dressed and disinfected (only the healing rate differs); severity reset to 1 for each run");
            float t0 = Time.time;
            yield return new WaitForSeconds(5f);
            float outside = (1f - probe.severity) / (Time.time - t0);
            var bedPiece = MedMineScenarios.Piece(g, "clinic_bed", pad - fwd * 2.2f, fwd);
            yield return null;
            var bed = bedPiece ? bedPiece.GetComponent<ClinicBed>() : null;
            if (!c.Check(bed, "clinic bed spawns")) yield break;
            bed.Use(g, false);
            yield return null;
            c.Check(g.Player.SeatedOn == bed.BedSeat, "the player lies in the clinic bed");
            probe.severity = 1f;
            t0 = Time.time;
            yield return new WaitForSeconds(5f);
            float inside = (1f - probe.severity) / (Time.time - t0);
            c.Metric("heal_rate_outside", outside * 3600f, "severity/h");
            c.Metric("heal_rate_clinic", inside * 3600f, "severity/h");
            c.Note($"clinic bed roofed: {bed.Roofed}");
            c.Check(outside > 0f && inside >= outside * 3f, $"the clinic bed heals at least 3x as fast ({inside / Mathf.Max(1e-6f, outside):0.0}x)");
            InjuryRules.Apply(g.Stats.injuries, 25f, "FALL", new System.Random(12));
            var leg = g.Stats.injuries[g.Stats.injuries.Count - 1];
            c.Fixture("a broken leg while in the bed (FALL 25)");
            if (c.Check(leg.type == Wound.Fracture && !leg.splinted, "a fresh fracture, not splinted"))
            {
                float w0 = Time.time;
                while (!leg.splinted && Time.time - w0 < bed.setAfter + 4f) yield return null;
                c.Check(leg.splinted, $"traction sets the fracture after lying {bed.Lain:0} s");
            }
            c.Screenshot("clinic_bed");
            yield return null;
            g.Player.StandUp();
            yield return new WaitForSeconds(0.3f);

            // ---- gold pan at the nearest running river
            if (!MedMineScenarios.RiverSpot(g, g.Player.transform.position, out var stand, out var face, out var channel)) { c.Block("no running river with a wading spot in this world"); yield break; }
            c.Note($"river spot {stand.x:0},{stand.z:0}, {Vector3.Distance(stand, pad):0} m from the pad");
            t.BuildAllNow(stand);
            stand.y = t.Height(stand.x, stand.z) + 0.2f;
            g.Player.Teleport(stand, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
            c.Fixture("teleported to a wading spot on the nearest running river (terrain built around it)");
            yield return new WaitForSeconds(1.5f);
            var feet = g.Player.transform.position; var front = feet + g.Player.transform.forward * 0.9f;
            c.Note($"water at the feet {t.WaterDepth(feet.x, feet.z):0.00} m, in front {t.WaterDepth(front.x, front.z):0.00} m, current {g.World.RiverFlow(front.x, front.z).magnitude:0.00} m/s, gold richness {g.GoldRichness(front):0.00}");
            var pan = ToolLibrary.Create("tool_gold_pan", g.propMaterial);
            if (!c.Check(pan is GoldPanTool, "the gold pan is a tool")) yield break;
            g.Inventory.AddItem("tool_gold_pan");
            g.Player.Equip(pan);
            c.Fixture("gold pan in hand");
            int ore0 = g.Inventory.Get(ResourceType.GoldOre), sand0 = g.Inventory.Get(ResourceType.Sand), tries = 0;
            while (tries < 30 && g.Inventory.Get(ResourceType.GoldOre) == ore0) { pan.Strike(g.Player); tries++; yield return null; }
            c.Metric("pans_to_first_flake", tries, "");
            c.Check(g.Inventory.Get(ResourceType.GoldOre) > ore0, $"panning wins gold ore within 30 tries ({tries})");
            c.Check(g.Inventory.Get(ResourceType.Sand) > sand0, "and some sand");
            c.Screenshot("gold_pan");
            yield return null;
            g.Player.Equip(null);

            // ---- sluice box in the current: soil in, ore (and gold) out
            var flowDir = g.World.RiverFlow(channel.x, channel.z);
            var sluicePiece = MedMineScenarios.Piece(g, "sluice_box", channel, new Vector3(-flowDir.x, 0f, -flowDir.y));
            yield return null;
            var sluice = sluicePiece ? sluicePiece.GetComponent<SluiceBox>() : null;
            if (!c.Check(sluice, "sluice box spawns in the channel")) yield break;
            sluice.Advance(0f);
            c.Note($"sluice current {sluice.Flow:0.00} m/s, water {t.WaterDepth(channel.x, channel.z):0.00} m, richness {sluice.Richness:0.00}");
            if (c.Check(sluice.Running, "the sluice runs in the current"))
            {
                g.Inventory.Add(ResourceType.Sand, SluiceBox.Capacity);
                c.Fixture(SluiceBox.Capacity + " sand into the pack");
                int fed = sluice.Feed(g.Inventory);
                c.Check(fed == SluiceBox.Capacity, $"the hopper takes {fed} sand");
                sluice.Advance(1200f);
                c.Fixture("the sluice run for 1200 s of water (Advance)");
                int got = sluice.TakeCatch(g.Inventory, out string what);
                c.Note("riffles:" + what);
                c.Metric("sluice_catch", got, "units");
                c.Check(sluice.Load == 0 && got > 0, "every load washed and ore caught in the riffles");
                c.Check(what.Contains(ResourceInfo.Name(ResourceType.GoldOre)), "the catch includes gold ore");
            }

            // ---- smelt gold at a furnace, sell it dear
            var furnace = MedMineScenarios.Station(MedMineScenarios.Piece(g, "furnace", g.Player.transform.position - g.Player.transform.forward * 2.5f, g.Player.transform.forward));
            yield return null;
            var rGold = RecipeLibrary.Get("s_furnace_gold");
            if (c.Check(furnace && rGold != null && rGold.outputResource == ResourceType.Gold, "the furnace smelts gold"))
            {
                int have = g.Inventory.Get(ResourceType.GoldOre);
                c.Metric("gold_ore_won", have - ore0, "");
                if (have < 3) { g.Inventory.Add(ResourceType.GoldOre, 3 - have); c.Fixture("topped the gold ore up to 3 (+" + (3 - have) + ")"); }
                g.Inventory.Add(ResourceType.Charcoal, 1);
                c.Fixture("1 charcoal");
                int gold0 = g.Inventory.Get(ResourceType.Gold);
                yield return MedMineScenarios.Make(c, furnace, rGold, false);
                c.Check(g.Inventory.Get(ResourceType.Gold) == gold0 + 1, "3 gold ore smelt to 1 gold");
            }
            string goldId = "res:" + (int)ResourceType.Gold;
            c.Check(MadMax.Npc.Trade.Value(goldId) >= 30f && MadMax.Npc.Trade.Buys("scrap", goldId) && MadMax.Npc.Trade.Buys("fuel", goldId), $"traders buy gold dear ({MadMax.Npc.Trade.Value(goldId):0} scrap a unit)");
            c.Screenshot("sluice_and_furnace");
            yield return null;
        }
    }

    /// <summary>The clinic rung: a medicine cabinet stocks the clinic bed (a bleeding cut is dressed from it); a lodged
    /// fragment keeps a wound from closing; the surgery table cuts it out and stitches deep wounds with a success roll
    /// (odds from Survival, Intelligence and an assistant), using a dose of antibiotics and painkillers; a failed cut
    /// tears the wound open; and a hurt neighbour laid on the clinic bed stops bleeding and mends until well.</summary>
    class MedMineClinic : Scenario
    {
        public override string Id => "medmine.clinic";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            c.Fixture("on foot at a level pad near the start");
            yield return new WaitForSeconds(0.4f);
            var fwd = g.Player.transform.forward; var right = g.Player.transform.right;
            MedMineScenarios.Healthy(c, "clinic");

            // ---- a stocked clinic bed dresses a bleeding cut
            var bedPiece = MedMineScenarios.Piece(g, "clinic_bed", pad + fwd * 2f, -fwd);
            var cabPiece = MedMineScenarios.Piece(g, MedSupply.CabinetId, pad + fwd * 2f + right * 2f, -fwd);
            yield return null;
            var bed = bedPiece ? bedPiece.GetComponent<ClinicBed>() : null;
            var cab = cabPiece ? cabPiece.GetComponent<Container>() : null;
            if (!c.Check(bed && cab, "clinic bed and medicine cabinet spawn")) yield break;
            cab.inventory.AddItem("med_bandage", 1);
            c.Fixture("one bandage in the cabinet");
            InjuryRules.Apply(g.Stats.injuries, 15f, "MELEE", new System.Random(7));
            var cut = g.Stats.injuries[g.Stats.injuries.Count - 1];
            c.Fixture("a bleeding cut (MELEE 15)");
            bed.Use(g, false);
            float t0 = Time.time;
            while (cut.Bleeding && Time.time - t0 < 6f) yield return null;
            c.Check(!cut.Bleeding && cab.inventory.GetItem("med_bandage") == 0, "the clinic dresses the cut from the cabinet's stock");
            g.Player.StandUp();
            yield return new WaitForSeconds(0.3f);

            // ---- shrapnel: a lodged fragment keeps the wound from closing
            var probe = new Injury { type = Wound.Laceration, shrapnel = true, bandaged = true, disinfected = true, severity = 0.5f };
            var clean = new Injury { type = Wound.Laceration, bandaged = true, disinfected = true, severity = 0.5f };
            probe.Tick(600f, 90f, 1f); clean.Tick(600f, 90f, 1f);
            c.Check(Mathf.Approximately(probe.severity, Injury.ShrapnelFloor) && clean.severity < Injury.ShrapnelFloor, $"a wound with shrapnel stops healing at {Injury.ShrapnelFloor:0.00} (clean: {clean.severity:0.00})");

            // ---- surgery: cut it out, stitch the deep wounds
            MedMineScenarios.Healthy(c, "surgery");
            InjuryRules.Apply(g.Stats.injuries, 20f, "SHOT", new System.Random(11));
            if (!c.Check(g.Stats.injuries.Count > 0, "gunshot wounds to operate on")) yield break;
            g.Stats.injuries[0].shrapnel = true;
            c.Fixture("two gunshot wounds (SHOT 20), a bullet lodged in the first");
            var tablePiece = MedMineScenarios.Piece(g, "surgery_table", pad - fwd * 2.2f, fwd);
            yield return null;
            var table = tablePiece ? tablePiece.GetComponent<SurgeryTable>() : null;
            if (!c.Check(table, "surgery table spawns")) yield break;
            c.Check(table.Missing(g) != null, "no operation without the drugs: needs " + table.Missing(g));
            g.Inventory.AddItem("med_antibiotics", 1); g.Inventory.AddItem("med_painkillers", 1);
            g.Stats.painkilled = false;
            c.Fixture("1 antibiotics and 1 painkillers in the pack");
            int targets = SurgeryTable.Targets(g.Stats);
            var (ok, failed) = table.Operate(g, new MedMineScenarios.FixedRoll(0.01));
            c.Fixture("the surgery roll fixed to a success (the odds are checked below)");
            bool anyLeft = false;
            foreach (var i in g.Stats.injuries) anyLeft |= SurgeryTable.Target(i);
            c.Check(targets >= 2 && ok == targets && failed == 0 && !anyLeft, $"surgery removes the shrapnel and stitches the deep wounds ({ok}/{targets})");
            c.Check(g.Inventory.GetItem("med_antibiotics") == 0 && g.Inventory.GetItem("med_painkillers") == 0 && g.Stats.painkilled, "the operation takes a dose of antibiotics and painkillers");
            c.Check(g.Player.SeatedOn == tablePiece.GetComponent<Seat>(), "the patient lies on the table");
            g.Player.StandUp();
            yield return new WaitForSeconds(0.2f);
            // a failed cut tears the wound open
            MedMineScenarios.Healthy(c, "failed surgery");
            InjuryRules.Apply(g.Stats.injuries, 20f, "SHOT", new System.Random(11));
            foreach (var i in g.Stats.injuries) { i.bandaged = true; i.severity = 0.7f; }
            g.Inventory.AddItem("med_antibiotics", 1); g.Inventory.AddItem("med_painkillers", 1);
            c.Fixture("dressed gunshot wounds, drugs again; the roll fixed to a failure");
            var (ok2, failed2) = table.Operate(g, new MedMineScenarios.FixedRoll(0.99));
            bool torn = true;
            foreach (var i in g.Stats.injuries) if (SurgeryTable.Target(i)) torn &= i.Bleeding && i.severity > 0.7f;
            c.Check(ok2 == 0 && failed2 > 0 && torn, "a failed cut tears the wound open (bleeding, worse)");
            c.Check(SurgeryTable.Chance(8, 5, false) > SurgeryTable.Chance(0, 5, false) && SurgeryTable.Chance(3, 5, true) > SurgeryTable.Chance(3, 5, false),
                $"skill and an assistant raise the odds ({SurgeryTable.Chance(0, 5, false):0.00} → {SurgeryTable.Chance(8, 5, false):0.00}, assisted +{SurgeryTable.Chance(3, 5, true) - SurgeryTable.Chance(3, 5, false):0.00})");
            g.Player.StandUp();
            yield return new WaitForSeconds(0.2f);
            MedMineScenarios.Healthy(c, "after surgery");

            // ---- treat someone else: a bleeding neighbour laid on the clinic bed
            var at = pad + right * 3f;
            at.y = DeformableTerrain.Instance.Height(at.x, at.z) + 0.05f;
            var prof = MadMax.Npc.NpcProfile.Make("medmine:patient", MadMax.Npc.NpcRole.Resident, 4242);
            var npc = MadMax.Npc.Npc.Spawn(prof, at, 0f, null, g.propMaterial);
            npc.mode = MadMax.Npc.Npc.Mode.Stand;
            npc.Bleed(60f);
            c.Fixture("a resident spawned beside the bed, bleeding (Npc.Bleed 60 s)");
            yield return new WaitForSeconds(1.5f);
            c.Check(npc.Wounded, $"the neighbour is hurt ({npc.Health:0}/{npc.MaxHealth:0})");
            c.Check(bed.Candidate(g) == npc, "the clinic bed offers to lay them down");
            bed.Use(g, true);
            yield return null;
            c.Check(npc.Berthed && bed.Patient == npc, "they lie in the clinic bed");
            c.Screenshot("clinic_patient");
            t0 = Time.time;
            while (bed.Patient && Time.time - t0 < 40f) yield return null;
            c.Metric("patient_mended_s", Time.time - t0, "s");
            c.Check(!bed.Patient && !npc.Berthed && !npc.Wounded && npc.Alive, "the bleeding stops, they mend and get up");
            Object.Destroy(npc.gameObject);
        }
    }

    /// <summary>The mine works: three lengths of mine rail make a line, a loaded ore cart set on it is pushed to the end
    /// beside a powered stamp mill, the mill stamps the cart's iron ore into concentrate (drawing from the parked cart),
    /// and the concentrate smelts to half again as much iron as the ore would; a 3 m pit is at risk of slumping until a
    /// mine prop stands in it.</summary>
    class MedMineWorks : Scenario
    {
        public override string Id => "medmine.works";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(12f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            c.Fixture("on foot at a level pad near the start");
            yield return new WaitForSeconds(0.4f);
            var fwd = g.Player.transform.forward; var right = g.Player.transform.right;

            // ---- rails and the ore cart
            for (int i = 0; i < 3; i++) MedMineScenarios.Piece(g, "mine_rail", pad + fwd * (2f + i * 2f), fwd);
            var cartPiece = MedMineScenarios.Piece(g, "ore_cart", pad + fwd * 1.5f, fwd);
            if (cartPiece) cartPiece.transform.position += Vector3.up * 0.25f;
            c.Fixture("three mine rails laid end to end, an ore cart set on the first");
            yield return null; yield return null;
            var cart = cartPiece ? cartPiece.GetComponent<OreCart>() : null;
            var tub = cartPiece ? cartPiece.GetComponent<Container>() : null;
            if (!c.Check(cart && tub, "ore cart spawns")) yield break;
            c.Check(cart.OnRails, "the cart sits on the rail");
            tub.inventory.Add(ResourceType.IronOre, 6);
            c.Fixture("6 iron ore in the cart");
            var start = cart.transform.position;
            float run = cart.Push(start - fwd * 1.5f);
            c.Metric("cart_run", run, "m");
            float t0 = Time.time;
            while (cart.Moving && Time.time - t0 < 8f) yield return null;
            var end = pad + fwd * 7f;
            float off = Vector2.Distance(new Vector2(cart.transform.position.x, cart.transform.position.z), new Vector2(end.x, end.z));
            c.Check(run > 5f && !cart.Moving && off < 0.3f, $"pushed, the cart runs {run:0.0} m through the joints to the end of the line ({off:0.00} m off)");

            // ---- powered stamp mill beside the end of the line, fed from the parked cart
            var mill = MedMineScenarios.Piece(g, "stamp_mill", end + right * 2.6f, -right);
            var gen = MedMineScenarios.Piece(g, "coal_generator", end + right * 2.6f + fwd * 3.5f, -fwd);
            yield return null;
            var st = MedMineScenarios.Station(mill);
            var genComp = gen ? gen.GetComponent<Generator>() : null;
            if (!c.Check(st && st.type == "stamp_mill" && genComp && mill.GetComponent<StampMill>(), "stamp mill and a steam generator spawn")) yield break;
            genComp.fuel = 60f; genComp.on = true;
            mill.GetComponent<UtilityNode>().Link(gen.GetComponent<UtilityNode>(), UtilityKind.Power);
            c.Fixture("fuelled steam generator cabled to the mill");
            t0 = Time.time;
            while (!st.Powered && Time.time - t0 < 3f) yield return null;
            c.Check(st.Powered, "the stamp mill has power");
            g.Player.Teleport(end + right * 1.2f - fwd * 1.5f + Vector3.up * 0.3f, 0f);
            yield return new WaitForSeconds(0.3f);
            var rStamp = RecipeLibrary.Get("stamp_iron");
            int conc0 = g.Inventory.GetItem("misc_iron_concentrate"), packOre = g.Inventory.Get(ResourceType.IronOre);
            if (packOre > 0) { g.Inventory.TrySpend(ResourceType.IronOre, packOre); c.Fixture("took " + packOre + " iron ore out of the pack (the cart must supply the mill)"); }
            if (c.Check(rStamp != null && rStamp.station == "stamp_mill", "the stamp mill stamps iron ore"))
            {
                yield return MedMineScenarios.Make(c, st, rStamp, false);
                c.Check(tub.inventory.Get(ResourceType.IronOre) == 3, "the mill drew 3 ore from the parked cart");
                c.Check(g.Inventory.GetItem("misc_iron_concentrate") == conc0 + 3, "3 iron ore stamp to 3 concentrate");
            }
            c.Screenshot("stamp_mill");
            yield return null;
            // concentrate smelts richer than ore
            var furnace = MedMineScenarios.Station(MedMineScenarios.Piece(g, "furnace", g.Player.transform.position - right * 2.5f, right));
            yield return null;
            var rConc = RecipeLibrary.Get("s_furnace_iron_conc"); var rOre = RecipeLibrary.Get("s_furnace_iron");
            if (c.Check(furnace && rConc != null && rOre != null, "the furnace smelts concentrate"))
            {
                g.Inventory.Add(ResourceType.Charcoal, 1);
                c.Fixture("1 charcoal");
                int iron0 = g.Inventory.Get(ResourceType.Iron);
                yield return MedMineScenarios.Make(c, furnace, rConc, false);
                int got = g.Inventory.Get(ResourceType.Iron) - iron0;
                c.Metric("iron_from_3_concentrate", got, "");
                c.Check(got == 3 && rOre.amount == 2, $"3 ore → 3 concentrate → {got} iron, against {rOre.amount} iron straight from 3 ore");
            }
            var gOre = RecipeLibrary.Get("s_furnace_gold"); var gConc = RecipeLibrary.Get("s_furnace_gold_conc");
            c.Check(gOre != null && gConc != null && gConc.amount == 2 * gOre.amount, "gold concentrate smelts to twice the gold");

            // ---- a deep pit slumps unless propped
            var pit = pad - right * 7f;
            pit.y = t.Height(pit.x, pit.z);
            for (int i = 0; i < 3; i++) t.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dig, pit, 2.6f, 1.1f, 0);
            c.Fixture("dug a 3 m pit (terraform)");
            yield return new WaitForSeconds(0.5f);
            pit.y = t.Height(pit.x, pit.z);
            c.Note($"pit depth {t.DugDepth(pit.x, pit.z):0.0} m");
            c.Check(g.SlumpRisk(pit, out _), "an unpropped 3 m pit can slump");
            MedMineScenarios.Piece(g, "mine_prop", pit, fwd);
            c.Check(!g.SlumpRisk(pit, out _), "a mine prop holds it");
        }
    }
}
