using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Front winch (on bull bars, plows and winch bumpers). While driving: 4 hooks the cable to the nearest
    /// anchor ahead (tree, rock, building, vehicle, built piece) within 25 m, hold 5 reels in (up to 80 kN), 6 pays out.
    /// Pulls both ends: frees a stuck vehicle, or drags another one out.</summary>
    public class Winch : MonoBehaviour
    {
        public static readonly string[] Parts = { "bumper_bull_bar", "bumper_winch", "bumper_plow", "bumper_ram" };
        public const float Reach = 25f, MaxPull = 80000f;

        VehicleDriver driver;
        Rigidbody rb;
        Collider anchorCol; Rigidbody anchorRb; Vector3 anchorLocal;
        float length;
        LineRenderer line;
        public string Status { get; private set; }

        void Awake() { driver = GetComponent<VehicleDriver>(); rb = GetComponent<Rigidbody>(); }

        public VehiclePart WinchPart
        {
            get
            {
                foreach (var s in GetComponentsInChildren<MountSocket>())
                    if (s.Current && System.Array.IndexOf(Parts, s.Current.partId) >= 0) return s.Current;
                return null;
            }
        }

        public bool Hooked => anchorCol;
        Vector3 Drum => WinchPart ? WinchPart.transform.TransformPoint(new Vector3(0, 0.16f, 0.25f)) : transform.position + transform.forward * 2f;
        Vector3 AnchorPoint => anchorCol ? anchorCol.transform.TransformPoint(anchorLocal) : Drum;

        /// <summary>Operator keys (called by the game while driving).</summary>
        public void Control(bool press4, bool hold5, bool hold6)
        {
            if (!WinchPart) { Status = null; if (Hooked) Release(); return; }
            if (press4) { if (Hooked) Release(); else Hook(); }
            if (Hooked)
            {
                float dt = Time.deltaTime;
                if (hold5) length = Mathf.Max(1.5f, length - 1.2f * dt);
                if (hold6) length = Mathf.Min(Reach, length + 2f * dt);
                float d = Vector3.Distance(Drum, AnchorPoint);
                Status = "WINCH " + d.ToString("0.0") + " M  [4] RELEASE  [5] REEL IN  [6] PAY OUT";
            }
            else Status = "WINCH  [4] HOOK ANCHOR AHEAD (" + Reach + " M)";
        }

        void Hook()
        {
            var from = Drum;
            Collider best = null; float bd = float.MaxValue; Vector3 bp = default;
            foreach (var c in Physics.OverlapSphere(from + transform.forward * Reach * 0.5f, Reach * 0.55f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(transform) || c is TerrainCollider) continue;
                if (c is MeshCollider && !c.GetComponentInParent<MadMax.World.DestructibleVoxels>() && !c.GetComponentInParent<MadMax.Building.Placeable>()) continue;   // bare ground
                var p = c.ClosestPoint(from);
                var dir = p - from;
                if (Vector3.Dot(dir.normalized, transform.forward) < 0.3f) continue;
                float d = dir.magnitude;
                if (d > Reach || d < 0.5f) continue;
                if (d < bd) { bd = d; best = c; bp = p; }
            }
            if (!best) { Status = "NO ANCHOR IN REACH"; return; }
            anchorCol = best; anchorRb = best.attachedRigidbody;
            anchorLocal = best.transform.InverseTransformPoint(bp);
            length = bd + 0.2f;
            if (anchorRb && anchorRb.isKinematic && anchorRb.GetComponent<VehicleDriver>()) anchorRb.isKinematic = false;   // wake a parked / wrecked vehicle
            if (!line)
            {
                line = new GameObject("WinchCable").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = MadMax.World.Fx.TransparentMaterial(null);
                line.widthMultiplier = 0.03f; line.positionCount = 2;
                line.startColor = line.endColor = new Color(0.12f, 0.11f, 0.1f, 1f);
            }
            line.enabled = true;
        }

        public void Release() { anchorCol = null; anchorRb = null; if (line) line.enabled = false; }

        void FixedUpdate()
        {
            if (!Hooked) return;
            if (!anchorCol) { Release(); return; }
            driver.KeepAwakeUntil = Time.time + 0.5f;
            if (anchorRb) { var od = anchorRb.GetComponent<VehicleDriver>(); if (od) od.KeepAwakeUntil = Time.time + 0.5f; if (anchorRb.IsSleeping()) anchorRb.WakeUp(); }
            Vector3 a = Drum, b = AnchorPoint;
            var d = b - a;
            float dist = d.magnitude;
            if (dist > Reach * 1.3f) { Release(); return; }
            if (dist <= length || dist < 0.01f) return;
            var dir = d / dist;
            // stiff rope with damping along the cable
            float closing = Vector3.Dot((anchorRb ? anchorRb.GetPointVelocity(b) : Vector3.zero) - rb.GetPointVelocity(a), dir);
            float force = Mathf.Clamp((dist - length) * 60000f + closing * 8000f, 0f, MaxPull);
            rb.AddForceAtPosition(dir * force, a);
            if (anchorRb && !anchorRb.isKinematic) anchorRb.AddForceAtPosition(-dir * force, b);
        }

        void LateUpdate()
        {
            if (!line || !line.enabled) return;
            var a = Drum; var b = AnchorPoint;
            float slack = Mathf.Max(0f, length - Vector3.Distance(a, b));
            line.positionCount = 8;
            for (int i = 0; i < 8; i++) { float t = i / 7f; line.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * slack * 0.5f * 4f * t * (1f - t)); }
        }
    }
}
