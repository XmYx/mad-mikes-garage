using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S02 THE GOAT HAS A LAWYER (storyline §10): Bess Rill's goat Horace broke out; follow the chewed beds to him, lure
    // him home with feed he likes, mend the gap in the pen, find out where the missing rail went (Oswin's fire) and settle
    // who pays. No animal has to be hurt; the settlement is remembered.
    public static partial class StoryLibrary
    {
        internal const string S02Even = "OSWIN, THAT'S BESS'S FENCE RAIL IN YOUR FIRE. HER GOAT ATE YOUR SHIRTS. I'D CALL THAT EVEN.";
        internal const string S02Oswin = "YOU BURNED THE RAIL THAT KEPT HORACE IN. THE SHIRTS ARE ON YOU, AND SO IS A NEW RAIL.";
        internal const string S02Bess = "HORACE ATE THE SHIRTS. BESS WILL PAY FOR THEM.";
        internal const string S02Me = "I'LL COVER THE SHIRTS MYSELF. NOBODY NEEDS A FEUD OVER A GOAT.";
        const string S02Default = "HORACE IS HOME AND THE PEN IS SHUT. THE TOWN WILL BE ARGUING ABOUT THE SHIRTS FOR YEARS.";

        /// <summary>The closing journal line for how the dispute was settled (null route: the plain one).</summary>
        internal static string S02_Payoff(string route, bool lost)
        {
            if (lost) return "HORACE DIDN'T MAKE IT HOME. BESS KEPT HIS BELL, AND OSWIN, OF ALL PEOPLE, BUILT THE NEW FENCE.";
            if (route == S02Even) return "HORACE IS HOME. THE SHIRTS AND THE RAIL CANCELLED OUT, AND BESS AND OSWIN ARE STILL ARGUING ABOUT WHO WON.";
            if (route == S02Oswin) return "OSWIN PAID FOR THE FENCE HE BURNED. BESS TELLS EVERYONE THAT HORACE WAS ACQUITTED ON ALL COUNTS.";
            if (route == S02Bess) return "BESS PAID FOR THE SHIRTS, UNDER PROTEST. HORACE MAINTAINS HIS INNOCENCE. NOBODY ASKED ABOUT THE RAIL.";
            if (route == S02Me) return "YOU PAID FOR THE SHIRTS. BESS AND OSWIN AGREE ON ONE THING AT LAST: YOU'RE A SOFT TOUCH.";
            return S02Default;
        }

        static partial void Author_S02(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_s02_rail", "HALF-BURNT FENCE RAIL");
            foreach (var k in new[] { "bess", "s02_pen", "s02_gap", "s02_trail1", "s02_trail2", "s02_goat", "oswin", "s02_fire" })
                if (!Anchors.Contains(k)) Anchors.Add(k);
            q.offerSay = "YOU LOOK LIKE YOU'VE LOST SOMETHING.";
            q.offerReply = "A GOAT. HORACE. HE WENT THROUGH MY FENCE IN THE NIGHT, AND OSWIN NEXT DOOR SAYS HE ATE A LINE OF WASHING ON THE WAY OUT. " +
                           "HORACE DENIES EVERYTHING AND I'M REPRESENTING HIM. FIND HIM BEFORE OSWIN DOES, WOULD YOU?";
            q.hook = "BESS RILL'S GOAT HORACE BROKE OUT OF HIS PEN. THE NEIGHBOUR SAYS HE ATE THE WASHING. BESS IS HANDLING HIS DEFENCE.";
            Step(q, "pen", "LOOK AT HORACE'S PEN", "s02_pen").When(Goal.Reach, "s02_pen", 4.5f)
                .Pays(r => { r.items.Add(("food_cabbage", 2)); r.resources.Add((ResourceType.Wood, 4)); });
            Step(q, "chew1", "A CHEWED CABBAGE BED AND HOOF PRINTS: HORACE WENT THIS WAY", "s02_trail1").When(Goal.Reach, "s02_trail1", 4f).Optional();
            Step(q, "chew2", "MORE CHEWING. THE PRINTS HEAD ON, AWAY FROM TOWN", "s02_trail2").When(Goal.Reach, "s02_trail2", 4f).Optional();
            Step(q, "find", "TRACK HORACE BY THE CHEWED PLANTS FROM THE PEN", "s02_trail1").When(Goal.Reach, "s02_goat", 9f);
            Step(q, "lure", "LURE HORACE HOME: CARRY FEED HE LIKES (CABBAGE, CARROT, APPLE, WHEAT OR BEET), WALK BACK AND STEP INTO HIS PEN", "s02_pen")
                .When(Goal.Event, "s02:home", label: "HORACE WALKED HOME")
                .When(Goal.Event, "s02:lost", label: "HORACE DIDN'T MAKE IT");
            Step(q, "mend", "MEND THE PEN: A FENCE OR A GATE IN THE GAP ([B])", "s02_gap").When(Goal.Build, "fence_wood|fence_wire|gate", 2f)
                .Pays(r => r.training.Add((Skill.Construction, 3f)));
            Step(q, "rail", "OSWIN'S FIRE NEXT DOOR IS STILL SMOKING. THERE'S A HALF-BURNT FENCE RAIL IN IT", "s02_fire").When(Goal.Reach, "s02_fire", 3f).Optional()
                .Pays(r => r.items.Add(("story_s02_rail", 1)));
            Step(q, "settle", "WHO PAYS FOR WHAT? SETTLE IT WITH OSWIN (A LOOK AROUND HIS YARD MIGHT HELP)", "oswin")
                .Says("oswin", "s02_even", S02Even, "IT WAS LYING THERE. ATTACHED TO A FENCE, BUT LYING THERE. ...FINE. EVEN. TELL HER HORACE STILL OWES ME AN APOLOGY.").Needs("story_s02_rail")
                .Says("oswin", "s02_oswin", S02Oswin, "...I'LL SPLIT HER A RAIL. AND I'M BUYING A GOAT OF MY OWN. OUT OF SPITE.").Needs("story_s02_rail")
                .Says("oswin", "s02_bess", S02Bess, "THANK YOU. THE FIRST PERSON IN THIS TOWN WITH ANY SENSE. SHE'LL SAY THEY WERE UGLY SHIRTS. THEY WERE NOT.")
                .Says("oswin", "s02_me", S02Me, "...WELL. THAT'S DECENT. HERE, TAKE THE ONE HE DIDN'T EAT. IT'S A SCARF NOW.");
            q.steps[q.steps.Count - 1].any[3].price = 8;
            Step(q, "tell", "TELL BESS IT'S SETTLED", "bess").Says("bess", "s02_tell", "IT'S SETTLED. HORACE'S PEN IS SHUT AND OSWIN'S SQUARED.",
                "THE DEFENCE RESTS. HERE: MILK, CHEESE, AND SOME ADVICE FOR NOTHING. A GOAT DOESN'T BREAK A FENCE, HE LEANS ON IT UNTIL IT GIVES. BUILD FOR LEANING.");
            q.reward.items.Add(("drink_milk", 2)); q.reward.items.Add(("food_cheese", 2)); q.reward.items.Add(("crop_wheat", 4));
            q.reward.training.Add((Skill.Farming, 8f)); q.reward.flag = "s02_done";
            q.payoff = S02Default;
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S02(List<Member> into)
        {
            into.Add(new Member { key = "bess", name = "BESS RILL", title = "GOATKEEPER", anchor = "bess", temper = Temper.Proud, female = true, outfit = new[] { "overalls", "boots", "sunhat", "scarf" } });
            into.Add(new Member { key = "oswin", name = "OSWIN TULL", title = "NEIGHBOUR", anchor = "oswin", temper = Temper.Gruff, outfit = new[] { "sweater", "pants", "boots", "beanie" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S02: Bess's pen at the edge of the first town, a trail of chewed beds leading out to where Horace grazes,
        /// and Oswin's yard next door with his fire.</summary>
        static partial void Anchors_S02(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tc = new Vector3(town.pos.x, 0f, town.pos.y);
            void Put(string k, Vector3 p, float y) { p.y = world.Sample(p.x, p.z).height; at[k] = p; yaw[k] = y; }
            bool Spare(Vector3 p, float d)
            {
                foreach (var kv in at) if (new Vector2(kv.Value.x - p.x, kv.Value.z - p.z).magnitude < d) return false;
                return true;
            }
            Vector3 pen = Vector3.zero, o = Vector3.forward;
            bool ok = false;
            for (int pass = 0; pass < 3 && !ok; pass++)
                for (float rr = town.radius + 20f; rr <= town.radius + 170f && !ok; rr += 10f)
                    for (int k = 0; k < 36 && !ok; k++)
                    {
                        float ang = (k * 10f + 205f) * Mathf.Deg2Rad;
                        var od = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                        var sd = new Vector3(od.z, 0f, -od.x);
                        var p = tc + od * rr;
                        if (!Open(world, p) || !Spare(p, pass < 2 ? 45f : 25f)) continue;
                        if (pass < 2 && !Level(world, p, 5f, pass == 0 ? 1.2f : 2f)) continue;
                        if (pass < 2 && (!Open(world, p + od * 16f + sd * 5f) || !Open(world, p + od * 30f - sd * 4f) || !Open(world, p + od * 46f + sd * 2f)
                                         || !Open(world, p + sd * 22f + od * 2f) || !Open(world, p + sd * 22f + od * 5.5f) || !Open(world, p - sd * 6f)
                                         || !Spare(p + od * 46f, 30f) || !Spare(p + sd * 22f, 30f))) continue;
                        pen = p; o = od; ok = true;
                    }
            if (!ok) { o = new Vector3(Mathf.Sin(205f * Mathf.Deg2Rad), 0f, Mathf.Cos(205f * Mathf.Deg2Rad)); pen = tc + o * (town.radius + 60f); }
            var s = new Vector3(o.z, 0f, -o.x);
            float outYaw = Mathf.Atan2(o.x, o.z) * Mathf.Rad2Deg;
            Put("s02_pen", pen, outYaw);                                                       // local +Z points away from town
            Put("s02_gap", pen + o * 2f + s * 1f, outYaw);                                    // the missing fence piece, outer side
            Put("bess", pen - s * 6f, outYaw + 180f);                                         // beside the pen, facing town
            Put("s02_trail1", pen + o * 16f + s * 5f, outYaw);
            Put("s02_trail2", pen + o * 30f - s * 4f, outYaw);
            Put("s02_goat", pen + o * 46f + s * 2f, outYaw);
            Put("oswin", pen + s * 22f + o * 2f, outYaw + 180f);
            Put("s02_fire", pen + s * 22f + o * 5.5f, outYaw);
            Clearing("s02_pen", 7f); Clearing("bess", 5f); Clearing("s02_trail1", 4f); Clearing("s02_trail2", 4f);
            Clearing("s02_goat", 10f); Clearing("oswin", 6f); Clearing("s02_fire", 4f);
        }
    }
}
