using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MadMax.EditorTools
{
    /// <summary>The per-asset sidecar written by tools/blender/hd/export/export_hd.py (<c>&lt;Asset&gt;.hd.json</c> next to the
    /// FBX). Positions are Unity/game space in metres; <c>position/rotation/scale</c> are local to the logical parent,
    /// <c>rootPosition/rootMatrix</c> to the prefab root. Schema: tools/blender/hd/PIPELINE.md.</summary>
    [Serializable]
    public class HDSidecar
    {
        public int format, exporter;
        public string gameId;
        public float gameScale = 1f;
        public string asset, group, kind, source, units, axes, rootName, fbx, lodSuffix, splitSuffix;
        public bool mirroredOnExport, readable, bakeAxisConversion;
        public HDProp[] rootProps;
        public HDAtlas[] atlases;
        public HDMatClass[] materials;
        public string[] paintMaterials;
        public HDObject[] objects;
        public HDBone[] bones;
        public float[] lodRatios, boundsMin, boundsMax;
        public int[] tris;
        public HDFile[] files;

        [NonSerialized] public string dir;
        [NonSerialized] Dictionary<string, HDObject> byName;

        public const string Root = "Assets/MadMax/Models/HD";

        public static string PathFor(string modelPath) =>
            Path.Combine(Path.GetDirectoryName(modelPath) ?? "", Path.GetFileNameWithoutExtension(modelPath) + ".hd.json").Replace('\\', '/');

        public static HDSidecar Load(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath)) return null;
            var s = JsonUtility.FromJson<HDSidecar>(File.ReadAllText(jsonPath));
            if (s == null) return null;
            s.dir = Path.GetDirectoryName(jsonPath)?.Replace('\\', '/');
            return s;
        }

        /// <summary>The sidecar of an asset by name, searched in every group folder.</summary>
        public static HDSidecar Find(string asset)
        {
            if (!Directory.Exists(Root)) return null;
            foreach (var g in Directory.GetDirectories(Root))
            {
                var p = Path.Combine(g, asset, asset + ".hd.json").Replace('\\', '/');
                if (File.Exists(p)) return Load(p);
            }
            return null;
        }

        public string ModelPath => dir + "/" + (string.IsNullOrEmpty(fbx) ? asset + ".fbx" : fbx);

        public HDObject Get(string name)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, HDObject>();
                if (objects != null) foreach (var o in objects) byName[o.name] = o;
            }
            return name != null && byName.TryGetValue(name, out var r) ? r : null;
        }

        public HDAtlas Atlas(string name)
        {
            if (atlases != null) foreach (var a in atlases) if (a.name == name) return a;
            return null;
        }

        /// <summary>Objects whose logical parent is <paramref name="parent"/> with the given role ("glass", "lamp_head",
        /// "lod1", ...); null role = any.</summary>
        public IEnumerable<HDObject> Children(string parent, string role = null)
        {
            if (objects == null) yield break;
            foreach (var o in objects) if (o.parent == parent && (role == null || o.role == role)) yield return o;
        }

        public HDObject WithProp(string key, string value)
        {
            if (objects == null) return null;
            foreach (var o in objects) if (o.role == "mesh" && o.Prop(key) == value) return o;
            return null;
        }

        public Bounds Size => new Bounds((V(boundsMin) + V(boundsMax)) * 0.5f, V(boundsMax) - V(boundsMin));

        public static Vector3 V(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
    }

    [Serializable]
    public class HDAtlas
    {
        public string name, baseMap, maskMap, normalMap, emissionMap;
        public int size;
        public float emissionScale = 1f, swayTip;
        public float[] paintRef;
        public bool hasAlpha;
        public string[] materials;
    }

    [Serializable] public class HDMatClass { public string name, cls; }
    [Serializable] public class HDProp { public string k, s; public float n; public float[] v; }
    [Serializable] public class HDFile { public string file; public long bytes; }

    [Serializable]
    public class HDBone
    {
        public string name, parent;
        public float[] head, tail, matrix;
    }

    [Serializable]
    public class HDObject
    {
        public string name, parent, parentBone, type, role, mesh, atlas;
        public float[] position, rotation, scale, rootPosition, rootMatrix, boundsMin, boundsMax;
        public HDProp[] props;
        public int tris, verts;
        public string[] materials, classes;
        public bool skinned;

        public string Prop(string key)
        {
            if (props != null) foreach (var p in props) if (p.k == key) return p.s;
            return null;
        }

        public float PropFloat(string key, float fallback)
        {
            if (props != null)
                foreach (var p in props)
                {
                    if (p.k != key) continue;
                    if (p.n != 0f) return p.n;
                    return float.TryParse(p.s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : p.n;   // floats written as text
                }
            return fallback;
        }

        /// <summary>Rotation relative to the prefab root (from <see cref="RootMatrix"/>; a mirror on X stays in the scale).</summary>
        public Quaternion RootRotation
        {
            get
            {
                var m = RootMatrix;
                Vector3 f = m.GetColumn(2), u = m.GetColumn(1);
                if (f.sqrMagnitude < 1e-8f || u.sqrMagnitude < 1e-8f) return Quaternion.identity;
                return Quaternion.LookRotation(f, u);
            }
        }

        public bool Has(string key)
        {
            if (props != null) foreach (var p in props) if (p.k == key) return true;
            return false;
        }

        public Vector3 LocalPosition => HDSidecar.V(position);
        public Quaternion LocalRotation => rotation != null && rotation.Length >= 4 ? new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]) : Quaternion.identity;
        public Vector3 LocalScale => scale != null && scale.Length >= 3 ? HDSidecar.V(scale) : Vector3.one;
        public Vector3 RootPosition => HDSidecar.V(rootPosition);
        /// <summary>Mesh bounds in the object's own frame.</summary>
        public Bounds MeshBounds => new Bounds((HDSidecar.V(boundsMin) + HDSidecar.V(boundsMax)) * 0.5f, HDSidecar.V(boundsMax) - HDSidecar.V(boundsMin));
        public Matrix4x4 RootMatrix
        {
            get
            {
                if (rootMatrix == null || rootMatrix.Length < 16) return Matrix4x4.TRS(RootPosition, Quaternion.identity, Vector3.one);
                var m = new Matrix4x4();
                for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) m[r, c] = rootMatrix[r * 4 + c];
                return m;
            }
        }

        /// <summary>Mesh bounds in prefab-root space (axis-aligned box around the transformed mesh box).</summary>
        public Bounds RootBounds
        {
            get
            {
                var b = MeshBounds; var m = RootMatrix;
                var r = new Bounds(m.MultiplyPoint3x4(b.min), Vector3.zero);
                for (int i = 1; i < 8; i++)
                    r.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) != 0 ? b.max.x : b.min.x, (i & 2) != 0 ? b.max.y : b.min.y, (i & 4) != 0 ? b.max.z : b.min.z)));
                return r;
            }
        }
    }
}
