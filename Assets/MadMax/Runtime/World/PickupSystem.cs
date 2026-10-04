using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Resource pickups (scrap sheets, planks, stones...) knocked loose by carving, salvage and breakage. Each is a
    /// <see cref="WorldItem"/> ("res:N", drawn as loose debris): nothing is collected by walking or driving near it,
    /// the player takes it with [E] (hold: everything within 3 m). A fresh bundle joins a resting pile of the same
    /// thing within 0.8 m; the oldest loose bundles go when more than <see cref="maxPickups"/> lie about.</summary>
    public class PickupSystem : MonoBehaviour
    {
        public static PickupSystem Instance { get; private set; }

        public int maxPickups = 250;

        readonly List<WorldItem> live = new List<WorldItem>();
        readonly Mesh[] meshes = new Mesh[ResourceInfo.Count];
        Material material;

        public void Init(Material mat) { material = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : mat; Instance = this; }
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
            string key = "res:" + (int)type;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var w = live[i];
                if (!w || w.count <= 0) { live.RemoveAt(i); continue; }
                if (w.key != key || w.OnVehicle || w.Body.linearVelocity.sqrMagnitude > 0.25f || (w.transform.position - pos).sqrMagnitude > 0.64f) continue;
                w.count += amount; w.Refresh();
                return;
            }
            while (live.Count >= maxPickups) { if (live[0]) Destroy(live[0].gameObject); live.RemoveAt(0); }
            var item = WorldItem.Create(key, amount, -1f, material, pos, Quaternion.Euler(0, Random.Range(0, 360f), 0), null, MeshFor(type));
            var game = MadMax.Game.WastelandGame.Instance;
            if (game && game.Player && game.Player.TryGetComponent<Collider>(out var pc)) Physics.IgnoreCollision(item.Box, pc);
            item.Body.linearVelocity = velocity;
            live.Add(item);
        }
    }
}
