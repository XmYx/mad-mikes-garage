using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Fine industrial geometry layered over the walkable city: smooth hollow stacks, pipe flanges,
    /// window reveals, braces and machinery. Pure mesh data, so generation remains deterministic and worker-safe.</summary>
    public static class CityDetail
    {
        sealed class Builder
        {
            public readonly VoxelMesher.MeshData data = new VoxelMesher.MeshData();
            void Vertex(Vector3 p, Vector3 n, Color32 c)
            { data.verts.Add(p); data.norms.Add(n); data.uv3.Add(n); data.cols.Add(c); }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Color32 color)
            {
                int k = data.verts.Count;
                Vertex(a, na, color); Vertex(b, nb, color); Vertex(c, nb, color); Vertex(d, na, color);
                data.tris.Add(k); data.tris.Add(k + 1); data.tris.Add(k + 2);
                data.tris.Add(k); data.tris.Add(k + 2); data.tris.Add(k + 3);
            }
            public void Box(Vector3 centre, Vector3 size, Color32 color)
            {
                foreach (Vector3 n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                {
                    Vector3 u = Mathf.Abs(n.y) > .5f ? Vector3.right : Vector3.up, v = Vector3.Cross(u, n);
                    Vector3 c = centre + Vector3.Scale(n, size) * .5f;
                    u = Vector3.Scale(u, size) * .5f; v = Vector3.Scale(v, size) * .5f;
                    Quad(c - u - v, c - u + v, c + u + v, c + u - v, n, n, color);
                }
            }
            // Ring section between a and b; an inner radius leaves a real opening at stack mouths and wheel rims.
            public void Tube(Vector3 a, Vector3 b, float radius, Color32 color, int segments = 20, float inner = 0f)
            {
                Vector3 axis = (b - a).normalized;
                if (axis.sqrMagnitude < .5f) return;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.right : Vector3.up).normalized;
                Vector3 v = Vector3.Cross(axis, u);
                for (int i = 0; i < segments; i++)
                {
                    float t = i * Mathf.PI * 2f / segments, t1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 n0 = u * Mathf.Cos(t) + v * Mathf.Sin(t), n1 = u * Mathf.Cos(t1) + v * Mathf.Sin(t1);
                    Quad(a + n0 * radius, a + n1 * radius, b + n1 * radius, b + n0 * radius, n0, n1, color);
                    Quad(b + n0 * radius, b + n1 * radius, b + n1 * inner, b + n0 * inner, axis, axis, color);
                    Quad(a + n1 * radius, a + n0 * radius, a + n0 * inner, a + n1 * inner, -axis, -axis, color);
                    if (inner > 0f) Quad(b + n0 * inner, b + n1 * inner, a + n1 * inner, a + n0 * inner, -n0, -n1, Pal.Black[1]);
                }
            }
        }

        public static VoxelMesher.MeshData Build(CityDesign.Plan plan, int seed)
        {
            var b = new Builder();
            const float V = CityDesign.V;
            foreach (var lot in plan.lots)
            {
                if (lot.kind == CityDesign.LotKind.Yard) continue;
                int pitch = lot.kind == CityDesign.LotKind.Panel ? 8 : lot.kind == CityDesign.LotKind.Works ? 16 : 12;
                int width = lot.kind == CityDesign.LotKind.Panel ? 3 : lot.kind == CityDesign.LotKind.Works ? 6 : 4;
                foreach (int side in new[] { -1, 1 })
                {
                    float x = (side < 0 ? lot.x0 : lot.x1 + 1) * V + side * .02f;
                    for (int f = 0; f < lot.floors; f++)
                    for (int z = lot.z0 + 4; z + width <= lot.z1 - 3; z += pitch)
                    {
                        bool bridgeDoor = f == 0 && z + width >= lot.doorZ && z <= lot.doorZ + 5 && side == lot.street;
                        foreach (var bridge in plan.bridges)
                            if (bridge.y == f * CityDesign.Storey && z + width >= bridge.z0 && z <= bridge.z1
                                && (side < 0 ? lot.x0 == bridge.xR : lot.x1 == bridge.xL)) bridgeDoor = true;
                        if (bridgeDoor) continue;
                        float y = f * 3f + .75f, zc = (z + width * .5f) * V, w = width * V;
                        var frame = Pal.Cream[1];
                        foreach (float zz in new[] { z * V, (z + width) * V })
                            b.Box(new Vector3(x, y + .75f, zz), new Vector3(.08f, 1.58f, .055f), frame);
                        foreach (float yy in new[] { y, y + 1.5f })
                            b.Box(new Vector3(x, yy, zc), new Vector3(.10f, .07f, w + .08f), frame);
                        b.Box(new Vector3(x + side * .07f, y - .04f, zc), new Vector3(.22f, .09f, w + .16f), Pal.Metal[2]);
                        b.Box(new Vector3(x - side * .17f, y + .75f, zc), new Vector3(.05f, 1.5f, .04f), Pal.Wood[1]);
                        if (f > 0 && Pal.Hash(z, f, side, seed) < .06f)
                        {
                            b.Box(new Vector3(x + side * .25f, y - .38f, zc), new Vector3(.5f, .5f, .7f), Pal.Cream[2]);
                            for (int vent = 0; vent < 6; vent++)
                                b.Box(new Vector3(x + side * .51f, y - .57f + vent * .065f, zc), new Vector3(.025f, .026f, .5f), Pal.Metal[0]);
                        }
                    }
                    // Downpipes follow the building corners; collars every storey, elbow into the deck.
                    float px = x + side * .22f, pz = (lot.z1 - 1) * V;
                    b.Tube(new Vector3(px, .4f, pz), new Vector3(px, lot.Top * V, pz), .10f, Pal.Rust[2]);
                    for (int f = 1; f < lot.floors; f++)
                        b.Tube(new Vector3(px, f * 3f, pz), new Vector3(px, f * 3f + .12f, pz), .15f, Pal.Metal[1]);
                }
            }
            foreach (var s in plan.stacks)
            {
                Vector3 p = new Vector3((s.x + .5f) * V, (s.y0 + .5f) * V, (s.z + .5f) * V);
                float h = s.h * V, r = s.r * V;
                for (float y = 0; y < h; y += 1.5f)
                {
                    float top = Mathf.Min(h, y + 1.5f), rad = r * Mathf.Lerp(1.12f, .82f, y / h);
                    var color = ((int)(y / 3f) & 1) == 0 ? Pal.Cream[1] : Pal.Rust[2];
                    b.Tube(p + Vector3.up * y, p + Vector3.up * top, rad, color, 40, rad - .12f);
                    b.Tube(p + Vector3.up * y, p + Vector3.up * (y + .08f), rad + .05f, Pal.Metal[1], 40, rad - .12f);
                }
                b.Tube(p + Vector3.up * h, p + Vector3.up * (h + .18f), r * .9f, Pal.Black[1], 40, r * .72f);
                float gy = h * .66f;
                b.Tube(p + Vector3.up * gy, p + Vector3.up * (gy + .12f), r + .7f, Pal.Metal[1], 40, r + .08f);
                b.Tube(p + Vector3.up * (gy + 1.1f), p + Vector3.up * (gy + 1.15f), r + .7f, Pal.Rust[2], 40, r + .64f);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8f;
                    Vector3 q = p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (r + .67f);
                    b.Tube(q + Vector3.up * gy, q + Vector3.up * (gy + 1.1f), .025f, Pal.Metal[1], 8);
                }
                // Rungs and two rails on the stack side provide readable scale even from the yard.
                for (float y = .3f; y < h; y += .3f)
                    b.Tube(p + new Vector3(r + .18f, y, -.22f), p + new Vector3(r + .18f, y, .22f), .022f, Pal.Chrome[1], 8);
                foreach (float z in new[] { -.25f, .25f }) b.Tube(p + new Vector3(r + .18f, 0, z), p + new Vector3(r + .18f, h, z), .035f, Pal.Metal[2], 10);
            }
            // Load-bearing underframe, service pipe bundles and angled knees along the flanks.
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * (plan.hx * V - 1f);
                for (int lane = 0; lane < 3; lane++)
                {
                    float y = -.9f - lane * .42f;
                    b.Tube(new Vector3(x, y, -plan.hz * V + 2f), new Vector3(x, y, plan.hz * V - 2f), .16f + lane * .04f, Pal.Rust[1], 24);
                    for (float z = -plan.hz * V + 4; z < plan.hz * V - 2; z += 4f)
                        b.Tube(new Vector3(x, y, z), new Vector3(x, y, z + .10f), .24f + lane * .04f, Pal.Metal[2], 24);
                }
                for (float z = -plan.hz * V + 4; z < plan.hz * V - 2; z += 6f)
                {
                    b.Tube(new Vector3(x, -2.4f, z), new Vector3(x - side * 4f, -4.2f, z), .20f, Pal.Rust[1], 12);
                    b.Box(new Vector3(x - side * 2, -2.35f, z), new Vector3(4f, .3f, .3f), Pal.Metal[1]);
                }
            }
            return b.data;
        }

        public static VoxelMesher.MeshData Bogie()
        {
            var b = new Builder();
            b.Box(new Vector3(0, -2.2f, 0), new Vector3(5.4f, 2.8f, 12.8f), Pal.Olive[1]);
            b.Tube(new Vector3(0, -1f, 0), Vector3.zero, 2.25f, Pal.Rust[1], 40);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 3f;
                foreach (float z in new[] { -5.5f, -3.25f, -1f, 1f, 3.25f, 5.5f })
                {
                    b.Tube(new Vector3(x, -3.25f, z), new Vector3(x + side * .45f, -3.25f, z), 1.05f, Pal.Metal[0], 40);
                    b.Tube(new Vector3(x + side * .45f, -3.25f, z), new Vector3(x + side * .51f, -3.25f, z), .86f, Pal.Rust[2], 40, .67f);
                    b.Tube(new Vector3(x + side * .44f, -3.25f, z), new Vector3(x + side * .65f, -3.25f, z), .28f, Pal.Chrome[1], 24);
                }
                foreach (float z in new[] { -6.75f, 6.5f })
                {
                    Vector3 c = new Vector3(x, -2.25f, z);
                    b.Tube(c, c + Vector3.right * side * .48f, 1.75f, Pal.Rust[2], 48, 1.38f);
                    b.Tube(c, c + Vector3.right * side * .7f, .57f, Pal.Metal[2], 32);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        b.Tube(c + Vector3.right * side * .3f, c + Vector3.right * side * .3f + new Vector3(0, Mathf.Cos(a), Mathf.Sin(a)) * 1.45f, .13f, Pal.Rust[1], 10);
                    }
                }
                b.Box(new Vector3(x + side * .1f, -1.55f, 0), new Vector3(.15f, 1.2f, 10f), Pal.Olive[2]);
                for (float z = -4.5f; z <= 4.5f; z += 1.5f)
                    b.Box(new Vector3(x + side * .22f, -1.55f, z), new Vector3(.12f, 1.3f, .07f), Pal.Rust[1]);
            }
            return b.data;
        }
    }
}
