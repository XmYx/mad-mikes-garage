using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S12 THE DOG AT PLATFORM THREE: Etta Pike feeds a dog who waits at the abandoned station. Watch her routine a while
    // (she trots to the platform end whenever the signal wire rattles, and favours her left forepaw), win her over with
    // meat, jerky or a bone, treat the paw (a bandage, a splint or honey salve), then read her tag: W. OBER. Etta knows
    // the name, and so does the station's notice board: Walt Ober, the old signalman, moved in with his niece. At his
    // porch he explains why she was left behind. Return her (lead her to his porch), share her (her platform, Etta's
    // scraps, Sundays at Walt's), or, only if he agrees when asked, adopt her. The adoption route alone ends with a dog;
    // the others pay in kind.
    public static partial class StoryLibrary
    {
        static partial void Author_S12(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_penny_leash", "PENNY'S OLD LEASH (WALT'S BLESSING)");
            StoryAnchors.Q5Declare("etta", "s12_board", "s12_home");
            q.offerSay = "WHOSE DOG IS THAT?";
            q.offerReply = "NOBODY'S, APPARENTLY. SHE'S BEEN ON THAT PLATFORM SINCE THE LAST TRAIN, WHICH WAS BEFORE MOST THINGS. I BRING HER SCRAPS. SHE TAKES THEM AND GOES BACK TO WAITING. " +
                           "SHE'S LIMPING NOW, AND SHE WON'T LET ME NEAR THE PAW. YOU'VE GOT PATIENCE ON YOU. SIT WITH HER A WHILE?";
            q.hook = "ETTA PIKE FEEDS A DOG THAT WAITS AT THE ABANDONED STATION. THE DOG IS LIMPING.";

            Step(q, "watch", "WATCH THE DOG'S ROUTINE AT PLATFORM THREE: STAY NEAR HER A WHILE, QUIETLY", "etta").When(Goal.Event, "s12:watched")
                .Pays(r => r.training.Add((Skill.Survival, 2f)));
            Step(q, "feed", "WIN HER OVER: OFFER MEAT, JERKY OR A BONE ([E] ON THE DOG)", "etta").When(Goal.Event, "s12:fed");
            Step(q, "paw", "SHE FAVOURS HER LEFT FOREPAW: TREAT IT WITH A BANDAGE, A SPLINT OR HONEY SALVE ([E] ON HER)", "etta").When(Goal.Event, "s12:treated")
                .Pays(r => r.training.Add((Skill.Survival, 4f)));
            Step(q, "clue", "HER TAG SAYS 'PENNY. W. OBER, PLATFORM 3'. FIND OUT WHO THAT IS: ASK ETTA, OR READ THE STATION'S NOTICE BOARD", "s12_board")
                .Says("etta", "s12_who", "HER TAG SAYS W. OBER. WHO'S THAT?",
                    "OBER? WALT OBER, THE OLD SIGNALMAN. THIS WAS HIS STATION. HIS HIP WENT AND HE MOVED IN WITH HIS NIECE UP THE ROAD. I NEVER KNEW THE DOG WAS HIS. I THOUGHT SHE WAS JUST... HERE.")
                .When(Goal.Reach, "s12_board", 2.8f, "READ THE NOTICE BOARD");
            Step(q, "visit", "FIND WALT OBER AT HIS NIECE'S PLACE UP THE ROAD", "s12_home").When(Goal.Reach, "s12_home", 9f);
            Step(q, "walt", "TELL WALT ABOUT PENNY", "s12_home")
                .Says("s12_walt", "s12_waiting", "YOUR DOG IS STILL WAITING AT PLATFORM THREE.",
                    "PENNY? ...I TOLD THEM TO TAKE HER. THE CART WAS FULL AND SHE WOULDN'T GET IN, SHE KEPT GOING BACK TO THE PLATFORM, AND MY HIP... " +
                    "I THOUGHT SOMEONE WOULD TAKE HER IN. SHE WAITED. OF COURSE SHE WAITED. SHE WAITED FOR EVERY TRAIN FOR NINE YEARS.");
            Step(q, "ask", "ASK WALT WHETHER HE CAN STILL KEEP HER", "s12_home")
                .Says("s12_walt", "s12_keep", "CAN YOU STILL LOOK AFTER HER?",
                    "HONESTLY? MY NIECE SAYS NO DOGS INSIDE, AND I CAN'T WALK HER, NOT PROPERLY. IF SHE'D GO WITH YOU, YOU'D HAVE MY BLESSING. HERE. HER LEASH. SHE'LL KNOW WHAT IT MEANS.")
                .Optional().Pays(r => r.items.Add(("story_penny_leash", 1)));
            Step(q, "decide", "DECIDE WITH WALT WHERE PENNY LIVES NOW", "s12_home")
                .Says("s12_walt", "s12_return", "SHE'S COMING HOME TO YOU. I'LL WALK HER OVER.",
                    "...I'LL PUT THE KETTLE ON. SHE LIKES THE WARM SPOT BY THE STOVE. MY NIECE WILL JUST HAVE TO LIKE IT TOO.")
                .Says("s12_walt", "s12_share", "SHE KEEPS HER PLATFORM AND ETTA'S SCRAPS, AND I WALK HER OVER TO YOU ON SUNDAYS.",
                    "SHARED CUSTODY OF A DOG. THE WORLD REALLY HAS ENDED. ...YES. YES, THAT'S GOOD. TELL ETTA I OWE HER A LOT OF SCRAPS.")
                .Says("s12_walt", "s12_adopt", "THEN SHE COMES WITH ME. I'LL BRING HER BY.",
                    "LOOK AFTER HER. SHE SNORES, SHE STEALS THE BLANKET, AND SHE'LL WAIT FOR YOU AT EVERY DOOR YOU GO THROUGH. THAT'S THE DEAL.").Needs("story_penny_leash");
            Step(q, "settle", "SEE PENNY SETTLED: IF SHE'S GOING HOME, FETCH HER FROM THE STATION AND WALK HER TO WALT'S PORCH (SHE FOLLOWS YOU ON FOOT)", "s12_home")
                .When(Goal.Event, "s12:home", label: "PENNY LIVES WITH WALT")
                .When(Goal.Event, "s12:shared", label: "PENNY SPLITS HER DAYS")
                .When(Goal.Event, "s12:adopted", label: "PENNY GOES WITH YOU");
            // what each ending hands over (the dog herself only on the adoption route)
            Step(q, "r_home", "WALT'S OLD SIGNAL LANTERN", "s12_home").When(Goal.Event, "s12:home").Optional()
                .Pays(r => { r.items.Add(("tool_lantern", 1)); r.scrap = 15; });
            Step(q, "r_shared", "ETTA'S STANDING ORDER OF STEW", "etta").When(Goal.Event, "s12:shared").Optional()
                .Pays(r => { r.items.Add(("food_stew", 3)); r.scrap = 10; });
            Step(q, "r_adopted", "A PAPER BAG OF BONES FOR PENNY", "s12_home").When(Goal.Event, "s12:adopted").Optional()
                .Pays(r => r.items.Add(("misc_bone", 3)));
            q.reward.items.Add(("food_can", 2)); q.reward.items.Add(("med_bandage", 1));
            q.reward.training.Add((Skill.Survival, 6f)); q.reward.flag = "penny_settled";
            q.payoff = "PENNY STOPPED WAITING AT PLATFORM THREE.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S12(List<Member> into)
        {
            into.Add(new Member { key = "etta", name = "ETTA PIKE", title = "COOK", anchor = "etta", temper = Temper.Friendly, female = true, outfit = new[] { "sweater", "pants", "boots", "beanie" } });
            into.Add(new Member { key = "s12_walt", name = "WALT OBER", title = "RETIRED SIGNALMAN", anchor = "s12_home", temper = Temper.Gruff, outfit = new[] { "coat", "pants", "boots", "cowboy" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>The abandoned station by a road out of the first town (a platform along an old track), its notice
        /// board, and Walt's niece's porch 350-900 m further along the roads.</summary>
        static partial void Anchors_S12(WorldGen world, Settlement town)
        {
            var tc = Q5Centre(world, town);
            float r0 = town != null ? town.radius : 40f;
            var st = Q5Road(world, town, "etta", tc, r0 + 160f, r0 + 700f, 60f, 110f);
            Q5Beside(world, "s12_board", "etta", new Vector3(-6.5f, 0f, 1.5f));
            Q5Road(world, town, "s12_home", st, 350f, 900f, 80f, 60f);
            Clearing("etta", 14f);
            Clearing("s12_home", 8f);
        }
    }
}
