using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Visual pass 2026-10-12: living faces (HumanFace blinks and eye look-at) and varied, worn NPC outfits
    /// (NpcLooks).</summary>
    public static class FaceScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new FaceAlive();
            yield return new OutfitVariety();
        }
    }

    /// <summary>A townsperson close to the player, half turned away: within a few seconds they blink and their eyes
    /// (ahead of the head) settle on the player.</summary>
    class FaceAlive : Scenario
    {
        public override string Id => "character.face";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var pt = g.Player.transform;
            var at = pt.position + pt.forward * 2.2f + pt.right * 0.6f;
            at.y = DeformableTerrain.Instance.Height(at.x, at.z) + 0.1f;
            float yaw = pt.eulerAngles.y + 180f + 45f;                                     // the player 45° off their nose
            var npc = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:face", NpcRole.Resident, 5150), at, yaw, null, g.propMaterial);
            c.Fixture("a resident 2.3 m from the player, turned 45° away");
            yield return null;
            var face = npc ? npc.Rig.GetComponent<HumanFace>() : null;
            if (!c.Check(face, "the rig has a HumanFace")) { if (npc) Object.Destroy(npc.gameObject); yield break; }
            c.Note(face.HDEyes ? "HD eye pivots" : face.VoxelLids ? "voxel eye lids" : "no eye geometry (logic only)");
            int total0 = HumanFace.TotalBlinks;
            float t0 = Time.time, best = 180f;
            bool sawOnTarget = false;
            while (Time.time - t0 < 12f && (face.Blinks < 1 || !sawOnTarget))
            {
                yield return null;
                if (!face) break;
                var to = g.Player.Eye.position - face.EyeCentre;
                float ang = Vector3.Angle(face.Gaze, to);
                best = Mathf.Min(best, ang);
                if (face.OnTarget && ang < 12f) sawOnTarget = true;
            }
            if (face)
            {
                c.Metric("blinks", face.Blinks, "");
                c.Metric("gaze_error", best, "deg");
                c.Check(face.Blinks >= 1 && HumanFace.TotalBlinks > total0, $"blinked ({face.Blinks} in {Time.time - t0:0.0} s)");
                c.Check(sawOnTarget, $"eyes on the player (best {best:0.0}° off, eye yaw {face.EyeYaw:0.0}°)");
                var headFwd = npc.Rig.Head.forward; var to = g.Player.Eye.position - face.EyeCentre; headFwd.y = to.y = 0f;
                c.Check(Vector3.Angle(headFwd, to) < 40f, $"the head turned most of the way ({Vector3.Angle(headFwd, to):0}° left for the eyes)");
            }
            if (npc) Object.Destroy(npc.gameObject);
        }
    }

    /// <summary>200 generated people: at least 50 garments in use across them, nobody wears the same id or two things
    /// in one slot, some undercuts, road folk wear worn clothes, townsfolk mended ones; the same seed gives the same
    /// person.</summary>
    class OutfitVariety : Scenario
    {
        public override string Id => "npc.outfit_variety";
        public override bool NeedsWorld => false;

        public override IEnumerator Run(ScenarioContext c)
        {
            var roles = (NpcRole[])System.Enum.GetValues(typeof(NpcRole));
            var kinds = NpcLore.Trades.Select(t => t.id).ToArray();
            var used = new HashSet<string>();
            int dupIds = 0, dupSlots = 0, undercut = 0, unstable = 0, wornRoad = 0, road = 0, shabbyTown = 0, town = 0;
            for (int i = 0; i < 200; i++)
            {
                var role = roles[i % roles.Length];
                string kind = role == NpcRole.Shopkeeper || role == NpcRole.Stallkeeper || role == NpcRole.Trader ? kinds[(i / roles.Length) % kinds.Length]
                    : role == NpcRole.Packer ? "pack" : null;
                int seed = unchecked(i * 7919 + 17);
                var p = NpcProfile.Make("test:look" + i, role, seed, kind);
                foreach (var id in p.outfit) used.Add(id);
                if (p.outfit.Distinct().Count() != p.outfit.Count) dupIds++;
                var slots = p.outfit.Select(id => ClothingLibrary.Get(id)?.slot).ToList();
                if (slots.Distinct().Count() != slots.Count) dupSlots++;
                if (p.look.hair == HairStyle.Undercut) undercut++;
                var q = NpcProfile.Make("test:look" + i, role, seed, kind);
                if (!q.outfit.SequenceEqual(p.outfit) || q.look.hair != p.look.hair || q.wear != p.wear || q.tool != p.tool) unstable++;
                if (role == NpcRole.Wanderer || p.Raider) { road++; if (p.outfit.Any(id => p.Condition(id) < 0.6f)) wornRoad++; }
                if (role == NpcRole.Resident || role == NpcRole.Shopkeeper) { town++; if (p.wear < 0.7f) shabbyTown++; }
            }
            var never = ClothingLibrary.All.Select(d => d.id).Where(id => !used.Contains(id)).ToList();
            c.Metric("garments_used", used.Count, "ids");
            c.Note("never worn by these 200: " + (never.Count == 0 ? "-" : string.Join(", ", never)));
            c.Check(used.Count >= 50, $"{used.Count} distinct garments in use (≥ 50)");
            c.Check(dupIds == 0, $"no outfit lists an id twice ({dupIds})");
            c.Check(dupSlots == 0, $"one garment per slot ({dupSlots} outfits clash)");
            c.Check(undercut > 0, $"some undercuts ({undercut})");
            c.Check(unstable == 0, $"the same seed gives the same person ({unstable} differ)");
            c.Check(road > 0 && wornRoad * 2 >= road, $"road folk wear worn clothes ({wornRoad}/{road})");
            c.Check(town > 0 && shabbyTown == 0, $"townsfolk keep theirs mended ({shabbyTown}/{town} below 0.7)");
            yield break;
        }
    }
}
