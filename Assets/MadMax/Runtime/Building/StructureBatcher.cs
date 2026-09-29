using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Big bases draw cheaply (gaps list): the static Structure pieces (walls, floors, roofs, foundations) of
    /// every 16 m cell more than ~28 m from the player are merged into one mesh per cell and their own renderers switched
    /// off; near the player each piece draws itself again (cutaways, build highlights, damage). A cell is re-merged when
    /// its pieces change (built, broken, dyed). Colliders and logic stay on the pieces. At most one merge per frame.</summary>
    public class StructureBatcher : MonoBehaviour
    {
        const float Cell = 16f, NearDist = 28f;

        class Batch
        {
            public GameObject go;
            public Mesh mesh;
            public int signature;
            public readonly List<MeshRenderer> hidden = new List<MeshRenderer>();
        }

        readonly Dictionary<Vector2Int, Batch> batches = new Dictionary<Vector2Int, Batch>();
        readonly Dictionary<Vector2Int, List<Placeable>> cells = new Dictionary<Vector2Int, List<Placeable>>();
        readonly List<CombineInstance> combine = new List<CombineInstance>();
        float scan;
        Material material;

        static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));

        /// <summary>A piece that can be merged: a static Structure piece with its own mesh, not a door or collapsing.</summary>
        static bool Mergeable(Placeable p)
        {
            if (!p || p.Collapsing || p.GetComponentInParent<Rigidbody>() || p.GetComponent<Door>()) return false;
            var def = FurnitureLibrary.Get(p.id);
            if (def == null || def.category != BuildCategory.Structure || p.id.Contains("garage_door") || p.id.Contains("shutter")) return false;
            return p.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh && p.GetComponent<MeshRenderer>();
        }

        void Update()
        {
            if ((scan -= Time.deltaTime) > 0f) return;
            scan = 1f;
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || !g.Player) return;
            var focus = g.Current ? g.Current.transform.position : g.Player.transform.position;
            foreach (var l in cells.Values) l.Clear();
            foreach (var p in Placeable.All)
            {
                if (!Mergeable(p)) continue;
                var c = CellOf(p.transform.position);
                if (!cells.TryGetValue(c, out var list)) cells[c] = list = new List<Placeable>();
                list.Add(p);
            }
            bool merged = false;
            foreach (var kv in cells)
            {
                var centre = new Vector3((kv.Key.x + 0.5f) * Cell, focus.y, (kv.Key.y + 0.5f) * Cell);
                bool near = Vector2.Distance(new Vector2(centre.x, centre.z), new Vector2(focus.x, focus.z)) < NearDist + Cell * 0.7f;
                batches.TryGetValue(kv.Key, out var b);
                if (near || kv.Value.Count < 3) { if (b != null) Split(kv.Key, b); continue; }
                int sig = Signature(kv.Value);
                if (b != null && b.signature == sig) continue;
                if (merged) { scan = 0.1f; continue; }                                            // the rest next time
                Merge(kv.Key, kv.Value, sig);
                merged = true;
            }
            // cells that emptied
            List<Vector2Int> drop = null;
            foreach (var kv in batches) if (!cells.TryGetValue(kv.Key, out var l) || l.Count == 0) (drop ??= new List<Vector2Int>()).Add(kv.Key);
            if (drop != null) foreach (var k in drop) Split(k, batches[k]);
        }

        static int Signature(List<Placeable> pieces)
        {
            unchecked
            {
                int h = pieces.Count;
                foreach (var p in pieces)
                {
                    h = h * 31 + p.GetEntityId().GetHashCode();
                    h = h * 31 + p.GetComponent<MeshFilter>().sharedMesh.GetEntityId().GetHashCode();             // dyed pieces swap their mesh
                }
                return h;
            }
        }

        void Merge(Vector2Int key, List<Placeable> pieces, int sig)
        {
            if (batches.TryGetValue(key, out var old)) Split(key, old);
            var b = new Batch { signature = sig };
            b.go = new GameObject("StructureBatch " + key.x + "," + key.y, typeof(MeshFilter), typeof(MeshRenderer));
            b.go.transform.SetParent(transform, false);
            b.go.transform.position = new Vector3(key.x * Cell, pieces[0].transform.position.y, key.y * Cell);
            var toLocal = b.go.transform.worldToLocalMatrix;
            combine.Clear();
            foreach (var p in pieces)
            {
                var mf = p.GetComponent<MeshFilter>();
                var mr = p.GetComponent<MeshRenderer>();
                if (!material) material = mr.sharedMaterial;
                if (mr.sharedMaterial != material) continue;                                     // one material per batch
                combine.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * p.transform.localToWorldMatrix });
                mr.enabled = false;
                b.hidden.Add(mr);
            }
            b.mesh = new Mesh { name = "StructureBatch", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            b.mesh.CombineMeshes(combine.ToArray(), true, true);
            b.go.GetComponent<MeshFilter>().sharedMesh = b.mesh;
            b.go.GetComponent<MeshRenderer>().sharedMaterial = material;
            batches[key] = b;
        }

        void Split(Vector2Int key, Batch b)
        {
            foreach (var r in b.hidden) if (r) r.enabled = true;
            if (b.mesh) Destroy(b.mesh);
            if (b.go) Destroy(b.go);
            batches.Remove(key);
        }

        void OnDestroy() { foreach (var b in batches.Values) if (b.mesh) Destroy(b.mesh); }
    }
}
