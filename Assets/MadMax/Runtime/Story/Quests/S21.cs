using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S21 THE LAST HONEST SAFE: Ruth Slate, locksmith, forgot the combination to her own safe. The gentle ways: her old
    // order books at the shop on the road out (the combination is the shop's opening day), or mending the seized linkage
    // in build mode; the loud ways: one controlled charge on the bolt (prepared with Ruth, the contents survive) or
    // blowing it apart (the contents don't, entirely; the pay drops, the task still closes). Inside: the last week's wages
    // and a love poem she would rather you didn't read.
    public static partial class StoryLibrary
    {
        static partial void Author_S21(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_order_book", "RUTH'S OLD ORDER BOOK");
            Q3Anchors("ruth", "s21_safe", "s21_oldshop", "s21_records");
            q.offerSay = "A LOCKSMITH WITH A LOCKED SAFE?";
            q.offerReply = "DON'T. I'VE HEARD IT. I SET THE COMBINATION THE NIGHT BEFORE EVERYTHING WENT WRONG AND I'VE BEEN BUSY SINCE. THE LINKAGE HAS SEIZED ANYWAY. " +
                           "MY OLD ORDER BOOKS ARE STILL AT THE SHOP ON THE ROAD OUT; THE COMBINATION'S IN THERE SOMEWHERE, KNOWING ME. OR MEND THE LINKAGE. OR, IF YOU MUST, A SMALL CHARGE. SMALL.";
            q.hook = "RUTH SLATE, LOCKSMITH, CAN'T OPEN HER OWN SAFE. SHE'D LIKE IT OPEN IN ONE PIECE.";

            Step(q, "look", "LOOK OVER RUTH'S SAFE", "s21_safe").When(Goal.Reach, "s21_safe", 3f);
            Step(q, "records", "OPTIONAL: SEARCH RUTH'S ORDER BOOKS AT HER OLD SHOP ON THE ROAD OUT", "s21_records")
                .When(Goal.Reach, "s21_records", 2.8f).Optional()
                .Pays(r => r.items.Add(("story_order_book", 1)));
            Step(q, "open", "OPEN THE SAFE: THE COMBINATION FROM HER OLD ORDER BOOKS (THE SHOP ON THE ROAD OUT), MEND THE SEIZED LINKAGE ([B] WITH A HAMMER, R ON THE SAFE), OR A CONTROLLED CHARGE (DYNAMITE)", "s21_oldshop")
                .Says("ruth", "s21_combo", "IT'S IN YOUR ORDER BOOK: THE DAY THE SHOP OPENED.",
                    "FOURTEEN, NINE, SIXTY-ONE. OF COURSE IT IS. I WAS SO PROUD OF THAT SHOP I PUT IT IN THE LOCK AND FORGOT BOTH. ...HEAR THAT? A GOOD LOCK OPENS LIKE A SIGH.")
                .Needs("story_order_book")
                .When(Goal.Event, "s21:linkage", label: "MENDED THE LINKAGE")
                .Says("ruth", "s21_charge", "ONE SMALL CHARGE ON THE BOLT. EVERYONE STAND BACK.",
                    "SMALL. I MEAN IT. ON THE BOLT, NOT THE DOOR. ...ALL RIGHT. FIRE IN THE HOLE, AS NOBODY HAS SAID SINCE THE WAR.")
                .Needs("throw_dynamite")
                .When(Goal.Event, "s21:wrecked", label: "TOOK IT APART THE HARD WAY");
            // what the method earned (hidden until paid; the journal lists them)
            Step(q, "intact", "THE WAGE ENVELOPES CAME OUT WHOLE: RUTH ADDS A BLUEPRINT FROM THE BOTTOM SHELF").When(Goal.Event, "s21:intact").Optional()
                .Pays(r => { r.scrap = 25; r.items.Add(("bp_framepack", 1)); });
            Step(q, "by_hand", "A LOCK MENDED BY HAND").When(Goal.Event, "s21:mechanics").Optional()
                .Pays(r => r.training.Add((Skill.Mechanics, 6f)));
            Step(q, "by_charge", "A CHARGE THAT WENT WHERE IT WAS TOLD").When(Goal.Event, "s21:demolition").Optional()
                .Pays(r => r.training.Add((Skill.Demolition, 6f)));
            Step(q, "by_paper", "AN ANSWER FOUND IN OLD PAPERS").When(Goal.Event, "s21:records").Optional()
                .Pays(r => r.training.Add((Skill.Speech, 4f)));
            Step(q, "inside", "SEE WHAT'S INSIDE WITH RUTH", "s21_safe")
                .Says("ruth", "s21_read", "THERE'S A POEM IN HERE. SHALL I READ IT OUT?",
                    "...'MY HEART IS A DEADBOLT, THROWN FOR YOU'? OH NO. I WROTE THAT. FOR ALBIE, THE WINTER WE MET. HE LAUGHED FOR A WEEK AND THEN HE MARRIED ME. READ THE NEXT VERSE AND I'LL LOCK YOU IN THERE.")
                .Says("ruth", "s21_unread", "THERE'S A POEM IN HERE. IT'S YOURS; I HAVEN'T READ IT.",
                    "...THANK YOU. IT'S FOR ALBIE, FROM THE WINTER WE MET. IT'S TERRIBLE AND HE KEPT IT ANYWAY. THE ENVELOPES ARE THE LADS' WAGES FROM THE SHOP'S LAST WEEK. I OWE THEM, WHEREVER THEY ARE.");
            q.reward.scrap = 15; q.reward.items.Add(("use_repair_kit", 1)); q.reward.flag = "s21_done";
            q.payoff = "RUTH'S SAFE IS OPEN. THE WAGES WILL GO TO WHOEVER OF HER LADS SHE CAN FIND, AND THE POEM IS BACK WHERE IT CAN'T EMBARRASS ANYONE.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S21(List<Member> into)
        {
            into.Add(new Member { key = "ruth", name = "RUTH SLATE", title = "LOCKSMITH", anchor = "ruth", temper = Temper.Proud, female = true, outfit = new[] { "sweater", "pants", "boots", "scarf" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Ruth's yard at the edge of the first town with the safe in front, and her old shop beside a road out of
        /// town (200-700 m), the order books on its shelf.</summary>
        static partial void Anchors_S21(WorldGen world, Settlement town)
        {
            var tc = Q3Town(world, town);
            Q3Edge(world, town, tc, "ruth", 245f);
            var ruth = Get("ruth");
            Q3Set(world, "s21_safe", ruth + Quaternion.Euler(0f, Yaw("ruth"), 0f) * new Vector3(1.6f, 0f, 1.6f), Yaw("ruth"));
            Clearing("ruth", 9f);
            float r0 = town != null ? town.radius + 180f : 200f;
            Q3Roadside(world, tc, r0, r0 + 520f, town, "s21_oldshop", 60f, 70f);
            var shop = Get("s21_oldshop");
            Q3Set(world, "s21_records", shop + Quaternion.Euler(0f, Yaw("s21_oldshop"), 0f) * new Vector3(-1.2f, 0f, -1.6f), Yaw("s21_oldshop"));
            Clearing("s21_oldshop", 10f);
        }
    }
}
