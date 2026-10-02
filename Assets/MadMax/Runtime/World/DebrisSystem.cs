using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Pooled physics pixel-cubes thrown off by destruction. Cubes keep the voxel's colour, then shrink away.</summary>
    public class DebrisSystem : MonoBehaviour
    {
        public static DebrisSystem Instance { get; private set; }

        public int maxPieces = 400;
        public float lifetime = 7f;
        public int maxPerBurst = 160;

        class Piece
        {
            public GameObject go;
            public Rigidbody rb;
            public MeshFilter mf;
            public float die, size;
        }

        readonly List<Piece> pool = new List<Piece>();

        class Puff { public Transform t; public MeshFilter mf; public Vector3 vel; public float born, life, size; }
        readonly List<Puff> puffs = new List<Puff>();
        int nextPuff;
        public int maxPuffs = 160;
        readonly Dictionary<int, Mesh> cubes = new Dictionary<int, Mesh>();
        Material material;
        int next;

        public struct Chunk
        {
            public Vector3 position;
            public Color32 color;
        }

        public void Init(Material mat) { material = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : mat; Instance = this; }   // HD: bevelled chunks, round puffs
        void OnDestroy() { if (Instance == this) Instance = null; foreach (var m in cubes.Values) Destroy(m); }

        Mesh Cube(Color32 c)
        {
            if (MadMax.Rendering.HDBits.On) return MadMax.Rendering.HDBits.Chunk(c);
            int key = c.r << 16 | c.g << 8 | c.b;
            if (cubes.TryGetValue(key, out var m) && m) return m;
            var g = new VoxelGrid();
            g.Set(0, 0, 0, Pal.Solid(c));
            m = VoxelMesher.Build(g, "Debris", 1f);
            cubes[key] = m;
            return m;
        }

        Piece Take()
        {
            if (pool.Count < maxPieces)
            {
                var go = new GameObject("Debris", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(Rigidbody));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                go.GetComponent<BoxCollider>().size = Vector3.one;
                var p = new Piece { go = go, rb = go.GetComponent<Rigidbody>(), mf = go.GetComponent<MeshFilter>() };
                p.rb.mass = 0.3f;
                p.rb.linearDamping = 0.2f;
                pool.Add(p);
                return p;
            }
            var reuse = pool[next];
            next = (next + 1) % pool.Count;
            return reuse;
        }

        /// <summary>Throw cubes for removed voxels. Large bursts are merged into 2x cubes and capped.</summary>
        public void Emit(List<Chunk> chunks, float voxelSize, Vector3 impulse, float spread = 1.5f)
        {
            if (chunks.Count == 0) return;
            float size = voxelSize;
            int step = 1;
            if (chunks.Count > maxPerBurst) { size = voxelSize * 2f; step = Mathf.CeilToInt(chunks.Count / (float)maxPerBurst / 4f) * 4; }
            for (int i = 0; i < chunks.Count; i += step)
            {
                var c = chunks[i];
                var p = Take();
                p.go.SetActive(true);
                p.mf.sharedMesh = Cube(c.color);
                p.size = size;
                p.go.transform.SetPositionAndRotation(c.position, Random.rotation);
                p.go.transform.localScale = Vector3.one * size;
                p.rb.linearVelocity = impulse * Random.Range(0.4f, 1f) + Random.insideUnitSphere * spread + Vector3.up * Random.Range(0.5f, 2f);
                p.rb.angularVelocity = Random.insideUnitSphere * 10f;
                p.die = Time.time + lifetime * Random.Range(0.7f, 1.2f);
            }
        }

        /// <summary>Non-physical pixel cube that drifts and shrinks: smoke, steam, drips, muzzle flash.</summary>
        public void EmitPuff(Vector3 pos, Color32 color, float size, Vector3 velocity, float life)
        {
            Puff p;
            if (puffs.Count < maxPuffs)
            {
                var go = new GameObject("Puff", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p = new Puff { t = go.transform, mf = go.GetComponent<MeshFilter>() };
                puffs.Add(p);
            }
            else { p = puffs[nextPuff]; nextPuff = (nextPuff + 1) % puffs.Count; }
            p.t.gameObject.SetActive(true);
            p.mf.sharedMesh = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDBits.Puff(color) : Cube(color);
            p.t.SetPositionAndRotation(pos, Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0));
            p.vel = velocity; p.born = Time.time; p.life = life; p.size = size;
            p.t.localScale = Vector3.one * size;
        }

        void Update()
        {
            float now = Time.time;
            foreach (var p in puffs)
            {
                if (!p.t.gameObject.activeSelf) continue;
                float age = (now - p.born) / p.life;
                if (age >= 1f) { p.t.gameObject.SetActive(false); continue; }
                p.t.position += p.vel * Time.deltaTime;
                p.t.localScale = Vector3.one * (p.size * (1f + age) * (1f - age * age));
            }
            foreach (var p in pool)
            {
                if (!p.go.activeSelf) continue;
                float left = p.die - now;
                if (left <= 0f) { p.go.SetActive(false); continue; }
                if (left < 1f) p.go.transform.localScale = Vector3.one * (p.size * Mathf.Max(0.05f, left));
            }
        }
    }
}
