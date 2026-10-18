using System.Collections.Generic;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Sawdust at a saw bench or sawmill (2026-10-16): while a cut runs, fine dust drifts down from the blade
    /// (slow, drag-bound droplets); every finished job leaves a share of its wood as a pile on the floor in front of the
    /// cut (hand saw: a third of a log, the mill's thin kerf much less), growing as a little cone. [E] sweeps it up as
    /// wood (whole logs' worth; the rest stays). Not saved: a pile lasts the session.</summary>
    public class Sawdust : MonoBehaviour, IInteractable
    {
        public static readonly List<Sawdust> All = new List<Sawdust>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        /// <summary>Wood (units) lying in the pile.</summary>
        public float amount;
        /// <summary>Share of a job's wood that ends up as dust: hand saw, sawmill.</summary>
        public const float HandShare = 0.3f, MillShare = 0.1f, PileFull = 4f;
        public bool mill;
        float cuttingUntil;
        Droplets dust;
        Transform pile;
        MeshFilter pileMesh;
        float shownAmount = -1f;
        Vector3 blade, pileAt;
        static Material pileMat;

        /// <summary>A cut is running now (dust in the air).</summary>
        public bool Cutting => Time.time < cuttingUntil;
        /// <summary>Dust specks in the air now (tests).</summary>
        public int Airborne => dust ? dust.Airborne : 0;
        public bool PileShown => pile && pile.gameObject.activeSelf;
        public Vector3 PilePoint => transform.TransformPoint(pileAt);

        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); PartFunctions.Interactables.Remove(this); }

        void Awake()
        {
            // the blade sits at the top of the bench, the pile on the floor in front of the cut
            var b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r.name == "Drop" || r.name == "SawdustPile" || r.name == "WorkPiece") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            var top = any ? new Vector3(b.center.x, b.max.y, b.center.z) : transform.position + Vector3.up * 0.8f;
            blade = transform.InverseTransformPoint(top);
            var size = any ? transform.InverseTransformVector(b.size) : Vector3.one;
            pileAt = new Vector3(blade.x, 0.01f, blade.z + Mathf.Clamp(Mathf.Abs(size.z) * 0.5f + 0.15f, 0.35f, 1.1f));
        }

        /// <summary>The station is cutting this frame (called from <see cref="CraftingStation"/> while a job runs).</summary>
        public void Cut()
        {
            cuttingUntil = Time.time + 0.25f;
            if (!dust)
            {
                Color tan = Color.Lerp((Color)Voxel.Pal.Wood[4], Color.white, 0.25f); tan.a = 1f;
                bool m = mill;
                var at0 = blade;
                dust = Droplets.Attach(gameObject, m ? 36 : 22, (int i, out Vector3 at, out Vector3 v) =>
                {
                    at = at0 + new Vector3(Random.Range(-0.05f, 0.05f), -0.04f, Random.Range(-0.05f, 0.05f));
                    // the teeth spit it forward and down; the air takes it from there
                    v = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, m ? 0.9f : 0.4f), Random.Range(0.4f, m ? 2.2f : 1.1f));
                }, tan, 0.03f, 1.6f, 2.2f);
            }
            dust.emitting = true;
        }

        /// <summary>A job of <paramref name="r"/> finished: its share of the wood falls to the pile.</summary>
        public void JobDone(Recipe r)
        {
            int wood = 0;
            foreach (var (t, n) in r.resources) if (t == ResourceType.Wood) wood += n;
            if (wood <= 0) return;
            amount += wood * (mill ? MillShare : HandShare);
            Shape();
        }

        void Update()
        {
            if (dust && dust.emitting && !Cutting) dust.emitting = false;
        }

        void Shape()
        {
            if (amount <= 0.01f) { if (pile) pile.gameObject.SetActive(false); PartFunctions.Interactables.Remove(this); return; }
            if (!PartFunctions.Interactables.Contains(this)) PartFunctions.Interactables.Add(this);
            if (!pile)
            {
                var go = new GameObject("SawdustPile", typeof(MeshFilter), typeof(MeshRenderer));
                pile = go.transform; pile.SetParent(transform, false); pile.localPosition = pileAt;
                pileMesh = go.GetComponent<MeshFilter>();
                var mr = go.GetComponent<MeshRenderer>();
                if (!pileMat)
                {
                    pileMat = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
                    pileMat.SetColor("_BaseColor", Color.Lerp((Color)Voxel.Pal.Wood[4], Color.white, 0.18f));
                }
                mr.sharedMaterial = pileMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            pile.gameObject.SetActive(true);
            float a = Mathf.Min(amount, PileFull);
            if (Mathf.Abs(a - shownAmount) < 0.02f) return;
            shownAmount = a;
            float r = 0.12f + 0.16f * Mathf.Sqrt(a), h = 0.03f + 0.05f * Mathf.Sqrt(a);
            pileMesh.sharedMesh = Cone(pileMesh.sharedMesh, r, h);
        }

        /// <summary>A low, lumpy cone of dust (8 sides, 2 rings).</summary>
        static Mesh Cone(Mesh m, float r, float h)
        {
            if (!m) m = new Mesh { name = "SawdustPile" };
            const int n = 8;
            var v = new Vector3[n * 2 + 1];
            var t = new int[n * 3 * 3];
            for (int i = 0; i < n; i++)
            {
                float ang = i * Mathf.PI * 2f / n, j = 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(i * 2.3f));
                v[i] = new Vector3(Mathf.Cos(ang) * r * j, 0f, Mathf.Sin(ang) * r * j);
                v[n + i] = new Vector3(Mathf.Cos(ang + 0.3f) * r * 0.5f, h * (0.55f + 0.1f * Mathf.Sin(i * 1.7f)), Mathf.Sin(ang + 0.3f) * r * 0.5f);
            }
            v[n * 2] = new Vector3(0f, h, 0f);
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                int a = i, b = (i + 1) % n, c = n + i, d = n + (i + 1) % n;
                t[k++] = a; t[k++] = c; t[k++] = b;
                t[k++] = b; t[k++] = c; t[k++] = d;
                t[k++] = c; t[k++] = n * 2; t[k++] = d;
            }
            m.Clear(); m.vertices = v; m.triangles = t; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (amount < 0.05f || g.Current || !g.Player) return null;
            if ((g.Player.transform.position - PilePoint).sqrMagnitude > 2.2f * 2.2f) return null;
            string sweep = amount >= 1f ? "[E] SWEEP UP THE SAWDUST (" + Mathf.FloorToInt(amount) + " WOOD)" : "[E] SWEEP UP THE SAWDUST (A HANDFUL)";
            return amount >= PerSack ? sweep + "  [T] BAG IT (" + Mathf.FloorToInt(amount / PerSack) + " SACKS)" : sweep;
        }

        /// <summary>A sack of sawdust (2026-10-17): bedding for the stable, a scoop down the latrine after use (no flies,
        /// faster compost), and it soaks up spilled fuel and oil (<c>WastelandGame.UseSawdust</c>).</summary>
        public const string Sack = "use_sawdust";
        /// <summary>Wood (units) of dust in one sack.</summary>
        public const float PerSack = 0.5f;

        /// <summary>Fill sacks from the pile into <paramref name="inv"/>; returns how many.</summary>
        public int Bag(Inventory inv)
        {
            int n = Mathf.FloorToInt(amount / PerSack + 0.001f);
            if (n <= 0) return 0;
            ItemIds.Register(Sack, "SACK OF SAWDUST");
            using (Inventory.Source("BAGGED")) inv.AddItem(Sack, n);
            amount = Mathf.Max(0f, amount - n * PerSack);
            if (amount < 0.05f) amount = 0f;
            Shape();
            return n;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (amount < 0.05f) return;
            if (secondary)
            {
                int b = Bag(g.Inventory);
                if (b <= 0) { g.Toast("NOT ENOUGH TO FILL A SACK"); return; }
                g.Toast("BAGGED " + b + " SACK" + (b == 1 ? "" : "S") + " OF SAWDUST: BEDDING, THE LATRINE, SPILLS");
                MadMax.Audio.Sfx.Play("scratch", PilePoint, 0.3f, 1.1f, 8f);
                return;
            }
            int n = Sweep(g.Inventory);
            if (n <= 0) { g.Toast("JUST A HANDFUL: NOT WORTH A LOG YET"); return; }
            MadMax.Audio.Sfx.Play("scratch", PilePoint, 0.3f, 1.3f, 8f);
        }

        /// <summary>Whole logs' worth into <paramref name="inv"/>; returns how many.</summary>
        public int Sweep(Inventory inv)
        {
            int n = Mathf.FloorToInt(amount);
            if (n <= 0) return 0;
            using (Inventory.Source("SWEPT")) inv.Add(ResourceType.Wood, n);
            amount -= n;
            if (amount < 0.2f) amount = 0f;
            Shape();
            return n;
        }
    }
}
