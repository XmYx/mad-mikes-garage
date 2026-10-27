using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Wire fish trap for shallow water (0.15 m+): baited, it catches small fish of the lake on its own every
    /// few minutes (one bait per catch, up to 4 waiting). [E] empties it, [T] adds a bait from the pack.
    /// 2026-10-26: the catch shows — each fish swims slow lengths of the wire drum, tail working, turning at the ends,
    /// now and then thrashing (a swirl and bubbles at the surface, a slap); a new one noses in through the funnel mouth
    /// with a splash; mutants glow; the bait hangs on its hook in the middle; out of water they lie and flop.</summary>
    public class FishTrap : MonoBehaviour, IInteractable, IPlaceState
    {
        public int bait, fish, glow;
        float timer = 120f;
        const int MaxCatch = 4, MaxBait = 6;
        const float VS = MadMax.Voxel.VoxelMesher.DefaultSize;
        /// <summary>Drum centre (local), half length inside the hoops, inner radius.</summary>
        static readonly Vector3 Centre = new Vector3(0f, 4f * VS, 0f);
        const float HalfLen = 0.36f, Inner = 0.2f;

        sealed class Swimmer
        {
            public Transform t, body;
            public bool toxic;
            public float ph, speed, lane, height, s, dir = 1f, yaw, thrashUntil, nextThrash, enterT = 1f;
        }
        readonly List<Swimmer> swimmers = new List<Swimmer>();
        Transform baitT;
        float waterAt = -1f, surface = float.NaN;
        bool wet;

        /// <summary>Fish visible in the drum now (tests).</summary>
        public int CatchShown { get { int n = 0; foreach (var s in swimmers) if (s.t && s.t.gameObject.activeSelf) n++; return n; } }
        /// <summary>The bait hangs inside (tests).</summary>
        public bool BaitShown => baitT && baitT.gameObject.activeSelf;
        /// <summary>The i-th fish's transform (tests: watch it swim).</summary>
        public Transform CatchVisual(int i) => i >= 0 && i < swimmers.Count ? swimmers[i].t : null;
        /// <summary>Time.time of the last catch / thrash (tests).</summary>
        public float LastCatchAt { get; private set; } = -1f;
        public float LastThrashAt { get; private set; } = -1f;

        bool InWater()
        {
            var t = DeformableTerrain.Instance;
            var p = transform.position;
            return t && t.WaterDepth(p.x, p.z) > 0.15f;
        }

        void Update()
        {
            Show();
            if (bait <= 0 || fish + glow >= MaxCatch || !wet) return;
            if ((timer -= Time.deltaTime) > 0f) return;
            timer = Random.Range(90f, 180f);
            if (Random.value < 0.25f) return;                                     // nothing swam in this time
            var t = DeformableTerrain.Instance;
            var p = transform.position;
            var biome = t.BiomeAt(p.x, p.z);
            var lake = t.World.LakeAt(p.x, p.z, out _);
            Catch(lake != null ? lake.toxic : biome == Biome.Nuclear);
        }

        /// <summary>A fish swims in and can't find the way out: one bait goes, the newcomer noses in through the mouth.</summary>
        public void Catch(bool toxic)
        {
            if (bait > 0) bait--;
            if (toxic) glow++; else fish++;
            LastCatchAt = Time.time;
            Show();
            int idx = Mathf.Min(MaxCatch, fish + glow) - 1;
            var s = idx >= 0 && idx < swimmers.Count ? swimmers[idx] : null;
            if (s != null) { s.enterT = 0f; s.dir = -1f; s.s = HalfLen + 0.2f; }
            if (PieceBits.Near(transform.position, 30f))
            {
                var top = SurfaceAbove();
                MadMax.Audio.Sfx.Play("splash", top, 0.35f, Random.Range(1.1f, 1.4f), 14f, 0.5f);
                for (int i = 0; i < 4; i++) Fx.Foam(top + Random.insideUnitSphere * 0.25f, Vector3.up * 0.1f, 0.12f, new Color(0.85f, 0.9f, 0.92f, 0.6f), 2f);
            }
            GetComponent<Placeable>()?.Dirty();
        }

        Vector3 SurfaceAbove()
        {
            var p = transform.TransformPoint(Centre);
            if (!float.IsNaN(surface)) p.y = surface + 0.02f;
            return p;
        }

        /// <summary>Keep one swimmer per fish (normal first, then mutants) and the bait in sight; animate them near the
        /// focus.</summary>
        void Show()
        {
            float now = Time.time;
            if (now >= waterAt)
            {
                waterAt = now + 2f;
                wet = InWater();
                var t = DeformableTerrain.Instance;
                surface = wet && t ? t.WaterLevel(transform.position.x, transform.position.z) : float.NaN;
            }
            int want = Mathf.Min(MaxCatch, fish + glow);
            bool near = PieceBits.Near(transform.position, 35f);
            if (!near && swimmers.Count == 0 && !baitT) return;
            var mat = MadMax.Game.WastelandGame.Instance ? MadMax.Game.WastelandGame.Instance.propMaterial : null;
            while (swimmers.Count < want && mat) swimmers.Add(MakeSwimmer(swimmers.Count, mat));
            for (int i = 0; i < swimmers.Count; i++)
            {
                var s = swimmers[i];
                bool on = i < want;
                bool toxic = i >= Mathf.Min(fish, MaxCatch);
                if (on && s.toxic != toxic) Reskin(s, toxic, mat);
                if (s.t.gameObject.activeSelf != on) s.t.gameObject.SetActive(on);
            }
            if (bait > 0 && !baitT && mat) baitT = MakeBait(mat);
            if (baitT && baitT.gameObject.activeSelf != bait > 0) baitT.gameObject.SetActive(bait > 0);
            if (!near) return;
            float dt = Time.deltaTime;
            if (baitT && bait > 0) baitT.localRotation = Quaternion.Euler(Mathf.Sin(now * 1.3f) * 8f, now * 20f, 0f);
            for (int i = 0; i < want && i < swimmers.Count; i++) Swim(swimmers[i], now, dt);
        }

        Swimmer MakeSwimmer(int i, Material mat)
        {
            var s = new Swimmer { ph = Random.Range(0f, 6.28f), speed = Random.Range(0.06f, 0.12f), lane = Random.Range(-0.08f, 0.08f),
                                  height = Random.Range(-0.08f, 0.06f), s = Random.Range(-HalfLen, HalfLen), dir = Random.value < 0.5f ? -1f : 1f,
                                  nextThrash = Time.time + Random.Range(3f, 10f) };
            s.t = new GameObject("Catch" + i).transform;
            s.t.SetParent(transform, false);
            s.body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            s.body.SetParent(s.t, false);
            s.body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            s.body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Reskin(s, false, mat);
            return s;
        }

        /// <summary>The item model of the catch, head along +Z, about 20-26 cm long, centred on its pivot.</summary>
        static void Reskin(Swimmer s, bool toxic, Material mat)
        {
            s.toxic = toxic;
            var mesh = ItemModels.Get(toxic ? "food_fish_glow" : "food_fish_raw");
            s.body.GetComponent<MeshFilter>().sharedMesh = mesh;
            if (!mesh) return;
            var b = mesh.bounds;
            float k = Random.Range(0.2f, 0.26f) / Mathf.Max(0.01f, b.size.x);
            s.body.localRotation = Quaternion.Euler(0f, -90f, 0f);                             // +X head → +Z
            s.body.localScale = Vector3.one * k;
            s.body.localPosition = -(s.body.localRotation * (b.center * k));
        }

        Transform MakeBait(Material mat)
        {
            var pivot = new GameObject("Bait").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Centre + new Vector3(0f, Inner * 0.6f, -0.1f);
            var go = new GameObject("Hook", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(pivot, false);
            var mesh = ItemModels.Get(FishLibrary.Worms);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (mesh)
            {
                float k = 0.09f / Mathf.Max(0.01f, mesh.bounds.size.magnitude);
                go.transform.localScale = Vector3.one * k;
                go.transform.localPosition = new Vector3(-mesh.bounds.center.x * k, -mesh.bounds.max.y * k, 0f);
            }
            return pivot;
        }

        void Swim(Swimmer s, float now, float dt)
        {
            var at = Centre;
            if (!wet)
            {
                // stranded: lie on the wire, flop now and then
                bool flop = now < s.thrashUntil;
                if (now >= s.nextThrash) { s.thrashUntil = now + 0.5f; s.nextThrash = now + Random.Range(2.5f, 7f); LastThrashAt = now; }
                at += new Vector3(s.lane, -Inner + 0.04f + (flop ? 0.05f * Mathf.Abs(Mathf.Sin(now * 14f)) : 0f), s.s * 0.6f);
                s.t.localPosition = at;
                s.t.localRotation = Quaternion.Euler(0f, s.ph * 57f, 90f + (flop ? Mathf.Sin(now * 22f) * 30f : 0f));
                return;
            }
            bool thrash = now < s.thrashUntil;
            if (now >= s.nextThrash && s.enterT >= 1f)
            {
                s.thrashUntil = now + 0.7f; s.nextThrash = now + Random.Range(5f, 14f); LastThrashAt = now;
                if (PieceBits.Near(transform.position, 20f))
                {
                    var top = SurfaceAbove();
                    Fx.Foam(top + new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f)), Vector3.up * 0.05f, 0.1f, new Color(0.85f, 0.9f, 0.92f, 0.5f), 1.6f);
                    MadMax.Audio.Sfx.Play("splash", top, 0.12f, Random.Range(1.5f, 1.9f), 8f, 1f);
                }
            }
            float v = s.speed * (thrash ? 3.5f : 1f);
            if (s.enterT < 1f)
            {
                s.enterT = Mathf.Min(1f, s.enterT + dt * 0.8f);                                 // nosing in through the funnel
                v = 0.25f;
            }
            s.s += s.dir * v * dt;
            if (s.s > HalfLen && s.enterT >= 1f) { s.s = HalfLen; s.dir = -1f; }
            if (s.s < -HalfLen) { s.s = -HalfLen; s.dir = 1f; }
            s.yaw = Mathf.MoveTowards(s.yaw, s.dir > 0f ? 0f : 180f, dt * 260f);             // turn at the ends
            float wag = Mathf.Sin(now * (thrash ? 22f : 7f) + s.ph) * (thrash ? 32f : 11f);
            float r = s.enterT < 1f ? 0f : 1f;
            at += new Vector3(s.lane * r + Mathf.Sin(now * 0.9f + s.ph) * 0.03f, s.height * r + Mathf.Sin(now * 1.7f + s.ph) * 0.012f, s.s);
            s.t.localPosition = at;
            s.t.localRotation = Quaternion.Euler(thrash ? Mathf.Sin(now * 17f) * 18f : 0f, s.yaw + wag, 0f);
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (!InWater()) return "FISH TRAP: SET IT IN WATER";
            string s = "FISH TRAP  BAIT " + bait + "  CATCH " + (fish + glow);
            if (fish + glow > 0) s += "  [E] EMPTY";
            if (bait < MaxBait && HasBait(g) != null) s += "  [T] BAIT";
            return s;
        }

        static string HasBait(MadMax.Game.WastelandGame g)
        {
            foreach (var b in FishLibrary.Baits) if (g.Inventory.GetItem(b) > 0) return b;
            if (g.Inventory.GetItem("food_rotten") > 0) return "food_rotten";          // rotten scraps work in a trap
            return null;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                var b = HasBait(g);
                if (b == null || bait >= MaxBait || !g.Inventory.TakeItem(b)) return;
                bait++;
                g.Toast("BAITED THE TRAP (" + bait + ")");
            }
            else if (fish + glow > 0)
            {
                if (fish > 0) g.Inventory.AddItem("food_fish_raw", fish);
                if (glow > 0) g.Inventory.AddItem("food_fish_glow", glow);
                g.Toast("TOOK " + (fish + glow) + " FISH FROM THE TRAP");
                MadMax.Audio.Sfx.Play("splash", transform.position, 0.5f, 1.2f);
                g.Stats.Practice(MadMax.RPG.Skill.Survival, 0.5f * (fish + glow));
                fish = glow = 0;
            }
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => bait + ";" + fish + ";" + glow;

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            if (p.Length > 0) int.TryParse(p[0], out bait);
            if (p.Length > 1) int.TryParse(p[1], out fish);
            if (p.Length > 2) int.TryParse(p[2], out glow);
        }
    }
}
