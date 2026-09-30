using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S16 RUST IN PEACE (storyline §10): Mo Danner has a council contract to strip an "abandoned" bus. Behind it: bedding,
    // a child's drawing, last night's ashes. Hanne Varga's family sleeps in it. Buy the salvage rights, get it running in
    // exchange for the spares, or strip it anyway and let the town hear about it. The game watches the bus itself: the
    // salvage cutter or a wrench taking it apart, or its engine catching.
    public static partial class StoryLibrary
    {
        public const string S16Bill = "story_bill_of_sale", S16Note = "story_varga_note";

        static partial void Author_S16(QuestDef q)
        {
            ItemIds.Register(S16Bill, "BILL OF SALE: ONE BUS (H. VARGA)");
            ItemIds.Register(S16Note, "HANNE'S NOTE: THE BUS STAYS, THE SPARES ARE YOURS");
            foreach (var k in new[] { "mo", "s16_bus", "s16_camp", "s16_hanne" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "BUSY DAY IN THE SCRAP TRADE?";
            q.offerReply = "THE COUNCIL WANTS THE OLD BUS OFF THE VERGE OUT OF TOWN. ABANDONED, THEY SAY. I'VE THE CONTRACT AND NO SPARE HANDS. " +
                           "LOOK IT OVER, TELL ME WHAT'S WORTH TAKING, AND WHAT COMES OFF IT IS YOURS TO KEEP. I JUST NEED IT GONE.";
            q.hook = "MO DANNER HAS A COUNCIL CONTRACT TO CLEAR AN 'ABANDONED' BUS FROM THE VERGE OUT OF TOWN.";

            Step(q, "look", "LOOK THE BUS OVER FOR MO: WHAT'S WORTH TAKING?", "s16_bus").When(Goal.Reach, "s16_bus", 9f)
                .Pays(r => r.training.Add((Skill.Salvaging, 3f)));
            Step(q, "signs", "GOOD TYRES, A TIRED ENGINE, SEATS... AND A SMELL OF WOODSMOKE. LOOK BEHIND THE BUS", "s16_camp").When(Goal.Reach, "s16_camp", 4f);
            Step(q, "owners", "BEDDING, A CHILD'S DRAWING, LAST NIGHT'S ASHES: SOMEONE LIVES HERE. FIND THEM (TRACKS LEAD OFF THE ROAD)", "s16_hanne")
                .Says("s16_hanne", "s16_yours", "IS THAT BUS YOURS?",
                      "IT WAS MY FATHER'S. HE DROVE THE SCHOOL ROUTE UNTIL THERE WASN'T A SCHOOL. THE ENGINE QUIT TWO WINTERS AGO; " +
                      "THE KIDS AND I SLEEP IN IT WHEN THE WIND COMES. NOBODY ON THE COUNCIL EVER ASKED.")
                .When(Goal.Event, "s16:taken", label: "NEVER ASKED");
            Step(q, "deal", "HANNE VARGA'S FAMILY SLEEPS IN THAT BUS. MAKE A DEAL WITH HER, OR DON'T", "s16_hanne")
                .Says("s16_hanne", "s16_buy", "I'LL BUY THE SALVAGE RIGHTS. THIRTY SCRAP, WRITTEN DOWN.",
                      "THIRTY IS A WINTER OF FLOUR. ...ALL RIGHT. WE'LL GO TO MY SISTER'S IN TOWN. LEAVE DAD'S SEAT, THE ONE WITH THE BURN MARK. I'LL SIGN.")
                .Says("s16_hanne", "s16_fix", "LET ME GET IT RUNNING AGAIN. YOU KEEP THE BUS; I KEEP THE SPARES.",
                      "RUNNING? IT HASN'T COUGHED IN TWO WINTERS. ...THE BATTERY LEAD'S LOOSE AND THE TANK'S DRY, THAT MUCH I KNOW. " +
                      "GET IT GOING AND THE SPARES IN THE BACK ARE YOURS.")
                .When(Goal.Event, "s16:taken", label: "TOOK IT ANYWAY");
            q.steps[q.steps.Count - 1].any[0].price = 30;
            Step(q, "sale", "THE BILL OF SALE", "s16_hanne").When(Goal.Event, "s16:sold").Optional()
                .Pays(r => r.items.Add((S16Bill, 1)));
            Step(q, "work", "DO WHAT YOU SETTLED: STRIP THE BUS (SALVAGE CUTTER, OR PARTS OFF WITH A WRENCH), OR GET ITS ENGINE RUNNING ([G] WITH A WRENCH FOR THE LEAD, FUEL, A REPAIR KIT)", "s16_bus")
                .When(Goal.Event, "s16:stripped", label: "STRIPPED IT")
                .When(Goal.Event, "s16:running", label: "GOT IT RUNNING");
            Step(q, "spares", "THE SPARES IN THE BACK OF THE BUS", "s16_bus").When(Goal.Event, "s16:spares").Optional()
                .Pays(r =>
                {
                    r.items.Add((S16Note, 1)); r.items.Add(("use_repair_kit", 1)); r.items.Add(("use_battery", 1));
                    r.resources.Add((ResourceType.Rubber, 4)); r.resources.Add((ResourceType.Scrap, 10)); r.training.Add((Skill.Mechanics, 5f)); r.flag = "s16_stop";
                });
            Step(q, "report", "TELL MO HOW THE BUS WENT", "mo")
                .Says("mo", "s16_paid", "HERE'S THE BILL OF SALE. THE VARGAS WERE PAID.",
                      "PAID. HUH. THE COUNCIL WOULDN'T HAVE. I'LL FILE IT WITH THE CONTRACT SO NOBODY CAN SAY OTHERWISE. HERE'S A LITTLE FOR THE TROUBLE.").Needs(S16Bill)
                .Says("mo", "s16_home", "THE BUS RUNS. IT'S A HOME, NOT SCRAP. HANNE WROTE IT DOWN.",
                      "A HOME. ...I'LL TELL THE COUNCIL IT DROVE ITSELF OFF THEIR LIST. NO CONTRACT, BUT I OWE YOU A DRINK, AND I PAY MY DRINKS.").Needs(S16Note)
                .Says("mo", "s16_took", "IT'S STRIPPED. NOBODY SIGNED ANYTHING.",
                      "THEN WE BOTH KNOW WHAT WE DID, AND SO WILL THE TOWN BY SUPPER. CONTRACT'S DONE. DON'T SPEND YOUR SHARE NEAR THE VARGAS.")
                .Pays(r => r.take.Add((S16Bill + "|" + S16Note, 1)));
            q.reward.scrap = 10; q.reward.training.Add((Skill.Salvaging, 6f)); q.reward.flag = "s16_done";
            q.payoff = "THE OLD BUS IS OFF MO'S LIST.";
        }

        /// <summary>Did the player choose to repair the bus for the family?</summary>
        public static bool S16Promised => Story.Route("S16", "deal") != null && Story.Route("S16", "deal").StartsWith("LET ME GET IT RUNNING");
        public static bool S16Bought => Story.Route("S16", "deal") != null && Story.Route("S16", "deal").StartsWith("I'LL BUY");

        /// <summary>S16's closing journal line from the routes taken.</summary>
        public static string S16Payoff()
        {
            string work = Story.Route("S16", "work");
            if (Story.Flag("s16_trust"))
                return "THE VARGAS' BUS WENT FOR SCRAP AND THE FAMILY WENT WITHOUT IT. THE TOWN HEARD; PEOPLE THERE ARE COOLER WITH YOU NOW.";
            if (work == "GOT IT RUNNING")
                return "THE VARGAS' BUS RUNS AGAIN. HANNE SAYS YOUR CAR CAN STAND BESIDE IT ANY NIGHT: A SAFE STOP ON THAT ROAD.";
            if (S16Bought)
                return "THE VARGAS WENT TO HANNE'S SISTER'S WITH THIRTY SCRAP AND THEIR FATHER'S SEAT. THE BUS WENT FOR PARTS, FAIRLY BOUGHT.";
            return "THE OLD BUS IS OFF MO'S LIST.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S16(List<Member> into)
        {
            into.Add(new Member { key = "mo", name = "MO DANNER", title = "SCRAP DEALER", anchor = "mo", temper = Temper.Greedy,
                                  outfit = new[] { "vest", "jeans", "boots", "welding_mask", "gloves" }, tool = "tool_cutter" });
            // the family moves on if their bus is taken from them
            into.Add(new Member { key = "s16_hanne", name = "HANNE VARGA", title = "BUS OWNER", anchor = "s16_hanne", temper = Temper.Nervous, female = true,
                                  outfit = new[] { "hoodie", "jeans", "boots", "scarf" },
                                  present = () => Story.StateOf("S16") != Story.State.Locked && !Story.Flag("s16_trust") });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S16: Mo's cart at the edge of town; the bus on a level verge 300-900 m out, the camp behind it, and
        /// Hanne's shelter 60-130 m off the road.</summary>
        static partial void Anchors_S16(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tc = new Vector3(town.pos.x, 0f, town.pos.y);
            if (!Roadside(world, tc, town.radius + 15f, town.radius + 160f, 16f, p => Clear(town, p, 8f) && Q2.Free(world, p, 35f), out var mo, out var mf)
                && !Q2.Near(world, tc, town.radius + 20f, town.radius + 260f, 35f, 3f, 1.5f, out mo, out mf))
                mo = Q2.Anywhere(world, tc, town.radius + 30f, 290f, out mf);
            Q2.Put(world, "mo", mo, mf);
            Clearing("mo", 8f);

            if (!Roadside(world, tc, town.radius + 300f, town.radius + 900f, 16f,
                    p => Clear(town, p, 150f) && Level(world, p, 7f, 1.1f) && Q2.OpenAround(world, p, 7f) && Q2.Free(world, p, 60f), out var bus, out var bf)
                && !Q2.Near(world, tc, town.radius + 300f, town.radius + 1100f, 60f, 7f, 1.4f, out bus, out bf))
                bus = Q2.Anywhere(world, tc, town.radius + 400f, 120f, out bf);
            Q2.Put(world, "s16_bus", bus, bf + 90f);                                                // parked along the road
            var away = Quaternion.Euler(0f, bf, 0f) * Vector3.back;                                  // the side away from the road
            var camp = bus + away * 6.5f;
            if (!Open(world, camp)) camp = bus + Quaternion.Euler(0f, bf + 90f, 0f) * Vector3.forward * 8f;
            Q2.Put(world, "s16_camp", camp, bf);
            Clearing("s16_bus", 13f);

            Vector3 hanne = Vector3.zero; bool got = false;
            for (float rr = 60f; rr <= 130f && !got; rr += 10f)
                for (int k = 0; k < 16 && !got; k++)
                {
                    float a = bf + 180f + (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * 22.5f;             // away from the road first
                    var p = bus + Quaternion.Euler(0f, a, 0f) * Vector3.forward * rr;
                    if (Open(world, p) && world.Sample(p.x, p.z).roadDist > 25f && Level(world, p, 4f, 1.2f) && Q2.Free(world, p, 30f, "s16_bus", "s16_camp")) { hanne = p; got = true; }
                }
            if (!got) hanne = Q2.Anywhere(world, bus, 70f, bf + 180f, out _);
            var toBus = bus - hanne;
            Q2.Put(world, "s16_hanne", hanne, Mathf.Atan2(toBus.x, toBus.z) * Mathf.Rad2Deg);
            Clearing("s16_hanne", 9f);
        }
    }
}
