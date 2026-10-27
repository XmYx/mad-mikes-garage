using System.Collections.Generic;
using MadMax.Building;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Tool remodels (2026-10-26). The mirror: [E] steps up to the glass and faces it, every haircut / shave /
    /// dye snips and drops a puff of trimmings in the cut colour. The binoculars: raised to the eyes, two lenses on the
    /// HUD (<c>PixelHud.Binoculars</c>), and with the sun ahead the lenses flash — a sparkle at the head, and hostiles on
    /// foot or a raider column in what you are watching may see it and come (<see cref="Npc.Npc.SeeGlint"/>,
    /// <see cref="Convoy.Answer"/>). The crowbar levering a door lives in <see cref="DoorPry"/>.</summary>
    public partial class WastelandGame
    {
        // ------------------------------------------------------------------ mirror
        public const float MirrorStand = 0.75f, MirrorStepMax = 1.4f;
        /// <summary>Haircuts, shaves and dyes at a mirror this session (tests), the last kind ("hair" / "beard" / "colour")
        /// and the bits of trimmings thrown.</summary>
        public static int MirrorTrims, MirrorBits;
        public static string LastMirrorTrim;
        static readonly List<DebrisSystem.Chunk> trimChunks = new List<DebrisSystem.Chunk>(16);
        Mirror usingMirror;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset1026Tools()
        {
            MirrorTrims = MirrorBits = 0; LastMirrorTrim = null; trimChunks.Clear();
            GlintStrength = 0f; BinocularGlints = GlintsSeen = 0; LastGlintAt = -9f; LastGlintSeenBy = null; GlintSunOverride = null;
        }

        /// <summary>The mirror whose page is open (null when none).</summary>
        public Mirror MirrorInUse => Menus && Menus.AtMirror && usingMirror ? usingMirror : null;

        /// <summary>[E] at a mirror: step in front of the glass, face it, open the mirror page.</summary>
        public void UseMirror(Mirror m)
        {
            usingMirror = m;
            FaceMirror(m);
            Menus.OpenMirror();
        }

        /// <summary>Stand <see cref="MirrorStand"/> m out from the glass when there is room within
        /// <see cref="MirrorStepMax"/> m of where you are, and turn to it (eyes on the glass; first person: the view too).</summary>
        public void FaceMirror(Mirror m)
        {
            if (!m || !Player || Current) return;
            var pl = Player;
            var n = m.Normal; n.y = 0f;
            var me = pl.transform.position;
            Vector3 face;
            if (n.sqrMagnitude > 0.25f && !pl.Interior)
            {
                n.Normalize();
                var stand = m.Centre + n * MirrorStand; stand.y = me.y;
                face = -n;
                if (MirrorFlat(stand, me) <= MirrorStepMax && StandingRoom(stand)) pl.Teleport(stand, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
                else pl.transform.rotation = Quaternion.LookRotation(face);
            }
            else
            {
                face = m.Centre - me; face.y = 0f;                                             // lying flat / on a moving floor: just turn
                if (face.sqrMagnitude < 1e-4f) return;
                face.Normalize();
                pl.transform.rotation = Quaternion.LookRotation(face);
            }
            var eye = pl.Rig && pl.Rig.Eye ? pl.Rig.Eye.position : pl.transform.position + Vector3.up * 1.68f;
            var look = m.Centre - eye;
            pl.lookPitch = Mathf.Clamp(-Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg, -40f, 40f);
            pl.viewYaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
            if (cameraRig && cameraRig.mode == ViewMode.FirstPerson) cameraRig.LookToward(look.normalized);
        }

        static float MirrorFlat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        /// <summary>Degrees between the player's facing and the glass (0 = square on; tests).</summary>
        public float MirrorFacing(Mirror m)
        {
            if (!m || !Player) return 180f;
            var to = m.Centre - Player.transform.position; to.y = 0f;
            var f = Player.transform.forward; f.y = 0f;
            return to.sqrMagnitude < 1e-4f ? 0f : Vector3.Angle(f, to);
        }

        /// <summary>A change at the glass: scissors (or the dye pot) and a puff of trimmings at the head in
        /// <paramref name="cut"/> (the colour that came off).</summary>
        public void MirrorTrim(string what, Color32 cut)
        {
            if (!Player || !Player.Rig) return;
            MirrorTrims++; LastMirrorTrim = what;
            var rig = Player.Rig;
            var head = rig.Head ? rig.Head.position : Player.transform.position + Vector3.up * 1.6f;
            var fwd = Player.transform.forward;
            bool beard = what == "beard", dye = what == "colour";
            var at = beard ? head - Vector3.up * 0.1f + fwd * 0.1f : head + Vector3.up * 0.08f + fwd * 0.04f;
            if (dye)
            {
                var now = HumanDesign.HairColors[Mathf.Clamp(rig.appearance.hairColor, 0, HumanDesign.HairColors.Length - 1)];
                MadMax.Audio.Sfx.Play("pour", at, 0.35f, 1.6f, 12f);
                var deb = DebrisSystem.Instance;
                if (deb) for (int i = 0; i < 3; i++) deb.EmitPuff(at + Random.insideUnitSphere * 0.08f, now, 0.06f, Vector3.down * 0.15f + Random.insideUnitSphere * 0.05f, 0.7f);
                return;
            }
            MadMax.Audio.Sfx.Play("snip", at, 0.7f, Random.Range(0.92f, 1.1f), 15f, 0.08f);
            var d = DebrisSystem.Instance;
            if (!d) return;
            trimChunks.Clear();
            int k = beard ? 9 : 14;
            for (int i = 0; i < k; i++)
            {
                var off = Random.insideUnitSphere * (beard ? 0.06f : 0.1f);
                trimChunks.Add(new DebrisSystem.Chunk { position = at + off + fwd * 0.06f, color = cut });
            }
            d.Emit(trimChunks, 0.018f, Vector3.down * 0.3f + fwd * 0.15f, 0.25f);
            for (int i = 0; i < 2; i++) d.EmitPuff(at + fwd * 0.08f, cut, 0.05f, Vector3.down * 0.25f + Random.insideUnitSphere * 0.05f, 0.6f);
            MirrorBits += k;
        }

        // ------------------------------------------------------------------ binocular glint
        public const float GlintEvery = 0.5f, GlintReach = 400f, ConvoyGlintReach = 600f, GlintCone = 14f, GlintSeeChance = 0.6f;
        /// <summary>How bright the lenses flash now (0..1: the sun ahead of where you look, high enough, clear air).</summary>
        public static float GlintStrength;
        /// <summary>Flashes thrown this session, when the last was, flashes seen by someone and who saw the last.</summary>
        public static int BinocularGlints, GlintsSeen;
        public static float LastGlintAt = -9f;
        public static string LastGlintSeenBy;
        /// <summary>Automation: the sun's direction for the glint (null = <see cref="DayNight.SunDirection"/>).</summary>
        public static Vector3? GlintSunOverride;
        float glintT;
        Camera glintCam;

        /// <summary>Where the glasses point: the view in the crosshair views, the body's heading looking down on it.</summary>
        public Vector3 BinocularLook()
        {
            if (cameraRig && cameraRig.CrosshairView && cameraRig.pixel)
            {
                if (!glintCam) glintCam = cameraRig.pixel.GetComponent<Camera>();
                if (glintCam) return glintCam.transform.forward;
            }
            return Player ? Player.transform.forward : Vector3.forward;
        }

        /// <summary>Glint strength for a look direction and the sun: the sun ahead (within ~70° of the look), up but
        /// not overhead, dimmed by dust and dusk.</summary>
        public static float GlintFor(Vector3 look, Vector3 sun, float darkness, float dust)
        {
            if (sun.y < 0.02f) return 0f;
            float ahead = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.9f, Vector3.Dot(look.normalized, sun.normalized)));
            float notHigh = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.9f, sun.y));
            return ahead * notHigh * Mathf.Clamp01(1f - darkness * 1.6f) * Mathf.Clamp01(1f - dust);
        }

        /// <summary>Called each frame while the player looks through the binoculars (<see cref="BinocularsTool"/>).</summary>
        public void BinocularTick(float dt)
        {
            if (!Player || Current) { GlintStrength = 0f; return; }
            var look = BinocularLook();
            GlintStrength = GlintFor(look, GlintSunOverride ?? DayNight.SunDirection, DayNight.Darkness, Storms.Dust);
            if ((glintT -= dt) > 0f || GlintStrength < 0.2f) return;
            glintT = GlintEvery * Random.Range(0.8f, 1.3f);
            var rig = Player.Rig;
            var lens = (rig && rig.Head ? rig.Head.position : Player.transform.position + Vector3.up * 1.6f) + look * 0.16f;
            Fx.Glint(lens, 0.1f + 0.14f * GlintStrength, MadMax.Voxel.Pal.LightW);
            BinocularGlints++; LastGlintAt = Time.time;
            if (GlintStrength > 0.5f) Hints.Show("binocular_glint", "SUN IN THE LENSES - THEY FLASH");
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient) return;                                                    // NPCs live on the host
            GlintSeen(Player.transform.position, look, GlintStrength);
        }

        /// <summary>Who sees a flash from <paramref name="from"/> looking along <paramref name="look"/>: hostiles on foot
        /// and raider columns in the binoculars' field, by chance with the strength. Returns how many noticed.</summary>
        public int GlintSeen(Vector3 from, Vector3 look, float strength, bool sure = false)
        {
            var flat = look; flat.y = 0f;
            if (flat.sqrMagnitude < 1e-4f) return 0;
            flat.Normalize();
            float cos = Mathf.Cos(GlintCone * Mathf.Deg2Rad);
            int seen = 0;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || n.proxy || !n.Alive || !n.Hostile || n.mode == MadMax.Npc.Npc.Mode.Fight || n.GlintAlert) continue;
                var to = n.transform.position - from; to.y = 0f;
                float d = to.magnitude;
                if (d < 8f || d > GlintReach || Vector3.Dot(to / d, flat) < cos) continue;
                if (!sure && Random.value > strength * GlintSeeChance) continue;
                n.SeeGlint(from);
                seen++; LastGlintSeenBy = n.KnownName;
            }
            var dir = NpcDirector.Instance;
            if (dir != null)
                for (int i = 0; i < dir.Convoys.Count; i++)
                {
                    var c = dir.Convoys[i];
                    if (c == null || !c.raiders || c.Friendly || c.phase != Convoy.Phase.Travel || c.Answering) continue;
                    var to = c.Position - from; to.y = 0f;
                    float d = to.magnitude;
                    if (d < 20f || d > ConvoyGlintReach || Vector3.Dot(to / d, flat) < cos) continue;
                    if (!sure && Random.value > strength * GlintSeeChance) continue;
                    c.Answer(from);                                                             // they turn toward the flash, looking hard
                    seen++; LastGlintSeenBy = "THE " + c.Gang;
                }
            GlintsSeen += seen;
            return seen;
        }
    }
}
