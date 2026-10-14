using MadMax.Game;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Pedestrians jump clear of cars: with cars free to cut across sidewalks, plazas and islands, anyone on
    /// foot whom a vehicle is about to pass within <see cref="DodgeMiss"/> m of in the next <see cref="DodgeLook"/> s
    /// steps off its line at a run (not fighters closing on that car, nor people already down).</summary>
    public partial class Npc
    {
        public const float DodgeLook = 1.6f, DodgeMiss = 2.2f;
        Vector3 dodgeDir;
        float dodgeUntil, dodgeCheck;
        /// <summary>Last time anyone dodged a car (tests).</summary>
        public static float LastDodge = -1f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetDodge() => LastDodge = -1f;

        /// <summary>Overrides the walk while a car bears down; true while dodging.</summary>
        bool Dodge(WastelandGame g, Vector3 me, ref Vector3 move, ref float speed)
        {
            if (Time.time < dodgeUntil) { move = dodgeDir; speed = 4.6f; return true; }
            if ((dodgeCheck -= Time.deltaTime) > 0f) return false;
            dodgeCheck = 0.15f;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.Body || v.Body.isKinematic) continue;
                var vel = v.Body.linearVelocity; vel.y = 0f;
                float sp2 = vel.sqrMagnitude;
                if (sp2 < 25f) continue;                                                        // under 18 km/h people just step round it
                var r = me - v.transform.position; r.y = 0f;
                if (r.sqrMagnitude > 900f) continue;
                float t = Vector3.Dot(r, vel) / sp2;                                            // time to the closest pass
                if (t < 0f || t > DodgeLook) continue;
                var miss = r - vel * t;
                if (miss.sqrMagnitude > DodgeMiss * DodgeMiss) continue;
                if (mode == Mode.Fight && v == g.Current) continue;                              // squaring up to it
                var side = Vector3.Cross(Vector3.up, vel).normalized;
                dodgeDir = Vector3.Dot(miss, side) >= 0f ? side : -side;                        // away from the line, the short way
                dodgeUntil = Time.time + Mathf.Clamp(t + 0.4f, 0.6f, 1.6f);
                LastDodge = Time.time;
                move = dodgeDir; speed = 4.6f;
                return true;
            }
            return false;
        }
    }
}
