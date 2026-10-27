using System.Collections.Generic;
using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Small moving bits a piece's code adds at runtime (gauge markers, flames, a fish in a trap): meshes built
    /// once per key in the look in use (voxels with the piece's material, or smooth <see cref="HDShapes"/> with the HD
    /// pack on), and flat glowing cubes for flames. Nothing allocates after the first build.</summary>
    public static class PieceBits
    {
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<Color32, Material> glows = new Dictionary<Color32, Material>();
        static Mesh cube;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { meshes.Clear(); glows.Clear(); cube = null; }

        /// <summary>The HD look is in use on this piece (pack on and the piece wears its HD model).</summary>
        public static bool HD(GameObject piece) => HDBits.On && piece && piece.TryGetComponent<HDVisual>(out var v) && v.root;

        /// <summary>A child carrying the mesh <paramref name="key"/>: <paramref name="voxel"/> paints a 0.08 m grid
        /// (origin = the child's pivot) for the voxel look, <paramref name="smooth"/> the HD shapes.</summary>
        public static Transform Shape(Transform parent, string name, Vector3 local, string key, bool hd,
                                      System.Action<VoxelGrid> voxel, System.Action<HDShapes> smooth, Material voxelMat)
        {
            string k = (hd ? "hd:" : "vx:") + key;
            if (!meshes.TryGetValue(k, out var mesh) || !mesh)
            {
                if (hd) { var s = new HDShapes(); smooth(s); mesh = s.ToMesh("Bit_" + key + "_HD"); }
                else { var g = new VoxelGrid(); voxel(g); g.Bevel(); mesh = VoxelMesher.Build(g, "Bit_" + key); }
                meshes[k] = mesh;
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = hd ? HDShapes.Solid : voxelMat;
            return go.transform;
        }

        /// <summary>A flat glowing cube (flames, embers): unlit, no shadows, one material per colour.</summary>
        public static Transform Glow(Transform parent, string name, Vector3 local, Vector3 size, Color32 c)
        {
            if (!cube) { var t = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = t.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(t); }
            if (!glows.TryGetValue(c, out var m) || !m)
            {
                m = new Material(MadMax.World.Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
                m.SetColor("_BaseColor", (Color)c);
                glows[c] = m;
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = cube;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>Within <paramref name="reach"/> m of the followed target (the player or the driven vehicle).</summary>
        public static bool Near(Vector3 p, float reach)
        {
            var g = MadMax.Game.WastelandGame.Instance;
            Vector3 f;
            if (g && (g.Current || g.Player)) f = g.FocusPos;
            else { var cam = Camera.main; if (!cam) return false; f = cam.transform.position; }
            return (p - f).sqrMagnitude < reach * reach;
        }
    }
}
