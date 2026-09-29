using MadMax.Building;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Side steps / roof ladder: [E] climbs onto the roof. The player stands on the highest point of the
    /// vehicle above the steps (found by casting down onto its own colliders), rides along, can use hand tools and
    /// weapons from up there, and climbs down beside the steps ([F] or any move).</summary>
    public class RoofAccess : MonoBehaviour, IInteractable
    {
        Seat perch;
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public string Prompt(MadMax.Game.WastelandGame g) => GetComponent<VehiclePart>().Socket && !(g.Player.Sitting && g.Player.SeatedOn && g.Player.SeatedOn.standing) ? "[E] CLIMB ONTO THE ROOF" : null;

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) return;
            var v = GetComponentInParent<VehicleDriver>();
            if (!v || !RoofTop(v, out var top)) { g.Toast("NOWHERE TO STAND UP THERE"); return; }
            if (!perch) { perch = gameObject.AddComponent<Seat>(); perch.standing = perch.hidden = true; perch.rest = 1f; perch.reading = 1f; perch.hasExit = true; }
            // climb down beside the steps, on the ground
            var outward = transform.TransformDirection(Vector3.right);
            var exit = transform.position + outward * 0.9f;
            var terrain = MadMax.World.DeformableTerrain.Instance;
            if (terrain) exit.y = terrain.Height(exit.x, exit.z) + 0.05f;
            perch.exitLocal = perch.transform.InverseTransformPoint(exit);
            float hips = 0.94f * g.Player.Rig.appearance.height;
            MadMax.Audio.Sfx.Play("hit_metal", transform.position, 0.35f, 1.4f);
            g.Player.SitOn(perch, perch.transform.InverseTransformPoint(top + Vector3.up * hips));
        }

        /// <summary>Highest surface of the vehicle itself over its centre line, level with the steps.</summary>
        bool RoofTop(VehicleDriver v, out Vector3 top)
        {
            top = default;
            var local = v.transform.InverseTransformPoint(transform.position);
            var from = v.transform.TransformPoint(new Vector3(0f, local.y + 8f, local.z));
            int n = Physics.RaycastNonAlloc(from, -v.transform.up, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.GetComponentInParent<VehicleDriver>() != v || hits[i].distance >= best) continue;
                best = hits[i].distance; top = hits[i].point;
            }
            return best < float.MaxValue;
        }
    }
}
