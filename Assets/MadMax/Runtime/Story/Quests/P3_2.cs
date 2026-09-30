using MadMax.Items;
using MadMax.RPG;

namespace MadMax.Story
{
    // P3.2 SHALLOW OR DEEP: two wrecks off Halvard's harbour, marked with buoys: the coaster ALBA spilled crates in
    // wading depth (tins, rope, safe), the packet boat MERIDIAN lies deep (instruments, the chart locker). Borrow his
    // helmet and tank if you want the deep one; survey both, salvage one, bring it in. No submarine needed.
    public static partial class StoryLibrary
    {
        static partial void Author_P3_2(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_p3_cargo", "ALBA'S CARGO MANIFEST TIN");
            ItemIds.Register("story_p3_instruments", "MERIDIAN'S BRASS INSTRUMENTS");
            ItemIds.Register("story_p3_photo", "A FRAMED PHOTOGRAPH (ALBA)");
            ItemIds.Register("story_p3_letters", "A TIN OF LETTERS (MERIDIAN)");
            q.offerSay = "YOU SAID SOMETHING ABOUT WRECKS?";
            q.offerReply = "TWO. THE COASTER ALBA SPILLED HER CRATES IN THE SHALLOWS: TINS AND ROPE, SAFE AS A BATH. THE PACKET BOAT MERIDIAN LIES DEEPER: " +
                           "INSTRUMENTS, A CHART LOCKER, MAYBE THE OWNER'S THINGS. I PUT BUOYS ON BOTH. TAKE MY OLD HELMET AND TANK IF YOU'RE GOING DEEP.";
            q.hook = "TWO WRECKS OFF HALVARD'S HARBOUR: THE ALBA IN THE SHALLOWS, THE MERIDIAN IN DEEP WATER.";
            Step(q, "gear", "OPTIONAL: BORROW HALVARD'S DIVING HELMET AND AIR TANK (WEAR BOTH TO WALK THE BOTTOM)", "p3_harbour")
                .Says("trawler", "p3_2_gear", "I'LL TAKE THE HELMET AND THE TANK.",
                    "IT FOGS; TALK TO IT, IT HELPS. THE TANK HOLDS THREE MINUTES. WHEN IT SAYS THIRTY SECONDS, YOU SURFACE. NO HEROICS ON MY AIR.").Optional()
                .Pays(r => { r.items.Add(("cloth_dive_helmet", 1)); r.items.Add(("cloth_air_tank", 1)); });
            Step(q, "alba", "SURVEY THE ALBA: THE BUOY IN THE SHALLOWS", "p3_shallow").When(Goal.Reach, "p3_shallow", 9f).Optional();
            Step(q, "meridian", "SURVEY THE MERIDIAN: THE BUOY IN DEEP WATER", "p3_deep").When(Goal.Reach, "p3_deep", 12f).Optional();
            Step(q, "survey", "SURVEY BOTH WRECKS (THE BUOYS): SHALLOW AND SAFE, OR DEEP AND VALUABLE", "p3_shallow").When(Goal.Steps, "alba,meridian", 2f)
                .Pays(r => r.training.Add((Skill.Survival, 3f)));
            Step(q, "salvage", "SALVAGE ONE: THE ALBA'S CRATE (WADE OUT, [E]) OR THE MERIDIAN'S CHART LOCKER (DIVE GEAR, [E])", "p3_deep")
                .When(Goal.Have, "story_p3_cargo", 1f, "SHALLOW: THE ALBA'S CARGO")
                .When(Goal.Have, "story_p3_instruments", 1f, "DEEP: THE MERIDIAN'S INSTRUMENTS")
                .Pays(r => r.training.Add((Skill.Salvaging, 6f)));
            Step(q, "report", "BRING WORD TO HALVARD", "p3_harbour")
                .Says("trawler", "p3_2_show", "THE WRECKS ARE SURVEYED AND SOMETHING CAME UP.",
                    "...THEN SOMEBODY WILL COME ASKING WHOSE IT IS. SOMEBODY ALWAYS DOES. KEEP THE HELMET FOR NOW.");
            q.reward.scrap = 15; q.reward.items.Add(("use_o2_bottle", 1)); q.reward.training.Add((Skill.Survival, 4f));
            q.payoff = "A CAREFUL SALVAGE: BOTH WRECKS SURVEYED, ONE OPENED.";
        }

        /// <summary>P3.2's salvage went deep (the Meridian), else shallow (the Alba).</summary>
        public static bool P3Deep(string route) => route != null && route.StartsWith("DEEP");
    }
}
