using MadMax.Building;
using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>A bike's side stand you can see (2026-10-29; the parked lean had nothing holding it): a steel strut on the
    /// left of the frame ahead of the rear wheel, kicked down with a click when the rider gets off and the bike stops, so
    /// the bike rests leaning on it; folded up along the frame (a clack) as soon as someone rides it. Added by
    /// <see cref="BikeBalance"/> on solo bikes (not sidecar outfits); posed only within 60 m of the camera.</summary>
    public class Kickstand : MonoBehaviour
    {
        const float Out = 22f, Back = 18f, Fold = 92f, Reach = 60f;
        Transform strut;
        VehicleDriver v;
        float down, length = 0.3f;
        bool built, wasDown;
        Vector3 pivot;

        /// <summary>0 folded .. 1 down on the ground (tests).</summary>
        public float Down => down;
        /// <summary>Kicked down / folded up (tests).</summary>
        public int Kicks { get; private set; }

        void Start()
        {
            v = GetComponent<VehicleDriver>();
            float zmin = float.MaxValue, zmax = float.MinValue, axle = 0.33f, r = 0.33f;
            foreach (var s in GetComponentsInChildren<MountSocket>(true))
            {
                if (s.accepts != PartCategory.Wheel || s.name.StartsWith("wheel_side")) continue;
                var p = s.transform.localPosition;
                if (p.z < zmin) { zmin = p.z; axle = p.y; if (s.Current && s.Current.radius > 0.05f) r = s.Current.radius; }
                zmax = Mathf.Max(zmax, p.z);
            }
            if (zmin == float.MaxValue) { enabled = false; return; }
            float wb = Mathf.Max(0.8f, zmax - zmin);
            pivot = new Vector3(-0.09f, axle - r * 0.12f, zmin + wb * 0.36f);
            length = Mathf.Max(0.15f, (pivot.y - (axle - r)) / (Mathf.Cos(Out * Mathf.Deg2Rad) * Mathf.Cos(Back * Mathf.Deg2Rad)) + 0.02f);
        }

        void Build()
        {
            built = true;
            bool hd = HDBits.On;
            var g = MadMax.Game.WastelandGame.Instance;
            var mat = hd ? HDShapes.Solid : g ? g.propMaterial : null;
            float len = length;
            var root = new GameObject("Kickstand").transform;
            root.SetParent(transform, false);
            root.localPosition = pivot;
            strut = root;
            int steps = Mathf.Max(2, Mathf.RoundToInt(len / 0.08f));
            PieceBits.Shape(root, "Strut", Vector3.zero, "kickstand_" + steps, hd, gr => StrutVoxel(gr, steps), s => StrutHD(s, len), mat);
            Pose(1f);
        }

        void Update()
        {
            if (!v) return;
            if (!PieceBits.Near(transform.position, Reach)) return;
            if (!built) Build();
            bool parked = !v.Occupied && Mathf.Abs(v.ForwardSpeed) < 0.6f && !v.aiDriven;
            float want = parked ? 1f : 0f;
            down = Mathf.MoveTowards(down, want, Time.deltaTime * (parked ? 3f : 6f));
            bool isDown = down > 0.95f;
            if (isDown != wasDown && (down < 0.05f || isDown))
            {
                wasDown = isDown;
                Kicks++;
                MadMax.Audio.Sfx.Play("click", strut.position, 0.4f, isDown ? 0.8f : 1.2f, 10f);
            }
            Pose(down);
        }

        void Pose(float d)
        {
            if (!strut) return;
            // down: hanging to the ground, splayed out to the left and raked back; up: folded back along the frame
            strut.localRotation = Quaternion.Slerp(Quaternion.Euler(Fold, 0f, 0f), Quaternion.Euler(-Back, 0f, -Out), d);
        }

        static void StrutVoxel(VoxelGrid g, int steps)
        {
            g.Mat((byte)MadMax.Items.ResourceType.Iron);
            for (int i = 0; i < steps; i++) g.Set(0, -i, 0, Pal.Solid(Pal.Metal[i % 3 == 0 ? 3 : 2]));
            g.Set(0, -steps, 0, Pal.Solid(Pal.Metal[1])); g.Set(0, -steps, 1, Pal.Solid(Pal.Metal[1]));   // the foot
        }

        static void StrutHD(HDShapes s, float len)
        {
            s.Tube(Vector3.zero, new Vector3(0f, -len, 0f), 0.014f, 0.011f, Pal.Metal[2], 6);
            s.Box(new Vector3(0f, -len, 0.02f), new Vector3(0.035f, 0.01f, 0.06f), Pal.Metal[1], 0.003f);
            s.Ellipsoid(Vector3.zero, new Vector3(0.02f, 0.02f, 0.02f), Pal.Metal[3], 6, 4);
        }
    }
}
