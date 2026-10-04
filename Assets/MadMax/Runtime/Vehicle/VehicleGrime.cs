using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Mud on a vehicle (roadmap 16): driving through mud and over wet ground cakes the body (voxel splatters
    /// thick along the sills, rising with the muck — shader <c>_Dirt</c> / <c>_DirtTop</c> per renderer); rain rinses it
    /// slowly, fording deep water washes it off. Tyres carry their own mud (<see cref="WheelStats.mud"/>, from
    /// <see cref="VehicleDriver"/>): their renderers show the whole wheel caked by it. The body's mud is cosmetic and not
    /// saved; the tyres' is saved with the wheel.</summary>
    public class VehicleGrime : MonoBehaviour
    {
        public float dirt;                    // 0 clean .. 1 caked
        /// <summary>Fire damage on the paint, 0..1 (<see cref="VehicleBurn"/>): darkens every renderer towards char and
        /// streaks it with ash.</summary>
        public float scorch;
        float shownScorch;
        VehicleDriver driver;
        Renderer body;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        float applyT, rescanT, shownDirt = -1f, shownTop;
        static readonly int DirtId = Shader.PropertyToID("_Dirt"), TopId = Shader.PropertyToID("_DirtTop"), TintId = Shader.PropertyToID("_Tint");
        static readonly Color Char = new Color(0.2f, 0.17f, 0.15f, 1f);

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
            bool tyres = TyresChanged();
            if (!tyres && Mathf.Abs(dirt - shownDirt) < 0.01f && Mathf.Abs(top - shownTop) < 0.04f && Mathf.Abs(scorch - shownScorch) < 0.01f) return;
            shownDirt = dirt; shownTop = top; shownScorch = scorch;
            var tint = Color.Lerp(Color.white, Char, scorch);
            foreach (var r in renderers)
            {
                if (!r) continue;
                var ws = r.GetComponentInParent<WheelStats>();
                float d = dirt, tp = top;
                if (ws && ws.mud > 0.01f) { d = Mathf.Max(dirt, ws.mud); tp = Mathf.Max(top, r.bounds.max.y + 0.2f); }   // the tread caked all round
                if (scorch > 0.01f) { d = Mathf.Max(d, scorch * 0.7f); tp = Mathf.Max(tp, bb.max.y + 0.5f); }               // ash streaks all over
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(DirtId, d);
                mpb.SetFloat(TopId, tp);
                if (scorch > 0.01f) mpb.SetColor(TintId, tint);
                r.SetPropertyBlock(mpb);
            }
        }

        WheelStats[] tyreList;
        float[] tyreShown;

        /// <summary>Any tyre's mud moved by more than 2 % since it was last drawn.</summary>
        bool TyresChanged()
        {
            if (tyreList == null || rescanT >= 4.99f) { tyreList = GetComponentsInChildren<WheelStats>(); tyreShown = new float[tyreList.Length]; for (int i = 0; i < tyreShown.Length; i++) tyreShown[i] = -1f; }
            bool any = false;
            for (int i = 0; i < tyreList.Length; i++)
            {
                var w = tyreList[i];
                if (!w || Mathf.Abs(w.mud - tyreShown[i]) < 0.02f) continue;
                tyreShown[i] = w.mud; any = true;
            }
            return any;
        }
    }
}
