using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public sealed partial class CityKit
    {
        /// <summary>One fixture of a kit that can be taken apart on its own (a railing section, a lamp post, a searchlight,
        /// a greenhouse, a cabin): the triangle ranges it wrote into the visible, glowing and solid meshes, its night lamps
        /// and its game-space bounds. <see cref="CitySalvage"/> blanks those ranges when the player takes it apart.</summary>
        public sealed class Mark
        {
            public string kind;
            public int t0, t1, g0, g1, s0, s1, l0, l1;
            internal int v0, gv0;
            public Bounds bounds;
        }

        /// <summary>Fixtures recorded with <see cref="BeginMark"/> / <see cref="EndMark"/> (only valid while the kit is
        /// meshed as it is, not after <see cref="Append"/>).</summary>
        public readonly List<Mark> marks = new List<Mark>();

        public Mark BeginMark(string kind) => new Mark
        {
            kind = kind, t0 = mesh.tris.Count, g0 = glow.tris.Count, s0 = solid.tris.Count, l0 = lamps.Count, v0 = mesh.verts.Count, gv0 = glow.verts.Count
        };

        public void EndMark(Mark m)
        {
            m.t1 = mesh.tris.Count; m.g1 = glow.tris.Count; m.s1 = solid.tris.Count; m.l1 = lamps.Count;
            bool any = false;
            var b = new Bounds();
            void Grow(List<Vector3> verts, int from)
            {
                for (int i = from; i < verts.Count; i++)
                {
                    if (!any) { b = new Bounds(verts[i], Vector3.zero); any = true; }
                    else b.Encapsulate(verts[i]);
                }
            }
            Grow(mesh.verts, m.v0);
            Grow(glow.verts, m.gv0);
            if (!any) return;
            m.bounds = b;
            marks.Add(m);
        }
    }
}
