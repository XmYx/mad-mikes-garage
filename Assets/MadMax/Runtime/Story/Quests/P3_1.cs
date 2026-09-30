using System.Collections.Generic;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // P3.1 THE SEA DOES NOT KEEP RECEIPTS (storyline §11, personal chain P3; sandbox too): Halvard Ness's trawler loses
    // half its catch every night and his deckhand Peg blames a sea monster. Look in the hold (a split bilge seam), tell
    // him, then mend it: weld the hull, hand him a repair kit, or pay the yard. Then prove it: trawl a catch that stays
    // in the hold, or let the crew try her that night.
    public static partial class StoryLibrary
    {
        static partial void Author_P3_1(QuestDef q)
        {
            q.build = Build.Playable;
            foreach (var a in new[] { "p3_harbour", "p3_trawler", "p3_shallow", "p3_deep", "p3_heir" }) if (!Anchors.Contains(a)) Anchors.Add(a);
            q.offerSay = "YOUR CREW LOOKS LIKE IT SAW A GHOST.";
            q.offerReply = "WORSE. WE FILL THE HOLD AT DUSK AND BY DAWN HALF OF IT'S GONE. PEG SAYS SOMETHING WITH ARMS COMES UP THE HULL AT NIGHT. " +
                           "I SAY NOBODY HUNTS A MONSTER ON MY MONEY TILL SOMEBODY'S LOOKED AT MY BOAT.";
            q.hook = "HALVARD NESS'S TRAWLER LOSES HALF ITS CATCH EVERY NIGHT. HIS DECKHAND BLAMES A SEA MONSTER.";
            Step(q, "peg", "OPTIONAL: HEAR PEG'S MONSTER", "p3_harbour")
                .Says("p3_peg", "p3_1_monster", "TELL ME ABOUT THE MONSTER.",
                    "ARMS LIKE HAWSERS. EYES LIKE LAMPS. TAKES ONLY THE BIG ONES. ...I NEVER SAW IT AS SUCH. BUT THE FISH ARE GONE, AREN'T THEY?").Optional();
            Step(q, "look", "LOOK IN THE TRAWLER'S HOLD (MOORED OFF THE SHORE: SWIM OUT OR TAKE A BOAT)", "p3_trawler").When(Goal.Reach, "p3_trawler", 6.5f)
                .Pays(r => r.training.Add((Skill.Salvaging, 3f)));
            Step(q, "diagnose", "TELL HALVARD WHAT YOU FOUND", "p3_harbour")
                .Says("trawler", "p3_1_seam", "IT'S NOT A MONSTER. THE HOLD'S BILGE SEAM IS SPLIT; THE CATCH WASHES OUT WITH THE BILGE.",
                    "...A SEAM. TWENTY YEARS I'VE PUMPED THAT BILGE AND NEVER LOOKED. MEND IT AND I'LL BUY PEG A PINT FOR HER MONSTER.");
            Step(q, "repair", "MEND THE SEAM: WELD THE HULL (WELDER), HAND HALVARD A REPAIR KIT, OR PAY THE BOATYARD", "p3_trawler")
                .When(Goal.Event, "p3_1:welded", label: "WELDED THE SEAM")
                .Says("trawler", "p3_1_kit", "HERE'S A REPAIR KIT. PATCH IT FROM INSIDE.", "A PATCH WILL HOLD TILL WE HAUL HER OUT. GOOD ENOUGH, AND CHEAPER THAN A MONSTER.").Needs("use_repair_kit")
                .Says("trawler", "p3_1_yard", "HAVE THE BOATYARD DO IT. I'LL PAY.", "THIRTY? YOU'RE A SOFT TOUCH. THE YARD WILL HAVE HER TIGHT BY MORNING.")
                .Pays(r => r.training.Add((Skill.Mechanics, 4f)));
            q.steps[q.steps.Count - 1].any[2].price = 30;
            Step(q, "proof", "PROVE IT: TAKE HER OUT AND TRAWL A CATCH THAT STAYS IN THE HOLD ([1] AT THE HELM, SLOW, OVER DEEP WATER), OR TELL HALVARD TO TRY TONIGHT", "p3_trawler")
                .When(Goal.Event, "p3_1:catch", label: "TRAWLED A CATCH")
                .Says("trawler", "p3_1_tonight", "SHE'S TIGHT. TRY HER TONIGHT.", "WE WILL. IF THE HOLD'S STILL FULL AT DAWN, PEG OWES ME A PINT AND AN APOLOGY TO THE SEA.")
                .Pays(r => r.training.Add((Skill.Survival, 3f)));
            q.reward.scrap = 30; q.reward.items.Add(("use_o2_bottle", 1)); q.reward.training.Add((Skill.Salvaging, 5f)); q.reward.flag = "p3_seam";
            q.payoff = "NO MONSTER: A SPLIT SEAM AND A LOT OF FISH GOING HOME BY THEMSELVES. PEG IS SULKING.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_P3_1(List<Member> into)
        {
            into.Add(new Member { key = "trawler", name = "HALVARD NESS", title = "TRAWLER SKIPPER", anchor = "p3_harbour", temper = Temper.Gruff,
                outfit = new[] { "sweater", "pants", "boots", "beanie" } });
            into.Add(new Member { key = "p3_peg", name = "PEG ORMSBY", title = "DECKHAND", anchor = "p3_harbour", temper = Temper.Nervous, female = true,
                outfit = new[] { "hoodie", "jeans", "boots", "bandana" }, present = () => Story.StateOf("P3.1") == Story.State.Active });
        }
    }

    public static partial class StoryAnchors
    {
        // P3: the coast nearest the first town: a dry shore (the harbour, where the crew stands), the trawler moored in
        // 1.6-3.2 m of water, the owners' granddaughter a little inland, the Alba's cargo in wading depth and the
        // Meridian in 5.5-10 m. Never in the date-line band or the fallout.
        static partial void Anchors_P3_1(WorldGen world, Settlement town)
        {
            world.Yard(out var o, out _, out _);
            var c0 = town != null ? town.pos : new Vector2(o.x, o.z);
            var taken = PersonalMoorings(world);
            bool found = false; Vector2 moor = default, shore = default;
            for (float r = 60f; r <= 5200f && !found; r += 30f)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(r * 2f * Mathf.PI / 40f), 24, 820);
                for (int a = 0; a < n && !found; a++)
                {
                    float ang = (a + 0.5f) / n * Mathf.PI * 2f;
                    var p = c0 + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    if (!PersonalSea(world, p) || !world.Ocean(p.x, p.y)) continue;
                    float depth = WorldGen.SeaLevel - world.BaseHeight(p.x, p.y);
                    if (depth < 1.6f || depth > 3.2f) continue;
                    bool clash = false;
                    foreach (var t in taken) if ((t - p).sqrMagnitude < 45f * 45f) { clash = true; break; }
                    if (clash) continue;
                    var back = (c0 - p).normalized;
                    for (float s = 6f; s <= 42f; s += 3f)
                    {
                        var q = p + back * s;
                        var smp = world.Sample(q.x, q.y);
                        if (!float.IsNaN(smp.water) || smp.height < WorldGen.SeaLevel + 1.9f || smp.feature != 0 || world.SiteAt(q.x, q.y) != null) continue;
                        if (!PersonalFree(new Vector3(q.x, 0f, q.y), 40f)) break;
                        shore = q; moor = p; found = true; break;
                    }
                }
            }
            if (!found) { shore = c0 + new Vector2(0f, (town != null ? town.radius : 40f) + 60f); moor = shore + new Vector2(0f, 30f); }
            var seaDir = (moor - shore).normalized;
            float faceSea = Mathf.Atan2(seaDir.x, seaDir.y) * Mathf.Rad2Deg;
            PersonalSet(world, "p3_harbour", new Vector3(shore.x, 0f, shore.y), faceSea);
            PersonalSet(world, "p3_trawler", new Vector3(moor.x, 0f, moor.y), faceSea + 90f);
            at["p3_trawler"] = new Vector3(moor.x, WorldGen.SeaLevel, moor.y);
            Clearing("p3_harbour", 10f);

            // Iris Marrow: dry level ground 25-80 m inland of the harbour
            var inland = new Vector3(-seaDir.x, 0f, -seaDir.y);
            var heir = new Vector3(shore.x, 0f, shore.y) + inland * 30f; bool hs = false;
            for (float d = 25f; d <= 80f && !hs; d += 5f)
                for (int k = 0; k < 7 && !hs; k++)
                {
                    var p = new Vector3(shore.x, 0f, shore.y) + Quaternion.Euler(0f, (k - 3) * 20f, 0f) * inland * d;
                    var smp = world.Sample(p.x, p.z);
                    if (!float.IsNaN(smp.water) || smp.feature != 0 || world.SiteAt(p.x, p.z) != null || world.RiverAt(p.x, p.z, out _, out _, out _) || !Level(world, p, 2.5f, 1.2f) || !PersonalFree(p, 18f)) continue;
                    heir = p; hs = true;
                }
            PersonalSet(world, "p3_heir", heir, faceSea);
            Clearing("p3_heir", 6f);

            // the wrecks: the nearest wading-depth sea (0.6-1.1 m) and the nearest 5.5-10 m water to the mooring
            PersonalSet(world, "p3_shallow", PersonalDepth(world, moor, 30f, 450f, 0.6f, 1.1f, out var shallow) ? shallow : new Vector3(moor.x - seaDir.x * 12f, 0f, moor.y - seaDir.y * 12f), faceSea);
            PersonalSet(world, "p3_deep", PersonalDepth(world, moor, 70f, 800f, 5.5f, 10f, out var deep) ? deep : new Vector3(moor.x + seaDir.x * 90f, 0f, moor.y + seaDir.y * 90f), faceSea);
        }

        /// <summary>Where the game moors its found boats (WastelandGame.Boats: the start skiff and one per coastal
        /// settlement), so the trawler doesn't share a berth.</summary>
        static List<Vector2> PersonalMoorings(WorldGen w)
        {
            var l = new List<Vector2>();
            void Moor(Vector2 c, float reach)
            {
                for (float r = 20f; r <= reach; r += 12f)
                    for (int a = 0; a < 36; a++)
                    {
                        float ang = a * Mathf.PI * 2f / 36f;
                        var p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                        if (!w.Ocean(p.x, p.y) || w.NaturalBiome(p.x, p.y) == Biome.Nuclear) continue;
                        float depth = WorldGen.SeaLevel - w.BaseHeight(p.x, p.y);
                        if (depth < 1.4f || depth > 3.5f) continue;
                        l.Add(p);
                        return;
                    }
            }
            Moor(Vector2.zero, 600f);
            foreach (var st in w.settlements) Moor(st.pos, st.radius + 220f);
            return l;
        }

        /// <summary>Clear of the date-line band, the poles and the fallout.</summary>
        static bool PersonalSea(WorldGen w, Vector2 p) =>
            Mathf.Abs(WorldGen.WrapX(p.x)) < WorldGen.HalfX - WorldGen.Meridian - 20f && Mathf.Abs(WorldGen.Latitude(p.y)) < 66f && w.NaturalBiome(p.x, p.y) != Biome.Nuclear;

        /// <summary>The nearest sea point to <paramref name="c"/> (rings of <paramref name="r0"/>..<paramref name="r1"/> m)
        /// whose depth under sea level is within [lo, hi].</summary>
        static bool PersonalDepth(WorldGen w, Vector2 c, float r0, float r1, float lo, float hi, out Vector3 spot)
        {
            for (float r = r0; r <= r1; r += 10f)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(r * 2f * Mathf.PI / 14f), 16, 360);
                for (int a = 0; a < n; a++)
                {
                    float ang = (a + 0.25f) / n * Mathf.PI * 2f;
                    var p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    if (!w.Ocean(p.x, p.y) || !PersonalSea(w, p)) continue;
                    float bd = WorldGen.SeaLevel - w.BaseHeight(p.x, p.y);
                    if (bd < lo - 0.6f || bd > hi + 0.6f) continue;
                    var s = w.Sample(p.x, p.y);
                    float depth = WorldGen.SeaLevel - s.height;
                    if (float.IsNaN(s.water) || depth < lo || depth > hi) continue;
                    spot = new Vector3(p.x, 0f, p.y);
                    return true;
                }
            }
            spot = Vector3.zero;
            return false;
        }
    }
}
