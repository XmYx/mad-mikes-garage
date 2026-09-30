using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S15 THE ORGAN RUNS ON DIESEL: Hollis Reed is preparing a public recital on an open-air stage at the edge of town.
    // The organ's blower needs power: repair and fuel his cracked generator, or bring a supply of your own. Three pipes
    // are missing: they ended up in the old chapel ruin (two on the ground, one in a crate); Hollis can also roll one. Cable the blower in, then
    // balance the load (the stage lamps and the audience heater share the supply and a 1.5 kW generator can't carry
    // them all: a load breaker, a switch, or more power). The recital is a performance scene, and it is what you made
    // it: quiet power (batteries, sun, or the generator far enough away) and the square hears the organ; a generator
    // thumping beside the stage and the audience hears both. Copper coils, Construction and Crafting, a recording of
    // the recital and a tin whistle.
    public static partial class StoryLibrary
    {
        static partial void Author_S15(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_organ_pipe", "ORGAN PIPE");
            ItemIds.Register("story_recital_tape", "HOLLIS REED: RECITAL (TAPE)");
            ItemIds.Register("story_tin_whistle", "TIN WHISTLE");
            StoryAnchors.Q5Declare("hollis", "s15_stage", "s15_ruin");
            q.offerSay = "IS THAT AN ORGAN? OUT HERE?";
            q.offerReply = "A PIPE ORGAN, IN THE OPEN AIR, IN THE WASTES. I BUILT IT FROM A CHAPEL'S WORTH OF PARTS AND I'M GIVING A RECITAL WHETHER THE WORLD HAS ENDED OR NOT. " +
                           "EXCEPT THE GENERATOR'S CRACKED, THREE PIPES WALKED OFF, AND THE BLOWER NEEDS MORE POWER THAN I'VE GOT. I CAN PLAY. I CAN'T WIRE.";
            q.hook = "HOLLIS REED WANTS TO GIVE AN ORGAN RECITAL IN THE OPEN AIR. THE ORGAN NEEDS POWER, PIPES AND A STEADY SUPPLY.";

            Step(q, "look", "LOOK OVER HOLLIS'S STAGE AND ORGAN", "hollis").When(Goal.Reach, "hollis", 7f);
            Step(q, "gen", "POWER FOR THE RECITAL: REPAIR HOLLIS'S GENERATOR ([B], R) AND FUEL IT ([T]), OR BUILD A SUPPLY OF YOUR OWN (GENERATOR, BATTERIES, SOLAR)", "hollis")
                .When(Goal.Event, "s15:gen", label: "HOLLIS'S GENERATOR, REPAIRED")
                .When(Goal.Build, "generator|coal_generator|biogas_generator|battery|solar_panel", 30f, "A SUPPLY OF YOUR OWN")
                .Pays(r => r.training.Add((Skill.Mechanics, 3f)));
            Step(q, "roll", "HOLLIS CAN ROLL A NEW PIPE FROM SCRAP SHEET", "hollis")
                .Says("hollis", "s15_roll", "ROLL A NEW PIPE FROM SCRAP SHEET.", "SCRAP SHEET, A MANDREL AND A LOT OF SWEARING. HERE. IT'LL SOUND A LITTLE BRIGHT. SO DID I, ONCE.")
                .Optional().Pays(r => r.items.Add(("story_organ_pipe", 1)));
            q.steps[q.steps.Count - 1].any[0].price = 6;
            Step(q, "pipes", "THREE PIPES ARE MISSING: THEY ENDED UP IN THE OLD CHAPEL RUIN (MARKED), TWO ON THE GROUND AND ONE IN A CRATE; HOLLIS CAN ROLL A NEW ONE FROM SCRAP", "s15_ruin").When(Goal.Have, "story_organ_pipe", 3f);
            Step(q, "fit", "BRING HOLLIS THE PIPES", "hollis")
                .Says("hollis", "s15_fit", "HERE: THREE PIPES.", "THERE THEY ARE. THE F, THE A AND THE WORRYING LOW D. GIVE ME A MINUTE WITH THE LADDER. ...THERE. SHE HAS A FULL SET OF TEETH AGAIN.").Needs("story_organ_pipe")
                .Pays(r => { r.take.Add(("story_organ_pipe", 3)); r.training.Add((Skill.Crafting, 4f)); });
            Step(q, "power", "CABLE THE ORGAN'S BLOWER TO THE SUPPLY ([B], CABLE TO THE LAMP OVER THE ORGAN)", "hollis").When(Goal.Event, "s15:cabled");
            Step(q, "balance", "BALANCE THE LOAD: THE ORGAN MUST RUN STEADY WITH THE STAGE LAMPS AND THE AUDIENCE HEATER ON THE SUPPLY (A LOAD BREAKER, A SWITCH, OR MORE POWER)", "hollis")
                .When(Goal.Event, "s15:balanced_breaker", label: "THE HEATER SHEDS FIRST (LOAD BREAKER)")
                .When(Goal.Event, "s15:balanced_off", label: "THE HEATER SWITCHED OFF")
                .When(Goal.Event, "s15:balanced_power", label: "ENOUGH POWER FOR EVERYTHING")
                .Pays(r => r.training.Add((Skill.Construction, 5f)));
            Step(q, "start", "WHEN YOU'RE READY, TELL HOLLIS TO PLAY", "hollis")
                .Says("hollis", "s15_play", "THE ORGAN'S READY. PLAY THEM SOMETHING.", "THEN LET THEM IN. AND IF ANYONE COUGHS DURING THE SLOW BIT, I WILL KNOW.");
            Step(q, "recital", "THE RECITAL", "s15_stage")
                .When(Goal.Event, "s15:quiet", label: "ON QUIET POWER")
                .When(Goal.Event, "s15:noisy", label: "OVER THE GENERATOR");
            Step(q, "after", "TELL HOLLIS HOW IT SOUNDED", "hollis")
                .Says("hollis", "s15_after", "THAT WAS SOMETHING.",
                    "IT WAS, WASN'T IT. HERE: A TAPE OF IT, THE COILS FROM THE OLD BLOWER MOTOR, AND A WHISTLE FOR YOUR POCKET. EVERYONE SHOULD CARRY ONE INSTRUMENT. IT KEEPS YOU HONEST.");
            q.reward.scrap = 20;
            q.reward.items.Add((ItemIds.Coil, 2)); q.reward.items.Add(("story_recital_tape", 1)); q.reward.items.Add(("story_tin_whistle", 1));
            q.reward.training.Add((Skill.Construction, 4f)); q.reward.training.Add((Skill.Crafting, 4f));
            q.reward.flag = "hollis_recital";
            q.payoff = "HOLLIS REED GAVE HIS RECITAL IN THE OPEN AIR.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S15(List<Member> into)
        {
            into.Add(new Member { key = "hollis", name = "HOLLIS REED", title = "ORGAN BUILDER", anchor = "hollis", temper = Temper.Proud, outfit = new[] { "coat", "pants", "boots", "scarf" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Hollis's open-air stage on level ground at the edge of the first town (the stage faces the town, the
        /// audience in between), and the old chapel ruin 70-180 m further out.</summary>
        static partial void Anchors_S15(WorldGen world, Settlement town)
        {
            var h = Q5Edge(world, town, "hollis", 14f, 100f, 110f, 9f, 40f);
            Q5Beside(world, "s15_stage", "hollis", new Vector3(0f, 0f, -2.2f));
            var tc = Q5Centre(world, town);
            float away = Q5Face(tc, h);
            if (!Q5Ring(world, "s15_ruin", h, 70f, 180f, away + 30f, p => Clear(town, p, 25f) && Q5Free(p, 30f) && Q5Dry(world, p, 4f), false)
                && !Q5Ring(world, "s15_ruin", h, 50f, 260f, away, p => Q5Free(p, 15f), false))
                Q5Set(world, "s15_ruin", h + Quaternion.Euler(0f, away, 0f) * new Vector3(0f, 0f, 90f), away);
            Clearing("hollis", 16f);
            Clearing("s15_ruin", 8f);
        }
    }
}
