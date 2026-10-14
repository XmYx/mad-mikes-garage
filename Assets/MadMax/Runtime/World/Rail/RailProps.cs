using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Shared meshes of the railway's small working props (lever stand, crossing post and arm, signal),
    /// built once and rebuilt after play mode destroyed them.</summary>
    public static class RailProps
    {
        static Mesh stand, standLever, standTarget, blade, post, arm, lampL, signal, lensRed, lensGreen;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { stand = standLever = standTarget = blade = post = arm = lampL = signal = lensRed = lensGreen = null; }

        static Mesh M(RailKit k, string n, bool glow = false) => VoxelMesher.ToMesh(glow ? k.glow : k.mesh, n);
        static void B(RailKit k, float x, float y, float z, float hx, float hy, float hz, Color32[] r, int s = 0) => k.Box(new Vector3(x, y, z), Vector3.right, Vector3.up, Vector3.forward, hx, hy, hz, r, s);

        public static Mesh Stand { get { if (!stand) { var k = new RailKit(1) { rust = 0.4f }; B(k, 0f, 0.12f, 0f, 0.3f, 0.12f, 0.3f, RailKit.Timber); B(k, 0f, 0.4f, 0f, 0.12f, 0.2f, 0.12f, RailKit.Iron); k.Cylinder(new Vector3(0f, 0.6f, 0f), new Vector3(0f, 1.3f, 0f), 0.04f, 6, RailKit.Iron); stand = M(k, "Rail_Stand"); } return stand; } }
        /// <summary>The throw lever: pivots on X at its origin, handle up and to +Z.</summary>
        public static Mesh StandLever { get { if (!standLever) { var k = new RailKit(2) { rust = 0.2f }; k.Beam(Vector3.zero, new Vector3(0f, 0.9f, 0f), 0.05f, 0.05f, RailKit.Iron); B(k, 0f, 0.9f, 0f, 0.06f, 0.08f, 0.06f, RailKit.Red); B(k, 0f, -0.15f, 0f, 0.1f, 0.14f, 0.1f, RailKit.Iron); standLever = M(k, "Rail_StandLever"); } return standLever; } }
        /// <summary>The target vane on the stand (turns 90° with the points).</summary>
        public static Mesh StandTarget { get { if (!standTarget) { var k = new RailKit(3) { rust = 0f }; B(k, 0f, 0f, 0f, 0.3f, 0.18f, 0.02f, RailKit.Paint); B(k, 0f, 0f, 0.02f, 0.3f, 0.05f, 0.01f, RailKit.Red); standTarget = M(k, "Rail_Target"); } return standTarget; } }
        /// <summary>A switch blade: a tapered rail 5 m long along +Z.</summary>
        public static Mesh Blade { get { if (!blade) { var k = new RailKit(4) { rust = 0.05f }; B(k, 0f, 0.07f, 2.5f, 0.035f, 0.07f, 2.5f, RailKit.RailHead); B(k, 0f, 0.01f, 2.5f, 0.07f, 0.01f, 2.5f, Pal.Rust); blade = M(k, "Rail_Blade"); } return blade; } }
        /// <summary>Crossing post: a pole, the white cross boards, a lamp housing, the barrier pivot box.</summary>
        public static Mesh Post
        {
            get
            {
                if (post) return post;
                var k = new RailKit(5) { rust = 0.1f };
                k.Cylinder(Vector3.zero, new Vector3(0f, 3.3f, 0f), 0.07f, 8, RailKit.Iron);
                for (int s = -1; s <= 1; s += 2) k.Box(new Vector3(0f, 2.9f, 0.05f), Quaternion.Euler(0f, 0f, 35f * s) * Vector3.right, Quaternion.Euler(0f, 0f, 35f * s) * Vector3.up, Vector3.forward, 0.62f, 0.09f, 0.02f, RailKit.Paint);
                B(k, 0f, 2.25f, 0.05f, 0.45f, 0.12f, 0.08f, RailKit.Iron);
                B(k, 0f, 0.9f, 0f, 0.22f, 0.3f, 0.22f, RailKit.Warn);
                post = M(k, "Rail_CrossPost");
                return post;
            }
        }
        /// <summary>Barrier arm along +Z (pivot at the origin), red and white bands.</summary>
        public static Mesh Arm { get { if (!arm) { var k = new RailKit(6) { rust = 0f }; for (int i = 0; i < 9; i++) B(k, 0f, 0f, 0.25f + i * 0.5f, 0.05f, 0.06f, 0.25f, (i & 1) == 0 ? RailKit.Red : RailKit.Paint); arm = M(k, "Rail_Arm"); } return arm; } }
        /// <summary>A crossing lamp lens (glow mesh).</summary>
        public static Mesh LampLens { get { if (!lampL) { var k = new RailKit(7) { emit = true }; B(k, 0f, 0f, 0f, 0.1f, 0.1f, 0.03f, RailKit.Red); lampL = M(k, "Rail_LampLens", true); } return lampL; } }
        public static Mesh Signal
        {
            get
            {
                if (signal) return signal;
                var k = new RailKit(8) { rust = 0.2f };
                k.Cylinder(Vector3.zero, new Vector3(0f, 4.6f, 0f), 0.08f, 8, RailKit.Iron);
                B(k, 0f, 4.3f, 0f, 0.28f, 0.6f, 0.15f, Pal.Black);
                B(k, 0f, 4.95f, 0f, 0.3f, 0.05f, 0.18f, Pal.Black);
                B(k, 0f, 0.1f, 0f, 0.35f, 0.1f, 0.35f, RailKit.Concrete);
                signal = M(k, "Rail_Signal");
                return signal;
            }
        }
        public static Mesh LensRed { get { if (!lensRed) { var k = new RailKit(9) { emit = true }; B(k, 0f, 0f, 0f, 0.13f, 0.13f, 0.02f, RailKit.Red); lensRed = M(k, "Rail_LensRed", true); } return lensRed; } }
        public static Mesh LensGreen { get { if (!lensGreen) { var k = new RailKit(10) { emit = true }; B(k, 0f, 0f, 0f, 0.13f, 0.13f, 0.02f, RailKit.Green); lensGreen = M(k, "Rail_LensGreen", true); } return lensGreen; } }
        /// <summary>The same lenses dark (lamp off).</summary>
        public static Mesh Child(Transform parent, string name, Mesh mesh, Material mat, Vector3 at, Quaternion rot)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localRotation = rot;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return mesh;
        }
    }
}
