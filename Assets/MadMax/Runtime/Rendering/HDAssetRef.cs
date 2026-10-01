using System;
using UnityEngine;

namespace MadMax.Rendering
{
    /// <summary>What the game uses an HD asset for (the folder under <c>Resources/HDGen</c>).</summary>
    public enum HDDomain { World, Furniture, Item, Tool, Animal, Part }

    /// <summary>One exported HD asset as the runtime sees it (written by the editor's <c>HDCatalogBuilder</c> from the
    /// sidecar into <c>Resources/HDGen/&lt;Domain&gt;/&lt;id&gt;.asset</c>, loaded on demand by <see cref="HDAssets"/>): the
    /// imported model, its bounds and the per-object data the game needs (part names, movers, markers, pivots).</summary>
    public class HDAssetRef : ScriptableObject
    {
        public string id, group, kind, category;
        public HDDomain domain;
        public GameObject model;
        public Vector3 boundsMin, boundsMax;
        [Tooltip("Voxel size of the matching game template (m); 0 = not a voxel replacement.")] public float voxel;
        [Tooltip("Scale of the model lying in the world (tools: 0.7 = the hand scale laid down).")] public float worldScale = 1f;
        public string origin;
        public bool sway;
        public ObjectInfo[] objects = new ObjectInfo[0];

        [Serializable]
        public class ObjectInfo
        {
            public string name, parent, role, part, rigPart;
            public int index = -1;
            public bool mover, shell, marker;
            public Matrix4x4 rootMatrix = Matrix4x4.identity;
            public Vector3 boundsMin, boundsMax;
        }

        public Bounds Bounds => new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);

        public ObjectInfo Find(Func<ObjectInfo, bool> match)
        {
            foreach (var o in objects) if (match(o)) return o;
            return null;
        }

        /// <summary>A marker (empty) by its short name (<c>grip</c>, <c>muzzle</c>; exported as <c>&lt;id&gt;:&lt;name&gt;</c>).</summary>
        public bool Marker(string name, out Vector3 at)
        {
            at = Vector3.zero;
            foreach (var o in objects)
            {
                if (!o.marker) continue;
                string n = o.name; int c = n.LastIndexOf(':');
                if ((c >= 0 ? n.Substring(c + 1) : n) != name) continue;
                at = o.rootMatrix.GetColumn(3);
                return true;
            }
            return false;
        }
    }
}
