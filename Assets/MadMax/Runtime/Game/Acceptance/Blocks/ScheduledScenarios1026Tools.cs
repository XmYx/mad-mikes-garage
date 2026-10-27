using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Tool remodels 2026-10-26: the mirror (step up and face the glass, a snip and a puff of trimmings per
    /// change), the binoculars (raised to the eyes, two lenses on the HUD, a lens flash with the sun ahead that hostiles
    /// in view notice) and the crowbar on a door (a timed lever with sparks / splinters, the door jolting open or holding).</summary>
    public static class ScheduledScenarios1026Tools
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new MirrorPose();
            yield return new BinocularGlint();
            yield return new CrowbarDoor();
        }

        internal static IEnumerator OnFoot(WastelandGame g)
        {
            if (g.Menus.IsOpen) g.Menus.Close();
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
        }
    }

    /// <summary>A mirror on a post beside the player: [E] steps the player square in front of the glass and opens the
    /// mirror page without pausing; a haircut snips and drops trimmings in the old hair colour, a shave too; closing
    /// the page ends it.</summary>
    class MirrorPose : Scenario
    {
        public override string Id => "tools.mirror_pose";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ScheduledScenarios1026Tools.OnFoot(g);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var root = g.Build.Structures;
            var at = PH.Ground(me + fwd * 1.7f) + Vector3.up * 1.5f;
            // upright, glass toward the player (wall pieces: +Y out of the wall, +Z up it)
            var p = FurnitureLibrary.Spawn("mirror", root, root.InverseTransformPoint(at), Quaternion.Inverse(root.rotation) * Quaternion.LookRotation(Vector3.up, -fwd), g.propMaterial);
            if (!c.Check(p && p.GetComponent<Mirror>(), "a mirror stands in front of the player")) yield break;
            var m = p.GetComponent<Mirror>();
            yield return null;
            // start off to one side, turned away
            var start = PH.Ground(m.Centre - fwd * 1.1f + right * 0.5f);
            g.Player.Teleport(start, PH.Yaw(right));
            yield return new WaitForSeconds(0.3f);
            c.Note($"facing {g.MirrorFacing(m):0}° before");
            m.Use(g, false);
            yield return null;
            c.Check(g.Menus.AtMirror && g.MirrorInUse == m, "the mirror page is open");
            c.Check(g.MirrorFacing(m) < 10f, $"the player turned to the glass ({g.MirrorFacing(m):0}° off)");
            float stand = PH.Flat(g.Player.transform.position, m.Centre);
            c.Check(Mathf.Abs(stand - WastelandGame.MirrorStand) < 0.2f, $"stepped up to it ({stand:0.00} m from the glass)");
            c.Check(Time.timeScale > 0.5f, "the world keeps running at the mirror (time scale " + Time.timeScale + ")");
            var a = g.Player.Rig.appearance;
            var style = a.hair; int beard = a.beard;
            int trims = WastelandGame.MirrorTrims, bits = WastelandGame.MirrorBits;
            c.Check(g.Menus.Press("HAIR", 1), "HAIR changes");
            yield return new WaitForSeconds(0.25f);
            c.Check(a.hair != style && WastelandGame.MirrorTrims == trims + 1 && WastelandGame.LastMirrorTrim == "hair", "a haircut: one snip (" + a.hair + ")");
            c.Check(WastelandGame.MirrorBits > bits, $"trimmings fall ({WastelandGame.MirrorBits - bits} bits)");
            c.Screenshot("mirror_haircut");
            yield return null;
            c.Check(g.Menus.Press("BEARD", 1) && a.beard != beard && WastelandGame.LastMirrorTrim == "beard", "a shave / trim of the beard");
            c.Check(g.Menus.Press("HAIR COLOUR", 1) && WastelandGame.LastMirrorTrim == "colour", "a dye");
            c.Check(g.MirrorFacing(m) < 10f, "still facing the glass after the changes");
            g.Menus.Close();
            yield return null;
            c.Check(!g.Menus.AtMirror && g.MirrorInUse == null, "closed: away from the mirror");
            a.hair = style; a.beard = beard; g.Player.RebuildBody();
            Object.Destroy(p.gameObject);
        }
    }

    /// <summary>Binoculars up in third person: the arms raise them to the eyes (both hands near the head), the HUD
    /// shows the two lenses; with the sun ahead the lenses flash, with the sun behind they don't; a raider on foot in
    /// the field of view who sees the flash comes for the player from beyond the usual 60 m.</summary>
    class BinocularGlint : Scenario
    {
        public override string Id => "tools.binocular_glint";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ScheduledScenarios1026Tools.OnFoot(g);
            PH.Daylight(c);
            var keepTool = g.Player.Tool ? g.Player.Tool.id : null;
            g.Player.Equip(ToolLibrary.Create("tool_binoculars", g.propMaterial));
            var bin = g.Player.Tool as BinocularsTool;
            if (!c.Check(bin, "binoculars in hand")) yield break;
            var mode = g.cameraRig ? g.cameraRig.mode : ViewMode.ThirdPerson;
            if (g.cameraRig) g.cameraRig.mode = ViewMode.ThirdPerson;
            var fwd = ScheduledScenarios1016.Fwd(g);
            if (g.cameraRig) g.cameraRig.LookToward(fwd);
            BinocularsTool.ForceLook = true;
            WastelandGame.GlintSunOverride = -fwd + Vector3.up * 0.3f;                          // sun behind: no flash
            yield return new WaitForSeconds(0.8f);
            c.Check(BinocularsTool.Looking && bin.RaisedNow && bin.Hold == null && bin.IdlePose.HasValue, "looking: raised to the eyes (the raised pose, not the side hold)");
            var rig = g.Player.Rig;
            var head = rig.Head.position;
            float hr = Vector3.Distance(rig.Bone(BodyPart.HandR).position, head), hl = Vector3.Distance(rig.Bone(BodyPart.HandL).position, head);
            c.Check(hr < 0.45f && hl < 0.45f, $"both hands up at the face (right {hr:0.00} m, left {hl:0.00} m from the head)");
            c.Check(PixelHud.LensMaskShown, "the HUD shows the two lenses");
            c.Screenshot("binoculars_raised");
            yield return null;
            int glints = WastelandGame.BinocularGlints;
            yield return new WaitForSeconds(1.5f);
            c.Check(WastelandGame.GlintStrength < 0.05f && WastelandGame.BinocularGlints == glints, $"sun behind: no flash (strength {WastelandGame.GlintStrength:0.00})");
            var look = g.BinocularLook();
            WastelandGame.GlintSunOverride = new Vector3(look.x, 0f, look.z).normalized + Vector3.up * 0.3f;   // low sun ahead
            yield return PH.Until(() => WastelandGame.BinocularGlints > glints, 3f);
            c.Check(WastelandGame.GlintStrength > 0.5f && WastelandGame.BinocularGlints > glints, $"sun ahead: the lenses flash (strength {WastelandGame.GlintStrength:0.00}, {WastelandGame.BinocularGlints - glints} flashes)");
            c.Screenshot("binoculars_glint");
            yield return null;
            // a raider 130 m off in the view: beyond the 60 m he would see you at, but he catches the flash
            var me = g.Player.transform.position;
            var flat = new Vector3(look.x, 0f, look.z).normalized;
            BinocularsTool.ForceLook = false;                                                   // no random flashes while setting up
            WastelandGame.GlintSunOverride = null;
            yield return null;
            var raider = PH.Raider(g, "t:glint_raider", 4711, me + flat * 130f, PH.Yaw(-flat), false, false);
            yield return new WaitForSeconds(1f);
            c.Check(raider && raider.Hostile && raider.mode != MadMax.Npc.Npc.Mode.Fight, "a hostile raider 130 m off hasn't seen the player");
            int seen = g.GlintSeen(me, flat, 1f, true);
            c.Check(seen >= 1 && raider.GlintAlert, $"he sees the flash ({seen} noticed, last {WastelandGame.LastGlintSeenBy})");
            yield return PH.Until(() => raider && raider.mode == MadMax.Npc.Npc.Mode.Fight, 4f);
            c.Check(raider && raider.mode == MadMax.Npc.Npc.Mode.Fight, "and comes for the player");
            int off = g.GlintSeen(me, -flat, 1f, true);
            c.Check(off == 0, "looking the other way: nobody in that field");
            if (raider) Object.Destroy(raider.gameObject);
            if (g.cameraRig) g.cameraRig.mode = mode;
            g.Player.Equip(keepTool != null ? ToolLibrary.Create(keepTool, g.propMaterial) : null);
        }
    }

    /// <summary>The crowbar on a locked metal door: a swing sets the bar in the gap and levers it for a moment (the
    /// player leans on it, sparks fly, the leaf shudders); held, it only knocks back; given, the lock snaps and the door
    /// jolts open past its stop and settles. A wood door splinters instead.</summary>
    class CrowbarDoor : Scenario
    {
        public override string Id => "tools.crowbar_door";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ScheduledScenarios1026Tools.OnFoot(g);
            var keepTool = g.Player.Tool ? g.Player.Tool.id : null;
            g.Player.Equip(ToolLibrary.Create("tool_crowbar", g.propMaterial));
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var p = PH.Piece(g, "door_metal", me + fwd * 1.3f, -fwd);
            if (!c.Check(p && p.GetComponent<Door>(), "a metal door")) yield break;
            var door = p.GetComponent<Door>();
            yield return null;
            door.SetLocked(true);
            var pry = door.GetComponent<DoorPry>();
            // stand square in front of the latch side
            var gapGuess = door.transform.TransformPoint(-door.hingeX * 0.92f, 1.05f, 0f);
            g.Player.Teleport(PH.Ground(gapGuess - fwd * 0.9f), PH.Yaw(fwd));
            yield return new WaitForSeconds(0.3f);

            // 1: it holds
            int sparks = DoorPry.SparkBursts, holds = DoorPry.Holds;
            DoorPry.ForceOutcome = false;
            c.Check(g.PryNearest(door.transform.position), "the crowbar finds the locked door");
            pry = door.GetComponent<DoorPry>();
            c.Check(pry && pry.Active && pry.Metal, "the bar goes into the gap (levering)");
            yield return new WaitForSeconds(0.4f);
            c.Check(g.Player.PoseOverride.HasValue, "the player leans on the bar");
            float shake = 0f;
            while (pry && pry.Active) { shake = Mathf.Max(shake, Mathf.Abs(door.JoltPeak)); yield return null; }
            c.Check(shake > 0.3f, $"the leaf shudders against its frame (up to {shake:0.0}°)");
            c.Check(DoorPry.SparkBursts > sparks, $"sparks off the steel ({DoorPry.SparkBursts - sparks} bursts)");
            c.Check(DoorPry.LastResult == "HELD" && DoorPry.Holds == holds + 1 && door.locked && !door.open, "this time it holds");
            c.Check(!g.Player.PoseOverride.HasValue, "the player lets go of the bar");
            yield return new WaitForSeconds(0.6f);

            // 2: it gives
            DoorPry.ForceOutcome = true;
            c.Check(g.PryNearest(door.transform.position) && pry.Active, "a second go");
            while (pry && pry.Active) yield return null;
            float over = 0f; bool shot = false;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                over = Mathf.Max(over, door.JoltPeak);
                if (!shot && t > 0.12f) { shot = true; c.Screenshot("crowbar_door_jolt"); }
                yield return null;
            }
            c.Check(DoorPry.LastResult == "SNAPPED" && !door.locked && door.open, "the lock snaps and the door is open");
            c.Check(over > 8f, $"it jolts open past its stop ({over:0.0}° over)");
            yield return new WaitForSeconds(2f);
            c.Check(Mathf.Abs(door.JoltAngle) < 1f, $"and settles ({door.JoltAngle:0.0}°)");
            Object.Destroy(p.gameObject);

            // 3: a wood door splinters
            var w = PH.Piece(g, "door_wood", me + fwd * 1.3f + right * 3f, -fwd);
            if (c.Check(w && w.GetComponent<Door>(), "a wood door"))
            {
                var wd = w.GetComponent<Door>();
                yield return null;
                wd.SetLocked(true);
                g.Player.Teleport(PH.Ground(wd.transform.TransformPoint(-wd.hingeX * 0.92f, 1.05f, 0f) - fwd * 0.9f), PH.Yaw(fwd));
                yield return new WaitForSeconds(0.3f);
                int spl = DoorPry.SplinterBursts;
                DoorPry.ForceOutcome = true;
                g.PryNearest(wd.transform.position);
                var wp = wd.GetComponent<DoorPry>();
                while (wp && wp.Active) yield return null;
                c.Check(DoorPry.SplinterBursts > spl && !wp.Metal, $"wood splinters ({DoorPry.SplinterBursts - spl} bursts)");
                c.Check(!wd.locked && wd.open, "and it bursts open");
                Object.Destroy(w.gameObject);
            }
            DoorPry.ForceOutcome = null;
            g.Player.Equip(keepTool != null ? ToolLibrary.Create(keepTool, g.propMaterial) : null);
        }
    }
}
