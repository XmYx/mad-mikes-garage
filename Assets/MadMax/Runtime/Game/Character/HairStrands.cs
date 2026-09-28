using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Verlet hair: chains hang from the head, swing with movement and wind, collide with head and shoulders,
    /// and spring back towards their rest direction. Each chain segment is a small voxel strand mesh.</summary>
    public class HairStrands : MonoBehaviour
    {
        class Chain
        {
            public Vector3 rootLocal, restDir;
            public Vector3[] pos, prev;
            public Transform[] segs;
        }

        readonly List<Chain> chains = new List<Chain>();
        HumanRig rig;
        Transform head, chest;
        float segLen = 0.06f;
        public float stiffness = 6f, damping = 0.92f;
        Mesh strandMesh;

        public void Init(HumanRig r)
        {
            rig = r;
            head = r.Bone(BodyPart.Head);
            chest = r.Bone(BodyPart.Chest);
            var a = r.appearance;
            var col = HumanDesign.HairColors[Mathf.Clamp(a.hairColor, 0, HumanDesign.HairColors.Length - 1)];
            float h = a.height;
            int nodes;
            var roots = new List<Vector3>();
            if (a.hair == HairStyle.Long)
            {
                nodes = 6; segLen = 0.055f;
                for (int i = 0; i < 7; i++)
                {
                    float ang = Mathf.Lerp(-110f, 110f, i / 6f) * Mathf.Deg2Rad;          // around the back of the head
                    roots.Add(new Vector3(Mathf.Sin(ang) * 0.075f, 0.2f * h, -Mathf.Cos(ang) * 0.08f));
                }
            }
            else if (a.hair == HairStyle.Ponytail) { nodes = 6; segLen = 0.055f; roots.Add(new Vector3(0, 0.25f * h, -0.1f)); roots.Add(new Vector3(0.012f, 0.25f * h, -0.1f)); }
            else if (a.hair == HairStyle.Mohawk) { nodes = 3; segLen = 0.04f; roots.Add(new Vector3(0, 0.25f * h, -0.08f)); }
            else return;

            strandMesh = HumanDesign.HairStrandMesh(col, segLen);
            foreach (var root in roots)
            {
                var c = new Chain { rootLocal = root, restDir = new Vector3(root.x * 1.5f, -1f, root.z * 3f).normalized, pos = new Vector3[nodes], prev = new Vector3[nodes], segs = new Transform[nodes - 1] };
                for (int i = 0; i < nodes - 1; i++)
                {
                    var go = new GameObject("HairSeg", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = strandMesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = rig.material;
                    c.segs[i] = go.transform;
                }
                chains.Add(c);
            }
            ResetChains();
        }

        void OnDestroy()
        {
            foreach (var c in chains) foreach (var s in c.segs) if (s) Destroy(s.gameObject);
            if (strandMesh) Destroy(strandMesh);
        }

        public void SetVisible(bool v)
        {
            foreach (var c in chains) foreach (var s in c.segs) if (s) s.GetComponent<Renderer>().enabled = v;
        }

        public void ResetChains()
        {
            if (!head) return;
            foreach (var c in chains)
            {
                var root = head.TransformPoint(c.rootLocal);
                var dir = head.TransformDirection(c.restDir);
                for (int i = 0; i < c.pos.Length; i++) c.pos[i] = c.prev[i] = root + dir * segLen * i;
            }
        }

        void LateUpdate()
        {
            if (!head || chains.Count == 0) return;
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            if (dt <= 0f) return;
            var headCenter = head.TransformPoint(new Vector3(0, 0.2f * rig.appearance.height, 0));
            var chestCenter = chest.TransformPoint(new Vector3(0, 0.3f, -0.02f));
            var wind = new Vector3(Mathf.PerlinNoise(Time.time * 0.7f, 0.3f) - 0.4f, 0, Mathf.PerlinNoise(0.8f, Time.time * 0.6f) - 0.5f) * 1.5f;
            foreach (var c in chains)
            {
                c.pos[0] = head.TransformPoint(c.rootLocal);
                var rest = head.TransformDirection(c.restDir);
                if ((c.pos[1] - c.pos[0]).sqrMagnitude > 1f) ResetChains();               // teleported
                for (int i = 1; i < c.pos.Length; i++)
                {
                    var p = c.pos[i];
                    var vel = (p - c.prev[i]) * damping;
                    c.prev[i] = p;
                    var target = c.pos[i - 1] + rest * segLen;
                    p += vel + (Physics.gravity * 0.6f + wind + (target - p) * stiffness * 30f) * dt * dt;
                    // distance constraint
                    var d = p - c.pos[i - 1];
                    p = c.pos[i - 1] + d.normalized * segLen;
                    // collisions: skull and upper back / shoulders
                    p = PushOut(p, headCenter, 0.115f);
                    p = PushOut(p, chestCenter, 0.16f);
                    c.pos[i] = p;
                }
                for (int i = 0; i < c.segs.Length; i++)
                {
                    var a = c.pos[i]; var b = c.pos[i + 1];
                    var dir = b - a;
                    c.segs[i].position = a;
                    if (dir.sqrMagnitude > 1e-6f) c.segs[i].rotation = Quaternion.FromToRotation(Vector3.down, dir.normalized);
                }
            }
        }

        static Vector3 PushOut(Vector3 p, Vector3 c, float r)
        {
            var d = p - c;
            float m = d.magnitude;
            return m < r && m > 1e-5f ? c + d / m * r : p;
        }
    }
}
