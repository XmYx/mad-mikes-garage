using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>What a moving-base build piece does (<see cref="RigVehicle"/>).</summary>
    public enum RigPart { Frame, Axle, Track, EngineBay, Seat, Tank, Suspension }

    /// <summary>A build piece of a moving base (FurnitureLibrary.Rig): a frame section (deck), an axle or track mount
    /// (a mirrored pair of wheel sockets at the frame's sides, with its beam, springs and hubs), an engine bay (engine
    /// socket), the driver's seat (the eye), a fuel tank (litres) or a suspension unit (spring and damper tune).
    /// Sockets live under the rig's "Sockets" (vehicle space: the drivetrain reads their z); they go with the piece.</summary>
    public class RigPiece : MonoBehaviour
    {
        public RigPart kind;
        /// <summary>Frames: half width (x) and half length (z) of the deck, m.</summary>
        public Vector2 half;
        /// <summary>Frames: deck top above the piece origin, m.</summary>
        public float top;
        /// <summary>Mounts: the biggest part size class taken.</summary>
        public int maxSize = 4;
        /// <summary>Mount look: 0 light, 1 medium, 2 heavy.</summary>
        public int grade;
        public float litres;
        /// <summary>Suspension tune; the highest <see cref="rank"/> on a rig wins.</summary>
        public float travel, frequency, damping, antiRoll; public int rank;
        /// <summary>Seats: driver eye in piece space.</summary>
        public Vector3 eye;
        /// <summary>Engine bay: the socket in piece space.</summary>
        public Vector3 socketAt;
        public float mass;

        readonly List<MountSocket> sockets = new List<MountSocket>();
        public IReadOnlyList<MountSocket> Sockets => sockets;
        Transform beam, hubR, hubL;

        /// <summary>Every socket of this piece holds a part (an axle with both wheels on).</summary>
        public bool Filled { get { if (sockets.Count == 0) return false; foreach (var s in sockets) if (!s || !s.Current) return false; return true; } }

        public bool IsMount => kind == RigPart.Axle || kind == RigPart.Track;

        public static RigPiece Add(GameObject go, RigPart kind, float mass)
        {
            var p = go.AddComponent<RigPiece>();
            p.kind = kind; p.mass = mass;
            return p;
        }

        void Start() { var rig = GetComponentInParent<RigVehicle>(); if (rig) rig.MarkDirty(); }

        /// <summary>Place the sockets (and the axle's beam / hubs) for the rig's frame as it is now.</summary>
        internal void Bind(RigVehicle rig)
        {
            var root = rig.transform;
            if (IsMount)
            {
                float z = transform.localPosition.z;
                rig.SidesAt(z, out float left, out float right);
                float y = transform.localPosition.y;
                string tag = (z >= 0f ? "+" : "") + z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                var r = Socket(rig, "wheel_" + tag + "_R", PartCategory.Wheel, new Vector3(right + 0.02f, y, z), Quaternion.identity, false);
                var l = Socket(rig, "wheel_" + tag + "_L", PartCategory.Wheel, new Vector3(left - 0.02f, y, z), Quaternion.identity, true);
                Visuals(r, l);
            }
            else if (kind == RigPart.EngineBay)
            {
                var at = root.InverseTransformPoint(transform.TransformPoint(socketAt));
                string tag = Mathf.RoundToInt(at.x * 10f) + "_" + Mathf.RoundToInt(at.z * 10f);
                Socket(rig, "engine_" + tag, PartCategory.Engine, at, transform.localRotation, false);
            }
        }

        MountSocket Socket(RigVehicle rig, string name, PartCategory accepts, Vector3 local, Quaternion rot, bool mirrored)
        {
            MountSocket s = null;
            foreach (var x in sockets) if (x && x.name == name) s = x;
            if (!s)
            {
                s = new GameObject(name).AddComponent<MountSocket>();
                s.transform.SetParent(rig.SocketRoot, false);
                s.accepts = accepts;
                s.SetMirrored(mirrored);
                sockets.Add(s);
            }
            s.maxSizeClass = maxSize;
            s.transform.localPosition = local;
            s.transform.localRotation = rot;
            return s;
        }

        // ------------------------------------------------------------------ axle / track running gear look
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => meshes.Clear();

        void Visuals(MountSocket r, MountSocket l)
        {
            var mr = GetComponent<MeshRenderer>();
            var mat = mr ? mr.sharedMaterial : null;
            if (!mat) return;
            string k = (kind == RigPart.Track ? "t" : "a") + grade;
            if (!beam) beam = Child("Beam", Mesh("beam" + k, () => Beam()), mat);
            if (!hubR) hubR = Child("Hub_R", Mesh("hub" + k, () => Hub()), mat);
            if (!hubL) hubL = Child("Hub_L", Mesh("hub" + k, () => Hub()), mat);
            var a = transform.InverseTransformPoint(l.transform.position);
            var b = transform.InverseTransformPoint(r.transform.position);
            float len = Mathf.Max(0.1f, b.x - a.x);
            beam.localPosition = new Vector3(a.x, a.y, a.z);
            beam.localScale = new Vector3(len / (BeamVox * VoxelMesher.DefaultSize), 1f, 1f);
            hubR.localPosition = b;
            hubL.localPosition = a; hubL.localScale = new Vector3(-1f, 1f, 1f);
        }

        Transform Child(string n, Mesh m, Material mat)
        {
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Mesh Mesh(string key, System.Func<VoxelGrid> make)
        {
            if (meshes.TryGetValue(key, out var m) && m) return m;
            var g = make();
            g.Bevel();
            return meshes[key] = VoxelMesher.Build(g, "Rig_" + key);
        }

        const int BeamVox = 10;
        float TubeR => grade == 0 ? 1.6f : grade == 1 ? 2.2f : 3.2f;

        /// <summary>Axle tube (track mounts: a box cross shaft), unit length along +x, scaled to the track width.</summary>
        VoxelGrid Beam()
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Iron);
            float r = TubeR;
            if (kind == RigPart.Track) { int h = Mathf.CeilToInt(r); g.Box(0, -h, -h, BeamVox - 1, h, h, Pal.Weathered(Pal.Ochre, 0.4f, 2301 + grade, 2, 0f)); }
            else g.CylX(0, 0, r, 0, BeamVox - 1, Pal.Weathered(Pal.RigGreen, 0.35f, 2302 + grade, 2, 0f));
            return g;
        }

        /// <summary>Right-hand end (mirrored for the left): axles get a brake drum with studs, a leaf-spring pack with
        /// U-bolts under the rail and its shackle; track mounts a planetary final drive and a track roller frame.</summary>
        VoxelGrid Hub()
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Iron);
            float r = TubeR;
            int s = grade + 1;
            if (kind == RigPart.Axle)
            {
                float dr = r * 2.1f;
                g.CylX(0, 0, dr, -3, -1, p => (p.y + p.z) % 3 == 0 ? Pal.Metal[1] : Pal.Metal[2]);                // brake drum (finned)
                g.CylX(0, 0, dr + 0.6f, -4, -4, Pal.Ramp(Pal.Rust, 2, 2310));                                        // backing plate
                for (int i = 0; i < 6; i++) { float t = i / 6f * Mathf.PI * 2f; g.Set(0, Mathf.RoundToInt(Mathf.Sin(t) * r), Mathf.RoundToInt(Mathf.Cos(t) * r), Pal.Solid(Pal.Chrome[2])); }
                int sx0 = -8 - s * 2, sx1 = sx0 + 2 + s, len = 6 + s * 4;
                for (int leaf = 0; leaf < 2 + s; leaf++)                                                              // leaves, shorter as they stack down
                {
                    int y = -Mathf.CeilToInt(r) - 1 - leaf, l2 = len - leaf * 2;
                    g.Box(sx0, y, -l2, sx1, y, l2, Pal.Ramp(Pal.Metal, leaf == 0 ? 2 : 1, 2311 + leaf));
                }
                foreach (int ux in new[] { sx0 - 1, sx1 + 1 }) g.Box(ux, -Mathf.CeilToInt(r) - 3 - s, -1, ux, Mathf.CeilToInt(r), -1, Pal.Solid(Pal.Chrome[1]));   // U-bolts
                g.Box(sx0, -Mathf.CeilToInt(r) - 1, len, sx1, 0, len, Pal.Ramp(Pal.Metal, 0, 2312));                // shackle up to the rail
                g.Box(sx0, -Mathf.CeilToInt(r) - 1, -len, sx1, 0, -len, Pal.Ramp(Pal.Metal, 0, 2313));               // fixed eye
                g.CylY(sx0 - 3, 4, 0.9f, -Mathf.CeilToInt(r) - 1, 0, Pal.Solid(Pal.Ochre[3]));                     // damper
            }
            else
            {
                float hr = r * 1.9f;
                g.CylX(0, 0, hr, -4, -1, Pal.Weathered(Pal.Ochre, 0.4f, 2320 + grade, 2, 0f));                     // final drive
                g.CylX(0, 0, hr * 0.55f, 0, 0, Pal.Solid(Pal.Chrome[2]));
                for (int i = 0; i < 8; i++) { float t = i / 8f * Mathf.PI * 2f; g.Set(-1, Mathf.RoundToInt(Mathf.Sin(t) * hr * 0.8f), Mathf.RoundToInt(Mathf.Cos(t) * hr * 0.8f), Pal.Solid(Pal.Chrome[1])); }
                int fl = 8 + s * 4, fy = -Mathf.CeilToInt(hr) + 1;
                g.Box(-4, fy - 2, -fl, -2, fy, fl, Pal.Weathered(Pal.Metal, 0.35f, 2321 + grade, 2, 0f));             // track roller frame
                for (int z = -fl + 2; z <= fl - 2; z += 5) g.Set(-3, fy - 3, z, Pal.Solid(Pal.Chrome[1]));          // roller bolts
                g.Box(-5, fy, -2, -5, 0, 2, Pal.Ramp(Pal.Rust, 2, 2322));                                            // gusset
            }
            return g;
        }

        // ------------------------------------------------------------------ removal
        void OnDestroy()
        {
            bool live = gameObject.scene.isLoaded && Application.isPlaying;
            var rig = GetComponentInParent<RigVehicle>();
            foreach (var s in sockets)
            {
                if (!s) continue;
                if (live && s.Current && (!rig || !rig.Dissolving)) s.Detach(true);                       // the wheel / engine drops off
                Destroy(s.gameObject);
            }
            sockets.Clear();
            if (rig) rig.MarkDirty();
        }
    }
}
