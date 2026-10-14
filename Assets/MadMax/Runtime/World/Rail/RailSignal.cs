using UnityEngine;

namespace MadMax.World
{
    /// <summary>A colour-light signal at a station throat: red while a train stands in (or runs through) the station,
    /// green when the road is clear.</summary>
    public class RailSignal : MonoBehaviour
    {
        RailStation st;
        GameObject red, green;
        float check;

        public static RailSignal Create(RailStation st, Vector3 at, float yaw, Material mat, Material glow, Transform parent)
        {
            var go = new GameObject("Signal", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            go.GetComponent<MeshFilter>().sharedMesh = RailProps.Signal;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 2.4f, 0f); bc.size = new Vector3(0.3f, 4.8f, 0.3f);
            var s = go.AddComponent<RailSignal>();
            s.st = st;
            s.red = Lens(go.transform, RailProps.LensRed, glow, 4.6f);
            s.green = Lens(go.transform, RailProps.LensGreen, glow, 4.0f);
            return s;
        }

        /// <summary>A built signal piece (no station): red while a train is within 120 m of it.</summary>
        public static RailSignal Attach(GameObject go, float headY, Material glow)
        {
            var s = go.AddComponent<RailSignal>();
            s.red = Lens(go.transform, RailProps.LensRed, glow, headY + 0.3f);
            s.green = Lens(go.transform, RailProps.LensGreen, glow, headY - 0.3f);
            return s;
        }

        static GameObject Lens(Transform parent, Mesh mesh, Material glow, float y)
        {
            var g = new GameObject("Lens", typeof(MeshFilter), typeof(MeshRenderer));
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(0f, y, 0.16f);
            g.GetComponent<MeshFilter>().sharedMesh = mesh;
            g.GetComponent<MeshRenderer>().sharedMaterial = glow;
            return g;
        }

        void Update()
        {
            if (Time.time < check || !red) return;
            check = Time.time + 0.5f;
            bool occupied = false;
            float r = st != null ? st.half + 25f : 120f;
            var c0 = st != null ? st.pos : new Vector2(transform.position.x, transform.position.z);
            foreach (var t in Train.All)
                foreach (var c in t.cars)
                    if (c && (new Vector2(c.transform.position.x, c.transform.position.z) - c0).sqrMagnitude < r * r) { occupied = true; break; }
            red.SetActive(occupied); green.SetActive(!occupied);
        }
    }
}
