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
            if (!Rules.survival || Vitals == null || Vitals.Dead) return;
            var s = Stats;
            float rate = Rules.hungerRate * (s.sick > 0f ? 1.5f : 1f);
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
            float sick = f.sickChance + (Stats.hygiene < 30f ? 0.15f : 0f) - Stats.Level(Skill.Survival) * 0.01f;
            if (Random.value < sick) Poison("FOOD POISONING");
            else Toast("ATE " + f.name);
            Stats.Practice(Skill.Survival, 1f);
        }

        public void Drink(float thirst, bool dirty)
        {
            Stats.thirst = Mathf.Min(100f, Stats.thirst + thirst);
            if (dirty && Random.value < 0.3f - Stats.Level(Skill.Survival) * 0.015f) Poison("BAD WATER");
            else Toast(dirty ? "DRANK DIRTY WATER" : "DRANK");
        }

        void Poison(string why) { Stats.sick = Mathf.Max(Stats.sick, 120f); Toast(why + "!"); }

        public void Wash(float amount, string msg) { Stats.hygiene = Mathf.Min(100f, Stats.hygiene + amount); Toast(msg); }

        public void SetSpawn(Vector3 p) { spawnPoint = p; Toast("SPAWN POINT SET"); }

        public void Sleep()
        {
            if (DayNight.Darkness > 0.2f || DayNight.Hours > 20f || DayNight.Hours < 5f)
            {
                float slept = Mathf.Repeat(7f - DayNight.Hours, 24f);
                DayNight.SetHours(7f);
                Stats.hunger = Mathf.Max(5f, Stats.hunger - slept * 2f);
                Stats.thirst = Mathf.Max(5f, Stats.thirst - slept * 3f);
                Stats.health = Mathf.Min(Stats.MaxHealth, Stats.health + Stats.MaxHealth * 0.35f);
                Toast("SLEPT UNTIL MORNING");
                MadMax.Net.NetSession.Instance?.SendWeather();
            }
            else Toast("RESTED");
            Stats.stamina = Stats.MaxStamina;
        }

        /// <summary>A shovel stab into bare ground: a small dig, soil into the pack.</summary>
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

        public void Harvest(CropDef def, Vector3 at)
        {
            var got = new List<string>();
            float skill = 1f + Stats.Level(Skill.Farming) * 0.08f;
            foreach (var (item, min, max) in def.yields)
            {
                int n = Mathf.RoundToInt(Random.Range(min, max + 1) * skill);
                if (n <= 0) continue;
                Inventory.AddItem(item, n);
                got.Add(n + " " + ItemCatalog.Name(item));
            }
            if (!def.tree && Random.value < def.seedChance + 0.35f) { Inventory.AddItem(def.seed, 1 + (Random.value < def.seedChance ? 1 : 0)); got.Add("SEEDS"); }
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
            if (id == ItemIds.Sponge)
            {
                int n = MadMax.World.BloodStains.Clean(Player.transform.position, 3f);
                MadMax.Audio.Sfx.Play("splash", Player.transform.position, 0.5f, 1.3f);
                Toast(n > 0 ? $"SCRUBBED {n} STAIN{(n == 1 ? "" : "S")}" : "NOTHING TO CLEAN HERE");
                return;
            }
            if (id == ItemIds.Pills && Inventory.TakeItem(id)) { Stats.sick = 0f; Stats.health = Mathf.Min(Stats.MaxHealth, Stats.health + 10f); Toast("FEELING BETTER"); return; }
            if (id == ItemIds.Fertilizer) Toast("USE ON A GARDEN PLOT [T]");
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
                    float d = Vector3.Distance(col.ClosestPoint(eye), eye);
                    if (d < best + 0.6f && d < 3f) { best = d; pick = t; break; }
                }
            }
            foreach (var p in GasPump.All) Consider(p, ref pick, ref best, eye);
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
            var p = col ? col.ClosestPoint(eye) : m.transform.position;
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
            if (terrain.WaterDepth(p.x, p.z) < 0.1f) return null;
            bool toxic = terrain.BiomeAt(p.x, p.z) == Biome.Nuclear;
            if (E) { Drink(30f, true); if (toxic) Vitals.Hurt(8f, "TOXIC WATER"); }
            if (T) { Inventory.Add(ResourceType.DirtyWater, 5); Toast("FILLED 5L DIRTY WATER"); }
            return "[E] DRINK" + (toxic ? " (TOXIC)" : " (DIRTY)") + "  [T] FILL 5L";
        }
    }
}
