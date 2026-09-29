using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Mud on a vehicle (roadmap 16): driving through mud and over wet ground cakes the body (voxel splatters
    /// thick along the sills, rising with the muck — shader <c>_Dirt</c> / <c>_DirtTop</c> per renderer); rain rinses it
    /// slowly, fording deep water washes it off. Cosmetic, not saved.</summary>
    public class VehicleGrime : MonoBehaviour
    {
        public float dirt;                    // 0 clean .. 1 caked
        VehicleDriver driver;
        Renderer body;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        float applyT, rescanT, shownDirt = -1f, shownTop;
        static readonly int DirtId = Shader.PropertyToID("_Dirt"), TopId = Shader.PropertyToID("_DirtTop");

        void Awake()
        {
            driver = GetComponent<VehicleDriver>();
            var b = transform.Find("Body");
            body = b ? b.GetComponent<Renderer>() : null;
            mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (!driver || !body) return;
            float dt = Time.deltaTime, speed = Mathf.Abs(driver.ForwardSpeed);
            var t = DeformableTerrain.Instance;
            float wet = 0f;
            if (t && speed > 2f) { var p = transform.position; wet = t.SurfaceAt(p.x, p.z).wet; }
            if (speed > 2f) dirt += dt * (driver.Mud * 0.05f + wet * 0.008f) * Mathf.Min(1f, speed / 10f);
            if (Weather.Raining && !Weather.Snowing) dirt -= dt * 0.004f;                    // rain rinses
            if (driver.InWater > 0.3f) dirt -= dt * 0.08f;                                    // fording washes it off
            dirt = Mathf.Clamp01(dirt - dt * 0.0002f);
            if ((applyT -= dt) > 0f) return;
            applyT = 0.25f;
            if ((rescanT -= 0.25f) <= 0f || renderers == null) { rescanT = 5f; renderers = GetComponentsInChildren<MeshRenderer>(); }   // parts come and go
            var bb = body.bounds;
            float top = bb.min.y + bb.size.y * (0.2f + 0.45f * Mathf.Sqrt(dirt));
            if (Mathf.Abs(dirt - shownDirt) < 0.01f && Mathf.Abs(top - shownTop) < 0.04f) return;
            shownDirt = dirt; shownTop = top;
            foreach (var r in renderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(DirtId, dirt);
                mpb.SetFloat(TopId, top);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
