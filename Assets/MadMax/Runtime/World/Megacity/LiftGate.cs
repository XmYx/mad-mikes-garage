using UnityEngine;

namespace MadMax.World
{
    /// <summary>The barrier across one open end of a megacity lift shaft at a landing: up (solid) unless the platform is
    /// parked at that landing, then it sinks into the deck so people and cars can board.</summary>
    public class LiftGate : MonoBehaviour
    {
        public CityLift lift;
        public bool top;
        BoxCollider box;
        float rest, sunk;

        /// <summary>The barrier is up (blocks the shaft).</summary>
        public bool Closed => box && box.enabled;

        public static LiftGate Create(Transform parent, Vector3 local, Quaternion rot, float width, Mesh mesh, Material mat, CityLift lift, bool top)
        {
            var go = new GameObject("LiftGate", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var g = go.AddComponent<LiftGate>();
            g.lift = lift; g.top = top; g.rest = local.y;
            g.box = go.AddComponent<BoxCollider>();
            g.box.center = new Vector3(0f, 0.65f, 0f);
            g.box.size = new Vector3(0.2f, 1.3f, width);
            return g;
        }

        void Update()
        {
            if (!lift) return;
            bool open = lift.Parked(top);
            sunk = Mathf.MoveTowards(sunk, open ? 1.25f : 0f, Time.deltaTime * 2.5f);
            var lp = transform.localPosition; lp.y = rest - sunk; transform.localPosition = lp;
            box.enabled = sunk < 0.6f;
        }
    }
}
