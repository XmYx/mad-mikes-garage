using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public static partial class TrackedCity
    {
        /// <summary>Open deck spots (generator space, deck level) on free 1 m cells with clearance: where residents linger.</summary>
        public static List<Vector3> DeckSpots(Plan plan, int max = 48)
        {
            var g = plan.grid; var list = new List<Vector3>();
            var rnd = new Rng(plan.p.seed + 71);
            for (int tries = 0; tries < 4000 && list.Count < max; tries++)
            {
                int i = rnd.Range(2, g.nx - 2), j = rnd.Range(2, g.ny - 2);
                var c = g.Centre(i, j);
                if (!g.Free(c.x - 1.5f, c.x + 1.5f, c.y - 1.5f, c.y + 1.5f)) continue;
                bool near = false;
                foreach (var q in list) if ((new Vector2(q.x, q.y) - c).sqrMagnitude < 25f) { near = true; break; }
                if (!near) list.Add(new Vector3(c.x, c.y, plan.ZD));
            }
            return list;
        }

        /// <summary>Walking route (generator space) from in front of a block's porch to the middle of one of its rooms:
        /// through the entrance and lobby, up the stair core's two flights per storey, along the corridor and through
        /// the flat's door. Reverse it to come back down.</summary>
        public static List<Vector3> RouteIn(Plan plan, Room r)
        {
            var b = plan.blocks[r.block];
            Bays(b.w, out _, out float core);
            float cw = Bay - Inner, half = (cw - 0.2f) / 2, hd = b.d / 2;
            float cx0 = core - cw / 2, cx1 = core + cw / 2, cy0 = 0.9f + Inner, cy1 = hd - Wall;
            float fx1 = b.cx + cx0 + 0.05f + half / 2, fx2 = b.cx + cx1 - 0.05f - half / 2, x = b.cx + core;
            var pts = new List<Vector3>
            {
                new Vector3(x, b.cy - hd - 3.0f, b.z0),                        // in front of the porch
                new Vector3(x, b.cy - hd + 0.6f, b.z0 + Slab),                 // through the entrance into the lobby
                new Vector3(x, b.cy, b.z0 + Slab),                             // corridor
            };
            for (int f = 0; f < r.floor; f++)
            {
                float z = b.z0 + f * Storey + Slab;
                pts.Add(new Vector3(x, b.cy + cy0 + 0.2f, z));                               // into the core
                pts.Add(new Vector3(fx1, b.cy + cy0 + 0.35f, z));                            // foot of the first flight
                pts.Add(new Vector3(fx1, b.cy + cy1 - 0.6f, z + Storey * 0.5f));             // landing
                pts.Add(new Vector3(fx2, b.cy + cy1 - 0.6f, z + Storey * 0.5f));
                pts.Add(new Vector3(fx2, b.cy + cy0 + 0.35f, z + Storey));                   // top of the second flight
                pts.Add(new Vector3(x, b.cy + cy0 + 0.2f, z + Storey));
                pts.Add(new Vector3(x, b.cy, z + Storey));                                   // corridor above
            }
            float side = r.y0 < b.cy ? -1f : 1f;
            pts.Add(new Vector3(r.door.x, b.cy, r.z0));                                      // along the corridor to the door
            pts.Add(new Vector3(r.door.x, b.cy + side * 1.3f, r.z0));                        // through it
            pts.Add(new Vector3((r.x0 + r.x1) / 2 + 0.6f, (r.y0 + r.y1) / 2, r.z0));         // into the room
            return pts;
        }

        /// <summary>Where a shopkeeper stands in a ground-floor shop (between the counter and the shelves on the facade
        /// wall) and the generator-space direction they face (towards the corridor door).</summary>
        public static Vector3 KeeperSpot(Plan plan, Room r, out Vector3 facing)
        {
            var b = plan.blocks[r.block];
            bool minus = r.y0 < b.cy;
            float facade = minus ? r.y0 : r.y1, mid = (r.y0 + r.y1) / 2;
            facing = new Vector3(0f, minus ? 1f : -1f, 0f);
            return new Vector3((r.x0 + r.x1) / 2 - 0.4f, (mid + facade) / 2, r.z0);
        }

        /// <summary>The housing clerk's post: beside the gangway landing on the deck.</summary>
        public static Vector3 ClerkSpot(Plan plan) => new Vector3(-4.5f, -plan.Wd / 2 + 4.5f, plan.ZD);
    }
}
