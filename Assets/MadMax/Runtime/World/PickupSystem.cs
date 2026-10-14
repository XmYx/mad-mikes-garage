using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Resource pickups (scrap sheets, planks, stones...). Collected by walking or driving near them.</summary>
    public class PickupSystem : MonoBehaviour
    {
        public static PickupSystem Instance { get; private set; }

        public int maxPickups = 250;
        public float magnetTime = 0.3f;

        [HideInInspector] public Transform collector;
        [HideInInspector] public float collectRadius = 1.8f;

        class Pickup
        {
            public GameObject go;
            public ResourceType type;
            public int amount;
            public float magnet = -1f;
            public Vector3 from;
        }

        readonly List<Pickup> live = new List<Pickup>();
        readonly Mesh[] meshes = new Mesh[ResourceInfo.Count];
        Material material;
        Inventory inventory;

        public void Init(Material mat, Inventory inv) { material = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : mat; inventory = inv; Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; foreach (var m in meshes) if (m) Destroy(m); }

        Mesh MeshFor(ResourceType t)
        {
            if (meshes[(int)t]) return meshes[(int)t];
            if (MadMax.Rendering.HDBits.On) return meshes[(int)t] = MadMax.Rendering.HDBits.Pickup(t);
            var g = new VoxelGrid();
            switch (t)
            {
                case ResourceType.Scrap: g.Box(-2, 0, -2, 2, 0, 1, Pal.Weathered(Pal.Chrome, 0.5f, 501, 1, 0)); g.Box(-1, 1, -1, 1, 1, 0, Pal.Ramp(Pal.Rust, 2)); break;
                case ResourceType.Wood: g.Box(-3, 0, -1, 3, 0, 0, Pal.Ramp(Pal.Wood, 3)); g.Box(-3, 1, -1, 2, 1, 0, Pal.Ramp(Pal.Wood, 2)); break;
                case ResourceType.Stone: g.Box(-1, 0, -1, 1, 1, 1, Pal.Ramp(Pal.Sand, 1)); g.Set(0, 2, 0, Pal.Ramp(Pal.Sand, 2)); break;
                case ResourceType.Glass: g.Box(-1, 0, 0, 1, 2, 0, Pal.Ramp(Pal.Glass, 3)); break;
                case ResourceType.Rubber: g.CylX(0.5f, 0, 1.6f, 0, 1, Pal.Ramp(Pal.Tire, 1), 0.7f); break;
                default: g.Box(-1, 0, -1, 1, 1, 1, Pal.Ramp(Pal.Sand, 3)); break;
            }
            g.Bevel();
            return meshes[(int)t] = VoxelMesher.Build(g, "Pickup_" + t);
        }

        public void Spawn(ResourceType type, int amount, Vector3 pos, Vector3 velocity)
        {
            if (type == ResourceType.None || amount <= 0) return;
            if (live.Count >= maxPickups) { Destroy(live[0].go); live.RemoveAt(0); }
            var go = new GameObject("Pickup_" + ResourceInfo.Name(type), typeof(MeshFilter), typeof(MeshRenderer), typeof(Rigidbody));
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            var mesh = MeshFor(type);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var box = go.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center; box.size = mesh.bounds.size + Vector3.one * 0.04f;
            var rb = go.GetComponent<Rigidbody>();
            rb.mass = 2f;
            rb.linearVelocity = velocity;
            live.Add(new Pickup { go = go, type = type, amount = amount });
        }

        void Update()
        {
            if (!collector || inventory == null) return;
            var target = collector.position + Vector3.up * 0.8f;
            float r2 = collectRadius * collectRadius;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                if (!p.go) { live.RemoveAt(i); continue; }
                if (p.magnet < 0f)
                {
                    if ((p.go.transform.position - target).sqrMagnitude > r2) continue;
                    p.magnet = 0f;
                    p.from = p.go.transform.position;
                    Destroy(p.go.GetComponent<Rigidbody>());
                    Destroy(p.go.GetComponent<Collider>());
                }
                p.magnet += Time.deltaTime / magnetTime;
                p.go.transform.position = Vector3.Lerp(p.from, target, p.magnet * p.magnet);
                if (p.magnet >= 1f)
                {
                    using (Inventory.Source("PICKED UP")) inventory.Add(p.type, p.amount);
                    MadMax.Audio.Sfx.Play("pickup", target, 0.35f, Random.Range(0.95f, 1.15f), 20f, 0.08f);
                    Destroy(p.go);
                    live.RemoveAt(i);
                }
            }
        }
    }
}
