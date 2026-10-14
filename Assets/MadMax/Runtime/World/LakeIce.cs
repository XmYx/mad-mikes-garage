using System.Collections.Generic;
using MadMax.Building;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Ice you can walk on: below <see cref="FreezeBelow"/> the lake nearest the player skins over with a sheet
    /// of 6 m tiles at the water level (box colliders for walkers, <see cref="StructureGround"/> decks for wheels).
    /// Thickness grows with the frost and the winter (<see cref="Thickness"/>), thinner along the shore early in winter.
    /// What it bears follows the old ice-road rule, kg = 3.5 × cm² (5 cm a walker, 20 cm a car, 30 cm a light truck):
    /// heavier and it creaks, much heavier and the tiles under it break (the vehicle goes through); broken tiles freeze
    /// over again after half a day. Derived from the weather (not saved); each peer simulates its own.</summary>
    public class LakeIce : MonoBehaviour
    {
        public const float FreezeBelow = -4f, TileSize = 6f, RefreezeDays = 0.5f;
        public static LakeIce Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Thickness = 0f; LastBreak = Vector3.zero; Breaks = 0; }

        /// <summary>Ice thickness in metres on the open lake now (0 = open water).</summary>
        public static float Thickness { get; private set; }
        /// <summary>Where the ice last gave way, and how many times (tests).</summary>
        public static Vector3 LastBreak { get; private set; }
        public static int Breaks { get; private set; }

        sealed class Tile { public GameObject go; public float shore; public bool broken; public double brokenAt; }
        readonly List<Tile> tiles = new List<Tile>();
        Lake lake;
        float level = float.NaN, checkAt, crackAt, builtThick;
        double lastDays = -1;
        Mesh mesh;
        MeshFilter mf;
        MeshRenderer mr;
        bool meshDirty;
        readonly Dictionary<Object, float> creaked = new Dictionary<Object, float>();

        public static void Ensure()
        {
            if (Instance || !DeformableTerrain.Instance) return;
            Instance = new GameObject("LakeIce").AddComponent<LakeIce>();
        }

        /// <summary>Kilograms a sheet of <paramref name="metres"/> bears.</summary>
        public static float Bears(float metres) { float cm = metres * 100f; return 3.5f * cm * cm; }

        /// <summary>Is there unbroken ice under <paramref name="p"/> (tests, fishing)?</summary>
        public static bool IceAt(Vector3 p)
        {
            if (!Instance || Thickness <= 0f) return false;
            foreach (var t in Instance.tiles)
                if (!t.broken && t.go && Mathf.Abs(t.go.transform.position.x - p.x) <= TileSize * 0.5f && Mathf.Abs(t.go.transform.position.z - p.z) <= TileSize * 0.5f) return true;
            return false;
        }

        void Update()
        {
            var terrain = DeformableTerrain.Instance;
            if (!terrain || terrain.World == null) return;
            var focus = Focus();
            double days = DayNight.TotalDays;
            float hours = lastDays < 0 ? -1f : (float)System.Math.Min(48.0, (days - lastDays) * 24.0);
            lastDays = days;
            float temp = Weather.TemperatureAt(focus.z);
            float target = 0f;
            if (temp <= FreezeBelow) target = Mathf.Clamp(0.03f + (FreezeBelow - temp) * 0.012f + (Weather.Season == 2 ? 0.08f + Weather.SeasonProgress * 0.25f : 0f), 0f, 0.45f);
            else if (temp < 0.5f) target = Thickness;                                                  // holds, doesn't grow
            if (hours < 0f) Thickness = target;                                                        // a fresh load: as the weather says
            else Thickness = Mathf.MoveTowards(Thickness, target, hours * (target > Thickness ? 0.02f : 0.05f));
            if (Time.time >= checkAt) { checkAt = Time.time + 2f; Rebuild(terrain, focus, days); }
            if (Time.time >= crackAt && tiles.Count > 0) { crackAt = Time.time + 0.25f; Loads(days); }
            if (meshDirty) DrawSheet();
        }

        static Vector3 Focus()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Current) return g.Current.transform.position;
            if (g && g.Player) return g.Player.transform.position;
            return Vector3.zero;
        }

        void Rebuild(DeformableTerrain terrain, Vector3 focus, double days)
        {
            var w = terrain.World;
            float tt = 99f;
            var near = Thickness > 0.01f ? w.LakeAt(focus.x, focus.z, out tt) : null;
            if (near != null && tt > 1.4f) near = null;
            float lvl = near != null ? near.level + Weather.LakeRise : float.NaN;
            bool same = near == lake && (near == null || Mathf.Abs(lvl - level) < 0.05f);
            if (!same) { Clear(); lake = near; level = lvl; if (lake != null) Lay(terrain); }
            if (lake == null) return;
            foreach (var t in tiles)                                                                   // holes freeze over again
                if (t.broken && days - t.brokenAt >= RefreezeDays && Thickness > 0.02f) { t.broken = false; Enable(t, true); meshDirty = true; }
            if (Mathf.Abs(builtThick - Thickness) > 0.02f) { builtThick = Thickness; meshDirty = true; }
        }

        void Lay(DeformableTerrain terrain)
        {
            float r = lake.radius * 1.3f;
            int n = Mathf.CeilToInt(r / TileSize);
            for (int j = -n; j <= n; j++)
            for (int i = -n; i <= n; i++)
            {
                float x = lake.pos.x + i * TileSize, z = lake.pos.y + j * TileSize;
                if (terrain.WaterDepthNoLoad(x, z) < 0.15f) continue;
                float edge = 0f;                                                                       // open water all round = 0, shore tiles 1
                for (int a = 0; a < 4; a++)
                {
                    float ax = x + (a == 0 ? TileSize : a == 1 ? -TileSize : 0f), az = z + (a == 2 ? TileSize : a == 3 ? -TileSize : 0f);
                    if (terrain.WaterDepthNoLoad(ax, az) < 0.15f) edge = 1f;
                }
                var go = new GameObject("IceTile");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(x, level, z);
                var t = new Tile { go = go, shore = edge };
                go.AddComponent<BoxCollider>();
                tiles.Add(t);
                Enable(t, true);
            }
            meshDirty = true;
        }

        /// <summary>Thickness of a tile: the shore band is thinner early in winter (warm ground and inflow).</summary>
        float TileThick(Tile t) => Thickness * (t.shore > 0f ? Mathf.Lerp(0.5f, 1f, Weather.Season == 2 ? Weather.SeasonProgress * 2f : 0f) : 1f);

        void Enable(Tile t, bool on)
        {
            if (!t.go) return;
            var box = t.go.GetComponent<BoxCollider>();
            float th = Mathf.Max(0.05f, TileThick(t));
            box.size = new Vector3(TileSize, th, TileSize); box.center = new Vector3(0f, -th * 0.5f, 0f);
            box.enabled = on;
            StructureGround.Remove(t.go);
            if (on) StructureGround.AddDeck(t.go, t.go.transform, box, new Vector4(TileSize * 0.5f, TileSize * 0.5f, 0f, 0f));
        }

        void Clear()
        {
            foreach (var t in tiles) if (t.go) { StructureGround.Remove(t.go); Destroy(t.go); }
            tiles.Clear();
            creaked.Clear();
            meshDirty = true;
            lake = null; level = float.NaN;
        }

        void OnDestroy() { foreach (var t in tiles) if (t.go) StructureGround.Remove(t.go); if (mesh) Destroy(mesh); }

        Tile Under(Vector3 p)
        {
            foreach (var t in tiles)
                if (t.go && Mathf.Abs(t.go.transform.position.x - p.x) <= TileSize * 0.5f && Mathf.Abs(t.go.transform.position.z - p.z) <= TileSize * 0.5f) return t;
            return null;
        }

        /// <summary>What stands on the ice: creaks near the limit, breaks through above it.</summary>
        void Loads(double days)
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g) return;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.Body) continue;
                Load(v, v.Body.worldCenterOfMass, v.Body.mass, v == g.Current, days, g);
            }
            if (g.Player && !g.Current && !g.Player.SeatedIn) Load(g.Player, g.Player.transform.position, 90f + g.CarriedWeight, true, days, g);
        }

        void Load(Object who, Vector3 p, float kg, bool player, double days, MadMax.Game.WastelandGame g)
        {
            if (p.y < level - 0.5f || p.y > level + 3.5f) return;
            var t = Under(p);
            if (t == null || t.broken) return;
            float bears = Bears(TileThick(t));
            if (kg > bears * 1.15f) { Break(p, kg > bears * 3f ? 6f : 3.5f, days); if (player) g.Toast("THE ICE GIVES WAY"); return; }
            if (kg > bears * 0.75f && (!creaked.TryGetValue(who, out float at) || Time.time - at > 6f))
            {
                creaked[who] = Time.time;
                MadMax.Audio.Sfx.Play("creak", p, 0.7f, Random.Range(0.5f, 0.7f), 40f);
                if (player) g.Toast("THE ICE CREAKS UNDER YOU");
            }
        }

        /// <summary>Break the tiles within <paramref name="radius"/> of a point.</summary>
        public void Break(Vector3 p, float radius, double days)
        {
            bool any = false;
            foreach (var t in tiles)
            {
                if (t.broken || !t.go) continue;
                var c = t.go.transform.position;
                if (Mathf.Max(Mathf.Abs(c.x - p.x), Mathf.Abs(c.z - p.z)) > radius + TileSize * 0.5f) continue;
                t.broken = true; t.brokenAt = days; Enable(t, false); any = true;
            }
            if (!any) return;
            meshDirty = true;
            LastBreak = p; Breaks++;
            MadMax.Audio.Sfx.Play("glass_break", p, 1f, 0.45f, 60f);
            for (int i = 0; i < 6; i++)
                Fx.Smoke(new Vector3(p.x, level + 0.05f, p.z) + Random.insideUnitSphere * radius * 0.5f, Vector3.up * Random.Range(1f, 2.5f), Random.Range(0.4f, 0.8f), new Color(0.85f, 0.92f, 0.96f, 0.8f), 0.8f);
        }

        void DrawSheet()
        {
            meshDirty = false;
            if (!mf)
            {
                mf = gameObject.AddComponent<MeshFilter>(); mr = gameObject.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var terrain = DeformableTerrain.Instance;
                mr.sharedMaterial = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : terrain ? terrain.worldPropMaterial : null;
                mesh = new Mesh { name = "LakeIce" };
                mf.sharedMesh = mesh;
            }
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();
            float h = TileSize * 0.5f;
            foreach (var t in tiles)
            {
                if (t.broken || !t.go) continue;
                var c = t.go.transform.position + Vector3.up * 0.015f;
                var col = TileThick(t) < 0.1f ? MadMax.Voxel.Pal.PaleBlue[2] : t.shore > 0f ? MadMax.Voxel.Pal.PaleBlue[3] : MadMax.Voxel.Pal.PaleBlue[4];
                int b = verts.Count;
                verts.Add(c + new Vector3(-h, 0f, -h)); verts.Add(c + new Vector3(-h, 0f, h)); verts.Add(c + new Vector3(h, 0f, h)); verts.Add(c + new Vector3(h, 0f, -h));
                for (int k = 0; k < 4; k++) cols.Add(col);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mr.enabled = verts.Count > 0;
        }
    }
}
