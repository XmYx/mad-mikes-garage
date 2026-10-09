using System.Collections;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>At rest nothing passes through the body: with each kind of tool in hand (its hold and its fidget) and with
    /// empty hands (every idle variant), the arms and the held tool stay out of the torso, hips, thighs and head. Measured
    /// on the composed pose (clips + procedural layers + IK) against the body shapes (<see cref="HumanDesign.Sdf"/>) at
    /// the player's build; a third-person shot from the front per hold.</summary>
    class IdleClearance : Scenario
    {
        public override string Id => "anim.idle_clearance";
        public override float Timeout => 200f;

        static readonly string[] Tools =
        {
            "tool_sledgehammer", "tool_pickaxe", "tool_shovel", "tool_axe", "tool_spear", MadMax.Items.ItemIds.Cutter, "tool_torch",
            "tool_flashlight", "tool_lantern", MadMax.Items.ItemIds.Shotgun, "tool_bolt_rifle", "tool_revolver", MadMax.Items.ItemIds.Machete,
            "tool_knife", MadMax.Items.ItemIds.Wrench, "tool_crowbar", "tool_extinguisher", "tool_fishing_rod",
        };
        const float Allowed = 0.01f;                     // 1 cm: skin contact, not passing through

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            if (!TestWorld.Pad(6f, 30f, out var pad, out var dir)) { c.Block("no open pad"); yield break; }
            yield return PH.Go(c, pad, PH.Yaw(dir), "an open pad");
            var P = g.Player;
            var rig = g.cameraRig;
            var keep = rig ? rig.mode : ViewMode.Isometric;
            if (rig) { rig.mode = ViewMode.ThirdPerson; rig.LookToward(Quaternion.Euler(0, 150f, 0) * P.transform.forward); }
            c.Fixture("third-person camera turned to face the player (three-quarter front)");
            if (!HumanClips.Enabled) { c.Block("clips disabled (procedural animation)"); yield break; }

            foreach (var id in Tools)
            {
                if (!ToolLibrary.Has(id)) { c.Fixture(id + " not in this build"); continue; }
                P.Equip(ToolLibrary.Create(id, g.propMaterial));
                var tool = P.Tool;
                yield return new WaitForSeconds(1.0f);                                          // the hold fades in
                if (tool is RangedTool gun) { yield return PH.Until(() => !gun.Reloading, 6f); yield return new WaitForSeconds(0.6f); }   // drawn guns chamber a round first
                string hold = ToolHolds.Clip(tool);
                float worst = 0f; string what = "";
                for (float t = 0f; t < 1.2f; t += Time.deltaTime) { Measure(P, tool, ref worst, ref what); yield return null; }
                c.Screenshot("hold_" + id.Replace("tool_", ""));
                yield return null;
                if (tool.HoldTwoHands)
                {
                    // both hands on it: the left palm on the line of the tool
                    var hl = P.Rig.Bone(BodyPart.HandL);
                    var palm = hl.position + hl.TransformVector(new Vector3(0f, -0.055f, 0f));
                    var t0 = tool.transform.position; var ax = tool.transform.TransformVector(Vector3.down).normalized;
                    float off = Vector3.Distance(palm, t0 + ax * Mathf.Max(0f, Vector3.Dot(palm - t0, ax)));
                    c.Metric("left_hand_off_" + id, off * 100f, "cm");
                    c.Check(off < 0.06f, $"{id}: the left hand holds it too ({off * 100f:0.0} cm off its line)");
                    if (rig) { rig.LookToward(Quaternion.Euler(0, 90f, 0) * P.transform.forward); yield return new WaitForSeconds(0.8f); c.Screenshot("side_" + id.Replace("tool_", "")); yield return null;
                               rig.LookToward(Quaternion.Euler(0, 150f, 0) * P.transform.forward); }
                }
                P.Animator.ForceFidget();
                yield return null;
                yield return null;
                string fidget = P.Animator.Fidget;
                for (float t = 0f; t < 4.2f && (t < 0.2f || P.Animator.Fidget != null); t += Time.deltaTime) { Measure(P, tool, ref worst, ref what); yield return null; }
                c.Metric("overlap_" + id, worst * 100f, "cm");
                c.Check(hold != null && worst <= Allowed,
                        $"{id} carried as {hold ?? "(no hold)"} (fidget {fidget ?? "none"}): worst overlap {worst * 100f:0.0} cm {what}");
            }

            P.Equip(null);
            yield return new WaitForSeconds(0.8f);
            foreach (var v in new[] { "idle", "idle_shift", "idle_look", "idle_stretch", "idle_neck", "idle_hips" })
            {
                P.Animator.ForceIdle(v);
                float worst = 0f; string what = "";
                bool shot = false;
                for (float t = 0f; t < 5.5f; t += Time.deltaTime)
                {
                    Measure(P, null, ref worst, ref what);
                    if (!shot && v != "idle" && t > 1.2f) { shot = true; c.Screenshot(v); }
                    yield return null;
                }
                c.Metric("overlap_" + v, worst * 100f, "cm");
                c.Check(worst <= Allowed, $"empty hands, {v} (playing {P.Animator.IdleClip}): worst overlap {worst * 100f:0.0} cm {what}");
            }
            if (rig) rig.mode = keep;
        }

        static readonly System.Collections.Generic.Dictionary<Mesh, Vector3[]> verts = new System.Collections.Generic.Dictionary<Mesh, Vector3[]>();
        static readonly BodyPart[] Limbs = { BodyPart.UpperArmL, BodyPart.UpperArmR, BodyPart.ForearmL, BodyPart.ForearmR, BodyPart.HandL, BodyPart.HandR };
        static readonly BodyPart[] Trunk = { BodyPart.Chest, BodyPart.Pelvis, BodyPart.ThighL, BodyPart.ThighR, BodyPart.Head };

        /// <summary>Deepest point of an arm or the held tool inside the torso / hips / thighs / head (metres).</summary>
        static void Measure(PlayerCharacter P, HandTool tool, ref float worst, ref string what)
        {
            var rig = P.Rig; var a = rig.appearance;
            float h = a.height, w = a.build;
            foreach (var limb in Limbs)
            {
                var b = rig.Bone(limb);
                if (!b || rig.BoneGone(limb)) continue;
                bool upper = limb == BodyPart.UpperArmL || limb == BodyPart.UpperArmR, hand = limb == BodyPart.HandL || limb == BodyPart.HandR;
                float len = (upper ? 0.29f : hand ? 0.09f : 0.25f) * h;
                for (int k = 1; k <= 5; k++)
                {
                    float u = k / 5f;
                    float r = hand ? 0.022f : (upper ? Mathf.Lerp(0.047f, 0.04f, u) : Mathf.Lerp(0.039f, 0.031f, u)) * w;
                    Test(rig, a, b.TransformPoint(new Vector3(0f, -u * len, 0f)), r, limb.ToString(), upper, ref worst, ref what);
                }
            }
            if (!tool) return;
            // the tool's own surface: every few vertices of its mesh (the grip, inside the hand, left out)
            var mf = tool.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return;
            if (!verts.TryGetValue(mf.sharedMesh, out var vs)) verts[mf.sharedMesh] = vs = mf.sharedMesh.vertices;
            for (int i = 0; i < vs.Length; i += 3)
            {
                if (vs[i].y > -0.12f) continue;
                Test(rig, a, tool.transform.TransformPoint(vs[i]), 0f, "tool", false, ref worst, ref what);
            }
        }

        static void Test(HumanRig rig, Appearance a, Vector3 p, float r, string name, bool shoulder, ref float worst, ref string what)
        {
            foreach (var t in Trunk)
            {
                if (shoulder && t == BodyPart.Chest || t == BodyPart.Head && name != "tool") continue;
                var tb = rig.Bone(t);
                if (!tb) continue;
                float d = r - HumanDesign.Sdf(t, tb.InverseTransformPoint(p), a);
                if (d > worst) { worst = d; what = name + " in " + t; }
            }
        }
    }
}
