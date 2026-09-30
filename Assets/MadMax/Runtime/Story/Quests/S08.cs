using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S08 THE WEDDING AT THE WRONG END OF THE ROAD: Fen Locke, tailor, has the groom's coat, a basket of cloth and a
    // two-piece band, at Bracken Bottom, where nobody is getting married. The invitation says "Bracken, at the end of the
    // road", and the road has two ends. Ask a local (or have Fen read the invitation again, or just find it) for the
    // landmark: the water tower at Bracken Top. Tell the bride's mother where everyone is (the mix-up, resolved), mend
    // the torn coat at Fen's sewing table (or with sewing kits), bring the band up (your passenger seat, on foot beside
    // you, or a carter Fen hires), dress the groom, switch on the radio for the dance, and start the ceremony when you
    // say everyone is ready. No clock runs while you get it together. The ceremony is a performance scene: the band, the
    // vows, the crowd. Formal clothes, cloth, Crafting and Speech, and a dedication on the radio.
    public static partial class StoryLibrary
    {
        internal static bool S08On => Story.StateOf("S08") == Story.State.Active;
        internal static bool S08Here => Story.StateOf("S08") == Story.State.Active || Story.StateOf("S08") == Story.State.Done;

        static partial void Author_S08(QuestDef q)
        {
            q.build = Build.Playable;
            StoryAnchors.Q5Declare("fen", "s08_wedding", "s08_stage", "s08_band1", "s08_band2");
            q.offerSay = "YOU LOOK LIKE YOU'RE WAITING FOR SOMETHING.";
            q.offerReply = "A WEDDING. THIS ONE, SUPPOSEDLY: 'THE KERRY WEDDING, BRACKEN, AT THE END OF THE ROAD.' I HAVE THE GROOM'S COAT, A BASKET OF CLOTH AND TWO MUSICIANS, " +
                           "AND NOBODY HERE IS GETTING MARRIED. NOBODY HERE HAS BEEN MARRIED SINCE THE MINE SHUT, I'M TOLD. CAN YOU FIND OUT WHERE I'M SUPPOSED TO BE?";
            q.hook = "FEN LOCKE, TAILOR, BROUGHT A WEDDING'S CLOTHES AND MUSICIANS TO BRACKEN. NOBODY THERE IS GETTING MARRIED.";

            Step(q, "coat", "FEN HAS THE GROOM'S COAT", "fen")
                .Says("fen", "s08_coat", "LET'S SEE THE GROOM'S COAT.",
                    "HERE. IT CAUGHT ON THE CART WHEEL COMING OVER THE PASS: A SLEEVE HALF OFF AND THE HEM IN RIBBONS. MY HANDS SHAKE TOO MUCH FOR LEATHER TODAY. " +
                    "THE SEWING TABLE'S THERE AND THE CLOTH IS IN THE BASKET. IN BRACKEN A DUSTER IS FORMAL WEAR, APPARENTLY.")
                .Optional().Pays(r => { r.items.Add(("cloth_duster", 1)); r.resources.Add((ResourceType.Cloth, 4)); });
            Step(q, "clue", "NOBODY HERE IS GETTING MARRIED: ASK FOR LANDMARKS (A LOCAL, OR FEN'S INVITATION), OR GO LOOKING", "fen")
                .Says("s08_local", "s08_where", "IS THERE A WEDDING IN BRACKEN TODAY?",
                    "IN BRACKEN BOTTOM? WE HAVEN'T HAD A WEDDING SINCE THE MINE SHUT. YOU WANT BRACKEN TOP: THE OTHER END OF THE ROAD, UNDER THE WATER TOWER. EVERYONE GETS IT WRONG. THE POSTMAN GETS IT WRONG.")
                .Says("fen", "s08_note", "READ ME THE INVITATION AGAIN.",
                    "'THE KERRY WEDDING. BRACKEN, AT THE END OF THE ROAD. LOOK FOR THE WATER TOWER.' ...THIS ROAD HAS TWO ENDS, DOESN'T IT. AND I'M AT THE ONE WITHOUT A WATER TOWER.")
                .When(Goal.Reach, "s08_wedding", 25f, "FOUND IT BY LOOKING");
            Step(q, "right", "FIND THE RIGHT END OF THE ROAD: BRACKEN TOP, UNDER THE WATER TOWER", "s08_wedding").When(Goal.Reach, "s08_wedding", 16f);
            Step(q, "mixup", "TELL THE WEDDING WHERE THEIR CLOTHES AND THEIR BAND ARE", "s08_wedding")
                .Says("s08_host", "s08_mixup", "FEN LOCKE IS AT THE OTHER END OF THE ROAD WITH THE CLOTHES AND THE BAND.",
                    "...BRACKEN BOTTOM. OF COURSE SHE IS. EVERYONE DOES THAT. THE GROOM IS STANDING THERE IN HIS VEST. BRING THEM UP, WOULD YOU? WE'LL WAIT. WE'VE WAITED THIS LONG.");
            Step(q, "mend", "MEND THE GROOM'S COAT: FEN HAS IT; HER SEWING TABLE (MEND CLOTHES), OR WEAR IT AND USE SEWING KITS", "fen").When(Goal.Event, "s08:mended")
                .Pays(r => r.training.Add((Skill.Crafting, 4f)));
            Step(q, "go", "THE BAND WILL COME WITH YOU", "fen")
                .Says("s08_fiddler", "s08_go", "COME ON, BOTH OF YOU. BRACKEN TOP, THE OTHER END OF THE ROAD.",
                    "FINALLY. DOT, THE BOX. MIND THE BOX. IF YOU'VE GOT A SPARE SEAT WE'LL RIDE, IF NOT WE'LL WALK BEHIND YOU AND COMPLAIN.")
                .Optional();
            Step(q, "band", "BRING THE BAND UP TO BRACKEN TOP: A RIDE IN YOUR PASSENGER SEAT, ON FOOT WITH YOU, OR A CARTER FEN CAN HIRE", "fen")
                .When(Goal.Event, "s08:band_rode", label: "RODE IN YOUR PASSENGER SEAT")
                .When(Goal.Event, "s08:band_walked", label: "WALKED UP WITH YOU")
                .Says("fen", "s08_carter", "HIRE A CARTER TO TAKE YOU AND THE BAND UP THE ROAD.", "THERE'S A MAN WITH A MULE WHO OWES ME A HEM. WE'LL BE UP THERE BEFORE YOU, PROBABLY.");
            q.steps[q.steps.Count - 1].any[2].price = 15;
            Step(q, "dress", "DRESS THE GROOM: GIVE TOBIAS HIS MENDED COAT", "s08_wedding")
                .Says("s08_groom", "s08_dress", "YOUR COAT. MENDED.",
                    "...YOU CAN'T EVEN SEE WHERE IT TORE. IF I CRY IT'S THE DUST. IT'S VERY DUSTY. EVERYONE AGREES IT'S DUSTY.").Needs("cloth_duster")
                .Pays(r => r.take.Add(("cloth_duster", 1)));
            Step(q, "radio", "MUSIC FOR THE DANCE AFTERWARDS: SWITCH ON THE RADIO BY THE TABLES ([E])", "s08_wedding").When(Goal.Event, "s08:radio");
            Step(q, "start", "EVERYONE'S HERE: TELL MAUD TO START WHEN YOU'RE READY", "s08_wedding")
                .Says("s08_host", "s08_start", "EVERYONE'S HERE. START WHENEVER YOU'RE READY.",
                    "RIGHT. RUBEN, DOT: FROM THE TOP. LIZA, TOBIAS: STOP FIDGETING. EVERYONE ELSE: FIND SOMEWHERE TO STAND THAT ISN'T IN FRONT OF ME.");
            Step(q, "ceremony", "THE WEDDING", "s08_stage").When(Goal.Event, "s08:ceremony:done");
            Step(q, "thanks", "CONGRATULATE THE HAPPY COUPLE'S MOTHER", "s08_wedding")
                .Says("s08_host", "s08_thanks", "CONGRATULATIONS. TO ALL OF YOU.",
                    "YOU'RE STAYING FOR THE DANCE. THAT WASN'T A QUESTION. AND FEN WANTS A WORD ABOUT PAYING YOU IN WAISTCOATS; SAY YES, SHE'S VERY GOOD. " +
                    "I ASKED THE RADIO PEOPLE FOR A SONG FOR YOU, TOO. LISTEN OUT FOR IT.");
            q.reward.scrap = 25;
            q.reward.items.Add(("cloth_bomber", 1)); q.reward.resources.Add((ResourceType.Cloth, 6));
            q.reward.training.Add((Skill.Crafting, 4f)); q.reward.training.Add((Skill.Speech, 4f));
            q.reward.flag = "kerry_dedication";
            q.payoff = "LIZA KERRY AND TOBIAS HALE WERE MARRIED AT THE RIGHT END OF THE ROAD, UNDER THE WATER TOWER. FEN SENT YOU OFF IN A SPARE WEDDING JACKET.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S08(List<Member> into)
        {
            System.Func<string> fenAt = () => Story.StepDone("S08", "band") || Story.StateOf("S08") == Story.State.Done ? "s08_wedding" : "fen";
            into.Add(new Member { key = "fen", name = "FEN LOCKE", title = "TAILOR", anchor = "fen", anchorNow = fenAt, temper = Temper.Nervous, female = true, outfit = new[] { "sweater", "pants", "boots", "scarf" } });
            into.Add(new Member { key = "s08_local", name = "MARGE TULLY", title = "BRACKEN BOTTOM", anchor = "fen", temper = Temper.Joker, female = true, outfit = new[] { "overalls", "boots", "beanie" } });
            into.Add(new Member { key = "s08_fiddler", name = "RUBEN ASH", title = "FIDDLER", anchor = "s08_band1", temper = Temper.Joker, outfit = new[] { "bomber", "jeans", "boots", "cowboy" } });
            into.Add(new Member { key = "s08_squeeze", name = "DOT MARLOW", title = "ACCORDION", anchor = "s08_band2", temper = Temper.Friendly, female = true, outfit = new[] { "hoodie", "pants", "boots", "sunhat" }, present = () => StoryLibrary.S08On });
            into.Add(new Member { key = "s08_host", name = "MAUD KERRY", title = "MOTHER OF THE BRIDE", anchor = "s08_wedding", temper = Temper.Proud, female = true, outfit = new[] { "coat", "pants", "boots", "sunhat" }, present = () => StoryLibrary.S08Here });
            into.Add(new Member { key = "s08_bride", name = "LIZA KERRY", title = "BRIDE", anchor = "s08_wedding", temper = Temper.Friendly, female = true, outfit = new[] { "sweater", "pants", "boots", "scarf" }, present = () => StoryLibrary.S08Here });
            into.Add(new Member { key = "s08_groom", name = "TOBIAS HALE", title = "GROOM", anchor = "s08_wedding", temper = Temper.Nervous, outfit = new[] { "tank", "jeans", "boots" }, present = () => StoryLibrary.S08Here });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Bracken Bottom (Fen's cart and the old mine camp) on a road out of the first town, and Bracken Top
        /// (the wedding, under a water tower) 350-850 m further along the roads: the two ends of one name. The band's
        /// places move with them once they travel.</summary>
        static partial void Anchors_S08(WorldGen world, Settlement town)
        {
            var tc = Q5Centre(world, town);
            float r0 = town != null ? town.radius : 40f;
            var fen = Q5Road(world, town, "fen", tc, r0 + 220f, r0 + 700f, 70f, 140f);
            Q5Road(world, town, "s08_wedding", fen, 350f, 850f, 70f, 140f);
            Q5Beside(world, "s08_band1", "fen", new Vector3(-3.4f, 0f, 3.2f));
            Q5Beside(world, "s08_band2", "fen", new Vector3(-4.8f, 0f, 2.4f));
            Q5Beside(world, "s08_stage", "s08_wedding", new Vector3(0f, 0f, -3f));
            Clearing("fen", 12f);
            Clearing("s08_wedding", 16f);
        }
    }
}
