using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // A6 TELL IT STRAIGHT (storyline §5): Dr. Ivo Rusk keeps the Bunker Remnants' archive in a roadside checkpoint; the
    // records are real and they name the drivers who smuggled medicine through Ada's own checkpoints. Cross-check one
    // town's deliveries (the freight office's book by road, or Ivo's runner), then decide with June at the relay: the
    // full records, a redacted release (one more task: a second source for the quantities, Wes's logbook), or hold it
    // all as leverage. Rehearse with captions, air it (or seal it) — <see cref="Broadcast"/> remembers how. Then Mara
    // apologises without asking to be forgiven, and the player decides whether she stays, rides along or goes.
    public static partial class StoryLibrary
    {
        internal const string A6Full = "FULL RELEASE", A6Redact = "REDACTED", A6Hold = "HELD FOR LEVERAGE";
        internal const string A6Stay = "SHE STAYS AT THE GARAGE", A6Travel = "SHE TRAVELS WITH YOU", A6Depart = "SHE DEPARTS";
        internal const string A6Script = "story_a6_script", A6Dossier = "story_a6_dossier";

        /// <summary>The broadcast choice as the mechanic's kind (null before the decision).</summary>
        internal static Broadcast.Kind? A6Kind(string route) => route == A6Full ? Broadcast.Kind.Full : route == A6Redact ? Broadcast.Kind.Redacted : route == A6Hold ? Broadcast.Kind.Held : (Broadcast.Kind?)null;

        internal static string A6_Payoff(string decide, string mara)
        {
            string air = decide == A6Full ? "THE WHOLE RECORD WENT OUT ON WASTETALK, NAMES AND ALL: NOBODY CAN CALL IT A RUMOUR, AND NOBODY WHO HELPED IS HIDDEN."
                : decide == A6Redact ? "THE RECORDS WENT OUT ON WASTETALK WITH THE HELPERS' NAMES CUT, EVERY FIGURE BACKED BY WES'S LOGBOOK."
                : decide == A6Hold ? "THE RECORDS ARE SEALED IN IVO'S ARCHIVE: LEVERAGE FOR THE TABLE WITH ADA, UNTIL SOMEONE CALLS THE BLUFF." : "THE RECORDS ARE CHECKED.";
            string her = mara == A6Stay ? "MARA HAS THE BACK ROOM AT THE GARAGE AND HASN'T ASKED FOR ANYTHING ELSE."
                : mara == A6Travel ? "MARA RIDES WITH YOU NOW: SHE KNOWS WHICH GUARDS DRINK."
                : mara == A6Depart ? "MARA TOOK THE NORTH ROAD AT DAWN. SHE LEFT HER CONVOY BADGE ON THE BENCH." : "";
            return air + " " + her + " WHAT'S LEFT IS DECIDING HOW THE SUPPLY GETS REOPENED.";
        }

        static partial void Author_A6(QuestDef q)
        {
            q.build = Build.Playable;
            q.giver = "ivo_archive";                                                          // Dr. Ivo Rusk at his checkpoint (core "ivo" has no place)
            ItemIds.Register(A6Script, "BROADCAST SCRIPT (JUNE'S HAND)");
            ItemIds.Register(A6Dossier, "SEALED DOSSIER (LEVERAGE)");
            Q7Anchors("a6_archive", "q7_porch", "a2_office", "relay");
            q.offerSay = "MARA SAYS YOU CAN TELL A REAL RECORD FROM A FORGED ONE.";
            q.offerReply = "I CAN TELL PAPER FROM PAPER. WHETHER ANYONE SHOULD READ IT IS A DIFFERENT QUESTION, AND NOT ONE THE ARCHIVE ANSWERS. SHOW ME WHAT YOU HAVE.";
            q.hook = "THE RECORDS CAN EXPOSE ADA VENN. PUBLISHED AS THEY ARE, THEY ALSO NAME THE DRIVERS AND FAMILIES WHO SMUGGLED MEDICINE PAST HER.";

            Step(q, "records", "SHOW THE RECORDS TO DR. IVO RUSK AT THE REMNANT CHECKPOINT", "a6_archive")
                .Says("ivo_archive", "a6_records", "HERE'S ALL OF IT: THE MANIFEST, THE RECEIPT, THE RECORDING, THE CARGO SERIALS, ADA'S SCHEDULE.",
                    "...THE STAMPS ARE GENUINE. THE SCHEDULE IS GENUINE. AND EVERY OTHER PAGE NAMES A DRIVER WHO RAN MEDICINE THROUGH ADA'S OWN CHECKPOINTS. " +
                    "READ IT OUT AS IT IS AND YOU HAND ADA A LIST OF WHO TO PUNISH. I'VE ARCHIVED WORSE. I'VE NEVER HAD TO DECIDE ABOUT IT.")
                .Pays(r => r.training.Add((Skill.Speech, 2f)));
            Step(q, "crosscheck", "CROSS-CHECK ONE TOWN'S DELIVERIES: THE FREIGHT OFFICE'S BOOK IN TOWN, OR SEND IVO'S RUNNER", "a2_office")
                .Says("clerk", "a6_book", "HOLLAND, I NEED YOUR DELIVERY BOOK. THE REAL ONE.",
                    "...ELEVEN FUEL DELIVERIES SIGNED FOR THIS SEASON. FOUR ARRIVED. I SIGNED FOR ELEVEN BECAUSE THE GUILD SAID TO. TAKE A COPY. TAKE TWO.").Q7Label("BY ROAD: THE FREIGHT OFFICE'S BOOK")
                .Says("ivo_archive", "a6_runner", "SEND YOUR RUNNER TO THE TOWN'S FREIGHT OFFICE.",
                    "HE'S FASTER THAN HE LOOKS AND MORE HONEST THAN I AM. ...BACK ALREADY. ELEVEN SIGNED FOR, FOUR ARRIVED. THE CLERK SENT HIS APOLOGIES, WHICH I HAVE ALSO ARCHIVED.").Q7Price(15).Q7Label("BY COURIER: IVO'S RUNNER")
                .Pays(r => { r.evidence.Add("tally"); r.training.Add((Skill.Speech, 2f)); });
            Step(q, "decide", "HOW DOES IT GO OUT? DECIDE WITH JUNE AT THE RELAY: EVERYTHING, REDACTED, OR NOTHING AT ALL", "relay")
                .Says("june", "a6_full", "READ IT ALL. NAMES, LOADS, DATES.",
                    "...AND THE DRIVERS WHO HELPED? THEIR KIDS LISTEN TO THIS STATION. OKAY. IT'S TRUE AND IT'S YOUR CALL. I'LL READ EVERY WORD. SCRIPT'S YOURS WHEN YOU WANT TO RUN IT.").Q7Label(A6Full)
                .Says("june", "a6_redact", "LOADS AND DATES. CUT THE NAMES OF THE PEOPLE WHO HELPED.",
                    "THEN ADA SAYS WE MADE THE NUMBERS UP. WE NEED A SECOND SOURCE FOR THE QUANTITIES THAT DOESN'T NAME ANYONE. WES CALLOWAY DROVE THOSE LOADS, AND I HEAR HE KEPT A LOGBOOK.").Q7Label(A6Redact)
                .Says("june", "a6_hold", "NOTHING GOES OUT. WE PUT IT ON THE TABLE IN FRONT OF ADA.",
                    "QUIET LEVERAGE. IT WORKS UNTIL SOMEBODY CALLS YOUR BLUFF, AND THEN IT'S PAPER IN A DRAWER. I'LL SEAL A COPY WITH IVO.").Q7Label(A6Hold);
            Step(q, "logbook", "REDACTED RELEASE: A SECOND SOURCE FOR THE QUANTITIES. WES CALLOWAY'S LOGBOOK, AT THE GARAGE", "q7_porch")
                .Says("wes_home", "a6_logbook", "WES, YOUR LOGBOOK. JUST THE LOADS AND DATES.",
                    "EVERY RUN, EVERY LITRE. NO NAMES: I NEVER WROTE NAMES DOWN. IT'S WHY I'M STILL BREATHING. TAKE IT, AND DON'T LET IVO SPILL TEA ON IT.").Optional()
                .Pays(r => r.evidence.Add("logbook"));
            Step(q, "prepare", "PREPARE THE BROADCAST WITH JUNE: A RUN-THROUGH WITH CAPTIONS", "relay")
                .Says("june", "a6_rehearse", "LET'S RUN IT ONCE, QUIETLY, BEFORE IT'S LIVE.", "WE'RE NOT ON AIR. READ ALONG WITH ME, AND TELL ME IF ANYTHING SOUNDS LIKE A GUESS.").Needs(A6Script).Q7Label("REHEARSED WITH CAPTIONS")
                .Says("june", "a6_seal", "SEAL IT. ONE COPY FOR IVO'S VAULT, ONE FOR THE TABLE.", "SEALED. IF ANYONE ASKS, IT NEVER EXISTED. IF ADA ASKS, IT'S EVERYWHERE.").Needs(A6Dossier).Q7Label("SEALED FOR THE TABLE");
            Step(q, "release", "ON AIR WHEN YOU SAY SO: TELL JUNE", "relay")
                .Says("june", "a6_air", "WE'RE LIVE. GO.", "THIS IS WASTETALK, NINETY POINT ONE...").Needs(A6Script).Q7Label("ON AIR")
                .When(Goal.Event, "a6:held", label: "KEPT OFF THE AIR");
            Step(q, "apology", "MARA IS WAITING AT THE GARAGE. HEAR HER OUT", "q7_porch")
                .Says("mara_home", "a6_listen", "YOU WANTED TO SAY SOMETHING.",
                    "I TOOK ADA'S MONEY TO KEEP TWENTY PEOPLE FED, AND IT COST WES HIS LEG AND YOU YOUR NAME. I'M NOT ASKING YOU TO SAY IT'S FINE. IT ISN'T. " +
                    "I'M SAYING I KNOW, AND I'LL CARRY IT WHERE YOU CAN SEE ME DO IT, OR WHERE YOU CAN'T. YOUR CHOICE.");
            Step(q, "mara", "STAY, RIDE ALONG, OR GO: WHAT HAPPENS WITH MARA IS UP TO YOU", "q7_porch")
                .Says("mara_home", "a6_stay", "STAY. THERE'S A ROOM, AND WORK.", "...A ROOM. ALL RIGHT. I'LL EARN IT. I'M GOOD AT EARNING THINGS; IT'S KEEPING THEM I'M BAD AT.").Q7Label(A6Stay)
                .Says("mara_home", "a6_travel", "RIDE WITH ME. I COULD USE SOMEONE WHO KNOWS THE ROADS.", "SHOTGUN SEAT, THEN. I KNOW EVERY CHECKPOINT BETWEEN HERE AND THE COAST, AND WHICH GUARDS DRINK.").Q7Label(A6Travel)
                .Says("mara_home", "a6_depart", "I THINK YOU SHOULD GO.", "...YEAH. I THOUGHT SO TOO. I'LL BE ON THE NORTH ROAD BY MORNING. KEEP THE LIGHT ON. I MEANT IT, EVERY NIGHT.").Q7Label(A6Depart);
            Step(q, "settled", "MARA'S DECISION", "q7_porch").When(Goal.Event, "a6:settled");
            q.reward.scrap = 25; q.reward.training.Add((Skill.Speech, 8f)); q.reward.flag = "a6_done";
            q.payoff = A6_Payoff(null, null);
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_A6(List<Member> into)
        {
            // Dr. Ivo Rusk (core "ivo") at his checkpoint: present from the moment A6 opens (its giver)
            into.Add(new Member { key = "ivo_archive", name = "DR. IVO RUSK", title = "REMNANT ARCHIVIST", anchor = "a6_archive", temper = Temper.Nervous,
                outfit = new[] { "coat", "pants", "boots", "goggles" } });
            // Mara on the road with the player (a companion; never placed by the cast system)
            into.Add(new Member { key = "mara_road", name = "MARA VALE", title = "CONVOY LEADER", anchor = null, temper = Temper.Proud, female = true,
                outfit = new[] { "bomber", "jeans", "combat_boots" }, tool = "tool_wrench", present = () => false });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>A6: the Bunker Remnants' roadside checkpoint 0.9-2.4 km out of the first town: a concrete hut of
        /// shelves, sandbags either side of the road, the archive's flag.</summary>
        static partial void Anchors_A6(WorldGen world, Settlement town)
        {
            Q7Place(world, town, "a6_archive", Q7Town(world, town), 900f, 2400f, 250f, 6f, 250f, 16f);
        }
    }
}
