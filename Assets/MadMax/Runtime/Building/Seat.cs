using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Somewhere to sit (chair, bench, sofa, armchair): [E] sits the player at the nearest spot. Sitting rests
    /// (stamina regen × <see cref="rest"/>) and reads faster (× <see cref="reading"/>); any move or [F] stands up.</summary>
    public class Seat : MonoBehaviour, IInteractable
    {
        public Vector3[] spots = { new Vector3(0f, 0.6f, -0.04f) };   // hip positions, local (the sitter faces +Z)
        public float rest = 1.5f, reading = 1.1f;
        /// <summary>Standing perch (vehicle roof): the player stands and can use tools; no [E] SIT prompt of its own.</summary>
        public bool standing, hidden;
        /// <summary>Where to get off (local), instead of in front of the spot.</summary>
        public Vector3 exitLocal;
        public bool hasExit;
        /// <summary>A mount's saddle (roadmap 23): the rider's move / run / jump input steers it instead of standing up.</summary>
        public System.Action<Vector2, bool, bool, float> ride;

        public string Prompt(MadMax.Game.WastelandGame g) => hidden || (g.Player && g.Player.SeatedOn == this) ? null : "[E] SIT";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary || !g.Player) return;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < spots.Length; i++)
            {
                float d = (transform.TransformPoint(spots[i]) - g.Player.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            g.Player.SitOn(this, spots[best]);
        }

        /// <summary>Candidate places to stand up from a spot: in front, then the sides, then behind (on the piece's floor).</summary>
        public Vector3 StandPoint(Vector3 spot, int attempt) => hasExit && attempt == 0 ? transform.TransformPoint(exitLocal)
            : transform.TransformPoint(spot.x + (attempt == 1 ? 0.6f : attempt == 2 ? -0.6f : 0f), 0.02f, attempt == 0 ? spot.z + 0.62f : attempt == 3 ? spot.z - 0.62f : spot.z);
    }
}
