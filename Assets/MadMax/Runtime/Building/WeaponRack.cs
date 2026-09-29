using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Wall rack (with a Container): the first three tools or weapons stored in it hang on its pegs,
    /// laid along the wall (the rack's XZ plane, +Z up, +Y out of the wall).</summary>
    public class WeaponRack : MonoBehaviour
    {
        readonly List<GameObject> shown = new List<GameObject>();
        Container box;
        bool dirty = true;

        void Start()
        {
            box = GetComponent<Container>();
            if (box) box.inventory.Changed += () => dirty = true;
        }

        void Update()
        {
            if (!dirty || !box) return;
            dirty = false;
            foreach (var go in shown) if (go) Destroy(go);
            shown.Clear();
            var mat = GetComponent<MeshRenderer>().sharedMaterial;
            // tools hang handle-left: their -Y (handle to head) runs along +X, their +Z faces out of the wall
            var rot = Quaternion.LookRotation(Vector3.up, Vector3.left);
            int row = 0;
            foreach (var kv in box.inventory.Items)
            {
                if (row >= 3) break;
                var cat = ItemCatalog.Category(kv.Key);
                if (kv.Value <= 0 || (cat != ItemCategory.Tool && cat != ItemCategory.Weapon)) continue;
                var mesh = MadMax.Game.ToolLibrary.MeshFor(kv.Key);
                if (!mesh) continue;
                var go = new GameObject("Rack_" + kv.Key, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.transform.localRotation = rot;
                go.transform.localPosition = new Vector3(0f, 0.2f, 0.32f - row * 0.32f) - rot * mesh.bounds.center;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                shown.Add(go);
                row++;
            }
        }
    }
}
