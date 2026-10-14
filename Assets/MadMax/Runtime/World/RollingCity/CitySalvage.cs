using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The fixtures of one generated city piece that can be taken apart one by one (railing sections, lamp posts,
    /// searchlights, floods, greenhouses, cabins, pods: <see cref="CityKit.Mark"/>). Taking one apart blanks its triangles
    /// in the piece's visible, glowing and collision meshes and puts its night lamps out; the removal is remembered by
    /// city, piece and fixture index (<see cref="SalvageDesigns.CityRemoved"/>, saved) and applied again when the city is
    /// rebuilt.</summary>
    public class CitySalvage : MonoBehaviour
    {
        public static readonly List<CitySalvage> All = new List<CitySalvage>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public CityHost city;
        public string piece;
        public List<CityKit.Mark> marks;
        public MeshFilter visual, glow;
        public MeshCollider solid;
        public CityLights lights;
        public int lampBase;
        readonly HashSet<int> gone = new HashSet<int>();

        public static CitySalvage Attach(GameObject go, CityHost city, string piece, List<CityKit.Mark> marks, GameObject glow, CityLights lights, int lampBase)
        {
            var s = go.AddComponent<CitySalvage>();
            s.city = city; s.piece = piece; s.marks = marks; s.lights = lights; s.lampBase = lampBase;
            s.visual = go.GetComponent<MeshFilter>();
            s.glow = glow ? glow.GetComponent<MeshFilter>() : null;
            s.solid = go.GetComponent<MeshCollider>();
            return s;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        void Start() => StartCoroutine(ApplyWhenReady());

        IEnumerator ApplyWhenReady()
        {
            while (city && !city.IsReady) yield return null;      // collision meshes are still baking on workers before that
            ApplySaved();
        }

        public bool Ready => city && city.IsReady;
        public bool Gone(int i) => gone.Contains(i);

        /// <summary>Saved key of fixture <paramref name="i"/>: "city index/piece/fixture".</summary>
        public string KeyOf(int i) => CityHost.Hosts.IndexOf(city) + "/" + piece + "/" + i;

        /// <summary>The fixture nearest <paramref name="world"/> within <paramref name="reach"/> of its bounds (-1: none).</summary>
        public int Nearest(Vector3 world, float reach, out float dist)
        {
            dist = float.MaxValue;
            int best = -1;
            if (marks == null) return -1;
            var l = transform.InverseTransformPoint(world);
            for (int i = 0; i < marks.Count; i++)
            {
                if (gone.Contains(i)) continue;
                var b = marks[i].bounds;
                float d = Mathf.Sqrt(b.SqrDistance(l));
                if (d <= reach && d < dist) { dist = d; best = i; }
            }
            return best;
        }

        public Vector3 Centre(int i) => transform.TransformPoint(marks[i].bounds.center);
        public Bounds LocalBounds(int i) => marks[i].bounds;

        /// <summary>Re-apply the remembered removals of this piece (load, rebuild).</summary>
        public void ApplySaved()
        {
            if (!Ready || marks == null) return;
            bool any = false;
            for (int i = 0; i < marks.Count; i++)
                if (!gone.Contains(i) && SalvageDesigns.CityRemoved.Contains(KeyOf(i))) { Blank(i, false); any = true; }
            if (any) Recook();
        }

        /// <summary>Take fixture <paramref name="i"/> out (remembered).</summary>
        public void Remove(int i)
        {
            if (marks == null || i < 0 || i >= marks.Count || gone.Contains(i)) return;
            SalvageDesigns.CityRemoved.Add(KeyOf(i));
            if (!Ready) return;                                     // applied once the city is ready
            Blank(i, true);
        }

        void Blank(int i, bool recook)
        {
            gone.Add(i);
            var m = marks[i];
            if (visual) Clear(visual.sharedMesh, m.t0, m.t1);
            if (glow) Clear(glow.sharedMesh, m.g0, m.g1);
            if (solid && m.s1 > m.s0) { Clear(solid.sharedMesh, m.s0, m.s1); if (recook) Recook(); }
            if (lights)
                for (int k = m.l0; k < m.l1; k++)
                {
                    int at = lampBase + k;
                    if (at < 0 || at >= lights.lamps.Count) continue;
                    var lamp = lights.lamps[at];
                    lights.lamps[at] = (new Vector3(0f, -100000f, 0f), lamp.color, 0f);   // never the nearest again (indices stay)
                }
        }

        void Recook()
        {
            if (!solid || !solid.sharedMesh) return;
            var mesh = solid.sharedMesh;
            solid.sharedMesh = null;
            solid.sharedMesh = mesh;
        }

        static readonly List<int> indices = new List<int>();

        /// <summary>Degenerate the triangles [t0, t1) (index entries) of a runtime mesh.</summary>
        static void Clear(Mesh mesh, int t0, int t1)
        {
            if (!mesh || t1 <= t0 || !mesh.isReadable) return;
            indices.Clear();
            mesh.GetIndices(indices, 0);
            if (t1 > indices.Count) t1 = indices.Count;
            if (t0 >= t1) return;
            int keep = indices[t0];
            for (int k = t0; k < t1; k++) indices[k] = keep;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
        }
    }
}
