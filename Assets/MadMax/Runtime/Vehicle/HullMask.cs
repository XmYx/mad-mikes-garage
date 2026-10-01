using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Keeps the water surface out of a boat: the hull's plan-view outline (half-width per slice along its
    /// length, from the body mesh up to the gunwale) is uploaded each frame for the nearest <see cref="MaxHulls"/>
    /// boats, and <c>MadMax/Water</c> discards surface pixels inside a hull between its keel and gunwale. Outside the
    /// outline the waterline is untouched; inside it the hull (or its deck) hides the water anyway from above, so an
    /// open skiff, a raft riding low or a pitching bow never shows water on its floor. Added by <see cref="BoatModel"/>.</summary>
    [DisallowMultipleComponent]
    public class HullMask : MonoBehaviour
    {
        public const int MaxHulls = 8, Slices = 16;
        /// <summary>Hull-local extent along the keel and the clipped height band (keel .. gunwale), metres.</summary>
        public float zMin, zMax, yMin, yMax;
        /// <summary>Half-width of the outline per slice from <see cref="zMin"/> to <see cref="zMax"/>.</summary>
        public readonly float[] halfWidth = new float[Slices];
        public bool FromMesh { get; private set; }
        /// <summary>Index in this frame's shader arrays (-1: not uploaded).</summary>
        public int Slot { get; private set; } = -1;
        bool built;
        float sortKey;

        static readonly List<HullMask> all = new List<HullMask>();
        static readonly Matrix4x4[] mats = new Matrix4x4[MaxHulls];
        static readonly Vector4[] boxes = new Vector4[MaxHulls];
        static readonly Vector4[] widths = new Vector4[MaxHulls * Slices / 4];
        static int uploadedFrame = -1;
        static readonly System.Comparison<HullMask> byKey = (a, b) => a.sortKey.CompareTo(b.sortKey);
        static readonly int MatId = Shader.PropertyToID("_MadMaxHullW2L"), BoxId = Shader.PropertyToID("_MadMaxHullBox"),
            WidthId = Shader.PropertyToID("_MadMaxHullWidth"), CountId = Shader.PropertyToID("_MadMaxHullCount");
        /// <summary>Hulls uploaded this frame.</summary>
        public static int Uploaded { get; private set; }
        public static IReadOnlyList<HullMask> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); uploadedFrame = -1; Uploaded = 0; }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() { all.Remove(this); Slot = -1; if (all.Count == 0) { Uploaded = 0; Shader.SetGlobalFloat(CountId, 0f); } }

        void LateUpdate()
        {
            if (!built) Build();
            if (uploadedFrame != Time.frameCount) Upload();
        }

        /// <summary>Measure the outline from the body meshes (or the design's hull box with a pointed bow).</summary>
        public void Build()
        {
            built = true;
            var boat = GetComponent<BoatModel>();
            float keel = boat ? boat.keelY : 0f, top = boat ? boat.keelY + boat.hull.y + 0.12f : 1f;
            yMin = keel - 0.06f; yMax = top;
            for (int i = 0; i < Slices; i++) halfWidth[i] = 0f;
            var verts = new List<Vector3>();
            var scratch = new List<Vector3>();
            var body = transform.Find("Body");
            float z0 = float.MaxValue, z1 = float.MinValue;
            if (body)
                foreach (var mf in body.GetComponentsInChildren<MeshFilter>())
                {
                    var m = mf.sharedMesh;
                    if (!m || !m.isReadable || mf.name == "Driver") continue;
                    var toRoot = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    m.GetVertices(scratch);                                                      // replaces the list's contents
                    foreach (var v in scratch)
                    {
                        var p = toRoot.MultiplyPoint3x4(v);
                        verts.Add(p);
                        if (p.y > top) continue;
                        z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
                    }
                }
            FromMesh = z1 > z0 + 0.2f;
            if (FromMesh)
            {
                zMin = z0; zMax = z1;
                float span = (zMax - zMin) / Slices;
                foreach (var p in verts)
                {
                    if (p.y > top || p.y < yMin) continue;
                    // a vertex bounds the outline in its slice (and the neighbour it touches: face corners sit on slice edges)
                    float s = (p.z - zMin) / span;
                    int k = Mathf.Clamp(Mathf.FloorToInt(s), 0, Slices - 1);
                    float ax = Mathf.Abs(p.x);
                    halfWidth[k] = Mathf.Max(halfWidth[k], ax);
                    float frac = s - k;
                    if (frac < 0.02f && k > 0) halfWidth[k - 1] = Mathf.Max(halfWidth[k - 1], ax);
                    if (frac > 0.98f && k < Slices - 1) halfWidth[k + 1] = Mathf.Max(halfWidth[k + 1], ax);
                }
                for (int i = 1; i < Slices - 1; i++)                                             // gaps between sparse slices
                    if (halfWidth[i] <= 0f) halfWidth[i] = Mathf.Min(halfWidth[i - 1], halfWidth[i + 1]);
            }
            else
            {
                var h = boat ? boat.hull : new Vector3(1.6f, 0.8f, 4f);
                zMin = -h.z * 0.5f; zMax = h.z * 0.5f;
                for (int i = 0; i < Slices; i++)
                {
                    float f = (i + 0.5f) / Slices, bow = Mathf.Clamp01((f - 0.75f) / 0.25f);
                    halfWidth[i] = h.x * 0.5f * Mathf.Sqrt(1f - bow * bow);
                }
            }
        }

        /// <summary>Half-width of the outline at hull-local <paramref name="z"/> (as the shader interpolates it).</summary>
        public float WidthAt(float z)
        {
            float s = Mathf.Clamp01((z - zMin) / Mathf.Max(1e-4f, zMax - zMin)) * (Slices - 1);
            int k = Mathf.Min(Mathf.FloorToInt(s), Slices - 2);
            return Mathf.Lerp(halfWidth[k], halfWidth[k + 1], s - k);
        }

        /// <summary>Does the water shader hide the surface at this world point (the same test as <c>MadMax/Water</c>)?</summary>
        public bool Covers(Vector3 world)
        {
            if (!built) Build();
            var lp = transform.worldToLocalMatrix.MultiplyPoint3x4(world);
            if (lp.z <= zMin || lp.z >= zMax || lp.y < yMin || lp.y > yMax) return false;
            return Mathf.Abs(lp.x) < WidthAt(lp.z);
        }

        static void Upload()
        {
            uploadedFrame = Time.frameCount;
            var g = MadMax.Game.WastelandGame.Instance;
            var cam = Camera.main;
            Vector3 focus = g && g.Current ? g.Current.transform.position : g && g.Player ? g.Player.transform.position : cam ? cam.transform.position : Vector3.zero;
            foreach (var m in all) { m.Slot = -1; m.sortKey = m ? (m.transform.position - focus).sqrMagnitude : float.MaxValue; }
            all.Sort(byKey);
            int n = 0;
            foreach (var m in all)
            {
                if (n >= MaxHulls || !m || m.sortKey > 400f * 400f) break;
                if (!m.built) m.Build();
                mats[n] = m.transform.worldToLocalMatrix;
                boxes[n] = new Vector4(m.zMin, m.zMax, m.yMin, m.yMax);
                for (int q = 0; q < Slices / 4; q++)
                    widths[n * (Slices / 4) + q] = new Vector4(m.halfWidth[q * 4], m.halfWidth[q * 4 + 1], m.halfWidth[q * 4 + 2], m.halfWidth[q * 4 + 3]);
                m.Slot = n++;
            }
            Uploaded = n;
            Shader.SetGlobalMatrixArray(MatId, mats);
            Shader.SetGlobalVectorArray(BoxId, boxes);
            Shader.SetGlobalVectorArray(WidthId, widths);
            Shader.SetGlobalFloat(CountId, n);
        }
    }
}
