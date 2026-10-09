using System.Collections;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>The weight of a blow: the sledgehammer plays its two-handed clip with the left hand on the handle, the
    /// head comes down in front of the body at the strike; a blow that lands holds a beat (hit-stop) where a whiff runs
    /// straight through; a raider struck by it staggers back a step; a solid hit during someone's wind-up spoils their
    /// swing (no damage reaches the player).</summary>
    class MeleeFeel : Scenario
    {
        public override string Id => "combat.melee_feel";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            if (!TestWorld.Pad(8f, 30f, out var pad, out var dir)) { c.Block("no open pad"); yield break; }
            yield return PH.Go(c, pad, PH.Yaw(dir), "an open pad");
            g.Stats.stamina = g.Stats.MaxStamina;
            if (!g.Player.Tool || g.Player.Tool.id != MadMax.Items.ItemIds.Sledgehammer)                // the starting kit may hold it already
            {
                PH.Grant(c, g, MadMax.Items.ItemIds.Sledgehammer, 1);
                g.Player.Equip(ToolLibrary.Create(MadMax.Items.ItemIds.Sledgehammer, g.propMaterial));
            }
            yield return null;
            var tool = g.Player.Tool;
            if (!c.Check(tool && tool.SecondGrip, "the sledgehammer in hand, held by a long handle")) yield break;
            yield return new WaitForSeconds(0.6f);

            // ---- a whiff: clip, both hands on the handle, the head lands ahead and low
            var P = g.Player;
            var handL = P.Rig.Bone(BodyPart.HandL);
            float worstGrip = 0f, clipFrames = 0f, frames = 0f, headAhead = 0f, headLow = 9f;
            bool shotTop = false, shotHit = false;
            float t0 = Time.time;
            P.Attack(false);
            while (P.Swinging && Time.time - t0 < 3f)
            {
                float prog = P.SwingProgress;
                frames++;
                if (P.Animator.ActionClip == "sledge") clipFrames++;
                if (prog > 0.12f && prog < 0.9f) worstGrip = Mathf.Max(worstGrip, OffHandle(tool, handL));
                if (prog >= tool.strikeAt && headLow > 8f)
                {
                    var head = tool.transform.position + tool.transform.TransformVector(Vector3.down * 1.1f);
                    var rel = head - P.transform.position;
                    headAhead = Vector3.Dot(rel, P.transform.forward); headLow = rel.y;
                }
                if (!shotTop && prog > 0.45f) { shotTop = true; c.Screenshot("sledge_top"); yield return null; continue; }
                if (!shotHit && prog > tool.strikeAt + 0.02f) { shotHit = true; c.Screenshot("sledge_impact"); yield return null; continue; }
                yield return null;
            }
            float whiff = Time.time - t0;
            if (HumanClips.Enabled) c.Check(clipFrames >= frames * 0.6f, $"the swing plays the sledge clip ({clipFrames}/{frames} frames)");
            c.Metric("left_hand_off_handle", worstGrip * 100f, "cm");
            c.Check(worstGrip < 0.08f, $"the left hand stays on the handle through the swing (worst {worstGrip * 100f:0.0} cm off)");
            c.Check(headAhead > 0.6f && headLow < 1.1f, $"the head comes down in front of the body at the strike ({headAhead:0.00} m ahead, {headLow:0.00} m up)");

            // ---- a blow that lands: hit-stop, the raider staggers back
            yield return new WaitForSeconds(0.4f);
            var me = P.transform.position;
            var foe = Dummy(g, "test:melee:feel", 4242, me + P.transform.forward * 1.25f, P.transform.eulerAngles.y + 180f);
            c.Fixture("a stallkeeper standing 1.25 m ahead (stands still until struck)");
            yield return new WaitForSeconds(0.5f);
            var start = foe.transform.position;
            float h0 = foe.Health;
            bool staggered = false;
            g.Stats.stamina = g.Stats.MaxStamina;
            t0 = Time.time;
            P.Attack(false);
            while ((P.Swinging || Time.time - t0 < 1.4f) && Time.time - t0 < 3f)
            {
                staggered |= foe.Staggering || foe.Reacting == "stagger";
                yield return null;
            }
            float pushed = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(foe.transform.position.x, 0f, foe.transform.position.z));
            c.Metric("swing_whiff", whiff, "s"); c.Metric("knockback", pushed, "m");
            if (!c.Check(foe.Health < h0, $"the blow lands ({Mathf.Max(0f, foe.Health):0}/{h0:0})")) yield break;
            c.Check(staggered && pushed > 0.25f, $"the raider staggers back ({pushed:0.00} m)");

            // ---- hit-stop: the same blow against a raider takes longer than a whiff
            Dummy(g, "test:melee:feel2", 4244, me + P.transform.forward * 1.25f, P.transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(0.5f);
            g.Stats.stamina = g.Stats.MaxStamina;
            t0 = Time.time;
            P.Attack(false);
            yield return PH.Until(() => !P.Swinging, 3f);
            float landed = Time.time - t0;
            c.Metric("swing_landed", landed, "s"); c.Metric("hit_stop", P.LastHitStop, "s");
            c.Check(P.LastHitStop >= 0.05f && landed >= whiff - 0.05f, $"the landed blow holds a beat ({P.LastHitStop * 1000f:0} ms; swing {landed:0.00} s vs whiff {whiff:0.00} s)");

            // ---- interrupting a wind-up
            var armed = PH.Raider(g, "test:melee:windup", 4243, me - Vector3.Cross(Vector3.up, P.transform.forward) * 1.0f + P.transform.forward * 0.6f, 0f, true, false, false);
            c.Fixture("an armed, calm raider beside the player, made to wind up a blow");
            yield return new WaitForSeconds(0.4f);
            float hp = g.Stats.health;
            armed.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(P.transform.position - armed.transform.position, Vector3.up));
            armed.BeginSwing();
            yield return new WaitForSeconds(0.05f);
            armed.ApplyHit(armed.transform.position + Vector3.up * 1.2f, -armed.transform.forward, 0.6f, 0.15f, null);   // not the player's: they stay calm
            c.Check(!armed.Swinging, "a solid hit during the wind-up spoils the swing");
            yield return new WaitForSeconds(1.2f);
            c.Check(g.Stats.health >= hp, $"the spoiled blow never lands ({g.Stats.health:0}/{hp:0})");
        }

        static MadMax.Npc.Npc Dummy(WastelandGame g, string id, int seed, Vector3 at, float yaw)
        {
            var p = MadMax.Npc.NpcProfile.Make(id, MadMax.Npc.NpcRole.Stallkeeper, seed);
            p.tool = null;
            var h = MadMax.World.DeformableTerrain.Instance ? MadMax.World.DeformableTerrain.Instance.Height(at.x, at.z) : at.y;
            return MadMax.Npc.Npc.Spawn(p, new Vector3(at.x, h + 0.1f, at.z), yaw, null, g.propMaterial);
        }

        /// <summary>How far the left palm is from the sledge's handle (grip to the head).</summary>
        static float OffHandle(HandTool tool, Transform handL)
        {
            var t = tool.transform;
            var a = t.position; var b = a + t.TransformVector(Vector3.down * 0.96f);
            var palm = handL.position + handL.TransformVector(new Vector3(0f, -0.055f, 0f));
            var ab = b - a;
            float u = Mathf.Clamp01(Vector3.Dot(palm - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(palm, a + ab * u);
        }
    }
}
