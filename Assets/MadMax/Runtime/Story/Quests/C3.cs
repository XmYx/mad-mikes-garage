using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // C3 ENOUGH TO GO AROUND (storyline §7): Sera bought twelve cans of diesel; the clinic, Bo Tillman's irrigation pump
    // at the village and the public generator in the square ask for sixteen. Ask each what they really burn (the clinic
    // four, the pump five because its fuel line weeps, the lamps four because they burn all night), mend the waste if you
    // like (the pump's generator, a timer or oil lamps), then share it out with Sera on the story system "allocation"
    // (Story/Mechanics/Allocation): by need, evenly, or one customer favoured and said so. Deliver the posted shares;
    // the village and the town sign a provisional passage agreement, and the square gets a produce stall, the clinic a
    // bed for anyone, the lamps their share.
    public static partial class StoryLibrary
    {
        public const string C3Can = "story_c3_can", C3Posted = "story_c3_posted", C3Mark = "story_c3_mark", C3Agreement = "story_c3_agreement";
        public const int C3Supply = 12;

        static partial void Author_C3(QuestDef q)
        {
            ItemIds.Register(C3Can, "SHIPMENT CAN (10 L DIESEL)");
            ItemIds.Register(C3Posted, "POSTED SHARES (COPY)");
            ItemIds.Register(C3Mark, "THE VILLAGE'S MARK");
            ItemIds.Register(C3Agreement, "PROVISIONAL PASSAGE AGREEMENT");
            foreach (var k in new[] { "c3_clinic", "c3_farm", "c3_generator" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "WHAT DID THE TOLL BOOK EVER BUY THE VILLAGE?";
            q.offerReply = "NOTHING. SO I BOUGHT SOMETHING MYSELF: A HUNDRED AND TWENTY LITRES OF DIESEL IN TWELVE CANS, OFF A NOMAD TANKER. THE CLINIC, BO TILLMAN'S " +
                           "IRRIGATION PUMP AND THE LAMPS IN THE SQUARE WANT IT, AND BETWEEN THEM THEY'VE ASKED FOR SIXTEEN CANS. SHARE IT OUT SO IT CAN BE DEFENDED " +
                           "IN PUBLIC. IF BOTH PLACES SEE IT DONE STRAIGHT, THEY'LL SIGN A PASSAGE AGREEMENT.";
            q.hook = "SERA'S SHIPMENT: TWELVE CANS OF DIESEL, THREE CUSTOMERS ASKING FOR SIXTEEN: THE CLINIC, BO TILLMAN'S PUMP AND THE LAMPS IN THE SQUARE.";

            Step(q, "shipment", "TAKE THE SHIPMENT FROM SERA", "town1").Says("sera", "c3_cans", "HAND ME THE CANS. I'LL FIND OUT WHO NEEDS WHAT.",
                "TWELVE CANS. THE CLAIMS: THE CLINIC SIX, BO FIVE, THE LAMPS FIVE. ASK WHAT THEY ACTUALLY BURN BEFORE YOU PROMISE ANYONE ANYTHING.")
                .Pays(r => r.items.Add((C3Can, C3Supply)));
            Step(q, "clinic", "ASK THE CLINIC WHAT IT REALLY BURNS", "c3_clinic").Says("c3_doctor", "c3_clinic", "HOW MUCH DOES THE CLINIC REALLY BURN?",
                "I ASKED FOR SIX. ...FOUR, IF I'M HONEST: TWO WERE FOR THE WINTER. THE STERILISER AND THE COLD CABINET CAN'T STOP, AND NEITHER CAN I.").Optional();
            Step(q, "farm", "ASK BO TILLMAN WHAT HIS PUMP REALLY BURNS", "c3_farm").Says("c3_farmer", "c3_farm", "SHOW ME WHAT THE PUMP BURNS.",
                "FIVE CANS, AND IT DRINKS EVERY DROP. ...ALL RIGHT, HALF OF IT GOES IN THE DIRT: THE GENERATOR'S FUEL LINE WEEPS. MEND IT AND IT'S THREE. " +
                "MY NEPHEW WOULD DO IT FOR FIFTEEN SCRAP AND A HAT.").Optional();
            Step(q, "lamps", "ASK THE LAMPLIGHTER WHAT THE PUBLIC GENERATOR RUNS", "c3_generator").Says("c3_warden", "c3_lamps", "WHAT DOES THE PUBLIC GENERATOR RUN?",
                "THE LAMPS IN THE SQUARE AND THE WELL PUMP. ALL NIGHT, EVERY NIGHT, BECAUSE NOBODY TRUSTS THE DARK. FOUR CANS. A TIMER ON THE LAMPS AND IT'S TWO. " +
                "OIL LAMPS AND IT'S TWO, AND I GET TO LIGHT THEM.").Optional();
            Step(q, "inspect", "FIND OUT WHAT EACH REALLY NEEDS: THE CLINIC, BO'S PUMP AND THE LAMPS", "c3_clinic").When(Goal.Steps, "clinic,farm,lamps", 3f);
            Step(q, "fix_farm", "OPTIONAL: STOP BO'S PUMP WASTING FUEL: MEND ITS GENERATOR ([B], AIM, R) OR PAY HIS NEPHEW", "c3_farm")
                .When(Goal.Event, "c3:pump_fixed", label: "MENDED THE PUMP'S GENERATOR")
                .Says("c3_farmer", "c3_nephew", "PAY YOUR NEPHEW FOR A NEW FUEL LINE.", "HE'LL BE THERE BY LUNCH. HE'LL WANT THE HAT AS WELL. HE ALWAYS WANTS THE HAT.").Optional()
                .Pays(r => r.training.Add((Skill.Mechanics, 3f)));
            q.steps[q.steps.Count - 1].any[1].price = 15;
            Step(q, "fix_lamps", "OPTIONAL: STOP THE LAMPS BURNING ALL NIGHT: A POWER TIMER OR LIGHT SENSOR BY THE PUBLIC GENERATOR, OR OIL LAMPS INSTEAD", "c3_generator")
                .When(Goal.Build, "power_timer|light_sensor", 10f, "A TIMER ON THE LAMPS")
                .When(Goal.Build, "lamp", 10f, "OIL LAMPS INSTEAD").Optional()
                .Pays(r => r.training.Add((Skill.Construction, 3f)));
            Step(q, "plan", "DECIDE THE SHARES WITH SERA AND POST THEM IN THE SQUARE", "town1")
                .Says("sera", "c3_byneed", "BY NEED, POSTED ON THE BOARD.", "BY NEED.")
                .Says("sera", "c3_even", "EVEN THIRDS, POSTED ON THE BOARD.", "EVEN THIRDS.")
                .Says("sera", "c3_clinicfirst", "THE CLINIC FIRST, AND WE SAY SO.", "THE CLINIC FIRST.")
                .Says("sera", "c3_farmfirst", "THE FARM FIRST, AND WE SAY SO.", "THE FARM FIRST.")
                .Pays(r => r.items.Add((C3Posted, 1)));
            Step(q, "d_clinic", "DELIVER THE CLINIC'S SHARE", "c3_clinic").Says("c3_doctor", "c3_give", "YOUR SHARE, AS POSTED.",
                "AS POSTED. ...AND IT ADDS UP, WHICH IS MORE THAN THE GUILD'S NUMBERS EVER DID. THE STERILISER THANKS YOU. SO DO I, QUIETLY.").Needs(C3Posted).Optional();
            Step(q, "d_farm", "DELIVER BO'S SHARE AT THE VILLAGE", "c3_farm").Says("c3_farmer", "c3_give", "YOUR SHARE, AS POSTED.",
                "AS POSTED, AND I CAN READ IT ON THE BOARD LIKE ANYONE. THE VILLAGE WILL SIGN. HERE'S OUR MARK: TAKE IT TO SERA BEFORE I CHANGE MY MIND. I WON'T.").Needs(C3Posted).Optional()
                .Pays(r => r.items.Add((C3Mark, 1)));
            Step(q, "d_lamps", "DELIVER THE LAMPS' SHARE", "c3_generator").Says("c3_warden", "c3_give", "YOUR SHARE, AS POSTED.",
                "AS POSTED. I'LL CHALK IT ON THE GENERATOR SO NOBODY ASKS ME TWICE. THEY WILL ASK ME TWICE.").Needs(C3Posted).Optional();
            Step(q, "deliver", "DELIVER EACH SHARE AS POSTED: THE CLINIC, BO'S PUMP, THE LAMPS", "c3_farm").When(Goal.Steps, "d_clinic,d_farm,d_lamps", 3f);
            Step(q, "sign", "BRING THE VILLAGE'S MARK TO SERA: THE TOWN SIGNS TOO", "town1").Says("sera", "c3_sign", "THE VILLAGE HAS PUT ITS MARK ON THE AGREEMENT. WILL THE TOWN?",
                "THE TOWN SIGNS BECAUSE THE TOWN SAW IT DONE. PROVISIONAL, MIND: GOOD FOR A SEASON, THEN WE SEE IF IT HELD. KEEP A COPY. SOMEBODY WILL ASK TO SEE IT.").Needs(C3Mark)
                .Pays(r => { r.take.Add((C3Mark, 1)); r.take.Add((C3Posted, 1)); r.items.Add((C3Agreement, 1)); });
            Step(q, "settled", "THE AGREEMENT GOES UP ON THE BOARD IN THE SQUARE", "c3_generator").When(Goal.Event, "c3:settled");
            q.reward.scrap = 30; q.reward.training.Add((Skill.Speech, 6f)); q.reward.training.Add((Skill.Mechanics, 3f)); q.reward.flag = "c3_done";
            q.payoff = "THE SHIPMENT WAS SHARED IN PUBLIC AND IT ADDED UP. THE VILLAGE AND THE TOWN SIGNED A PROVISIONAL PASSAGE AGREEMENT.";
        }

        /// <summary>C3's customers as they stand now (needs drop when their waste is mended).</summary>
        public static List<Allocation.Customer> C3Customers() => new List<Allocation.Customer>
        {
            new Allocation.Customer("clinic", "CLINIC", 6, 4, true),
            new Allocation.Customer("farm", "BO'S PUMP", 5, Story.StepDone("C3", "fix_farm") ? 3 : 5),
            new Allocation.Customer("lamps", "LAMPS", 5, Story.StepDone("C3", "fix_lamps") ? 2 : 4),
        };

        /// <summary>The plan a plan-topic label stands for.</summary>
        public static Allocation.Plan C3PlanOf(string label, out int favoured)
        {
            favoured = -1;
            if (label == null) return Allocation.Plan.ByNeed;
            if (label.StartsWith("EVEN")) return Allocation.Plan.Even;
            if (label.StartsWith("THE CLINIC")) { favoured = 0; return Allocation.Plan.Favour; }
            if (label.StartsWith("THE FARM")) { favoured = 1; return Allocation.Plan.Favour; }
            return Allocation.Plan.ByNeed;
        }

        /// <summary>What Sera says to each plan topic: the numbers it would post today.</summary>
        public static string C3PlanReply(string label)
        {
            var cs = C3Customers();
            var plan = C3PlanOf(label, out int fav);
            var sh = Allocation.Shares(cs, C3Supply, plan, fav);
            string head = plan == Allocation.Plan.Even ? "FOUR EACH, AND NOBODY CAN SAY THEY WERE CHEATED, ONLY THAT THEY WERE COUNTED THE SAME."
                : plan == Allocation.Plan.Favour ? (fav == 0 ? "THE CLINIC FIRST, IN WRITING. PEOPLE CAN ARGUE WITH IT, BUT THEY CAN'T SAY IT WAS HIDDEN."
                                                             : "THE FARM FIRST, IN WRITING: FOOD IS MEDICINE TOO, SLOWER. SOMEONE WILL SAY SO LOUDLY.")
                : "BY NEED: WHAT EACH ONE SHOWED YOU, ESSENTIAL FIRST, THE REST IN PROPORTION.";
            return head + " POSTED: " + Allocation.Posted(cs, sh, C3Supply, "CANS") + ". HERE'S YOUR COPY.";
        }

        /// <summary>C3's closing line from the posted plan, what was mended and what was handed over.</summary>
        public static string C3Payoff()
        {
            string plan = Story.Route("C3", "plan");
            string how = plan == null ? "SHARED" : plan.StartsWith("BY NEED") ? "BY NEED" : plan.StartsWith("EVEN") ? "IN EVEN THIRDS" : plan.StartsWith("THE CLINIC") ? "CLINIC FIRST, OPENLY" : "FARM FIRST, OPENLY";
            string given = "CLINIC " + ArcCRecord.Get("c3:given:clinic") + ", BO'S PUMP " + ArcCRecord.Get("c3:given:farm") + ", LAMPS " + ArcCRecord.Get("c3:given:lamps");
            int reserve = ArcCRecord.Get("c3:reserve");
            var fixes = new List<string>();
            if (Story.StepDone("C3", "fix_farm")) fixes.Add("BO'S PUMP STOPPED WEEPING");
            if (Story.StepDone("C3", "fix_lamps")) fixes.Add(Story.Route("C3", "fix_lamps") == "OIL LAMPS INSTEAD" ? "THE SQUARE BURNS OIL LAMPS" : "THE LAMPS RUN ON A TIMER");
            return "TWELVE CANS WENT " + how + ": " + given + (reserve > 0 ? ", " + reserve + " HELD FOR THE CONVOY" : "") + "."
                   + (fixes.Count > 0 ? " " + string.Join(" AND ", fixes) + "." : "")
                   + " THE VILLAGE AND THE TOWN SIGNED A PROVISIONAL PASSAGE AGREEMENT: PRODUCE IN THE SQUARE, A CLINIC BED FOR ANYONE.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_C3(List<Member> into)
        {
            into.Add(new Member { key = "c3_doctor", name = "DR. HESTER VANE", title = "CLINIC DOCTOR", anchor = "c3_clinic", temper = Temper.Gruff, female = true,
                                  outfit = new[] { "coat", "pants", "boots", "gloves" } });
            into.Add(new Member { key = "c3_farmer", name = "BO TILLMAN", title = "FARMER", anchor = "c3_farm", temper = Temper.Joker,
                                  outfit = new[] { "overalls", "boots", "sunhat" }, tool = "tool_hoe" });
            into.Add(new Member { key = "c3_warden", name = "SAM ORTEGA", title = "LAMPLIGHTER", anchor = "c3_generator", temper = Temper.Nervous,
                                  outfit = new[] { "jacket", "pants", "boots", "beanie" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>C3: the clinic at a building in the first town that no other chapter uses, the public generator on open
        /// ground in its square, Bo Tillman's pump just outside the village.</summary>
        static partial void Anchors_C3(WorldGen world, Settlement town)
        {
            ArcC.Bind(world, town);
            var tc = ArcC.market;
            bool clinicSet = false, genSet = false;
            var blds = new List<(string id, Vector2 pos, float yaw)>();
            if (town != null) BiomeProps.Buildings(world, town, blds);
            foreach (var bl in blds)
            {
                if (clinicSet) break;
                if (!(bl.id.StartsWith("BrickHouse") || bl.id.StartsWith("Shop") || bl.id.StartsWith("Farmhouse"))) continue;
                var f = Quaternion.Euler(0f, bl.yaw, 0f) * Vector3.forward;
                var front = new Vector3(bl.pos.x, 0f, bl.pos.y) + f * 6.5f;
                if (!ArcC.Free(front, 12f) || !float.IsNaN(world.Sample(front.x, front.z).water)) continue;
                ArcC.Put(world, "c3_clinic", front, bl.yaw + 180f);
                clinicSet = true;
            }
            // the square: open ground 9-30 m from the centre, off the street, clear of houses and other places
            for (float r = 9f; r <= 30f && !genSet; r += 3f)
                for (int k = 0; k < 16 && !genSet; k++)
                {
                    float a = (k * 22.5f + 11f) * Mathf.Deg2Rad;
                    var p = tc + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                    var s = world.Sample(p.x, p.z);
                    if (s.roadDist < 3.5f || !float.IsNaN(s.water) || s.feature != 0 || !ArcC.Free(p, 10f) || !Level(world, p, 3f, 0.8f)) continue;
                    bool clear = true;
                    foreach (var bl in blds) if (Vector2.Distance(bl.pos, new Vector2(p.x, p.z)) < 9.5f) { clear = false; break; }
                    if (!clear) continue;
                    ArcC.Put(world, "c3_generator", p, ArcC.Yaw(tc - p));
                    genSet = true;
                }
            if (!clinicSet) ArcC.Put(world, "c3_clinic", tc + new Vector3(-12f, 0f, 9f), 135f);
            if (!genSet) ArcC.Put(world, "c3_generator", tc + new Vector3(10f, 0f, -11f), -45f);

            // Bo's pump: open, level ground just outside the village
            var vc = ArcC.villageAt; float vr = ArcC.village != null ? ArcC.village.radius : 30f;
            bool farmSet = false;
            for (float r = vr + 18f; r <= vr + 110f && !farmSet; r += 12f)
                for (int k = 0; k < 18 && !farmSet; k++)
                {
                    float a = (k * 20f + 7f) * Mathf.Deg2Rad;
                    var p = vc + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                    if (!ArcC.OffRoad(world, p, 9f) || !Level(world, p, 7f, 1.2f) || !ArcC.Free(p, 30f)) continue;
                    ArcC.Put(world, "c3_farm", p, ArcC.Yaw(vc - p));
                    farmSet = true;
                }
            if (!farmSet)
            {
                var road = ArcC.road;
                var p = ArcC.Beside(world, road, ArcC.Length(road) - 45f, 16f, q => ArcC.Free(q, 20f), out float ff, -ArcC.trackSide);
                ArcC.Put(world, "c3_farm", p, ff);
            }
            Clearing("c3_farm", 12f);
        }
    }
}
