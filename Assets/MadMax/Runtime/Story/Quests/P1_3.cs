using MadMax.RPG;

namespace MadMax.Story
{
    // P1.3 ONE MORE SUNDAY: service the coupe (fuel from your pack, oil and coolant from Nell's barrel), then the lookout
    // up the road: Nell drives and you ride in the passenger seat, or you drive because her hands are bad today. An
    // ordinary conversation at the view, the drive home. The coupe stays Nell's.
    public static partial class StoryLibrary
    {
        static partial void Author_P1_3(QuestDef q)
        {
            q.build = Build.Playable;
            q.offerSay = "IT'S SUNDAY SOMEWHERE.";
            q.offerReply = "HA. THEN SHE NEEDS FUEL, OIL AND COOLANT. OIL AND COOLANT ARE IN MY BARREL; THE FUEL'S ON YOU. " +
                           "THE LOOKOUT UP THE ROAD. WE WENT EVERY SUNDAY. I HAVEN'T BEEN SINCE.";
            q.hook = "NELL WANTS ONE MORE SUNDAY DRIVE TO THE LOOKOUT UP THE ROAD, IN THE COUPE.";
            Step(q, "ready", "GET HER READY: FUEL, OIL AND COOLANT ([G] BESIDE THE COUPE; OIL AND COOLANT ARE IN NELL'S BARREL)", "p1_car")
                .When(Goal.Event, "p1_3:ready").Pays(r => r.training.Add((Skill.Mechanics, 3f)));
            Step(q, "who", "WHO DRIVES? ASK NELL", "nell")
                .Says("nell", "p1_3_ride", "YOU DRIVE, NELL. I'LL RIDE.", "GET IN, THEN. PASSENGER SIDE ([E] AT THE DOOR). HANDS OFF THE RADIO.")
                .Says("nell", "p1_3_wheel", "I'LL DRIVE, IF YOU'D LIKE.", "...MY HANDS ARE BAD TODAY. YES. YOU DRIVE; I'LL SIT WHERE TOM SAT. SHE PULLS LEFT UNDER BRAKING.");
            Step(q, "lookout", "THE LOOKOUT UP THE ROAD, NELL IN THE COUPE (SHE WON'T DRIVE IT IN THE DARK)", "p1_view").When(Goal.Event, "p1_3:lookout")
                .Pays(r => r.training.Add((Skill.Driving, 4f)));
            Step(q, "talk", "SIT WITH NELL AT THE LOOKOUT ([F] TO GET OUT)", "p1_view")
                .Says("nell", "p1_3_tom", "WHAT WAS TOM LIKE?", "LOUD. KIND. TERRIBLE AT CARDS. HE'D HAVE LIKED YOU: HE LIKED ANYONE WHO SHOWED UP.")
                .Says("nell", "p1_3_sundays", "WHAT DID YOU TWO TALK ABOUT UP HERE?", "NOTHING MUCH. THAT WAS THE POINT. WE COUNTED TRUCKS AND ARGUED ABOUT CLOUDS.")
                .Says("nell", "p1_3_quiet", "(SAY NOTHING. WATCH THE ROAD WITH HER.)", "...YEAH. LIKE THAT.");
            Step(q, "home", "DRIVE HOME: THE COUPE BACK AT NELL'S STOP, NELL WITH IT", "p1_car").When(Goal.Event, "p1_3:home");
            q.reward.items.Add(("tool_jack", 1)); q.reward.items.Add(("use_spark_plugs", 1));
            q.reward.training.Add((Skill.Mechanics, 6f)); q.reward.training.Add((Skill.Driving, 4f)); q.reward.flag = "paint_nell";
            q.payoff = "ONE MORE SUNDAY AT THE LOOKOUT. THE COUPE STAYS NELL'S; SHE GAVE YOU TOM'S JACK AND HIS PAINT CODE, BRONZE OVER BLACK.";
        }

        /// <summary>P1.3: Nell drives (the player rides), else the player drives her.</summary>
        public static bool P1NellDrives(string route) => route != null && route.StartsWith("YOU DRIVE");
    }
}
