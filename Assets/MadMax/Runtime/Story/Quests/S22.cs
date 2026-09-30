using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S22 A JACKET FOR THE END OF THE WORLD: Dax Wren's hands shake too much for the heavy seams. Ottilie Marsh is walking
    // north to her sister's by choice, and can carry one coat: sew it at Dax's table for the cold (parka, bomber) or for
    // the rain (duster, poncho) from cloth or leather, let her try it on, walk her up to the lookout and back, then tell
    // Dax. The coat she wears on the walk is the one you chose.
    public static partial class StoryLibrary
    {
        static partial void Author_S22(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_road_patch", "DAX'S ROAD PATCH");
            Q3Anchors("dax", "s22_home", "s22_view");
            q.offerSay = "YOU LOOK LIKE A MAN WITH A DEADLINE.";
            q.offerReply = "A MAN WITH HALF A COAT. OTTILIE MARSH IS WALKING NORTH TO HER SISTER'S BEFORE THE WEATHER TURNS, AND MY HANDS SHAKE TOO MUCH FOR THE HEAVY SEAMS NOW. " +
                           "BRING THE MAKINGS AND SEW IT AT MY TABLE. I'LL SUPERVISE, WHICH IS THE HARD PART.";
            q.hook = "DAX WREN'S HANDS SHAKE. OTTILIE MARSH NEEDS A TRAVELLING COAT BEFORE SHE WALKS NORTH.";

            Step(q, "meet", "MEET THE CUSTOMER, OTTILIE MARSH", "s22_home")
                .Says("s22_ottilie", "s22_meet", "DAX SAYS YOU'RE WALKING NORTH.",
                    "TO MY SISTER'S, PAST THE SALT FLATS. NOBODY'S MAKING ME; THE HOUSE IS JUST QUIET NOW. IT'S COLD UP THERE AND IT RAINS SIDEWAYS ON THE FLATS. " +
                    "I CAN CARRY ONE COAT. YOU'LL HAVE TO CHOOSE WHICH MISERY IT'S FOR.");
            Step(q, "makings", "BRING THE MAKINGS: 8 CLOTH, OR 4 LEATHER (DAX CUTS THE LINING FROM HIS REMNANTS)", "dax")
                .When(Goal.Have, "res:" + (int)ResourceType.Cloth, 8f, "CLOTH")
                .When(Goal.Have, "res:" + (int)ResourceType.Leather, 4f, "LEATHER")
                .Pays(r => { r.resources.Add((ResourceType.Rubber, 2)); r.resources.Add((ResourceType.Oil, 1)); });
            Step(q, "sew", "SEW IT AT DAX'S TABLE: FOR THE COLD (WINTER PARKA OR BOMBER JACKET) OR FOR THE RAIN (LEATHER DUSTER OR RAIN PONCHO)", "dax")
                .When(Goal.Craft, "cloth_coat", label: "FOR THE COLD: A WINTER PARKA")
                .When(Goal.Craft, "cloth_bomber", label: "FOR THE COLD: A BOMBER JACKET")
                .When(Goal.Craft, "cloth_duster", label: "FOR THE RAIN: A LEATHER DUSTER")
                .When(Goal.Craft, "cloth_poncho", label: "FOR THE RAIN: A RAIN PONCHO")
                .Pays(r => r.training.Add((Skill.Crafting, 5f)));
            Step(q, "fit", "LET OTTILIE TRY IT ON", "s22_home")
                .Says("s22_ottilie", "s22_fit_coat", "HERE: A WINTER PARKA. TRY IT ON.", "IT'S LIKE WEARING A HOUSE. A WARM HOUSE. I MAY NEVER TAKE IT OFF.").Needs("cloth_coat")
                .Says("s22_ottilie", "s22_fit_bomber", "HERE: A BOMBER JACKET. TRY IT ON.", "SHORT, BUT IT HOLDS THE HEAT. AND I LOOK LIKE I KNOW HOW TO FLY SOMETHING.").Needs("cloth_bomber")
                .Says("s22_ottilie", "s22_fit_duster", "HERE: A LEATHER DUSTER. TRY IT ON.", "IT SMELLS OF WAX AND GOOD SENSE. THE RAIN CAN TRY.").Needs("cloth_duster")
                .Says("s22_ottilie", "s22_fit_poncho", "HERE: A RAIN PONCHO. TRY IT ON.", "I LOOK LIKE A TENT. A DRY TENT. I'LL TAKE IT.").Needs("cloth_poncho");
            Step(q, "walk", "A SHORT TEST WALK: TAKE OTTILIE UP TO THE LOOKOUT AND SEE HOW IT WEARS", "s22_view").When(Goal.Event, "s22:walked");
            Step(q, "dax", "TELL DAX HOW IT WORE", "dax")
                .Says("dax", "s22_done", "SHE WALKED IT IN. SHE'S KEEPING IT.",
                    "OF COURSE SHE IS. YOU SEW STRAIGHTER THAN I DID AT YOUR AGE, WHICH I WILL DENY. HERE: MY PATCH. SEW IT ON SOMETHING OF YOURS; IT MEANS A COAT WAS MADE PROPERLY.");
            q.reward.items.Add(("story_road_patch", 1)); q.reward.items.Add(("use_sewing_kit", 1)); q.reward.resources.Add((ResourceType.Cloth, 4));
            q.reward.training.Add((Skill.Crafting, 6f)); q.reward.flag = "dax_patch";
            q.payoff = "OTTILIE LEAVES FOR HER SISTER'S IN A COAT YOU SEWED. DAX'S ROAD PATCH IS YOURS, AND SO IS THE HABIT OF CHECKING OTHER PEOPLE'S SEAMS.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S22(List<Member> into)
        {
            into.Add(new Member { key = "dax", name = "DAX WREN", title = "TAILOR", anchor = "dax", temper = Temper.Joker, outfit = new[] { "sweater", "pants", "boots", "scarf" } });
            into.Add(new Member { key = "s22_ottilie", name = "OTTILIE MARSH", title = "TRAVELLER", anchor = "s22_home", temper = Temper.Friendly, female = true, outfit = new[] { "tshirt", "pants", "boots", "sunhat" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Dax's stall at the edge of the first town, Ottilie's doorstep beside it, and a lookout 80-120 m out.</summary>
        static partial void Anchors_S22(WorldGen world, Settlement town)
        {
            var tc = Q3Town(world, town);
            Q3Edge(world, town, tc, "dax", 20f);
            var dax = Get("dax");
            Q3Set(world, "s22_home", dax + Quaternion.Euler(0f, Yaw("dax"), 0f) * new Vector3(7f, 0f, -2f), Yaw("dax"));
            Clearing("dax", 10f);
            var away = dax - tc; away.y = 0f;
            float a0 = away.sqrMagnitude > 1f ? Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg : 0f;
            Q3Ring(world, dax, 80f, 120f, a0 - 45f, 30f, p => Clear(town, p, 20f), out var view, out var vf);
            Q3Set(world, "s22_view", view, vf);
            Clearing("s22_view", 5f);
        }
    }
}
