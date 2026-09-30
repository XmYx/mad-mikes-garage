using System.Collections.Generic;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;

namespace MadMax.Story
{
    // B3 LIGHTS WORTH COMING BACK TO (storyline §6): a supply for the garage (fuel generator, solar with storage, or a
    // local renewable: wind or a water wheel where there's a river), a lamp and one essential service on it, a patched
    // roof, a bed and sanitation. Then Nell's test: she plugs her big compressor into the line through a load breaker on
    // her lead. A generator that can't carry it stalls (the merged power control: essential / normal / low shedding);
    // setting her breaker to LOW and restarting brings the priority load back, or a battery bank carries the surge
    // without a flicker. The garage becomes the campaign's refuge: the recovery point, a lantern under the sign.
    public static partial class StoryLibrary
    {
        internal const string B3Fuel = "FUEL GENERATOR", B3Solar = "SOLAR AND STORAGE", B3Renewable = "A LOCAL RENEWABLE";
        internal const string B3Restored = "OUTAGE, THEN THE PRIORITY LOAD BACK", B3Carried = "STORAGE CARRIED THE SURGE";

        internal static string B3_Payoff(string supply, string test)
        {
            string power = supply == B3Solar ? "SUN BY DAY AND A BATTERY BANK BY NIGHT" : supply == B3Renewable ? "POWER FROM THE WEATHER, PAID FOR IN UPKEEP" : "A GENERATOR THAT RUNS ON WHATEVER YOU FEED IT";
            string held = test == B3Carried ? "THE STORAGE CARRIED NELL'S SURGE WITHOUT A FLICKER." : "WHEN NELL'S COMPRESSOR KNOCKED IT FLAT, YOU GOT THE ESSENTIALS BACK FIRST.";
            return "THE GARAGE IS A REFUGE NOW: " + power + ", A DRY BED, A PLACE TO WASH. " + held + " YOU COME TO HERE WHEN THINGS GO WRONG, AND THE LANTERN UNDER MIKE'S SIGN TELLS THE ROAD SOMEONE'S HOME.";
        }

        static partial void Author_B3(QuestDef q)
        {
            q.build = Build.Playable;
            Q7Anchors("garage", "garage_yard");
            q.offerSay = "WHAT WOULD MAKE THE GARAGE WORTH COMING BACK TO?";
            q.offerReply = "LIGHT. A ROOF THAT DOESN'T LEAK, A BED, SOMEWHERE TO WASH, AND POWER YOU CAN TRUST WHEN IT MATTERS. FUEL, SUN OR WIND, I DON'T CARE WHICH. " +
                           "BUILD IT, THEN I'LL COME OVER AND TRY TO BREAK IT.";
            q.hook = "NELL: A PLACE PEOPLE COME BACK TO HAS A LIGHT ON, A DRY BED, AND POWER THAT HOLDS WHEN SOMEBODY PLUGS IN SOMETHING STUPID.";
            Step(q, "supply", "POWER FOR THE GARAGE: A FUEL GENERATOR, SOLAR PANELS (A BATTERY BANK KEEPS THE NIGHT), OR A WIND TURBINE / WATER WHEEL", "garage")
                .When(Goal.Build, "generator|coal_generator|biogas_generator", 30f, B3Fuel)
                .When(Goal.Build, "solar_panel", 30f, B3Solar)
                .When(Goal.Build, "windmill|wind_turbine_large|water_turbine", 45f, B3Renewable)
                .Pays(r => r.training.Add((Skill.Construction, 4f)));
            Step(q, "lamp", "A LAMP ON THAT POWER: CABLE A CEILING LIGHT, FLOODLIGHT OR LAMP POST TO THE SUPPLY", "garage").When(Goal.Event, "b3:lamp");
            Step(q, "service", "AN ESSENTIAL SERVICE ON THE SAME LINE: A FRIDGE OR FREEZER, A WATER PUMP, A HEATER, AN OVEN OR RANGE", "garage").When(Goal.Event, "b3:service")
                .Pays(r => r.training.Add((Skill.Mechanics, 3f)));
            Step(q, "roof", "PATCH THE ROOF OVER THE BAY: A ROOF PIECE OVER A HOLE", "garage").When(Goal.Build, "roof_flat|roof_slope", 6f);
            Step(q, "bed", "A BED TO SLEEP IN AT THE GARAGE", "garage").When(Goal.Build, "bed|clinic_bed", 16f);
            Step(q, "sanitation", "SANITATION: A LATRINE, A SHOWER, A BATH OR A SINK", "garage").When(Goal.Build, "latrine|shower|bathtub|sink", 24f)
                .Pays(r => r.training.Add((Skill.Survival, 3f)));
            Step(q, "test", "TELL NELL IT'S READY FOR A TEST (SHE'S COME OVER TO THE GARAGE)", "garage_yard")
                .Says("nell_garage", "b3_test", "IT'S READY. TRY TO BREAK IT.",
                    "RIGHT. THIS IS MY BIG COMPRESSOR, ON MY LEAD, THROUGH A LOAD BREAKER. I'M PLUGGING IT INTO YOUR LINE. WHEN THINGS GO DARK, GET THE IMPORTANT STUFF BACK FIRST. THAT'S THE WHOLE TEST.");
            Step(q, "restore", "NELL'S COMPRESSOR IS ON YOUR LINE: KEEP THE ESSENTIAL SERVICE POWERED. SET HER BREAKER TO LOW ([E]) AND RESTART THE GENERATOR, OR LET STORAGE CARRY IT", "garage_yard")
                .When(Goal.Event, "b3:restored", label: B3Restored)
                .When(Goal.Event, "b3:carried", label: B3Carried)
                .Pays(r => r.training.Add((Skill.Mechanics, 5f)));
            Step(q, "refuge", "NELL HANGS A LANTERN UNDER MIKE'S SIGN", "garage").When(Goal.Event, "b3:refuge");
            q.reward.scrap = 20; q.reward.training.Add((Skill.Construction, 6f)); q.reward.flag = "b3_done";
            q.payoff = B3_Payoff(null, null);
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_B3(List<Member> into)
        {
            // Nell (core "nell") over at the garage for the test, for B5's supper and charter
            into.Add(new Member { key = "nell_garage", name = "NELL MERCER", title = "ROADSIDE MECHANIC", anchor = "garage_yard", temper = Temper.Gruff, female = true,
                outfit = new[] { "overalls", "boots", "gloves", "beanie" }, tool = "tool_wrench",
                present = () => (Q7State.At("B3", "test", "restore", "refuge") || Q7State.At("B5", "supper", "charter", "posted")) && !Q7State.At("B2", "supper", "welcome") });   // not while B2 already has her at the garage
        }
    }
}
