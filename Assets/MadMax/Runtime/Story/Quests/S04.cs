using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S04 THE HOUSE THAT WALKED (storyline §10): Della Shaw's timber house stands on posts on a slope; the rain barrel's
    // overflow has washed the ground out from under the kitchen corner and its post has fallen. Look it over, empty the
    // pantry, put a real support under the corner (it must stand on the ground and carry the floor), send the water
    // elsewhere (a ditch, or the barrel moved), give the door steps again and walk in. Pays foundation materials,
    // Construction and Della's porch as a structure plan.
    public static partial class StoryLibrary
    {
        internal const string S04Ditch = "DIG A DITCH AT THE FLAG", S04Barrel = "MOVE THE RAIN BARREL TO THE FLAG";
        const string S04Default = "DELLA'S KITCHEN STAYS WHERE SHE PUT IT. SHE SAYS SO TO ANYONE WHO STANDS STILL LONG ENOUGH.";

        /// <summary>The closing line: what holds the corner up and where the water goes now.</summary>
        internal static string S04_Payoff(string drain, bool wall)
        {
            if (drain == null) return S04Default;
            return "DELLA'S KITCHEN STANDS ON A NEW " + (wall ? "WALL" : "POST") + " AT THE CORNER, AND THE RAIN NOW " +
                   (drain == S04Ditch ? "RUNS AROUND THE HOUSE IN YOUR DITCH." : "FILLS A BARREL ON THE FAR SIDE OF THE YARD.") + " SHE CHECKS IT EVERY MORNING ANYWAY.";
        }

        static partial void Author_S04(QuestDef q)
        {
            q.build = Build.Playable;
            foreach (var k in new[] { "della", "s04_house", "s04_corner", "s04_spout", "s04_kitchen", "s04_door", "s04_ditch" })
                if (!Anchors.Contains(k)) Anchors.Add(k);
            q.offerSay = "EVERYTHING ALL RIGHT HERE?";
            q.offerReply = "NO. MY KITCHEN MOVED IN THE NIGHT. FORTY YEARS I LAID STONE; I KNOW WHERE I PUT A KITCHEN, AND IT WAS A HAND'S WIDTH FURTHER UP THE HILL. " +
                           "MY KNEES WON'T LET ME GET UNDER IT. HAVE A LOOK BEFORE IT GOES VISITING AGAIN.";
            q.hook = "DELLA SHAW, RETIRED MASON, SAYS HER KITCHEN MOVED OVERNIGHT. SHE IS PROBABLY RIGHT.";
            Step(q, "gully", "THE GROUND UNDER THE KITCHEN CORNER IS WASHED OUT, AND ITS POST LIES IN THE MUD", "s04_corner").When(Goal.Reach, "s04_corner", 2.6f).Optional();
            Step(q, "spout", "THE RAIN BARREL'S OVERFLOW RUNS DOWN THE BACK WALL STRAIGHT INTO THAT GULLY", "s04_spout").When(Goal.Reach, "s04_spout", 2.6f).Optional();
            Step(q, "survey", "INSPECT THE HOUSE: THE KITCHEN CORNER, AND WHERE THE WATER GOES", "s04_corner").When(Goal.Steps, "gully,spout", 2f)
                .Pays(r => r.training.Add((Skill.Construction, 3f)));
            Step(q, "unload", "TAKE THE WEIGHT OFF BEFORE YOU WORK UNDER IT: EMPTY DELLA'S PANTRY (JARS AND HER SPARE BRICKS: KEEP THE BRICKS)", "s04_kitchen")
                .When(Goal.Event, "s04:unloaded");
            Step(q, "shore", "SHORE UP THE CORNER: A POST OR A WALL STANDING ON THE GROUND UNDER IT AND REACHING THE KITCHEN FLOOR ([B])", "s04_corner")
                .When(Goal.Event, "s04:shored")
                .Pays(r => r.training.Add((Skill.Construction, 5f)));
            Step(q, "drain", "SEND THE WATER ELSEWHERE: DIG A DITCH AT THE FLAG (SHOVEL OR A DIGGER), OR MOVE THE RAIN BARREL THERE", "s04_ditch")
                .When(Goal.Ground, "s04_ditch", -0.35f, S04Ditch)
                .When(Goal.Event, "s04:barrel_moved", label: S04Barrel);
            Step(q, "access", "THE DOOR IS A CLIMB NOW: BUILD STEPS OR A RAMP UP TO IT", "s04_door")
                .When(Goal.Build, "ramp_wood|ramp_concrete|stairs|foundation_wood|foundation_stone|floor_wood", 2.6f);
            Step(q, "walk", "TRY IT: WALK INTO THE KITCHEN THROUGH THE DOOR", "s04_kitchen").When(Goal.Event, "s04:inside");
            Step(q, "tell", "TELL DELLA HER KITCHEN IS STAYING PUT", "della").Says("della", "s04_tell",
                "YOUR KITCHEN'S STAYING PUT. THE CORNER'S PROPPED, THE WATER GOES AROUND AND THERE ARE STEPS TO THE DOOR.",
                "...THAT'S A MASON'S JOB AND YOU DID IT LIKE ONE. TAKE THE STONE, I WON'T BE LAYING IT. AND HERE: MY PORCH. COMPACT, SENSIBLE, NEVER WALKED ANYWHERE.");
            Step(q, "plan", "DELLA DRAWS YOU HER PORCH PLAN", "della").When(Goal.Event, "s04:plan");
            q.reward.resources.Add((ResourceType.Stone, 16)); q.reward.resources.Add((ResourceType.Lime, 2)); q.reward.resources.Add((ResourceType.Wood, 10));
            q.reward.training.Add((Skill.Construction, 8f)); q.reward.flag = "s04_done";
            q.payoff = S04Default;
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S04(List<Member> into)
        {
            into.Add(new Member { key = "della", name = "DELLA SHAW", title = "RETIRED MASON", anchor = "della", temper = Temper.Proud, female = true, outfit = new[] { "sweater", "pants", "boots", "scarf" }, tool = "tool_claw_hammer" });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S04: a 4 x 4 m house on a gentle slope at the edge of the first town, turned so its kitchen half and door
        /// face downhill (local -X), the barrel at the uphill back corner, the ditch flag behind it.</summary>
        static partial void Anchors_S04(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tc = new Vector3(town.pos.x, 0f, town.pos.y);
            float H(Vector3 p) => world.Sample(p.x, p.z).height;
            void Put(string k, Vector3 p, float y) { p.y = H(p); at[k] = p; yaw[k] = y; }
            bool Spare(Vector3 p, float d)
            {
                foreach (var kv in at) if (new Vector2(kv.Value.x - p.x, kv.Value.z - p.z).magnitude < d) return false;
                return true;
            }
            float Turn(Vector3 p)
            {
                var gr = new Vector2(H(p + new Vector3(3f, 0f, 0f)) - H(p - new Vector3(3f, 0f, 0f)), H(p + new Vector3(0f, 0f, 3f)) - H(p - new Vector3(0f, 0f, 3f)));
                var d = gr.sqrMagnitude > 0.0004f ? -gr.normalized : new Vector2(tc.x - p.x, tc.z - p.z).normalized;   // downhill (or toward town on the flat)
                return Mathf.Atan2(d.y, -d.x) * Mathf.Rad2Deg;                                                          // local -X points downhill
            }
            Vector3 house = Vector3.zero; bool ok = false;
            for (int pass = 0; pass < 3 && !ok; pass++)
                for (float rr = town.radius + 25f; rr <= town.radius + 190f && !ok; rr += 10f)
                    for (int k = 0; k < 36 && !ok; k++)
                    {
                        float ang = (k * 10f + 95f) * Mathf.Deg2Rad;
                        var p = tc + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * rr;
                        if (!Open(world, p) || !Spare(p, pass < 2 ? 45f : 28f)) continue;
                        if (pass < 2 && !Level(world, p, 4f, pass == 0 ? 1.0f : 1.4f)) continue;
                        var q = Quaternion.Euler(0f, Turn(p), 0f);
                        bool clear = true;
                        foreach (var l in new[] { new Vector3(-4f, 0f, -4f), new Vector3(4f, 0f, -4f), new Vector3(-4f, 0f, 4f), new Vector3(4f, 0f, 4f), new Vector3(-5.5f, 0f, 1f), new Vector3(3.4f, 0f, -5f), new Vector3(1.5f, 0f, 6f) })
                            if (!Open(world, p + q * l)) { clear = false; break; }
                        if (!clear && pass < 2) continue;
                        house = p; ok = true;
                    }
            if (!ok) house = tc + new Vector3(Mathf.Sin(95f * Mathf.Deg2Rad), 0f, Mathf.Cos(95f * Mathf.Deg2Rad)) * (town.radius + 70f);
            float y = Turn(house);
            var r = Quaternion.Euler(0f, y, 0f);
            Put("s04_house", house, y);
            Put("s04_corner", house + r * new Vector3(-2f, 0f, -2f), y);
            Put("s04_spout", house + r * new Vector3(2.8f, 0f, -2.9f), y);
            Put("s04_kitchen", house + r * new Vector3(-1f, 0f, 0.6f), y);
            Put("s04_door", house + r * new Vector3(-3.3f, 0f, 1f), y);
            Put("s04_ditch", house + r * new Vector3(3.4f, 0f, -5f), y);
            Put("della", house + r * new Vector3(1.5f, 0f, 6f), y + 180f);                   // in the front yard, looking at the house
            Clearing("s04_house", 8f); Clearing("s04_ditch", 3f); Clearing("della", 4f); Clearing("s04_door", 4f);
        }
    }
}
