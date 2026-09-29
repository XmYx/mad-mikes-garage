using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A pack trader's mule (roadmap 20): a voxel donkey loaded with saddlebags and pots that plods after its
    /// trader, legs swinging with the pace, head bobbing. Folded away with its owner. (Roadmap 23's animal framework
    /// can take it over.)</summary>
    public class PackAnimal : MonoBehaviour
    {
        Npc owner;
        Transform[] legs;
        Transform head;
        float phase, speed;
        static Mesh bodyMesh, legMesh, headMesh;

        public static PackAnimal Attach(Npc owner, Material mat)
        {
            if (!bodyMesh || !legMesh || !headMesh) Build();
            var go = new GameObject("PackMule", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = owner.transform.position - owner.transform.forward * 1.8f;
            go.GetComponent<MeshFilter>().sharedMesh = bodyMesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var a = go.AddComponent<PackAnimal>();
            a.owner = owner;
            a.legs = new Transform[4];
            int i = 0;
            foreach (var (x, z) in new[] { (-3, 7), (3, 7), (-3, -7), (3, -7) })
            {
                var leg = new GameObject("Leg", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                leg.SetParent(go.transform, false);
                leg.localPosition = new Vector3(x, 11, z) * VoxelMesher.DefaultSize;
                leg.GetComponent<MeshFilter>().sharedMesh = legMesh;
                leg.GetComponent<MeshRenderer>().sharedMaterial = mat;
                a.legs[i++] = leg;
            }
            a.head = new GameObject("Head", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            a.head.SetParent(go.transform, false);
            a.head.localPosition = new Vector3(0, 15, 10) * VoxelMesher.DefaultSize;
            a.head.GetComponent<MeshFilter>().sharedMesh = headMesh;
            a.head.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return a;
        }

        static void Build()
        {
            var hide = Pal.Ramp(new[] { Pal.Wood[0], Pal.Wood[1], Pal.Metal[2] }, 1, 2101);
            var b = new VoxelGrid();
            b.Box(-3, 11, -9, 3, 17, 9, hide);                                                    // barrel body
            b.Box(-2, 17, -8, 2, 17, 8, hide);
            b.Box(0, 12, -11, 0, 16, -10, Pal.Ramp(Pal.Black, 1, 2102));                           // tail
            foreach (int s in new[] { -1, 1 })                                                    // saddlebags, pots
            {
                b.Box(s * 4, 12, -4, s * 6, 17, 3, Pal.Ramp(Pal.Olive, 1, 2103));
                b.Box(s * 5, 13, -3, s * 5, 16, 2, Pal.Ramp(Pal.Olive, 2, 2104));
                b.Box(s * 6, 15, 4, s * 7, 17, 6, Pal.Ramp(Pal.Metal, 2, 2105));
            }
            b.Box(-3, 18, -6, 3, 20, 5, Pal.Ramp(Pal.Crimson, 1, 2106));                           // blanket roll on top
            b.Box(-4, 21, -3, 4, 22, 2, Pal.Ramp(Pal.Cream, 1, 2107));                             // sack
            b.Bevel();
            bodyMesh = VoxelMesher.Build(b, "PackMule");
            var l = new VoxelGrid();
            l.Box(-1, -11, -1, 0, 0, 0, hide);
            l.Box(-1, -12, -1, 0, -12, 1, Pal.Ramp(Pal.Black, 0, 2108));                            // hoof
            l.Bevel();
            legMesh = VoxelMesher.Build(l, "PackMuleLeg");
            var h = new VoxelGrid();
            h.Box(-1, 0, 0, 1, 5, 2, hide);                                                        // neck
            h.Box(-2, 4, 2, 2, 7, 7, hide);                                                        // head
            h.Box(-1, 4, 7, 1, 6, 8, Pal.Ramp(Pal.Metal, 1, 2109));                                // muzzle
            h.Box(-2, 8, 2, -2, 11, 3, hide); h.Box(2, 8, 2, 2, 11, 3, hide);                      // ears
            h.Set(-2, 6, 5, Pal.Solid(Pal.Black[0])); h.Set(2, 6, 5, Pal.Solid(Pal.Black[0]));     // eyes
            h.Bevel();
            headMesh = VoxelMesher.Build(h, "PackMuleHead");
        }

        void Update()
        {
            if (!owner || !owner.Alive) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            var want = owner.transform.position - owner.transform.forward * 1.9f;
            var to = want - transform.position; to.y = 0f;
            float dist = to.magnitude;
            float target = dist > 0.4f ? Mathf.Clamp(dist * 1.6f, 0f, 3.2f) : 0f;
            speed = Mathf.MoveTowards(speed, target, dt * 3f);
            var p = transform.position + (dist > 0.01f ? to / dist : Vector3.zero) * speed * dt;
            var t = DeformableTerrain.Instance;
            if (t) p.y = t.Height(p.x, p.z);
            transform.position = p;
            var face = owner.transform.position - transform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-4f * dt));
            // walk cycle: diagonal pairs swing together, the head nods with each step
            phase += speed * dt * 4.2f;
            float swing = Mathf.Clamp01(speed / 1.2f) * 28f;
            for (int i = 0; i < legs.Length; i++)
                legs[i].localRotation = Quaternion.Euler(Mathf.Sin(phase + (i == 0 || i == 3 ? 0f : Mathf.PI)) * swing, 0f, 0f);
            head.localRotation = Quaternion.Euler(Mathf.Sin(phase * 2f) * 4f * Mathf.Clamp01(speed) + (speed < 0.1f ? Mathf.PerlinNoise(Time.time * 0.3f, 1f) * 18f : 0f), 0f, 0f);
        }
    }
}
