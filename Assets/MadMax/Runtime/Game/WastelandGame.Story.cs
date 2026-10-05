using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The campaign KEEP THE LIGHT ON (storyline.md) in the running game: the STORY start (almost nothing,
    /// an overturned convoy, a stranded car with a loose battery lead, Nell's roadside stop down the road), the cast's
    /// bodies at their anchors, the once-a-second quest tick and the save hooks. Sandbox worlds run only the side
    /// quests that don't need the campaign.</summary>
    public partial class WastelandGame
    {
        readonly HashSet<uint> storyProps = new HashSet<uint>();
        readonly Dictionary<string, MadMax.Npc.Npc> castBodies = new Dictionary<string, MadMax.Npc.Npc>();
        readonly Dictionary<string, string> castAt = new Dictionary<string, string>();
        float castCheck;

        /// <summary>A piece the campaign set up (it doesn't count as the player's own work).</summary>
        public bool IsStoryProp(Placeable p) => p && storyProps.Contains(p.Id);

        /// <summary>A story prop the story wants out of the way (the garage's barricade): it can be dismantled like an own piece.</summary>
        public bool StoryClearable(Placeable p) => IsStoryProp(p) && p.id == "barricade";

        /// <summary>The campaign's objective line for the HUD (null outside a campaign).</summary>
        public string StoryLine => Story.Story.Campaign ? Story.Story.Line : null;

        /// <summary>A fresh STORY world: bind the anchors, set the scene, start A1.</summary>
        void StoryNewGame()
        {
            Story.Story.Clear();
            Story.Story.Campaign = true;
            StoryAnchors.Bind(World);
            foreach (var c in ClothingLibrary.Starter) Inventory.AddItem("cloth_" + c);             // worn everyday layers, nothing else
            Inventory.AddItem("cloth_jeans");

            // the convoy: a box trailer on its side and the tractor unit that pulled it
            var w = StoryAnchors.Get("wreck"); float wy = StoryAnchors.Yaw("wreck");
            var q = Quaternion.Euler(0f, wy, 0f);
            var trailer = SpawnRoadWreck("BoxTrailer", w, Quaternion.Euler(0f, wy, 88f), World.seed ^ 0x51) ?? SpawnRoadWreck("CargoTrailer", w, Quaternion.Euler(0f, wy, 172f), World.seed ^ 0x51);
            Rest(trailer);
            Rest(SpawnRoadWreck("Hauler", w + q * new Vector3(-5f, 0f, 16f), Quaternion.Euler(0f, wy + 25f, 0f), World.seed ^ 0x52));

            // the stranded car: some fuel still in it, dependable once its battery lead is back on
            var carPrefab = PrefabFor("Fiat126p") ?? PrefabFor("Trabant");
            if (carPrefab)
            {
                var cp = StoryAnchors.Get("car"); cp.y = terrain.Height(cp.x, cp.z) + 0.6f;
                var car = Instantiate(carPrefab, cp, Quaternion.Euler(0f, StoryAnchors.Yaw("car"), 0f)).GetComponent<VehicleDriver>();
                Register(car, fleet);
                if (car.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = Mathf.Min(sys.fuelCapacity * 0.4f, 12f); sys.disconnected = true; }
                if (car.TryGetComponent<VehicleDamage>(out var dmg)) dmg.AddFrameDamage(0.05f, 1f);
            }
            BuildNellsStop();

            // the player comes to against the trailer's flank
            var sp = w + q * new Vector3(3.5f, 0f, -2f);
            if (trailer)
            {
                var tb = Bounds(trailer.gameObject);
                sp = new Vector3(tb.center.x, 0f, tb.center.z) + q * new Vector3(tb.extents.magnitude * 0.55f + 1.2f, 0f, 0f);
                if (Vector3.Distance(sp, StoryAnchors.Get("satchel")) < 2.8f) sp += q * new Vector3(0f, 0f, 3f);
            }
            sp.y = terrain.Height(sp.x, sp.z) + 0.1f;
            Player.gameObject.SetActive(true);
            Player.Teleport(sp, wy + 180f);
            terrain.focus = Player.transform;
            if (cameraRig) cameraRig.SetTarget(Player.transform);
            Story.Story.Activate(this, "A1");
            Journal.Add("RADIO", "\"...SEVEN, EIGHT... IF YOU MADE IT, KEEP THE LIGHT ON.\"");
            Toast("THE RADIO CRACKLES: \"IF YOU MADE IT, KEEP THE LIGHT ON.\"");
            MadMax.Npc.QuestVoice.Radio("radio_mara", "...SEVEN, EIGHT... IF YOU MADE IT, KEEP THE LIGHT ON.");
        }

        /// <summary>Sit a wreck spawned at an angle on the ground (lowest point at ground level) so it doesn't start inside
        /// the terrain and get thrown when physics wakes it.</summary>
        void Rest(VehicleDriver v)
        {
            if (!v) return;
            var b = Bounds(v.gameObject);
            float ground = terrain.HeightNoLoad(b.center.x, b.center.z);
            foreach (var c in new[] { new Vector3(b.min.x, 0f, b.min.z), new Vector3(b.max.x, 0f, b.min.z), new Vector3(b.min.x, 0f, b.max.z), new Vector3(b.max.x, 0f, b.max.z) })
                ground = Mathf.Max(ground, terrain.HeightNoLoad(c.x, c.z));
            var lift = Vector3.up * (ground + 0.05f - b.min.y);
            v.transform.position += lift;
            if (v.Body) v.Body.position = v.transform.position;
        }

        static Bounds Bounds(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Nell's roadside stop: an awning, a fire, a bench, a water barrel and a cracked rain collector.</summary>
        void BuildNellsStop()
        {
            if (!StoryAnchors.Has("nell") || !Build || !Build.Structures) return;
            var a = StoryAnchors.Get("nell"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("nell"), 0f);
            Placeable Put(string id, Vector3 local, float turn)
            {
                var p = a + r * local; p.y = terrain.HeightNoLoad(p.x, p.z);
                var pl = FurnitureLibrary.Spawn(id, Build.Structures, p, r * Quaternion.Euler(0f, turn, 0f), propMaterial);
                if (pl) storyProps.Add(pl.Id);
                return pl;
            }
            Put("porch_awning", new Vector3(0f, 0f, -1.5f), 0f);
            Put("campfire", new Vector3(-2.6f, 0f, 2.2f), 0f);
            Put("bench", new Vector3(-2.6f, 0f, 4.1f), 180f);
            Put("barrel", new Vector3(1.8f, 0f, -2.6f), 0f);
            var rc = Put("rain_collector", new Vector3(3.2f, 0f, 0.5f), 0f);
            if (rc) { rc.hits = 1; rc.Dirty(); }
        }

        /// <summary>The garage at the bend (B1): brick walls, a garage doorway blocked by a barricade, half a roof, junk
        /// inside and a sign. All campaign props; the player's own bench and claim flag are what count.</summary>
        void BuildGarage()
        {
            if (!StoryAnchors.Has("garage") || !Build || !Build.Structures) return;
            var a = StoryAnchors.Get("garage"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("garage"), 0f);
            float y = terrain.HeightNoLoad(a.x, a.z);
            Placeable Put(string id, float x, float z, float turn, float up = 0f)
            {
                var p = a + r * new Vector3(x, 0f, z); p.y = y + up;
                var pl = FurnitureLibrary.Spawn(id, Build.Structures, p, r * Quaternion.Euler(0f, turn, 0f), propMaterial);
                if (pl) storyProps.Add(pl.Id);
                return pl;
            }
            Put("garage_frame", 0f, 3f, 0f);
            Put("wall_brick", -3f, 3f, 0f); Put("wall_brick", 3f, 3f, 0f);
            foreach (float x in new[] { -3f, -1f, 1f, 3f }) Put(x == 1f ? "wall_brick_window" : "wall_brick", x, -3f, 180f);
            foreach (float z in new[] { -2f, 0f, 2f }) { Put("wall_brick", -4f, z, -90f); Put(z == 0f ? "doorway_brick" : "wall_brick", 4f, z, 90f); }
            for (int i = 0; i < 12; i++)
            {
                if (i == 4 || i == 7 || i == 9) continue;                                         // holes in the roof
                Put("roof_flat", -3f + (i % 4) * 2f, -2f + (i / 4) * 2f, 0f, 2.42f);
            }
            var blocker = Put("barricade", 0f, 3.4f, 0f);
            if (blocker) { blocker.hits = 2; blocker.Dirty(); }
            Put("tyres", -2.6f, 1.4f, 0f); Put("barrel", 2.7f, -1.9f, 0f); Put("tyres", 2.4f, 0.6f, 30f);
            Put("sign", -4.8f, 4.6f, 0f);
            Journal.Add("PLACE", "THE GARAGE AT THE BEND: MIKE'S SIGN, A BLOCKED DOOR, HALF A ROOF");
        }

        Placeable PutAt(string anchor, string id, Vector3 local, float turn)
        {
            var a = StoryAnchors.Get(anchor); var r = Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f);
            var p = a + r * local; p.y = terrain.HeightNoLoad(p.x, p.z);
            var pl = FurnitureLibrary.Spawn(id, Build.Structures, p, r * Quaternion.Euler(0f, turn, 0f), propMaterial);
            if (pl) storyProps.Add(pl.Id);
            return pl;
        }

        /// <summary>S09: three dry beds with crops going under, the scarecrow planted far off where no crow lands, a
        /// water barrel.</summary>
        void BuildUnaGarden()
        {
            if (!StoryAnchors.Has("una") || !Build || !Build.Structures) return;
            string[] crops = { "seed_corn", "seed_tomato", "seed_cabbage" };
            for (int i = 0; i < 3; i++)
            {
                var bed = PutAt("una", "garden_plot", new Vector3(-2.4f + i * 2.4f, 0f, -1.5f), 0f);
                if (bed && bed.TryGetComponent<GardenPlot>(out var plot)) { plot.crop = crops[i]; plot.growth = 0.45f + i * 0.1f; plot.water = 0f; plot.health = 0.7f; bed.Dirty(); }
            }
            PutAt("una", "scarecrow", new Vector3(9f, 0f, 7f), 30f);
            PutAt("una", "barrel", new Vector3(3.8f, 0f, 1.2f), 0f);
        }

        bool UnaWatered()
        {
            if (!StoryAnchors.Has("una")) return false;
            var a = StoryAnchors.Get("una"); int beds = 0;
            foreach (var p in Placeable.All)
            {
                if (!p || p.id != "garden_plot" || !IsStoryProp(p) || (p.transform.position - a).sqrMagnitude > 64f) continue;
                if (!p.TryGetComponent<GardenPlot>(out var plot) || plot.water < 0.5f) return false;
                beds++;
            }
            return beds >= 3;
        }

        /// <summary>S24: Gus's corner: a bench, a table with the wobbling half-machine, tyres and a barrel of junk.</summary>
        void BuildGusCorner()
        {
            if (!StoryAnchors.Has("gus") || !Build || !Build.Structures) return;
            PutAt("gus", "bench", new Vector3(-1.5f, 0f, -1.2f), 0f);
            PutAt("gus", "table", new Vector3(1.2f, 0f, -0.8f), 0f);
            PutAt("gus", "tyres", new Vector3(2.8f, 0f, -2.2f), 20f);
            PutAt("gus", "barrel", new Vector3(-3f, 0f, 0.5f), 0f);
        }

        /// <summary>A2: Len Pike's roadside repair stall: a table, tyres, a barrel and a bench.</summary>
        void BuildLenStall()
        {
            if (!StoryAnchors.Has("a2_stall") || !Build || !Build.Structures) return;
            PutAt("a2_stall", "table", new Vector3(0f, 0f, -1.2f), 0f);
            PutAt("a2_stall", "tyres", new Vector3(2.2f, 0f, -1.8f), 15f);
            PutAt("a2_stall", "tyres", new Vector3(3.1f, 0f, -0.4f), 50f);
            PutAt("a2_stall", "barrel", new Vector3(-2.2f, 0f, -1.5f), 0f);
            PutAt("a2_stall", "bench", new Vector3(-1.2f, 0f, 1.4f), 90f);
        }

        /// <summary>S03: Sol's black hearse on the road out of town, engine seized (repair it or winch it).</summary>
        void SpawnHearse()
        {
            if (!StoryAnchors.Has("hearse")) return;
            var pf = PrefabFor("Wagon") ?? PrefabFor("Sedan");
            if (!pf) return;
            var rot = Quaternion.Euler(0f, StoryAnchors.Yaw("hearse"), 0f);
            var p = StoryAnchors.Get("hearse");
            for (int k = 0; k < 6; k++)                                                             // step along the verge past anything parked there
            {
                bool busy = false;
                foreach (var o in vehicles) if (o && Vector2.Distance(new Vector2(o.transform.position.x, o.transform.position.z), new Vector2(p.x, p.z)) < 7f) { busy = true; break; }
                if (!busy) break;
                p += rot * Vector3.forward * 9f;
            }
            p.y = terrain.HeightNoLoad(p.x, p.z) + 0.7f;
            var v = Instantiate(pf, p, rot).GetComponent<VehicleDriver>();
            v.name = "Hearse";
            Register(v, null);
            StoryTag.Set(v.gameObject, "hearse");
            var paint = v.GetComponent<VehiclePaint>(); if (!paint) paint = v.gameObject.AddComponent<VehiclePaint>();
            paint.colour = 5; paint.Apply();
            if (v.Engine && v.Engine.TryGetComponent<VehiclePart>(out var ep)) ep.damage = 1f;                     // seized
            if (v.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = 12f;
        }

        void HearseCheck()
        {
            var tag = StoryTag.Find("hearse");
            if (!tag || !StoryAnchors.Has("chapel")) return;
            var a = StoryAnchors.Get("chapel");
            if (new Vector2(tag.transform.position.x - a.x, tag.transform.position.z - a.z).magnitude > 14f) return;
            var dmg = tag.GetComponent<VehicleDamage>();
            if (!dmg || dmg.FrameDamage < 0.15f) Story.Story.Note("s03:gentle");
        }

        /// <summary>S06: Jo's farm: flags along the buried trench, a sign on the garden patch, dry beds and a fuelled
        /// excavator anyone may borrow.</summary>
        void BuildJoFarm()
        {
            if (!StoryAnchors.Has("jo") || !Build || !Build.Structures) return;
            foreach (var t in new[] { "jo_t1", "jo_t2", "jo_t3" })
            {
                var r = Quaternion.Euler(0f, StoryAnchors.Yaw("jo"), 0f);
                var p = StoryAnchors.Get(t) + r * new Vector3(0f, 0f, 2.6f); p.y = terrain.HeightNoLoad(p.x, p.z);
                var f = FurnitureLibrary.Spawn("flag", Build.Structures, p, r, propMaterial);
                if (f) storyProps.Add(f.Id);
            }
            PutAt("jo_garden", "sign", new Vector3(2.8f, 0f, 0f), 0f);
            for (int i = 0; i < 3; i++)
            {
                var bed = PutAt("jo", "field_bed", new Vector3(-8f + i * 2f, 0f, 12f), 0f);
                if (bed && bed.TryGetComponent<GardenPlot>(out var plot)) { plot.crop = "seed_corn"; plot.growth = 0.3f; plot.water = 0f; bed.Dirty(); }
            }
            PutAt("jo", "water_tank", new Vector3(9f, 0f, -9f), 0f);
            var pf = PrefabFor("Excavator");
            if (pf && StoryAnchors.Has("jo_digger"))
            {
                var p = StoryAnchors.Get("jo_digger"); p.y = terrain.HeightNoLoad(p.x, p.z) + 0.8f;
                var ex = Instantiate(pf, p, Quaternion.Euler(0f, StoryAnchors.Yaw("jo_digger"), 0f)).GetComponent<VehicleDriver>();
                ex.name = "Jo's Excavator";
                Register(ex, null);
                StoryTag.Set(ex.gameObject, "jo_digger");
                if (ex.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = sys.fuelCapacity * 0.8f;
            }
        }

        /// <summary>A3: the relay's dead generator beside the mast (cracked casing, empty tank) and a fuel can.</summary>
        void BuildRelay()
        {
            if (!StoryAnchors.Has("relay_gen") || !Build || !Build.Structures) return;
            var gen = PutAt("relay_gen", "generator", Vector3.zero, 0f);                             // dry and switched off
            if (gen && gen.TryGetComponent<Generator>(out var gc)) { gc.fuel = 0f; gc.on = false; gen.Dirty(); }
            PutAt("relay_gen", "barrel", new Vector3(1.8f, 0f, 0.6f), 0f);
            PutAt("relay_gen", "sign", new Vector3(-1.6f, 0f, 1f), 0f);
        }

        bool RelayPowered()
        {
            if (!StoryAnchors.Has("relay_gen")) return false;
            var a = StoryAnchors.Get("relay_gen");
            foreach (var p in Placeable.All)
                if (p && p.id == "generator" && IsStoryProp(p) && (p.transform.position - a).sqrMagnitude < 9f && p.TryGetComponent<Generator>(out var gen) && gen.on && gen.fuel > 0f) return true;
            return false;
        }

        /// <summary>A4: the pumping depot: the convoy's trucks parked by the gate, crates of diverted cargo in the store,
        /// a marker on the service hatch.</summary>
        void BuildDepot()
        {
            if (!StoryAnchors.Has("depot_gate") || !Build || !Build.Structures) return;
            var rot = Quaternion.Euler(0f, StoryAnchors.Yaw("depot"), 0f);
            foreach (var (design, off) in new[] { ("Hauler", new Vector3(-9f, 0f, 22f)), ("BoxTrailer", new Vector3(-9f, 0f, 34f)), ("Hauler", new Vector3(9f, 0f, 24f)) })
            {
                var pf = PrefabFor(design);
                if (!pf) continue;
                var p = StoryAnchors.Get("depot") + rot * off; p.y = terrain.HeightNoLoad(p.x, p.z) + 1f;
                var v = Instantiate(pf, p, rot).GetComponent<VehicleDriver>();
                v.name = "Convoy " + design;
                Register(v, null);
                var paint = v.GetComponent<VehiclePaint>(); if (!paint) paint = v.gameObject.AddComponent<VehiclePaint>();
                paint.colour = 11; paint.Apply();
            }
            PutAt("depot_store", "crate", new Vector3(-1.5f, 0f, 1f), 0f);
            PutAt("depot_store", "crate", new Vector3(1.4f, 0f, 1.2f), 20f);
            PutAt("depot_store", "barrel", new Vector3(0f, 0f, -1.8f), 0f);
            PutAt("depot_tunnel", "sign", new Vector3(0f, 0f, 1f), 0f);
        }

        bool GuardsDown()
        {
            foreach (var k in new[] { "guard1", "guard2" })
            {
                if (MadMax.Npc.NpcRegistry.IsDead("cast:" + k)) continue;
                var b = CastBody(k);
                if (!b || b.Alive) return false;
            }
            return true;
        }

        /// <summary>A3's mast map: every radio mast goes on the map.</summary>
        void MarkMasts()
        {
            foreach (var m in MadMax.World.BiomeProps.Landmarks(World))
                if (m.kind == MadMax.World.BiomeProps.MarkKind.Mast) Discovered.Add("lm:" + m.index);
            Journal.Add("PLACE", "JUNE'S MAST MAP: EVERY RELAY MAST IS ON YOUR MAP NOW");
        }

        void UpdateStory()
        {
            if (!played || !Player) return;
            if (Story.Story.Campaign && Story.Story.StateOf("B1") != Story.Story.State.Locked && !Story.Story.Flag("garage_built")) { BuildGarage(); Story.Story.SetFlag("garage_built"); }
            if (Story.Story.StateOf("S09") != Story.Story.State.Locked && !Story.Story.Flag("scene:S09")) { BuildUnaGarden(); Story.Story.SetFlag("scene:S09"); }
            if (Story.Story.StateOf("S24") != Story.Story.State.Locked && !Story.Story.Flag("scene:S24")) { BuildGusCorner(); Story.Story.SetFlag("scene:S24"); }
            if (Story.Story.StateOf("S09") == Story.Story.State.Active && Time.frameCount % 30 == 0 && UnaWatered()) Story.Story.Note("una:watered");
            if (Story.Story.StateOf("A2") != Story.Story.State.Locked && !Story.Story.Flag("scene:A2")) { BuildLenStall(); Story.Story.SetFlag("scene:A2"); }
            if (Story.Story.StateOf("S03") != Story.Story.State.Locked && !Story.Story.Flag("scene:S03")) { SpawnHearse(); Story.Story.SetFlag("scene:S03"); }
            if (Story.Story.StateOf("S06") != Story.Story.State.Locked && !Story.Story.Flag("scene:S06")) { BuildJoFarm(); Story.Story.SetFlag("scene:S06"); }
            if (Story.Story.StateOf("S03") == Story.Story.State.Active) HearseCheck();
            if (Story.Story.StateOf("A3") != Story.Story.State.Locked && !Story.Story.Flag("scene:A3")) { BuildRelay(); Story.Story.SetFlag("scene:A3"); }
            if (Story.Story.StateOf("A4") != Story.Story.State.Locked && !Story.Story.Flag("scene:A4")) { BuildDepot(); Story.Story.SetFlag("scene:A4"); }
            if (Story.Story.StateOf("A3") == Story.Story.State.Active && Time.frameCount % 15 == 0 && RelayPowered()) Story.Story.Note("a3:power");
            if (Story.Story.StateOf("A4") == Story.Story.State.Active && Time.frameCount % 15 == 0 && GuardsDown()) Story.Story.Note("a4:guards_down");
            if (Story.Story.Flag("masts_known") && !Story.Story.Flag("masts_marked")) { MarkMasts(); Story.Story.SetFlag("masts_marked"); }
            QuestHooks();
            Story.Story.Tick(this);
            if (Time.time < castCheck) return;
            castCheck = Time.time + 1f;
            UpdateCast();
        }

        /// <summary>Cast members stand at their anchors while the story needs them and the player is near.</summary>
        void UpdateCast()
        {
            var focus = FocusPos;
            foreach (var m in StoryCast.All)
            {
                string anchor = CastAnchor(m);
                bool want = anchor != null && StoryAnchors.Has(anchor) && CastPresent(m.key);
                castBodies.TryGetValue(m.key, out var body);
                if (body && castAt.TryGetValue(m.key, out var was) && was != anchor) { Destroy(body.gameObject); castBodies.Remove(m.key); body = null; }   // moved (supper at the garage)
                var at = want ? StoryAnchors.Get(anchor) : Vector3.zero;
                float d = want ? Vector2.Distance(new Vector2(at.x, at.z), new Vector2(focus.x, focus.z)) : 1e9f;
                if (want && !body && d < 110f)
                {
                    var p = StoryCast.Profile(m.key, World.seed);
                    int slot = StoryCast.All.FindIndex(x => x.key == m.key) % 5;                 // several at one anchor stand apart
                    var pos = at + Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f) * new Vector3(0.6f + (slot - 2) * 1.3f, 0f, 0.8f + (slot % 2) * 0.9f);
                    pos.y = terrain.Height(pos.x, pos.z) + 0.05f;
                    var n = MadMax.Npc.Npc.Spawn(p, pos, StoryAnchors.Yaw(anchor), null, propMaterial);
                    n.mode = MadMax.Npc.Npc.Mode.Stand;
                    n.homeRadius = 5f;
                    castBodies[m.key] = n; castAt[m.key] = anchor;
                }
                else if (body && (!want || d > 170f)) { Destroy(body.gameObject); castBodies.Remove(m.key); }
            }
        }

        /// <summary>Who is around: Nell for the campaign; anyone else while a quest they give or take part in runs here
        /// and isn't finished (the rest arrive with their chapters).</summary>
        static bool CastPresent(string key)
        {
            if (MadMax.Npc.NpcRegistry.IsDead("cast:" + key)) return false;
            if (key == "nell") return Story.Story.Campaign;
            if (key == "guard2") return CastPresent("guard1");
            if (key == "vic" || key == "ezra") return SupperTime || (key == "ezra" && Story.Story.Route("B2", "welcome") != null && Story.Story.Route("B2", "welcome").StartsWith("EZRA"));
            var mem = StoryCast.Find(key);
            if (mem != null && mem.Value.present != null) return mem.Value.present();
            foreach (var q in StoryLibrary.All)
            {
                if (!Story.Story.Runs(q) || Story.Story.StateOf(q.id) == Story.Story.State.Locked) continue;
                if (q.giver == key) return true;
                if (Story.Story.StateOf(q.id) != Story.Story.State.Active) continue;
                foreach (var s in q.steps) foreach (var c in s.any) if (c.goal == Goal.Talk && c.key == key) return true;
            }
            return false;
        }

        /// <summary>B2's supper is on: Nell and her guests are at the garage.</summary>
        static bool SupperTime
        {
            get
            {
                var q = StoryLibrary.Get("B2");
                var cur = q != null ? Story.Story.Current(q) : null;
                return cur != null && (cur.id == "supper" || cur.id == "welcome");
            }
        }

        /// <summary>Where a cast member stands right now (Nell comes to the garage for supper).</summary>
        static string CastAnchor(StoryCast.Member m) => m.key == "nell" && SupperTime ? "garage_yard" : m.anchorNow != null ? m.anchorNow() : m.anchor;

        /// <summary>The spawned body of a cast member (null when not around).</summary>
        public MadMax.Npc.Npc CastBody(string key) => castBodies.TryGetValue(key, out var n) && n ? n : null;

        void SaveStory(SaveData d)
        {
            d.story = Story.Story.Save();
            d.storyProps = new List<uint>(storyProps);
            d.heardOf = new List<string>(HeardOf);
        }

        void LoadStory(SaveData d)
        {
            Story.Story.Load(d.story);
            storyProps.Clear();
            if (d.storyProps != null) foreach (var id in d.storyProps) storyProps.Add(id);
            HeardOf.Clear();
            if (d.heardOf != null) foreach (var k in d.heardOf) HeardOf.Add(k);
            if (World != null) StoryAnchors.Bind(World);
        }

        /// <summary>Unmet story givers with work on offer shown as pins (the nearest ones; met givers always show).</summary>
        public const int StoryOfferStrangers = 3;
        static readonly List<string> offerSeen = new List<string>();
        static readonly List<KeyValuePair<float, Pin>> offerStrangers = new List<KeyValuePair<float, Pin>>();

        /// <summary>Map pins: quests in progress (the active step's place) and quests on offer — one "?" per giver, named
        /// once met; of the strangers only the <see cref="StoryOfferStrangers"/> nearest, by what they look like.</summary>
        void StoryPins(List<Pin> into)
        {
            var offer = new Color32(240, 200, 90, 255); var job = new Color32(255, 230, 150, 255);
            offerSeen.Clear(); offerStrangers.Clear();
            var me = FocusPos;
            foreach (var q in StoryLibrary.All)
            {
                if (!Story.Story.Runs(q)) continue;
                var st = Story.Story.StateOf(q.id);
                var m = q.giver != null ? StoryCast.Find(q.giver) : null;
                if (st == Story.Story.State.Open && m != null && m.Value.anchor != null && StoryAnchors.Has(m.Value.anchor) && !offerSeen.Contains(m.Value.key))
                {
                    offerSeen.Add(m.Value.key);
                    var at = StoryAnchors.Get(m.Value.anchor);
                    if (CastMet(m.Value.key)) into.Add(new Pin { label = "? " + m.Value.name, pos = at, color = offer });
                    else if (HeardOf.Contains(m.Value.key)) into.Add(new Pin { label = "? " + m.Value.title, pos = at, color = offer });   // heard of in town talk
                    else offerStrangers.Add(new KeyValuePair<float, Pin>(Flat(at - me), new Pin { label = "? " + m.Value.title, pos = at, color = offer }));
                }
                var s = Story.Story.Current(q);
                if (s != null && s.waypoint != null && StoryAnchors.Has(s.waypoint)) into.Add(new Pin { label = q.title, pos = StoryAnchors.Get(s.waypoint), color = job });
            }
            offerStrangers.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < offerStrangers.Count && i < StoryOfferStrangers; i++) into.Add(offerStrangers[i].Value);
        }

        /// <summary>Story givers the player has heard of in rumours (cast keys): pinned like met ones. Saved.</summary>
        public readonly HashSet<string> HeardOf = new HashSet<string>();
        public const float GiverRumourReach = 4000f;

        /// <summary>Town talk about a stranger with work on offer that the map doesn't show yet (beyond the
        /// <see cref="StoryOfferStrangers"/> nearest): the nearest one to <paramref name="at"/> is now heard of and
        /// pinned. Null when there is nobody left to tell of.</summary>
        public string GiverRumour(Vector3 at)
        {
            if (World == null) return null;
            var shown = new List<KeyValuePair<float, string>>();
            var cands = new List<StoryCast.Member>();
            var me = FocusPos;
            foreach (var q in StoryLibrary.All)
            {
                if (!Story.Story.Runs(q) || Story.Story.StateOf(q.id) != Story.Story.State.Open || q.giver == null) continue;
                var m = StoryCast.Find(q.giver);
                if (m == null || m.Value.anchor == null || !StoryAnchors.Has(m.Value.anchor)) continue;
                if (CastMet(m.Value.key) || HeardOf.Contains(m.Value.key) || cands.Exists(c => c.key == m.Value.key)) continue;
                cands.Add(m.Value);
                shown.Add(new KeyValuePair<float, string>(Flat(StoryAnchors.Get(m.Value.anchor) - me), m.Value.key));
            }
            shown.Sort((a, b) => a.Key.CompareTo(b.Key));
            StoryCast.Member? best = null; float bd = GiverRumourReach;
            foreach (var m in cands)
            {
                int rank = shown.FindIndex(e => e.Value == m.key);
                if (rank < StoryOfferStrangers) continue;                                        // already on the map
                float d = Flat(StoryAnchors.Get(m.anchor) - at);
                if (d < bd) { bd = d; best = m; }
            }
            if (best == null) return null;
            var b = best.Value;
            HeardOf.Add(b.key);
            var p = StoryAnchors.Get(b.anchor);
            return "THERE'S A " + b.title + " " + MadMax.Npc.NpcLore.Compass(p.x - at.x, p.z - at.z) + ", " + MadMax.Npc.NpcLore.Distance(bd) + ", LOOKING FOR A HAND.";
        }

        /// <summary>The player has spoken with this cast member.</summary>
        bool CastMet(string key)
        {
            var p = World != null ? StoryCast.Profile(key, World.seed) : null;
            return p != null && MadMax.Npc.NpcRegistry.Peek(p.id) is MadMax.Npc.NpcSave s && s.Has(MadMax.Npc.NpcSave.Met);
        }
    }
}
