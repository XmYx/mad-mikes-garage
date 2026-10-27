using System.Collections.Generic;
using MadMax.Npc;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Dressed scarecrows in town (2026-10-26): the fields of farm lots in villages and towns get a scarecrow
    /// (deterministic per lot) wearing the town's cast-offs, flapping in the wind; a settlement on a raider gang's road
    /// has one on its approach in that gang's colours with its emblem — their ground starts here (a toast and a journal
    /// line the first time the player passes within <see cref="WarnReach"/> m). Decoration only, spawned within
    /// <see cref="Near"/> m of the player and folded beyond <see cref="Far"/>; not saved.</summary>
    public class TownScarecrows : MonoBehaviour
    {
        public const float Near = 150f, Far = 200f, WarnReach = 40f;
        /// <summary>Spawned scarecrows (tests) and the last gang warning.</summary>
        public static readonly Dictionary<string, Transform> Shown = new Dictionary<string, Transform>();
        public static string LastWarning;
        static readonly HashSet<int> warned = new HashSet<int>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Shown.Clear(); LastWarning = null; warned.Clear(); Dressed.Clear(); Torn.Clear(); gangOf.Clear(); }

        /// <summary>Scarecrows the player gave a garment (key → item id; they wear it) and gang scarecrows torn down
        /// (2026-10-27, saved by <c>WastelandGame.Save1027</c>).</summary>
        public static readonly Dictionary<string, string> Dressed = new Dictionary<string, string>();
        public static readonly HashSet<string> Torn = new HashSet<string>();
        public static bool IsGang(string key) => key != null && key.EndsWith(":gang", System.StringComparison.Ordinal);
        public static int TownOf(string key) { int i = key != null ? key.IndexOf(':') : -1; return i > 0 && int.TryParse(key.Substring(0, i), out int t) ? t : -1; }
        /// <summary>The gang whose colours the scarecrow at <paramref name="key"/> wears (null when not shown or not a gang's).</summary>
        public static string GangOf(string key) => gangOf.TryGetValue(key, out var g) ? g : null;
        static readonly Dictionary<string, string> gangOf = new Dictionary<string, string>();

        /// <summary>The scarecrow wears <paramref name="item"/> from now on (re-dressed when shown).</summary>
        public static void Dress(string key, string item) { Dressed[key] = item; Refold(key); }
        public static void TearDown(string key) { Torn.Add(key); Refold(key); }
        static void Refold(string key) { if (Shown.TryGetValue(key, out var t)) { if (t) Destroy(t.gameObject); Shown.Remove(key); } }

        public struct Spot { public string key; public Vector3 at; public float yaw; public int town, gang; public Color coat; }

        readonly Dictionary<int, List<Spot>> plans = new Dictionary<int, List<Spot>>();
        readonly List<Transform> coats = new List<Transform>();
        readonly List<string> drop = new List<string>();
        float scanT;

        static readonly Color[] GangCoats = { new Color(0.62f, 0.64f, 0.68f), new Color(0.55f, 0.24f, 0.1f), new Color(0.86f, 0.82f, 0.72f), new Color(0.12f, 0.11f, 0.11f) };

        void Update()
        {
            if (Time.time < scanT) return;
            scanT = Time.time + 2f;
            var g = MadMax.Game.WastelandGame.Instance;
            var w = g ? g.World : null;
            if (w == null || !g.Player) return;
            var me = g.Player.transform.position;
            for (int i = 0; i < w.settlements.Count; i++)
            {
                var st = w.settlements[i];
                if (st.plan == null || st.kind == Biome.City) continue;
                float reach = st.plan.radius + Far;
                if ((st.pos - new Vector2(me.x, me.z)).sqrMagnitude > reach * reach) continue;
                foreach (var s in Plan(i, st, g)) Consider(s, me, g);
            }
            drop.Clear();
            foreach (var kv in Shown)
            {
                if (!kv.Value) { drop.Add(kv.Key); continue; }
                var d = kv.Value.position - me; d.y = 0f;
                if (d.sqrMagnitude > Far * Far) { Destroy(kv.Value.gameObject); drop.Add(kv.Key); }
            }
            foreach (var k in drop) Shown.Remove(k);
            coats.RemoveAll(c => !c);
        }

        void LateUpdate()
        {
            float wv = Fx.Wind.magnitude;
            for (int i = 0; i < coats.Count; i++)
                if (coats[i]) coats[i].localRotation = Quaternion.Euler(Mathf.Sin(Time.time * (1.5f + wv * 0.4f) + i * 2.1f) * (3f + wv * 2.4f), 0f, Mathf.Sin(Time.time * 0.8f + i) * 2.5f);
        }

        /// <summary>The scarecrows of settlement <paramref name="index"/>: one in most farm lots' fields, and (on a gang's
        /// road) one on the approach nearest that road dressed in the gang's colours.</summary>
        public List<Spot> Plan(int index, Settlement st, MadMax.Game.WastelandGame g)
        {
            if (plans.TryGetValue(index, out var list)) return list;
            list = new List<Spot>();
            var plan = st.plan;
            var rnd = new System.Random(g.World.seed * 7919 + index * 131 + 17);
            for (int li = 0; li < plan.lots.Count; li++)
            {
                var lot = plan.lots[li];
                double roll = rnd.NextDouble();
                if (lot.use != LotUse.Farm || lot.Depth < 20f || roll > 0.7) continue;
                float hw = lot.Width * 0.5f;
                var l = lot.At(((float)rnd.NextDouble() - 0.5f) * (hw * 1.2f), 16f + (lot.Depth - 16f) * (0.35f + (float)rnd.NextDouble() * 0.3f));
                var p = plan.World(l);
                list.Add(new Spot { key = index + ":" + li, at = new Vector3(p.x, 0f, p.y), yaw = (float)rnd.NextDouble() * 360f, town = index, gang = -1, coat = Cast(rnd) });
            }
            var d = NpcDirector.Instance;
            var centre = new Vector3(st.pos.x, 0f, st.pos.y);
            var gang = d ? d.RaidersByRoad(centre) : null;
            if (gang != null && gang.RoadDistance(centre) < Factions.RoadTerritory + plan.radius)
            {
                int gi = System.Array.IndexOf(NpcLore.Gangs, gang.Gang);
                if (gi >= 0)
                {
                    Vector3 at = centre; float best = float.MaxValue;
                    for (int k = 0; k < 12; k++)                                                      // on the way out toward their road
                    {
                        float a = k * Mathf.PI / 6f;
                        var c = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (plan.radius + 14f);
                        float rd = gang.RoadDistance(c);
                        if (rd < best) { best = rd; at = c; }
                    }
                    list.Add(new Spot { key = index + ":gang", at = at, yaw = Mathf.Atan2(at.x - centre.x, at.z - centre.z) * Mathf.Rad2Deg, town = index, gang = gi, coat = GangCoats[gi % GangCoats.Length] });
                }
            }
            plans[index] = list;
            return list;
        }

        static Color Cast(System.Random rnd)
        {
            var ramps = new[] { MadMax.Voxel.Pal.Moss, MadMax.Voxel.Pal.Navy, MadMax.Voxel.Pal.Crimson, MadMax.Voxel.Pal.Ochre, MadMax.Voxel.Pal.Wood, MadMax.Voxel.Pal.Cream };
            var r = ramps[rnd.Next(ramps.Length)];
            Color c = r[1 + rnd.Next(3)];
            return c * 0.85f;
        }

        void Consider(Spot s, Vector3 me, MadMax.Game.WastelandGame g)
        {
            var d = s.at - me; d.y = 0f;
            float dist = d.magnitude;
            if (s.gang >= 0 && dist < WarnReach && !Torn.Contains(s.key) && warned.Add(s.town))
            {
                LastWarning = "A SCARECROW IN " + NpcLore.Gangs[s.gang] + " COLOURS: THEIR GROUND STARTS HERE";
                g.Toast(LastWarning);
                MadMax.Game.Journal.Add("PEOPLE", LastWarning);
            }
            if (s.gang >= 0) gangOf[s.key] = NpcLore.Gangs[s.gang];
            if (dist > Near || Shown.ContainsKey(s.key) || Torn.Contains(s.key)) return;
            var def = MadMax.Building.FurnitureLibrary.Get("scarecrow");
            if (def == null || !def.mesh) return;
            var t = DeformableTerrain.Instance;
            var at = s.at; at.y = t ? t.HeightNoLoad(at.x, at.z) : me.y;
            var go = new GameObject("TownScarecrow " + s.key, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, s.yaw, 0f));
            go.transform.SetParent(transform, true);
            go.GetComponent<MeshFilter>().sharedMesh = def.mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = g.propMaterial;
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(c.GetComponent<Collider>());
            c.name = "Coat";
            var coat = c.transform; coat.SetParent(go.transform, false);
            coat.localPosition = new Vector3(0f, 1.25f, 0.06f);
            coat.localScale = new Vector3(0.62f, 0.55f, 0.05f);
            var m = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", Dressed.TryGetValue(s.key, out var given) ? MadMax.Building.ClothesLine.Colour(given) * 0.9f : s.coat);
            c.GetComponent<MeshRenderer>().sharedMaterial = m;
            coats.Add(coat);
            if (s.gang >= 0)
            {
                var emblem = MadMax.Vehicles.Decals.Mesh(s.gang + 1);
                if (emblem)
                {
                    var e = new GameObject("Emblem", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    e.SetParent(go.transform, false);
                    e.localPosition = new Vector3(0f, 1.3f, 0.1f);
                    e.localScale = Vector3.one * 1.4f;
                    e.GetComponent<MeshFilter>().sharedMesh = emblem;
                    e.GetComponent<MeshRenderer>().sharedMaterial = g.propMaterial;
                }
            }
            Shown[s.key] = go.transform;
        }

        void OnDestroy()
        {
            foreach (var kv in Shown) if (kv.Value) Destroy(kv.Value.gameObject);
            Shown.Clear();
        }
    }
}
