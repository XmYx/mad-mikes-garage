using System.Collections.Generic;
using MadMax.Building;
using MadMax.Game;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>A kept animal in the save.</summary>
    [System.Serializable]
    public class AnimalSave
    {
        public string species, bags;
        public Vector3 position, home;
        public float yaw, trust, health, stamina;
        public int born, fedDay, hungry, stock, order;
        public bool saddled;
    }

    /// <summary>Puts animals in the world (roadmap 23). Wild herds are deterministic per 200 m cell and biome (seeded
    /// from the world), spawn within 150 m and fold away beyond 220 m; a herd that lost animals to you stays smaller
    /// for three days. Villages keep chickens, goats, pigs and cows. Vultures gather over fresh bodies in the open.
    /// Kept animals (tamed, bought young, bred) are saved; once a day they eat from a trough near home (or what you
    /// fed them), lay and give milk when fed, grow up, breed in pairs, and starve when forgotten. Loud noises and horns
    /// scatter what hears them. Authority only, like the NPCs.</summary>
    public class AnimalDirector : MonoBehaviour
    {
        public static AnimalDirector Instance { get; private set; }

        const float Cell = 200f, SpawnRange = 150f, FoldRange = 220f;
        const int MaxWild = 45, MaxVultures = 12;

        class Herd
        {
            public string key; public AnimalDef def; public Vector3 at; public int count, id;
            public readonly List<Animal> members = new List<Animal>();
            public bool spawned;
        }

        WastelandGame game;
        readonly Dictionary<Vector2Int, List<Herd>> cells = new Dictionary<Vector2Int, List<Herd>>();
        readonly List<Herd> active = new List<Herd>();
        readonly Dictionary<string, Vector2Int> kills = new Dictionary<string, Vector2Int>();           // herd key → (day, killed)
        readonly Dictionary<int, List<Animal>> villages = new Dictionary<int, List<Animal>>();
        readonly Dictionary<Component, List<Animal>> flocks = new Dictionary<Component, List<Animal>>();
        readonly HashSet<Component> rolled = new HashSet<Component>();
        public readonly List<Animal> kept = new List<Animal>();
        readonly List<AnimalDef> tmpDefs = new List<AnimalDef>();
        List<AnimalSave> pending;
        int herdIds = 1, nextKey, lastDay = -1;
        float tickT, vultureT;

        public void Init(WastelandGame g)
        {
            game = g; Instance = this;
            // every species' meshes now, behind the loading screen: built on first sight they cost a 40 ms hitch each
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.AnimalModels.Prewarm");
            foreach (var d in AnimalLibrary.All) AnimalModels.For(d);
            UnityEngine.Profiling.Profiler.EndSample();
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        string NewKey() => "o:" + (nextKey++) + ":" + Random.Range(0, 99999);

        void Update()
        {
            if (!game || !game.Player || !game.Ready) return;
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient) return;
            if (pending != null) Restore();
            int day = DayNight.Day;
            if (lastDay < 0) lastDay = day;
            else if (day != lastDay) { for (int d = Mathf.Max(lastDay + 1, day - 4); d <= day; d++) DayTick(d); lastDay = day; }
            if ((tickT -= Time.deltaTime) > 0f) return;
            tickT = 1f;
            var focus = game.Current ? game.Current.transform.position : game.Player.transform.position;
            UpdateWild(focus);
            UpdateVillages(focus);
            if ((vultureT -= 1f) <= 0f) { vultureT = 12f; UpdateVultures(focus); }
        }

        // ------------------------------------------------------------------ wild herds

        List<Herd> CellHerds(Vector2Int c)
        {
            if (cells.TryGetValue(c, out var l)) return l;
            l = new List<Herd>();
            var w = game.World; var t = DeformableTerrain.Instance;
            var rnd = new System.Random(game.seed ^ (c.x * 73856093) ^ (c.y * 19349663) ^ 0x5a17);
            var biome = w.BiomeAt((c.x + 0.5f) * Cell, (c.y + 0.5f) * Cell);
            AnimalLibrary.WildIn(biome, tmpDefs);
            for (int i = 0; i < tmpDefs.Count; i++)
            {
                var d = tmpDefs[i];
                if (rnd.NextDouble() >= d.density) continue;
                for (int tries = 0; tries < 4; tries++)
                {
                    float x = c.x * Cell + 15f + (float)rnd.NextDouble() * (Cell - 30f), z = c.y * Cell + 15f + (float)rnd.NextDouble() * (Cell - 30f);
                    if (new Vector2(x, z).magnitude < 70f) continue;                                  // the start stays quiet
                    if (t && t.WaterDepthNoLoad(x, z) > 0.1f) continue;
                    if (System.Array.IndexOf(d.biomes, w.BiomeAt(x, z)) < 0) continue;               // herbivores out of towns, rats and strays in them
                    l.Add(new Herd { key = "w:" + c.x + ":" + c.y + ":" + i, def = d, at = new Vector3(x, 0f, z), count = rnd.Next(d.herdMin, d.herdMax + 1) });
                    break;
                }
            }
            cells[c] = l;
            return l;
        }

        int Killed(string herdKey)
        {
            if (!kills.TryGetValue(herdKey, out var k)) return 0;
            if (DayNight.Day - k.x >= 3) { kills.Remove(herdKey); return 0; }
            return k.y;
        }

        void UpdateWild(Vector3 focus)
        {
            int wild = 0;
            foreach (var h in active) foreach (var a in h.members) if (a && a.Alive) wild++;
            var fc = new Vector2Int(Mathf.FloorToInt(focus.x / Cell), Mathf.FloorToInt(focus.z / Cell));
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    foreach (var h in CellHerds(new Vector2Int(fc.x + dx, fc.y + dz)))
                    {
                        if (h.spawned || wild >= MaxWild || Flat(h.at - focus).magnitude > SpawnRange) continue;
                        wild += SpawnHerd(h);
                    }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var h = active[i];
                bool near = false;
                foreach (var a in h.members) if (a && a.Alive && !a.owned && Flat(a.transform.position - focus).magnitude < FoldRange) { near = true; break; }
                if (near) continue;
                foreach (var a in h.members) if (a && a.Alive && !a.owned) Destroy(a.gameObject);
                h.members.Clear(); h.spawned = false;
                active.RemoveAt(i);
            }
        }

        int SpawnHerd(Herd h)
        {
            int n = h.count - Killed(h.key);
            if (n <= 0) return 0;
            var t = DeformableTerrain.Instance;
            var rnd = new System.Random(h.key.GetHashCode() ^ DayNight.Day);
            h.id = herdIds++;
            for (int i = 0; i < n; i++)
            {
                float r = 2f + (float)rnd.NextDouble() * (3f + n), a = (float)rnd.NextDouble() * 6.283f;
                var p = h.at + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (t) { if (t.WaterDepthNoLoad(p.x, p.z) > 0.2f) p = h.at; p.y = t.HeightNoLoad(p.x, p.z); }
                var an = Animal.Spawn(h.def, p, (float)rnd.NextDouble() * 360f, game.propMaterial, h.key + ":" + i);
                an.herd = h.id;
                h.members.Add(an);
            }
            h.spawned = true;
            active.Add(h);
            return n;
        }

        // ------------------------------------------------------------------ village livestock

        static readonly string[] VillageStock = { "chicken", "chicken", "chicken", "goat", "goat", "pig", "cow", "chicken" };

        void UpdateVillages(Vector3 focus)
        {
            var w = game.World;
            var t = DeformableTerrain.Instance;
            for (int i = 0; i < w.settlements.Count; i++)
            {
                var st = w.settlements[i];
                if (st.kind != Biome.Village) continue;
                float d = Vector2.Distance(st.pos, new Vector2(focus.x, focus.z));
                bool has = villages.TryGetValue(i, out var list);
                if (!has && d < st.radius + 120f)
                {
                    list = new List<Animal>();
                    var rnd = new System.Random(game.seed ^ (i * 7919) ^ 0x1ee7);
                    int n = 4 + rnd.Next(4);
                    for (int j = 0; j < n; j++)
                    {
                        var def = AnimalLibrary.Get(VillageStock[rnd.Next(VillageStock.Length)]);
                        for (int tries = 0; tries < 6; tries++)
                        {
                            float r = st.radius * (0.2f + 0.5f * (float)rnd.NextDouble()), a = (float)rnd.NextDouble() * 6.283f;
                            var p = new Vector3(st.pos.x + Mathf.Cos(a) * r, 0f, st.pos.y + Mathf.Sin(a) * r);
                            if (t) { if (t.WaterDepthNoLoad(p.x, p.z) > 0.1f) continue; p.y = t.HeightNoLoad(p.x, p.z); }
                            if (Physics.CheckSphere(p + Vector3.up * 0.9f, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;   // not inside a house
                            var an = Animal.Spawn(def, p, (float)rnd.NextDouble() * 360f, game.propMaterial, "v:" + i + ":" + j);
                            an.town = i;
                            list.Add(an);
                            break;
                        }
                    }
                    villages[i] = list;
                }
                else if (has && d > st.radius + 190f)
                {
                    foreach (var a in list) if (a && a.Alive) Destroy(a.gameObject);
                    villages.Remove(i);
                }
            }
        }

        // ------------------------------------------------------------------ vultures

        void UpdateVultures(Vector3 focus)
        {
            int count = 0;
            var gone = new List<Component>();
            foreach (var kv in flocks)
            {
                kv.Value.RemoveAll(a => !a);
                // a flock leaves with its meal: carcass gone, picked clean or butchered, or the scene left far behind
                var carcass = kv.Key as Carcass;
                bool done = !kv.Key || (carcass && (carcass.butchered || !carcass.Fresh)) || Flat(kv.Key.transform.position - focus).magnitude > FoldRange;
                if (done) { foreach (var v in kv.Value) if (v && v.Alive) Destroy(v.gameObject); kv.Value.Clear(); }
                if (kv.Value.Count == 0) gone.Add(kv.Key);
                count += kv.Value.Count;
            }
            foreach (var k in gone) flocks.Remove(k);
            if (count >= MaxVultures) return;
            foreach (var c in Carcass.All)
                if (c && !c.butchered && c.Fresh && c.def != null && c.def.id != "vulture") TryFlock(c, c.transform.position, focus, ref count);
            foreach (var n in MadMax.Npc.Npc.All)
                if (n && !n.Alive) TryFlock(n, n.transform.position, focus, ref count);
        }

        void TryFlock(Component body, Vector3 at, Vector3 focus, ref int count)
        {
            if (count >= MaxVultures || flocks.ContainsKey(body) || rolled.Contains(body)) return;
            if (Flat(at - focus).magnitude > 140f || game.World.SettlementAt(at.x, at.z) != null) return;
            rolled.Add(body);
            if (Random.value < 0.4f) return;
            var def = AnimalLibrary.Get("vulture");
            var list = new List<Animal>();
            int n = Random.Range(2, 5);
            for (int i = 0; i < n; i++)
            {
                var p = at + new Vector3(Random.Range(-15f, 15f), Random.Range(28f, 40f), Random.Range(-15f, 15f));
                var v = Animal.Spawn(def, p, Random.value * 360f, game.propMaterial, "vul");
                v.Circle(at, body);
                list.Add(v);
            }
            flocks[body] = list;
            count += n;
        }

        // ------------------------------------------------------------------ kept animals

        /// <summary>Release a young animal bought or bred as an item (animal_*) at your feet; it makes its home here.</summary>
        public void Release(WastelandGame g, string item)
        {
            var d = AnimalLibrary.ForYoung(item);
            if (d == null || g.Current || !g.Inventory.TakeItem(item)) return;
            var p = g.Player.transform.position + g.Player.transform.forward * 1.2f;
            var t = DeformableTerrain.Instance;
            if (t) p.y = t.HeightNoLoad(p.x, p.z);
            var a = Keep(d, p, g.Player.transform.eulerAngles.y + 180f, DayNight.Day);
            a.fedDay = DayNight.Day;
            g.Toast("THE " + a.Label + " MAKES ITS HOME HERE - KEEP A TROUGH FILLED NEARBY ([T] LEADS IT)");
        }

        Animal Keep(AnimalDef d, Vector3 p, float yaw, int born)
        {
            var a = Animal.Spawn(d, p, yaw, game.propMaterial, NewKey());
            a.owned = true; a.born = born; a.home = p; a.order = 0;
            a.ApplySize();
            kept.Add(a);
            return a;
        }

        /// <summary>A wild animal was tamed: it leaves its herd.</summary>
        public void Adopt(Animal a)
        {
            foreach (var h in active) h.members.Remove(a);
            a.key = NewKey();
            if (!kept.Contains(a)) kept.Add(a);
        }

        public void Died(Animal a)
        {
            kept.Remove(a);
            if (a.key == null || !a.key.StartsWith("w:")) return;
            int cut = a.key.LastIndexOf(':');
            var herdKey = cut > 0 ? a.key.Substring(0, cut) : a.key;
            int k = Killed(herdKey);
            kills[herdKey] = new Vector2Int(DayNight.Day, k + 1);
        }

        void DayTick(int day)
        {
            kept.RemoveAll(a => !a || !a.Alive);
            var breeders = new Dictionary<string, List<Animal>>();
            var t = DeformableTerrain.Instance;
            foreach (var a in kept.ToArray())
            {
                if (!a || !a.Alive) continue;
                var d = a.Def;
                bool fed = a.fedDay >= day - 1;
                if (!fed) { var tr = Trough.Near(a.home, d.feedPerDay); if (tr && tr.Eat(d.feedPerDay)) fed = true; }
                if (!fed && (d.id == "goat" || d.id == "cow" || d.id == "horse") && t)
                {
                    var b = t.BiomeAt(a.home.x, a.home.z);
                    if ((b == Biome.Forest || b == Biome.Tropical || b == Biome.Village) && Random.value < 0.6f) fed = true;     // found grazing
                }
                if (fed)
                {
                    a.hungryDays = 0;
                    a.health = Mathf.Min(d.health, a.health + d.health * 0.25f);
                    if (a.Adult && d.product != null) Produce(a);
                    if (a.Grazer || d.id == "pig")                                                   // the pen yields dung for the composter
                    {
                        var tr = Trough.Nearest(a.home);
                        if (tr) { tr.manure = Mathf.Min(Trough.ManureCap, tr.manure + (d.id == "cow" || d.id == "horse" ? 2 : 1)); tr.GetComponent<Placeable>()?.Dirty(); }
                    }
                    if (a.Adult) { if (!breeders.TryGetValue(d.id, out var l)) breeders[d.id] = l = new List<Animal>(); l.Add(a); }
                }
                else
                {
                    a.hungryDays++;
                    if (a.hungryDays >= 3) a.health -= d.health * 0.25f;
                    if (a.hungryDays >= 6 || a.health <= 0f)
                    {
                        if (d.tameable) { a.owned = false; a.trust = 0.3f; a.order = 0; kept.Remove(a); game.Toast("YOUR " + a.Label + " WANDERED OFF - NOBODY FED IT"); }
                        else { a.ApplyHit(a.transform.position, Vector3.down, 99f, 0.1f, null); game.Toast("YOUR " + a.Label + " STARVED"); }
                        continue;
                    }
                    if (a.hungryDays == 2) game.Toast("YOUR " + a.Label + " IS HUNGRY - FILL A TROUGH NEAR ITS HOME");
                }
                a.ApplySize();
            }
            // breeding: two fed adults sharing a pen, room for more
            foreach (var kv in breeders)
            {
                var l = kv.Value;
                if (l.Count < 2) continue;
                var d = l[0].Def;
                var parent = l[Random.Range(0, l.Count)];
                int pair = 0, local = 0;
                foreach (var a in l) if ((a.home - parent.home).sqrMagnitude < 15f * 15f) pair++;
                foreach (var a in kept) if (a && a.Def == d && (a.home - parent.home).sqrMagnitude < 15f * 15f) local++;
                if (pair < 2 || local >= 10 || Random.value > (d.id == "chicken" ? 0.5f : 0.3f)) continue;
                var p = parent.transform.position + parent.transform.right * 0.8f;
                if (t) p.y = t.HeightNoLoad(p.x, p.z);
                var young = Keep(d, p, Random.value * 360f, day);
                young.home = parent.home; young.fedDay = day;
                game.Toast("A NEW " + (d.id == "chicken" ? "CHICK" : d.id == "cow" ? "CALF" : d.id == "goat" ? "KID" : d.id == "pig" ? "PIGLET" : d.id == "horse" ? "FOAL" : "PUP") + " IN THE PEN");
            }
        }

        void Produce(Animal a)
        {
            int n = a.Def.productPerDay;
            if (a.Def.product == "food_egg")
            {
                // hens lay in a nest box when there is one
                foreach (var c in Container.All)
                    if (c && c.title == "NEST BOX" && (c.transform.position - a.home).sqrMagnitude < 12f * 12f) { c.inventory.AddItem("food_egg", n); return; }
            }
            a.stock = Mathf.Min(a.Def.productPerDay * 3, a.stock + n);
        }

        // ------------------------------------------------------------------ noise

        /// <summary>A loud noise at <paramref name="at"/> heard within <paramref name="radius"/> m.</summary>
        public void Noise(Vector3 at, float radius)
        {
            float r2 = radius * radius;
            for (int i = Animal.All.Count - 1; i >= 0; i--)
            {
                var a = Animal.All[i];
                if (a && (a.transform.position - at).sqrMagnitude < r2) a.Hear(at, radius);
            }
        }

        public void Horn(Vector3 at) => Noise(at, 45f);

        // ------------------------------------------------------------------ save

        public void Save(SaveData d)
        {
            d.animals.Clear();
            foreach (var a in kept)
            {
                if (!a || !a.Alive) continue;
                d.animals.Add(new AnimalSave
                {
                    species = a.Def.id, position = a.transform.position, home = a.home, yaw = a.transform.eulerAngles.y, trust = a.trust, health = a.health, stamina = a.stamina,
                    born = a.born, fedDay = a.fedDay, hungry = a.hungryDays, stock = a.stock, order = a.order, saddled = a.saddled, bags = a.bags ? a.bags.SaveState() : null
                });
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in kills) sb.Append(kv.Key).Append('=').Append(kv.Value.x).Append(',').Append(kv.Value.y).Append(';');
            d.animalKills = sb.ToString();
        }

        public void Load(SaveData d)
        {
            pending = d.animals != null ? new List<AnimalSave>(d.animals) : null;
            kills.Clear();
            if (string.IsNullOrEmpty(d.animalKills)) return;
            foreach (var e in d.animalKills.Split(';'))
            {
                int eq = e.IndexOf('='), comma = e.LastIndexOf(',');
                if (eq <= 0 || comma < eq) continue;
                if (int.TryParse(e.Substring(eq + 1, comma - eq - 1), out int day) && int.TryParse(e.Substring(comma + 1), out int n)) kills[e.Substring(0, eq)] = new Vector2Int(day, n);
            }
        }

        void Restore()
        {
            var list = pending; pending = null;
            foreach (var s in list)
            {
                var d = AnimalLibrary.Get(s.species);
                if (d == null) continue;
                var a = Keep(d, s.position, s.yaw, s.born);
                a.home = s.home; a.trust = s.trust; a.health = s.health > 0f ? s.health : d.health; a.stamina = s.stamina;
                a.fedDay = s.fedDay; a.hungryDays = s.hungry; a.stock = s.stock; a.order = s.order;
                if (s.saddled) { a.SetSaddled(true); if (a.bags && !string.IsNullOrEmpty(s.bags)) a.bags.LoadState(s.bags); }
                a.ApplySize();
            }
        }
    }
}
