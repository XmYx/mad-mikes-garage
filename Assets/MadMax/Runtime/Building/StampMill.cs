using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Stamp mill (depth stage H, the powered rung of mining): three iron stamps lifted by the cams and dropped
    /// into the mortar while the station works, crushing ore to a concentrate that smelts richer (recipes at the
    /// <c>stamp_mill</c> station). This component only animates the stamps and the thud.</summary>
    public class StampMill : MonoBehaviour
    {
        static Mesh stampMesh;
        public static readonly float[] StampX = { -0.4f, 0f, 0.4f };
        const float RestY = 0.36f;                                                           // shoe foot in the mortar
        readonly Transform[] stamps = new Transform[3];
        CraftingStation station;
        float phase, dust;

        /// <summary>One stamp: stem, tappet collar and a heavy shoe (origin at the shoe's foot).</summary>
        static Mesh StampMesh()
        {
            if (stampMesh) return stampMesh;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Iron);
            g.Box(-1, 0, -1, 1, 2, 1, Pal.Ramp(Pal.Metal, 3, 4411));                        // shoe
            g.Box(0, 3, 0, 0, 17, 0, Pal.Ramp(Pal.Chrome, 1, 4412));                         // stem
            g.Box(-1, 13, -1, 1, 14, 1, Pal.Ramp(Pal.Rust, 2, 4413));                        // tappet
            g.Bevel();
            return stampMesh = VoxelMesher.Build(g, "Furniture_stamp");
        }

        void Awake()
        {
            station = GetComponent<CraftingStation>();
            var mat = GetComponent<MeshRenderer>() ? GetComponent<MeshRenderer>().sharedMaterial : null;
            for (int i = 0; i < stamps.Length; i++)
            {
                var s = new GameObject("Stamp" + i, typeof(MeshFilter), typeof(MeshRenderer));
                s.transform.SetParent(transform, false);
                s.transform.localPosition = new Vector3(StampX[i], RestY, 0f);
                s.GetComponent<MeshFilter>().sharedMesh = StampMesh();
                s.GetComponent<MeshRenderer>().sharedMaterial = mat;
                stamps[i] = s.transform;
            }
        }

        void Update()
        {
            bool working = station && station.Busy && station.Powered;
            if (!working) return;
            float dt = Time.deltaTime;
            float before = phase;
            phase += dt * 1.6f;
            for (int i = 0; i < stamps.Length; i++)
            {
                // each cam lifts its stamp slowly and lets it fall; the three are a third of a turn apart
                float t = Mathf.Repeat(phase + i / 3f, 1f);
                float lift = t < 0.75f ? t / 0.75f : 1f - (t - 0.75f) / 0.25f;
                stamps[i].localPosition = new Vector3(StampX[i], RestY + lift * 0.22f, 0f);
            }
            if (Mathf.FloorToInt(before * 3f) != Mathf.FloorToInt(phase * 3f))
            {
                var g = MadMax.Game.WastelandGame.Instance;
                var ear = g && g.Player ? g.Player.transform.position : transform.position;
                if ((ear - transform.position).sqrMagnitude < 30f * 30f) MadMax.Audio.Sfx.Play("hammer", transform.position + Vector3.up * 0.7f, 0.35f, 0.6f, 25f, 0.1f);
            }
            if ((dust -= dt) <= 0f)
            {
                dust = 0.7f;
                Color c = Pal.WorkshopDust; c.a = 0.35f;
                MadMax.World.Fx.Smoke(transform.TransformPoint(0f, 0.8f, 0.5f), Vector3.up * 0.3f, 0.2f, c, 1.4f);
            }
        }
    }
}
