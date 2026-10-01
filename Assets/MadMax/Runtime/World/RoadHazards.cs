using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Caltrop scatters and oil slicks dropped behind vehicles (rear dropper). Wheels rolling over caltrops
    /// puncture; oil takes most of their grip (VehicleDriver). Patches wear off after a few minutes.</summary>
    public class RoadHazards : MonoBehaviour
    {
        public enum Kind { Caltrops, Oil }
        struct Patch { public Kind kind; public Vector3 pos; public float radius, until; public GameObject visual; }

        static readonly List<Patch> patches = new List<Patch>();
        static RoadHazards instance;
        static Mesh caltropMesh;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { patches.Clear(); instance = null; caltropMesh = null; }

        public static int Count => patches.Count;

        /// <summary>Hazard under a point (wheel contact), if any.</summary>
        public static bool At(Vector3 p, out Kind kind)
        {
            foreach (var h in patches)
            {
                float dx = p.x - h.pos.x, dz = p.z - h.pos.z;
                if (dx * dx + dz * dz < h.radius * h.radius && Mathf.Abs(p.y - h.pos.y) < 1.2f) { kind = h.kind; return true; }
            }
            kind = Kind.Caltrops;
            return false;
        }

        public static void Drop(Kind kind, Vector3 pos, float radius)
        {
            if (!instance) instance = new GameObject("RoadHazards").AddComponent<RoadHazards>();
            var terrain = DeformableTerrain.Instance;
            if (terrain) pos.y = terrain.Height(pos.x, pos.z);
            var visual = new GameObject(kind == Kind.Oil ? "OilSlick" : "Caltrops");
            visual.transform.SetParent(instance.transform, false);
            visual.transform.position = pos;
            if (kind == Kind.Oil) Slick(visual, radius);
            else Scatter(visual, radius, pos);
            patches.Add(new Patch { kind = kind, pos = pos, radius = radius, until = Time.time + (kind == Kind.Oil ? 240f : 180f), visual = visual });
            if (patches.Count > 24) { if (patches[0].visual) Destroy(patches[0].visual); patches.RemoveAt(0); }
        }

        static void Slick(GameObject go, float radius)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(go.transform, false);
            q.transform.localPosition = Vector3.up * 0.03f;
            q.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            q.transform.localScale = new Vector3(radius * 2.2f, radius * 1.6f, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = Fx.TransparentMaterial(null);
            r.material.color = new Color(0.03f, 0.03f, 0.04f, 0.85f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Scatter(GameObject go, float radius, Vector3 pos)
        {
            if (!caltropMesh)
            {
                var g = new VoxelGrid();
                g.Box(-1, 0, 0, 1, 0, 0, Pal.Ramp(Pal.Metal, 2)); g.Box(0, 0, -1, 0, 0, 1, Pal.Ramp(Pal.Metal, 2)); g.Set(0, 1, 0, Pal.Solid(Pal.Chrome[2]));
                caltropMesh = VoxelMesher.Build(g, "Caltrop", 0.04f);
            }
            var game = MadMax.Game.WastelandGame.Instance;
            var mat = game ? game.propMaterial : null;
            var terrain = DeformableTerrain.Instance;
            for (int i = 0; i < 14; i++)
            {
                var c = new GameObject("Caltrop", typeof(MeshFilter), typeof(MeshRenderer));
                c.transform.SetParent(go.transform, false);
                var off = Random.insideUnitCircle * radius;
                var p = pos + new Vector3(off.x, 0f, off.y);
                if (terrain) p.y = terrain.Height(p.x, p.z) + 0.02f;
                c.transform.position = p;
                c.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                c.GetComponent<MeshFilter>().sharedMesh = caltropMesh;
                c.GetComponent<MeshRenderer>().sharedMaterial = mat;
                MadMax.Rendering.HDVisual.Dress(c, MadMax.Rendering.HDDomain.World, "Caltrop", MadMax.Rendering.HDAssets.NoShadow);
            }
        }

        void Update()
        {
            for (int i = patches.Count - 1; i >= 0; i--)
                if (Time.time > patches[i].until) { if (patches[i].visual) Destroy(patches[i].visual); patches.RemoveAt(i); }
        }
    }
}
