using System.Threading.Tasks;
using MadMax.Building;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A rolling city's dock (roadmap 28): the pier at deck height beside the circuit where the gangway lands,
    /// with a ramp down to the graded yard. Static; the pier and the ramp are StructureGround decks for wheels and the
    /// mesh collider carries walkers.</summary>
    public class CityDock : MonoBehaviour
    {
        public int index;
        public string dockName;

        /// <summary>World frame of dock k: origin at the pier's inner edge (deck height), +x away from the circuit, +z along it.</summary>
        public static void Frame(CityRoute route, int k, out Vector3 origin, out Quaternion rot)
        {
            var bed = route.At(route.docks[k], out var fwd);
            var right = Vector3.Cross(Vector3.up, fwd);
            origin = bed + right * (CityRoute.HalfWidth + CityDesign.PierGap) + Vector3.up * CityRoute.DeckHeight;
            rot = Quaternion.LookRotation(fwd, Vector3.up);
        }

        /// <summary>Pier-top world point at a dock-local spot (metres: x from the inner edge, z along).</summary>
        public Vector3 PierPoint(float x, float z) => transform.TransformPoint(new Vector3(x, 0f, z));

        public static CityDock Create(CityRoute route, int k, int seed, Material mat, Task<VoxelMesher.MeshData> data)
        {
            Frame(route, k, out var origin, out var rot);
            var go = new GameObject("CityDock_" + route.dockNames[k]);
            go.transform.SetPositionAndRotation(origin, rot);
            var dock = go.AddComponent<CityDock>();
            dock.index = k; dock.dockName = route.dockNames[k];
            var half = Vector3.one * (CityDesign.V * 0.5f);
            var vis = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            vis.transform.SetParent(go.transform, false);
            vis.transform.localPosition = half;
            var mesh = VoxelMesher.ToMesh(data.Result, "CityDock");
            vis.GetComponent<MeshFilter>().sharedMesh = mesh;
            vis.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = vis.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            // decks for wheels: the pier (flat) and the ramp (sloped along its local z, from the pier down to the yard)
            float V = CityDesign.V;
            var pier = new GameObject("PierDeck").transform;
            pier.SetParent(go.transform, false);
            pier.localPosition = new Vector3(CityDesign.PierW * V * 0.5f, 0f, 0f);
            StructureGround.AddDeck(pier, pier, col, new Vector4(CityDesign.PierW * V * 0.5f, CityDesign.PierHalf * V, 0f, 0f));
            float drop = (-CityDesign.DockGround - 1) * V, len = CityDesign.RampLen * V;
            var ramp = new GameObject("RampDeck").transform;
            ramp.SetParent(go.transform, false);
            ramp.localPosition = new Vector3(CityDesign.PierW * V + len * 0.5f, -drop * 0.5f, 0f);
            ramp.localRotation = Quaternion.Euler(0f, 90f, 0f);                                    // local +z runs out, down the ramp
            StructureGround.AddDeck(ramp, ramp, col, new Vector4(CityDesign.RampHalf * V, len * 0.5f, drop * 0.5f, -drop * 0.5f));
            return dock;
        }

        void OnDestroy()
        {
            foreach (Transform c in transform) StructureGround.Remove(c);
        }
    }
}
