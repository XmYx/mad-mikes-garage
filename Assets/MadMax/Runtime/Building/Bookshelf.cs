using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Bookshelf (with a Container): books and tapes stored on it make reading nearby faster
    /// (+4 % per volume, up to +40 %, within 6 m). What it holds stands on its shelves (2026-10-23): one spine per book
    /// (colour, height and width by title), tapes as black cassettes with a label, filling the shelves from the top down;
    /// an empty shelf is bare. [E] says what is on it.</summary>
    public class Bookshelf : MonoBehaviour, IInteractable
    {
        public static readonly List<Bookshelf> All = new List<Bookshelf>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        Container box;
        bool dirty = true;
        GameObject books;
        Mesh booksMesh;
        /// <summary>Spines standing on the shelves (tests).</summary>
        public int Shown { get; private set; }

        public int Volumes
        {
            get
            {
                if (!TryGetComponent<Container>(out var b)) return 0;
                int n = 0;
                foreach (var kv in b.inventory.Items) if (kv.Key.StartsWith("book_") || kv.Key.StartsWith("vhs_")) n += kv.Value;
                return n;
            }
        }

        public string Prompt(MadMax.Game.WastelandGame g) { int v = Volumes; return v + " VOLUME" + (v == 1 ? "" : "S") + (v > 0 ? " (A GOOD PLACE TO READ)" : ""); }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!box) { g.Toast(Prompt(g)); return; }
            var titles = new List<string>();
            foreach (var kv in box.inventory.Items)
                if (kv.Value > 0 && (kv.Key.StartsWith("book_") || kv.Key.StartsWith("vhs_"))) titles.Add(MadMax.Items.ItemIds.Name(kv.Key) + (kv.Value > 1 ? " x" + kv.Value : ""));
            g.Toast(titles.Count == 0 ? "THE SHELVES ARE BARE" : "ON THE SHELVES: " + string.Join(", ", titles.Count > 5 ? titles.GetRange(0, 5) : titles) + (titles.Count > 5 ? " AND MORE" : ""));
        }

        void Start()
        {
            box = GetComponent<Container>();
            if (box) box.inventory.Changed += () => dirty = true;
        }

        void Update()
        {
            if (!dirty || !box || MadMax.Rendering.HDBits.On) return;                                // the HD model carries its own books
            dirty = false;
            Rebuild();
        }

        void OnDestroy() { if (booksMesh) Destroy(booksMesh); }

        static readonly Color32[][] Spines = { Pal.Rust, Pal.Olive, Pal.PaleBlue, Pal.Cream, Pal.RigGreen, Pal.Bronze, Pal.Crimson, Pal.Navy, Pal.Ochre };

        /// <summary>Spines along the shelves (voxel coordinates of the piece: shelves at y 1, 9, 17 from x −7..8, depth −2..2).</summary>
        void Rebuild()
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Cloth);
            int shelf = 2, x = -7, count = 0;
            int[] floors = { 1, 9, 17 };
            foreach (var kv in box.inventory.Items)
            {
                bool tape = kv.Key.StartsWith("vhs_");
                if (!tape && !kv.Key.StartsWith("book_")) continue;
                int h0 = kv.Key.GetHashCode() & 0x7fffffff;
                for (int k = 0; k < kv.Value && shelf >= 0; k++)
                {
                    int w = tape ? 2 : 1 + (h0 >> 3) % 2, h = tape ? 4 : 4 + (h0 >> 5) % 3;
                    if (x + w - 1 > 8) { shelf--; x = -7; if (shelf < 0) break; }
                    int y = floors[shelf];
                    if (tape)
                    {
                        g.Box(x, y, -1, x + 1, y + h - 1, 2, Pal.Ramp(Pal.Black, 1, 1130 + count));
                        g.Box(x, y + 1, 2, x + 1, y + 2, 2, Pal.Ramp(Pal.Cream, 3, 1131 + count));             // the label
                    }
                    else
                    {
                        g.Box(x, y, -2, x + w - 1, y + h - 1, 2, Pal.Ramp(Spines[h0 % Spines.Length], 1 + (h0 >> 7) % 2, 1132 + count));
                        g.Set(x, y + h - 2, 2, Pal.Ramp(Pal.Bronze, 3));                                    // gilt on the spine
                    }
                    x += w; count++;
                }
            }
            Shown = count;
            if (count == 0) { if (books) books.SetActive(false); return; }
            if (!books)
            {
                books = new GameObject("Books", typeof(MeshFilter), typeof(MeshRenderer));
                books.transform.SetParent(transform, false);
                books.GetComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>() ? GetComponent<MeshRenderer>().sharedMaterial : null;
            }
            books.SetActive(true);
            if (booksMesh) Destroy(booksMesh);
            g.Bevel();
            booksMesh = VoxelMesher.Build(g, "Bookshelf_books");
            books.GetComponent<MeshFilter>().sharedMesh = booksMesh;
        }

        static float Bonus(int volumes) => Mathf.Min(0.4f, volumes * 0.04f);

        /// <summary>Reading speed multiplier at a spot (best shelf in reach).</summary>
        public static float ReadingBonus(Vector3 p)
        {
            float best = 0f;
            foreach (var s in All) if (s && (s.transform.position - p).sqrMagnitude < 36f) best = Mathf.Max(best, Bonus(s.Volumes));
            return 1f + best;
        }
    }
}
