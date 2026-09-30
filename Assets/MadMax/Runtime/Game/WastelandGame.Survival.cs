using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Hunger, thirst, hygiene, sickness, food spoilage, sleeping, farming hooks and on-foot interaction with pieces.</summary>
    public partial class WastelandGame
    {
        DayNight dayNight;
        Vector3? spawnPoint;
        float spoilTimer;
        public IInteractable Focused { get; private set; }

        void InitSurvival()
        {
            dayNight = gameObject.AddComponent<DayNight>();
            dayNight.Init(sun);
            DayNight.DayMinutes = GameRules.DayLengths[Mathf.Clamp(Rules.dayLength, 0, GameRules.DayLengths.Length - 1)];
        }

        void UpdateSurvival(float dt)
        {
            UtilityGrid.Tick(dt);
            if ((spoilTimer += dt) > 10f) { Spoil(spoilTimer); spoilTimer = 0f; }
            var s = Stats;
            float now = DayNight.TotalDays * 24f;
            if (s.rested && now >= s.restedUntil) Toast("NO LONGER WELL RESTED");
            if (s.fed && now >= s.fedUntil) Toast("NO LONGER WELL FED");
            s.rested = now < s.restedUntil; s.fed = now < s.fedUntil;
            if (s.painkilled && now >= s.painkillerUntil) Toast("THE PAINKILLERS WEAR OFF");
            s.painkilled = now < s.painkillerUntil;
            if (!Rules.survival || Vitals == null || Vitals.Dead) return;
            if (s.waste >= 100f && !wasteWarned) { wasteWarned = true; Toast("YOU NEED A LATRINE"); }
            if (s.waste >= 140f)
            {
                // nature calls: behind the nearest bush, with the hygiene that goes with it
                s.waste = 0f; wasteWarned = false;
                s.hygiene = Mathf.Max(0f, s.hygiene - 15f);
                Toast("YOU WENT BEHIND A BUSH");
            }
            float rate = Rules.hungerRate * (s.sick > 0f ? 1.5f : 1f) * (s.fed ? 0.7f : 1f);
            bool active = Player && Player.run && Player.Velocity.sqrMagnitude > 4f;
            s.hunger = Mathf.Max(0f, s.hunger - dt * 100f / (45f * 60f) * rate * (active ? 1.4f : 1f));
            s.thirst = Mathf.Max(0f, s.thirst - dt * 100f / (28f * 60f) * rate * (Weather.Temperature > 30f ? 1.4f : 1f) * (active ? 1.3f : 1f));
            s.hygiene = Mathf.Max(0f, s.hygiene - dt * 100f / (70f * 60f) * rate * (Player && Player.Swimming ? -8f : 1f));
            if (Player && Player.Swimming) s.hygiene = Mathf.Min(100f, s.hygiene + dt * 2f);
            if (s.hunger <= 0f) Vitals.Hurt(dt * 0.6f, "STARVATION");
            if (s.thirst <= 0f) Vitals.Hurt(dt * 1.0f, "THIRST");
            if (s.sick > 0f)
            {
                s.sick -= dt;
                s.stamina = Mathf.Max(0f, s.stamina - dt * 3f);
                Vitals.Hurt(dt * 0.15f, "SICKNESS");
            }
        }

        /// <summary>Dirt from work (salvaging, digging, mud).</summary>
        public void Soil(float amount) => Stats.hygiene = Mathf.Max(0f, Stats.hygiene - amount);

        public void Eat(string id)
        {
            var f = FoodLibrary.Get(id);
            if (f == null || !Inventory.TakeItem(id)) return;
            Stats.hunger = Mathf.Clamp(Stats.hunger + f.hunger, 0f, 100f);
            Stats.thirst = Mathf.Clamp(Stats.thirst + f.thirst, 0f, 100f);
            Stats.health = Mathf.Min(Stats.MaxHealth, Stats.health + f.heal);
            if (Rules.survival) Stats.waste += Mathf.Max(0f, f.hunger) * 0.5f;
            float sick = f.sickChance + (Stats.hygiene < 30f ? 0.15f : 0f) - Stats.Level(Skill.Survival) * 0.01f;
            float table = f.hunger >= 15f && !Current ? DiningTable.Near(Player.transform.position) : 0f;
            if (f.rads > 0f && Vitals) { Vitals.Hurt(f.rads, "RADIATION"); Toast("IT TASTES OF METAL"); }
            if (Random.value < sick) Poison("FOOD POISONING");
            else if (table > 0f)
            {
                // a meal at the table: longer when sitting down to it
                Stats.fedUntil = Mathf.Max(Stats.fedUntil, DayNight.TotalDays * 24f + table * (Player.Sitting ? 1.5f : 1f));
                Toast("ATE " + f.name + " AT THE TABLE: WELL FED");
            }
            else Toast("ATE " + f.name);
            Stats.Practice(Skill.Survival, 1f);
        }

        public void Drink(float thirst, bool dirty)
        {
            Stats.thirst = Mathf.Min(100f, Stats.thirst + thirst);
            if (dirty && Random.value < 0.3f - Stats.Level(Skill.Survival) * 0.015f) Poison("BAD WATER");
            else Toast(dirty ? "DRANK DIRTY WATER" : "DRANK");
        }

        bool wasteWarned;

        void Poison(string why) { Stats.sick = Mathf.Max(Stats.sick, 120f); Toast(why + "!"); }

        public void Wash(float amount, string msg) { Stats.hygiene = Mathf.Min(100f, Stats.hygiene + amount); Toast(msg); }

        public void SetSpawn(Vector3 p) { spawnPoint = p; Toast("SPAWN POINT SET"); }

        /// <summary>Sleep until morning (or rest by day). <paramref name="comfort"/> (0..10, the bed's room) scales the
        /// healing; 3+ wakes you WELL RESTED for 4..16 game hours (faster learning, more stamina).</summary>
        public void Sleep(float comfort = 3f, string note = null)
        {
            if (MadMax.Npc.BaseRaid.Instance && MadMax.Npc.BaseRaid.Instance.WakeFor(this)) { Stats.stamina = Stats.MaxStamina * 0.6f; return; }
            if (DayNight.Darkness > 0.2f || DayNight.Hours > 20f || DayNight.Hours < 5f)
            {
                float slept = Mathf.Repeat(7f - DayNight.Hours, 24f);
                if (DayNight.Hours > 7f) DayNight.SetDay(DayNight.Day + 1);        // slept past midnight
                DayNight.SetHours(7f);
                Stats.hunger = Mathf.Max(5f, Stats.hunger - slept * 2f);
                Stats.thirst = Mathf.Max(5f, Stats.thirst - slept * 3f);
                Stats.health = Mathf.Min(Stats.MaxHealth, Stats.health + Stats.MaxHealth * (0.2f + comfort * 0.03f));
                if (comfort >= 3f)
                {
                    Stats.restedUntil = DayNight.TotalDays * 24f + 4f + comfort * 1.2f;
                    Stats.rested = true;
                    Toast("SLEPT UNTIL MORNING: WELL RESTED (" + Comfort.Word(comfort) + ")");
                }
                else Toast("SLEPT BADLY: " + Comfort.Word(comfort) + (note != null ? " (" + note + ")" : ""));
                MadMax.Net.NetSession.Instance?.SendWeather();
                StarterNote("slept");
                if (GameSettings.Current.autosaveMinutes > 0) Invoke(nameof(AutosaveNow), 1.5f);         // after the toast
            }
            else Toast("RESTED");
            Stats.stamina = Stats.MaxStamina;
        }

        void AutosaveNow() => Autosave();

        /// <summary>A long soak in the tub: clean, warm, rested for a while.</summary>
        public void Bathe(bool clean)
        {
            Stats.hygiene = clean ? 100f : Mathf.Max(Stats.hygiene, 70f);
            Stats.stamina = Stats.MaxStamina;
            Stats.bodyTemp = Mathf.Max(Stats.bodyTemp, 36.9f);
            Stats.restedUntil = Mathf.Max(Stats.restedUntil, DayNight.TotalDays * 24f + 2f);
            Toast(clean ? "A LONG HOT SOAK: CLEAN AND RELAXED" : "A MURKY BATH: CLEANER, AT LEAST");
        }

        /// <summary>A shovel stab into bare ground: a small dig, soil into the pack.</summary>
        /// <summary>Heaviest fish per species (saved).</summary>
        public readonly Dictionary<string, float> FishRecords = new Dictionary<string, float>();

        /// <summary>Note a catch; true when it beats the record for its species.</summary>
        public bool RecordFish(string species, float kg)
        {
            if (FishRecords.TryGetValue(species, out var best) && best >= kg) return false;
            FishRecords[species] = kg;
            return true;
        }

        void SaveFishing(SaveData d)
        {
            d.fishRecordIds = new List<string>(); d.fishRecordKg = new List<float>();
            foreach (var kv in FishRecords) { d.fishRecordIds.Add(kv.Key); d.fishRecordKg.Add(kv.Value); }
        }

        void RestoreFishing(SaveData d)
        {
            FishRecords.Clear();
            if (d.fishRecordIds == null) return;
            for (int i = 0; i < d.fishRecordIds.Count && i < d.fishRecordKg.Count; i++) FishRecords[d.fishRecordIds[i]] = d.fishRecordKg[i];
        }

        /// <summary>Worms turn up in moist ground (bait for the fishing rod and traps).</summary>
        public void FindWorms(Vector3 at, float chance)
        {
            var b = terrain ? terrain.BiomeAt(at.x, at.z) : Biome.Desert;
            chance *= b switch { Biome.Forest => 1.2f, Biome.Tropical => 1.4f, Biome.Desert => 0.2f, Biome.Nuclear => 0.4f, _ => 0.8f };
            if (Weather.Raining) chance *= 1.5f;
            if (Random.value > chance) return;
            int n = Random.Range(1, 3);
            Inventory.AddItem("bait_worms", n);
            Toast("FOUND " + n + " WORM" + (n > 1 ? "S" : ""));
        }

        public void ShovelDig(Vector3 at)
        {
            var p = new Vector3(at.x, terrain.Height(at.x, at.z), at.z);
            if (terrain.SurfaceAt(p.x, p.z).road > 0.6f) { Toast("TOO HARD TO DIG"); return; }
            float depth = terrain.DugDepth(p.x, p.z);
            if (depth > 2.5f) { Toast("TOO DEEP FOR A SHOVEL"); return; }
            float moved = terrain.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dig, p, 0.45f, 0.1f, 0);
            MadMax.Net.NetSession.Instance?.SendTerraform((byte)DeformableTerrain.TerraOp.Dig, p, 0.45f, 0.1f, 0);
            int units = Mathf.Max(1, Mathf.RoundToInt(moved * Machine.UnitsPerM3));
            Inventory.Add(terrain.SoilAt(p.x, p.z, depth), units);
            Stats.Practice(Skill.Survival, 0.5f);
            Soil(0.6f);
            if (depth < 0.8f) FindWorms(p, 0.22f);
        }

        /// <summary>Pickaxe on rock: a chance of ore that depends on the region.</summary>
        public void MineOre(Vector3 at)
        {
            float chance = 0.18f + Stats.Level(Skill.Salvaging) * 0.02f;
            if (Random.value > chance) return;
            var b = terrain.BiomeAt(at.x, at.z);
            var ore = b switch
            {
                Biome.Desert => Random.value < 0.6f ? ResourceType.Silica : ResourceType.IronOre,
                Biome.Tropical => ResourceType.Bauxite,
                Biome.Nuclear => Random.value < 0.5f ? ResourceType.CopperOre : ResourceType.TinOre,
                Biome.Forest => Random.value < 0.5f ? ResourceType.IronOre : ResourceType.CopperOre,
                _ => ResourceType.IronOre
            };
            Inventory.Add(ore, 1);
            Toast("+1 " + ResourceInfo.Name(ore));
        }

        public bool OwnsPiece(Placeable p) => !p || string.IsNullOrEmpty(p.owner) || p.owner == Stats.name;

        /// <summary>Seed to plant: the selected hotbar item if it is a seed, else the first seed carried.</summary>
        public string FirstSeed(bool sapling)
        {
            string Ok(string id) => id != null && Inventory.GetItem(id) > 0 && (sapling ? id.StartsWith("sapling_") : id.StartsWith("seed_")) ? id : null;
            if (HotbarSelected >= 0 && Ok(Hotbar[HotbarSelected]) != null) return Hotbar[HotbarSelected];
            if (selectedSeed != null && Ok(selectedSeed) != null) return selectedSeed;
            foreach (var kv in Inventory.Items) if (Ok(kv.Key) != null) return kv.Key;
            return null;
        }
        string selectedSeed;

        public void Harvest(CropDef def, Vector3 at) => Harvest(def, at, 0.8f, 1f, 1f);

        /// <summary>Pick a crop: more from fertile soil and healthy plants; seeds saved carry the harvest's make
        /// (Farming skill, soil, parent seeds), and better seeds grow faster.</summary>
        public void Harvest(CropDef def, Vector3 at, float fertility, float health, float seedQuality)
        {
            var got = new List<string>();
            int farm = Stats.Level(Skill.Farming);
            float skill = (1f + farm * 0.08f) * (0.6f + 0.6f * fertility) * Mathf.Max(0.3f, health) * (0.9f + 0.1f * seedQuality);
            foreach (var (item, min, max) in def.yields)
            {
                int n = Mathf.RoundToInt(Random.Range(min, max + 1) * skill);
                if (n <= 0) continue;
                Inventory.AddItem(item, n);
                got.Add(n + " " + ItemCatalog.Name(item));
            }
            if (!def.tree && Random.value < def.seedChance + 0.35f + farm * 0.04f)
            {
                int q = Mathf.Clamp(Mathf.RoundToInt(farm / 4f + fertility + (seedQuality - 1f) * 0.5f + Random.Range(-0.6f, 0.6f)), 0, 2);
                int n = 1 + (Random.value < def.seedChance + farm * 0.03f ? 1 : 0);
                AddMade(def.seed, n, q);
                got.Add(n + " " + QualityNames[q] + " SEEDS");
            }
            if (def.tree && Random.value < def.seedChance) { Inventory.AddItem(def.seed); got.Add("SAPLING"); }
            Stats.Practice(Skill.Farming, 6f);
            Soil(2f);
            Toast("HARVESTED " + string.Join(", ", got));
        }

        /// <summary>Plant a sapling on the ground in front of the player.</summary>
        void PlantSapling(string id)
        {
            var p = Player.transform.position + Player.transform.forward * 1.2f;
            p.y = terrain.Height(p.x, p.z);
            var s = terrain.SurfaceAt(p.x, p.z);
            if (s.road > 0.3f || terrain.WaterDepth(p.x, p.z) > 0f) { Toast("CAN'T PLANT HERE"); return; }
            if (!Inventory.TakeItem(id)) return;
            var tree = FurnitureLibrary.Spawn("tree_planted", Build.Structures, p, Quaternion.Euler(0, Random.Range(0f, 360f), 0), propMaterial);
            if (!tree) return;
            tree.owner = Stats.name;
            var pt = tree.GetComponent<PlantedTree>();
            pt.sapling = id;
            tree.Dirty();
            MadMax.Net.NetSession.Instance?.SendPlaced(tree);
            Stats.Practice(Skill.Farming, 4f);
            Toast("PLANTED " + ItemCatalog.Name(id));
        }

        void UseConsumable(string id)
        {
            if (id == ItemIds.Canteen)
            {
                if (Inventory.TrySpend(ResourceType.Water, 1)) Drink(45f, false);
                else if (Inventory.TrySpend(ResourceType.DirtyWater, 1)) Drink(45f, true);
                else Toast("CANTEEN EMPTY: FILL AT A SINK, BARREL OR LAKE");
                return;
            }
            if (id == "use_repair_kit")
            {
                if (UseRepairKit()) Inventory.TakeItem(id);
                return;
            }
            if (id == O2Bottle) { if (!UseO2Bottle()) Toast("NO O2 BOTTLE"); return; }
            if (id == "med_antivenom" && Inventory.TakeItem(id))
            {
                bool was = Stats.sick > 5f;
                Stats.sick = 0f;
                Toast(was ? "ANTIVENOM: THE FEVER BREAKS" : "ANTIVENOM: NOTHING TO FIGHT, BUT YOU FEEL BRAVE");
                return;
            }
            if (id == "med_antibiotics" && Inventory.TakeItem(id))
            {
                int n = 0;
                foreach (var inj in Stats.injuries) if (inj.infection > 0f) { inj.infection = 0f; n++; }
                Stats.sick = Mathf.Min(Stats.sick, 5f);
                Toast(n > 0 ? "ANTIBIOTICS: INFECTION CLEARED" : "ANTIBIOTICS: YOU FEEL CLEANER INSIDE");
                return;
            }
            if (id == "med_painkillers" && Inventory.TakeItem(id))
            {
                Stats.painkillerUntil = DayNight.TotalDays * 24f + 3f;
                Stats.painkilled = true;
                Toast("PAINKILLERS: THE WOUNDS DULL FOR A WHILE");
                return;
            }
            if (id == "use_fuel_additive")
            {
                var v = Current ? Current : FindNearby(4f);
                var sys = v ? v.GetComponent<VehicleSystems>() : null;
                if (!sys) { Toast("GET IN OR NEXT TO A VEHICLE"); return; }
                if (!Inventory.TakeItem(id)) return;
                sys.additive = sys.fuelCapacity;
                Toast("FUEL ADDITIVE: THIS TANK GOES FURTHER");
                return;
            }
            if (id.StartsWith("bp_"))
            {
                if (UseBlueprint(id)) Inventory.TakeItem(id);
                return;
            }
            if (id == "use_sewing_kit")
            {
                if (UseSewingKit()) Inventory.TakeItem(id);
                return;
            }
            if (id == ItemIds.Sponge)
            {
                int n = MadMax.World.BloodStains.Clean(Player.transform.position, 3f);
                MadMax.Audio.Sfx.Play("splash", Player.transform.position, 0.5f, 1.3f);
                Toast(n > 0 ? $"SCRUBBED {n} STAIN{(n == 1 ? "" : "S")}" : "NOTHING TO CLEAN HERE");
                return;
            }
            if (id == ItemIds.Pills && Inventory.TakeItem(id)) { Stats.sick = 0f; Stats.health = Mathf.Min(Stats.MaxHealth, Stats.health + 10f); Toast("FEELING BETTER"); return; }
            if (id == ItemIds.Fertilizer) Toast("USE ON A GARDEN PLOT [T]");
            if (id.StartsWith("dye_")) DyePiece(id);
        }

        /// <summary>Paint the built piece in front of you with a dye item (pieces you own).</summary>
        void DyePiece(string id)
        {
            if (Current) return;
            var p = PieceInFront(2.6f);
            if (!p) { Toast("LOOK AT A BUILT PIECE TO PAINT IT"); return; }
            if (!OwnsPiece(p)) { Toast("NOT YOUR PIECE"); return; }
            int dye = Dyes.IndexOf(id);
            if (p.dye == dye) { Toast("ALREADY THAT COLOUR"); return; }
            if (!Inventory.TakeItem(id)) return;
            p.SetDye(dye);
            p.Dirty();
            Stats.Practice(Skill.Construction, 1f);
            MadMax.Audio.Sfx.Play("pour", p.transform.position, 0.6f, 1.2f);
            var def = FurnitureLibrary.Get(p.id);
            Toast("PAINTED " + (def != null ? def.name : "PIECE") + " " + ItemCatalog.Name(id).Replace(" DYE", ""));
        }

        /// <summary>Built piece under the crosshair (first / third person) or nearest in front of the player.</summary>
        Placeable PieceInFront(float reach)
        {
            if (cameraRig && cameraRig.pixel && cameraRig.CrosshairView)
            {
                var cam = cameraRig.pixel.transform;
                if (Physics.Raycast(cam.position, cam.forward, out var hit, reach + 4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var pl = hit.collider.GetComponentInParent<Placeable>();
                    if (pl && Vector3.Distance(hit.point, Player.Eye.position) < reach + 0.5f) return pl;
                }
            }
            var eye = Player.Eye.position;
            Placeable best = null; float bd = reach;
            foreach (var pl in Placeable.All)
            {
                if (!pl || !pl.TryGetComponent<Collider>(out var col)) continue;
                bool closest = !(col is MeshCollider mc) || mc.convex;
                var q = closest ? col.ClosestPoint(eye) : col.bounds.ClosestPoint(eye);
                float d = Vector3.Distance(q, eye);
                if (d > bd) continue;
                var toward = q - eye; toward.y = 0f;
                if (toward.sqrMagnitude > 0.04f && Vector3.Dot(toward.normalized, Player.transform.forward) < 0.2f) continue;
                bd = d; best = pl;
            }
            return best;
        }

        /// <summary>Food rots over time (much slower in a powered fridge).</summary>
        void Spoil(float seconds)
        {
            SpoilIn(Inventory, seconds, 1f);
            foreach (var c in Container.All) if (c) SpoilIn(c.inventory, seconds, c.Cooling ? 0.12f : 1f);
        }

        readonly Dictionary<Inventory, Dictionary<string, float>> spoilAcc = new Dictionary<Inventory, Dictionary<string, float>>();
        readonly List<(string, int)> rotting = new List<(string, int)>();

        void SpoilIn(Inventory inv, float seconds, float mult)
        {
            if (!spoilAcc.TryGetValue(inv, out var acc)) spoilAcc[inv] = acc = new Dictionary<string, float>();
            rotting.Clear();
            foreach (var kv in inv.Items)
            {
                var f = FoodLibrary.Get(kv.Key);
                if (f == null || f.spoilMinutes <= 0f || kv.Value <= 0) continue;
                acc.TryGetValue(kv.Key, out float a);
                a += kv.Value * seconds * mult / (f.spoilMinutes * 60f);
                int n = Mathf.FloorToInt(a);
                acc[kv.Key] = a - n;
                if (n > 0) rotting.Add((kv.Key, Mathf.Min(n, kv.Value)));
            }
            foreach (var (id, n) in rotting) { inv.TakeItem(id, n); inv.AddItem("food_rotten", n); }
        }

        // ------------------------------------------------------------------ on-foot use of pieces
        /// <summary>Nearest usable thing in reach and in view (pieces, loot spots). Returns its prompt.</summary>
        string PieceInteraction(bool E, bool T)
        {
            Focused = null;
            var eye = Player.Eye.position;
            float best = 2.2f;
            MonoBehaviour pick = null;
            foreach (var p in Placeable.All) Consider(p, ref pick, ref best, eye);
            foreach (var l in LootSpots) Consider(l, ref pick, ref best, eye);
            foreach (var c in Container.All) if (c && !c.GetComponent<Placeable>()) Consider(c, ref pick, ref best, eye);   // truck beds, hoppers
            foreach (var t in TrailerDeck.All)
            {
                if (!t) continue;
                foreach (var col in t.GetComponentsInChildren<Collider>())
                {
                    if (!col.enabled) continue;                                 // ClosestPoint of a disabled collider is the query point
                    float d = Vector3.Distance(col.ClosestPoint(eye), eye);
                    if (d < best + 0.6f && d < 3f) { best = d; pick = t; break; }
                }
            }
            foreach (var p in GasPump.All) Consider(p, ref pick, ref best, eye);
            foreach (var b in BountyBoard.All) Consider(b, ref pick, ref best, eye);
            foreach (var m in PartFunctions.Interactables) Consider(m, ref pick, ref best, eye);          // water tanks, generators, roof steps
            foreach (var n in MadMax.Npc.Npc.All) if (n.Available) Consider(n, ref pick, ref best, eye);
            if (!pick) return LakeInteraction(E, T);
            Focused = pick as IInteractable;
            // several functions on one piece (a stove cooks and heats): [E] goes to the first that offers it, [T] likewise
            string all = null; IInteractable eUser = null, tUser = null;
            foreach (var it in pick.GetComponents<IInteractable>())
            {
                string prompt = it.Prompt(this);
                if (prompt == null) continue;
                all = all == null ? prompt : all + "  " + prompt;
                if (eUser == null && (prompt.Contains("[E]") || !prompt.Contains("["))) eUser = it;
                if (tUser == null && prompt.Contains("[T]")) tUser = it;
            }
            if (E && eUser != null) eUser.Use(this, false);
            if (T && tUser != null) tUser.Use(this, true);
            if ((E || T) && pick && pick.TryGetComponent<Placeable>(out var pl)) pl.Dirty();
            return all;
        }

        void Consider(MonoBehaviour m, ref MonoBehaviour pick, ref float best, Vector3 eye)
        {
            if (!m || m.GetComponent<IInteractable>() == null) return;
            var col = m.GetComponent<Collider>();
            // ClosestPoint only works on primitive and convex colliders (not CharacterControllers: NPCs)
            bool closest = col && col.enabled && col.gameObject.activeInHierarchy && !(col is CharacterController) && (!(col is MeshCollider mc) || mc.convex);   // a disabled collider's ClosestPoint is the query point itself
            var p = closest ? col.ClosestPoint(eye) : m.transform.position + Vector3.up;
            float d = Vector3.Distance(p, eye);
            if (d > best) return;
            var toward = (p - eye); toward.y = 0f;
            if (toward.sqrMagnitude > 0.04f && Vector3.Dot(toward.normalized, Player.transform.forward) < -0.2f) return;   // behind the player
            if (!Build.Visible(eye, p, col)) return;
            best = d; pick = m;
        }

        public static readonly List<Lootable> LootSpots = new List<Lootable>();

        string LakeInteraction(bool E, bool T)
        {
            var p = Player.transform.position + Player.transform.forward * 0.8f;
            if (terrain.WaterDepth(p.x, p.z) < 0.1f || Player.HeadUnder || MadMax.Building.AirPocket.Contains(Player.transform.position + Vector3.up * 1.5f)) return null;   // not through a diving helmet, nor in a sea base
            bool toxic = terrain.BiomeAt(p.x, p.z) == Biome.Nuclear;
            if (E) { Drink(30f, true); if (toxic) Vitals.Hurt(8f, "TOXIC WATER"); }
            if (T) { Inventory.Add(ResourceType.DirtyWater, 5); Toast("FILLED 5L DIRTY WATER"); }
            return "[E] DRINK" + (toxic ? " (TOXIC)" : " (DIRTY)") + "  [T] FILL 5L";
        }
    }
}
