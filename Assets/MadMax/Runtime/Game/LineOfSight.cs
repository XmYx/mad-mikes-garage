using System.Collections.Generic;
using MadMax.Building;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Only what the character can see is shown: vehicles, loose parts, furniture, crates, loot spots and other
    /// players behind walls, buildings, rocks or hills are hidden (terrain and structures stay, like remembered walls).
    /// Rays from the eye to each object, a few dozen per frame, round robin.</summary>
    public class LineOfSight : MonoBehaviour
    {
        class Target { public GameObject root; public Renderer[] renderers; public bool hidden; public float unseen; }

        readonly List<Target> targets = new List<Target>();
        readonly Dictionary<GameObject, Target> byRoot = new Dictionary<GameObject, Target>();
        readonly RaycastHit[] hits = new RaycastHit[24];
        readonly HashSet<GameObject> seen = new HashSet<GameObject>();
        float rescan;
        int cursor;
        public const float Range = 70f;
        public static bool Enabled = true;

        WastelandGame game;
        void Awake() => game = GetComponent<WastelandGame>();

        void Rescan()
        {
            seen.Clear();
            void Add(GameObject go)
            {
                if (!go || seen.Contains(go)) return;
                seen.Add(go);
                if (!byRoot.ContainsKey(go)) { var t = new Target { root = go, renderers = go.GetComponentsInChildren<Renderer>(true) }; byRoot[go] = t; targets.Add(t); }
            }
            foreach (var v in game.AllVehicles) if (v && v != game.Current) Add(v.gameObject);
            foreach (var p in VehiclePart.Registry) if (p && !p.IsMounted && !p.transform.parent?.GetComponentInParent<VehicleChassis>()) Add(p.gameObject);
            foreach (var p in Placeable.All)
            {
                if (!p) continue;
                var def = FurnitureLibrary.Get(p.id);
                if (def != null && def.category == BuildCategory.Structure) continue;       // walls and floors are remembered
                Add(p.gameObject);
            }
            foreach (var l in WastelandGame.LootSpots) if (l) Add(l.gameObject);
            foreach (var d in DestructibleVoxels.All)
            {
                if (!d) continue;
                var r = d.GetComponent<Renderer>();
                if (r && r.bounds.size.y < 2.2f && r.bounds.size.x < 3f) Add(d.gameObject);   // crates, tables, barrels, bushes
            }
            foreach (var a in FindObjectsByType<MadMax.Net.RemoteAvatar>()) Add(a.gameObject);
            for (int i = targets.Count - 1; i >= 0; i--)
                if (!targets[i].root || !seen.Contains(targets[i].root)) { Show(targets[i], true); byRoot.Remove(targets[i].root ?? gameObject); targets.RemoveAt(i); }
        }

        void Update()
        {
            bool active = Enabled && game.Player && !game.Menus.IsOpen && !TitleSequence.Playing;
            if (!active) { foreach (var t in targets) if (t.hidden) Show(t, true); return; }
            if ((rescan -= Time.deltaTime) <= 0f) { rescan = 1.5f; Rescan(); }
            if (targets.Count == 0) return;
            var eye = EyePosition();
            int budget = Mathf.Min(targets.Count, 40);
            for (int n = 0; n < budget; n++)
            {
                cursor = (cursor + 1) % targets.Count;
                var t = targets[cursor];
                if (!t.root) continue;
                bool visible = Visible(t, eye);
                if (visible) { t.unseen = 0f; if (t.hidden) Show(t, true); }
                else
                {
                    t.unseen += Time.deltaTime * targets.Count / budget;
                    if (!t.hidden && t.unseen > 0.25f) Show(t, false);
                }
            }
        }

        Vector3 EyePosition()
        {
            if (game.Current)
            {
                var e = game.Current.transform.Find("DriverEye");
                return e ? e.position : game.Current.transform.position + Vector3.up * 1.4f;
            }
            return game.Player.Eye.position;
        }

        bool Visible(Target t, Vector3 eye)
        {
            var b = Bounds(t);
            if ((b.center - eye).sqrMagnitude > Range * Range) return true;
            if (Clear(eye, b.center, t.root)) return true;
            if (Clear(eye, b.center + Vector3.up * b.extents.y * 0.9f, t.root)) return true;
            return false;
        }

        static Bounds Bounds(Target t)
        {
            var b = new Bounds(t.root.transform.position, Vector3.one * 0.2f);
            foreach (var r in t.renderers) if (r) b.Encapsulate(r.bounds);
            return b;
        }

        bool Clear(Vector3 eye, Vector3 point, GameObject self)
        {
            var d = point - eye;
            float len = d.magnitude;
            if (len < 0.5f) return true;
            int n = Physics.RaycastNonAlloc(eye, d / len, hits, len - 0.1f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.transform.IsChildOf(self.transform)) continue;
                if (IsBlocker(c)) return false;
            }
            return true;
        }

        bool IsBlocker(Collider c)
        {
            if (game.Player && c.transform.IsChildOf(game.Player.transform)) return false;
            if (game.Current && c.transform.IsChildOf(game.Current.transform)) return false;
            if (c.attachedRigidbody && !c.attachedRigidbody.isKinematic) return false;            // loose, moving things never hide others
            var dv = c.GetComponentInParent<DestructibleVoxels>();
            if (dv) { var r = dv.GetComponent<Renderer>(); return r && (r.bounds.size.y >= 2.2f || r.bounds.size.x >= 3f); }
            var p = c.GetComponentInParent<Placeable>();
            if (p) { var def = FurnitureLibrary.Get(p.id); return def != null && def.category == BuildCategory.Structure; }
            if (c.GetComponentInParent<VehicleChassis>()) return false;
            return c is MeshCollider;                                                             // terrain chunk
        }

        static void Show(Target t, bool on)
        {
            t.hidden = !on;
            foreach (var r in t.renderers) if (r) r.forceRenderingOff = !on;
        }
    }
}
