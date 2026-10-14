using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Npc;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios for the HD human (HumanRig HD mode + HumanAnimator clips).</summary>
    public static class HDCharacterScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new HDCharacter();
        }
    }

    /// <summary>hd.character: the player and three NPCs (one woman) are built from the HD pack as skinned meshes on the
    /// HumanRig bones (every bone keeps its BodyPart name, every skin bone is one of them); NPCs switch LODs; an outfit
    /// change swaps the garment meshes and re-cuts the skin under them, a worn-out garment gets holes, first person keeps
    /// only the arms; the ragdoll throws an HD NPC and its skin follows the bones; the walk, run, idle, sit_drive and sit
    /// clips play (player on foot and at the wheel, a test body on a chair).</summary>
    class HDCharacter : Scenario
    {
        public override string Id => "hd.character";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var cat = HDCharacterCatalog.Instance;
            if (!cat) { c.Block("no HD character catalogue: export the character pack into Models/HD/character and run MadMax/HD/Build Character Catalog"); yield break; }
            if (!HDHuman.Enabled) { c.Block("HD humans are switched off (--voxel-humans / MadMax > Dev > Voxel Humans)"); yield break; }
            c.Metric("catalogue pieces", cat.pieces.Count, "pieces");
            c.Metric("body shapes", cat.shapes.Count, "shapes");
            c.Metric("clips", HumanClips.Count, "clips");
            yield return ItemsScenarios.OnFootAtPad(c, 8f);
            var player = g.Player;
            if (!player.Rig.IsHD) { player.RebuildBody(); yield return null; }
            c.Check(player.Rig.IsHD, $"the player is built from the HD pack (shape {player.Rig.HDShape}, {player.Rig.HDPieces.Count} pieces)");
            CheckRig(c, player.Rig, "player");

            // ---- three NPCs: a wanderer, a raider and a woman
            var npcs = new List<MadMax.Npc.Npc>();
            var profiles = new List<NpcProfile> { NpcProfile.Make("test:hd1", NpcRole.Wanderer, 1111), NpcProfile.Make("test:hd2", NpcRole.Raider, 2222) };
            for (int seed = 3000; seed < 3400; seed++)
            {
                var p = NpcProfile.Make("test:hd3", NpcRole.Resident, seed);
                if (NpcLore.Feminine(p.first)) { profiles.Add(p); break; }
            }
            var at = player.transform.position;
            var fwd = player.transform.forward; fwd.y = 0f; fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            for (int i = 0; i < profiles.Count; i++)
            {
                var pos = at + fwd * 3f + right * ((i - 1) * 1.2f);
                var t = MadMax.World.DeformableTerrain.Instance;
                if (t) pos.y = t.Height(pos.x, pos.z) + 0.05f;
                npcs.Add(MadMax.Npc.Npc.Spawn(profiles[i], pos, Quaternion.LookRotation(-fwd).eulerAngles.y, null, g.propMaterial));
            }
            c.Fixture($"{npcs.Count} NPCs spawned 3 m in front of the player");
            try
            {
                yield return new WaitForSeconds(0.6f);
                foreach (var n in npcs)
                {
                    c.Check(n.Rig.IsHD, $"{n.Profile.Name}: HD body (shape {n.Rig.HDShape})");
                    CheckRig(c, n.Rig, n.Profile.Name);
                    var lg = n.Rig.GetComponent<LODGroup>();
                    c.Check(lg && lg.enabled && lg.lodCount >= 2, $"{n.Profile.Name}: LOD group with {(lg ? lg.lodCount : 0)} levels");
                }
                bool femaleShapes = cat.shapes.Contains("F") || cat.shapes.Contains("F2");
                if (profiles.Count == 3 && femaleShapes) c.Check(npcs[2].Rig.HDShape == "F" || npcs[2].Rig.HDShape == "F2", $"the woman wears a female body ({npcs[2].Rig.HDShape})");
                else c.Note("no female body shape in the catalogue (or no feminine name found): woman check skipped");
                c.Screenshot("hd_character_npcs");
                yield return null;

                // ---- outfit change: swap the outer layer, the garment meshes and the skin cut follow
                var rig = player.Rig;
                var saved = rig.outfit.ToList();
                string add = new[] { "duster", "bomber", "hoodie", "vest" }.FirstOrDefault(id => !rig.outfit.Contains(id) && HDHuman.HasGarment(cat, rig.HDShape, id));
                if (add == null) c.Note("no other HD outer garment for this shape: outfit swap skipped");
                else
                {
                    var bodyBefore = BodyMesh(rig);
                    var slot = ClothingLibrary.Get(add).slot;
                    var removed = rig.outfit.Where(o => ClothingLibrary.Get(o)?.slot == slot).ToList();
                    rig.outfit.RemoveAll(o => ClothingLibrary.Get(o)?.slot == slot);
                    rig.outfit.Add(add);
                    player.RebuildBody();
                    yield return null;
                    bool has = rig.HDPieces.Any(k => cat.pieces.Any(p => p.key == k && p.garment == add));
                    bool gone = removed.All(r => !rig.HDPieces.Any(k => cat.pieces.Any(p => p.key == k && p.garment == r)));
                    c.Check(has, $"wearing {add}: its HD pieces are on the body");
                    c.Check(gone, $"the replaced {string.Join(",", removed)} pieces are gone");
                    c.Check(BodyMesh(rig) != bodyBefore, "the skin under the clothes was cut again for the new outfit");
                    CheckRig(c, rig, "player after the outfit change");
                    // worn out: holes
                    // through the game's wear table: RebuildBody hands the rig g.GarmentCondition
                    bool hadWear = g.ClothWear.TryGetValue(add, out float wearBefore);
                    g.ClothWear[add] = 0.8f;
                    player.RebuildBody();
                    yield return null;
                    c.Check(rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh && r.sharedMesh.name.EndsWith("_torn")), $"a worn-out {add} renders with holes");
                    if (hadWear) g.ClothWear[add] = wearBefore; else g.ClothWear.Remove(add);
                    rig.outfit.Clear(); rig.outfit.AddRange(saved);
                    player.RebuildBody();
                    yield return null;
                    c.Fixture("outfit and garment condition restored");
                }

                // ---- first person: the arms only
                rig.SetHeadVisible(false);                     // checked at once: CameraRig re-applies its own view next frame
                var smrs = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                bool headHidden = smrs.Where(r => r.name.StartsWith("hair") || r.name.StartsWith("Eye") || r.name == "brows").All(r => !r.enabled);
                bool armsOnly = smrs.Where(r => r.enabled).All(r => r.sharedMesh && r.sharedMesh.name.EndsWith("_arms"));
                string odd = string.Join(", ", smrs.Where(r => r.enabled && !(r.sharedMesh && r.sharedMesh.name.EndsWith("_arms"))).Select(r => r.name + "=" + (r.sharedMesh ? r.sharedMesh.name : "null")).Take(6));
                c.Check(headHidden && armsOnly && smrs.Any(r => r.enabled), "first person: head pieces hidden, the rest reduced to arms and hands" + (odd.Length > 0 ? " (still drawn: " + odd + ")" : ""));
                rig.SetHeadVisible(true);
                yield return null;

                // ---- clips: walk, run, idle on foot
                c.Check(HumanClips.Enabled, $"keyframed clips installed ({HumanClips.Count})");
                WastelandGame.ExternalInput = true;
                player.moveInput = Vector2.up; player.run = false;
                yield return new WaitForSeconds(1.2f);
                c.Check(player.Animator.UsingClips && player.Animator.BaseClip == "walk", $"walking plays the walk clip ({player.Animator.BaseClip}, {player.Velocity.magnitude:0.0} m/s)");
                player.run = true;
                yield return new WaitForSeconds(1.2f);
                c.Check(player.Animator.BaseClip == "run" || player.Animator.BaseClip == "sprint", $"running plays run / sprint ({player.Animator.BaseClip}, {player.Velocity.magnitude:0.0} m/s)");
                player.moveInput = Vector2.zero; player.run = false;
                yield return new WaitForSeconds(1.0f);
                c.Check(player.Animator.BaseClip != null && player.Animator.BaseClip.StartsWith("idle"), $"standing plays an idle clip ({player.Animator.BaseClip})");
                WastelandGame.ExternalInput = false;

                // ---- at the wheel: sit_drive
                VehicleDriver car = null; float best = 400f * 400f;
                foreach (var v in Object.FindObjectsByType<VehicleDriver>(FindObjectsSortMode.None))
                {
                    if (!v.driveable || v.aiDriven || !v.Body) continue;
                    float d = (v.transform.position - player.transform.position).sqrMagnitude;
                    if (d < best) { best = d; car = v; }
                }
                if (car && TestWorld.Pad(6f, out var pad))
                {
                    yield return TestWorld.Place(c, car, pad, fwd, 1f);
                    g.Enter(car);
                    yield return new WaitForSeconds(1.0f);
                    c.Check(player.SeatedIn == car && player.Animator.BaseClip == "sit_drive", $"at the wheel of {car.name.Replace("(Clone)", "")}: sit_drive ({player.Animator.BaseClip})");
                    g.Exit();
                    yield return new WaitForSeconds(0.6f);
                }
                else c.Note("no drivable vehicle within 400 m: drive check skipped");

                // ---- a chair: sit (a test body ticked by hand)
                var go = new GameObject("HDTestBody");
                go.transform.position = player.transform.position + right * 2f;
                var tr = go.AddComponent<HumanRig>();
                tr.material = g.propMaterial; tr.shareMeshes = true; tr.noStrands = true;
                tr.outfit.AddRange(ClothingLibrary.Starter);
                tr.Rebuild();
                var an = new HumanAnimator(tr);
                for (int i = 0; i < 40; i++) { an.Tick(1f / 30f, new HumanAnimator.State { sitting = true, lounging = true, grounded = true }); yield return null; }
                float thigh = Mathf.DeltaAngle(0f, tr.Bone(BodyPart.ThighL).localEulerAngles.x);
                c.Check(an.BaseClip == "sit" && thigh < -60f, $"seated on furniture: sit clip, thighs forward ({an.BaseClip}, thigh {thigh:0} deg)");
                Object.Destroy(go);

                // ---- ragdoll: an HD NPC thrown, the skin follows its bones
                var victim = npcs[0];
                var pelvis = victim.Rig.Bone(BodyPart.Pelvis);
                var p0 = pelvis.position; var r0 = pelvis.rotation;
                Ragdoll.For(victim.Rig).Go(fwd * 90f + Vector3.up * 30f, p0 + Vector3.up * 0.3f, Vector3.zero);
                yield return new WaitForSeconds(1.5f);
                var rd = victim.Rig.GetComponent<Ragdoll>();
                float moved = Vector3.Distance(pelvis.position, p0), turned = Quaternion.Angle(pelvis.rotation, r0);
                c.Check(rd && rd.Active && (moved > 0.2f || turned > 25f), $"ragdoll: the HD body went limp (pelvis moved {moved:0.00} m, turned {turned:0} deg)");
                var body = victim.Rig.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.name == "Body");
                c.Check(body && Vector3.Distance(body.bounds.center, pelvis.position) < 1.3f, "the skinned body follows the ragdoll bones");
                c.Screenshot("hd_character_ragdoll");
                yield return null;
            }
            finally
            {
                WastelandGame.ExternalInput = false;
                if (player) { player.moveInput = Vector2.zero; player.run = false; }
                foreach (var n in npcs) if (n) Object.Destroy(n.gameObject);
            }
        }

        static Mesh BodyMesh(HumanRig rig)
        {
            foreach (var r in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name == "Body") return r.sharedMesh;
            return null;
        }

        /// <summary>Bones keep their HumanRig names; every skinned mesh is bound to those bones (or the rig root).</summary>
        static void CheckRig(ScenarioContext c, HumanRig rig, string who)
        {
            bool named = true;
            var allowed = new HashSet<Transform> { rig.transform };
            foreach (BodyPart p in System.Enum.GetValues(typeof(BodyPart)))
            {
                var t = rig.bones.TryGetValue(p, out var b) ? b : null;
                named &= t && t.name == p.ToString();
                if (t) allowed.Add(t);
            }
            c.Check(named, $"{who}: all 15 bones named as HumanRig");
            var smrs = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool bound = smrs.Length >= 4;
            foreach (var r in smrs) foreach (var b in r.bones) bound &= b && allowed.Contains(b);
            c.Check(bound, $"{who}: {smrs.Length} skinned meshes, every bone one of the rig's");
            c.Check(rig.HDPieces.Any(k => k.EndsWith("/Body")), $"{who}: an HD body is worn");
            // facing: the eyes sit in front of the head joint and the toes in front of the ankles (rig space, +Z forward)
            float eyes = float.NaN, toes = float.NaN;
            var baked = new Mesh();
            foreach (var r in smrs)
            {
                if (r.name.Contains("__L")) continue;
                bool eye = r.name.StartsWith("Eye"), body = r.name == "Body";
                if (!eye && !body) continue;
                r.BakeMesh(baked, true);
                var vs = baked.vertices;
                if (eye && float.IsNaN(eyes))
                {
                    var head = rig.transform.InverseTransformPoint(rig.bones[BodyPart.Head].position);
                    var sum = Vector3.zero; foreach (var v in vs) sum += rig.transform.InverseTransformPoint(r.transform.TransformPoint(v));
                    eyes = sum.z / Mathf.Max(1, vs.Length) - head.z;
                }
                if (body)
                {
                    var foot = rig.transform.InverseTransformPoint(rig.bones[BodyPart.FootL].position);
                    double z = 0; int n = 0;
                    foreach (var v in vs)
                    {
                        var w = rig.transform.InverseTransformPoint(r.transform.TransformPoint(v));
                        if (w.y < foot.y && Mathf.Abs(w.x - foot.x) < 0.07f) { z += w.z - foot.z; n++; }
                    }
                    if (n > 0) toes = (float)(z / n);
                }
            }
            Object.Destroy(baked);
            c.Check(eyes > 0.004f, $"{who}: faces forward, eyes {eyes * 100f:0.0} cm in front of the head joint");
            c.Check(toes > 0.01f, $"{who}: toes {toes * 100f:0.0} cm in front of the left ankle");
        }
    }
}
