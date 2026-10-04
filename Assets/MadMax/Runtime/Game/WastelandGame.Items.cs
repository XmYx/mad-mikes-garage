using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Hotbar, item use, encumbrance, reading / VHS / research and loot finds.</summary>
    public partial class WastelandGame
    {
        public const int HotbarSize = 8;
        public string[] Hotbar = new string[HotbarSize];
        public int HotbarSelected { get; private set; } = -1;

        // ---- learning state
        public string LearningId { get; private set; }            // media id or research id
        public float LearningProgress { get; private set; }
        public string LearningName { get; private set; }
        TvSet watchingTv;
        CraftingStation researchBench;
        Vector3 learnStart;

        static bool HotbarItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var c = ItemCatalog.Category(id);
            return c == ItemCategory.Tool || c == ItemCategory.Weapon || c == ItemCategory.Throwable || c == ItemCategory.Media || c == ItemCategory.Kit || c == ItemCategory.Food || c == ItemCategory.Consumable || c == ItemCategory.Seed;
        }

        /// <summary>Drop gone items from the hotbar and slot in newly owned ones.</summary>
        void SyncHotbar()
        {
            for (int i = 0; i < HotbarSize; i++) if (Hotbar[i] != null && Inventory.GetItem(Hotbar[i]) + BeltCount(Hotbar[i]) + BuiltInTool(Hotbar[i]) <= 0) Hotbar[i] = null;   // tools on a belt or a mount arm stay slotted
            var order = new List<string>(ToolLibrary.AllIds);
            foreach (var kv in Inventory.Items) if (kv.Value > 0 && !order.Contains(kv.Key)) order.Add(kv.Key);
            foreach (var id in order)
            {
                if (Inventory.GetItem(id) + BeltCount(id) + BuiltInTool(id) <= 0 || !HotbarItem(id) || System.Array.IndexOf(Hotbar, id) >= 0) continue;
                var cat = ItemCatalog.Category(id);
                if ((cat == ItemCategory.Media && !autoSlotMedia) || cat == ItemCategory.Food || cat == ItemCategory.Seed) continue;   // food and seeds only when assigned
                int free = System.Array.IndexOf(Hotbar, null);
                if (free < 0) break;
                Hotbar[free] = id;
            }
        }
        bool autoSlotMedia = true;

        public void UpdateHotbarNow() => SyncHotbar();

        public void AssignHotbar(int slot, string id)
        {
            if (slot < 0 || slot >= HotbarSize) return;
            int old = System.Array.IndexOf(Hotbar, id);
            if (old >= 0) Hotbar[old] = Hotbar[slot];
            Hotbar[slot] = id;
            SyncHotbar();                                   // a displaced tool finds another free slot
            Toast((slot + 1) + ": " + ItemCatalog.Name(id));
        }

        void UpdateHotbar(Keyboard kb, Mouse mouse)
        {
            if (Current || Build.Active) return;
            int pick = -1;
            if (kb != null) for (int i = 0; i < HotbarSize; i++) if (kb[Key.Digit1 + i].wasPressedThisFrame) pick = i;
            if (pick >= 0 && Hotbar[pick] != null) UseItem(Hotbar[pick]);
            int sel = Player.Tool ? System.Array.IndexOf(Hotbar, Player.Tool.id) : -1;
            HotbarSelected = sel;
            // scroll cycles tools while the mouse is not zooming (hold ALT to scroll the hotbar)
            if (mouse != null && kb != null && kb.leftAltKey.isPressed)
            {
                float s = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 0.01f)
                {
                    int dir = s > 0 ? -1 : 1;
                    for (int k = 1; k <= HotbarSize; k++)
                    {
                        int j = ((sel < 0 ? 0 : sel) + dir * k + HotbarSize * 2) % HotbarSize;
                        var id = Hotbar[j];
                        if (id != null && (ItemCatalog.Category(id) == ItemCategory.Tool || ItemCatalog.Category(id) == ItemCategory.Weapon)) { UseItem(id); break; }
                    }
                }
            }
        }

        /// <summary>Equip a tool, read a book, play a tape, or place a kit.</summary>
        public void UseItem(string id)
        {
            if (Inventory.GetItem(id) <= 0 && BuiltInTool(id) <= 0 && !DrawFromBelt(id)) return;   // a tool belt: straight into the hand; a mount arm has its own
            switch (ItemCatalog.Category(id))
            {
                case ItemCategory.Tool:
                case ItemCategory.Weapon:
                    if (Current) return;
                    if (!CanUse(id) && !(Player.Tool && Player.Tool.id == id)) { Toast("NO HAND TO HOLD IT"); return; }
                    if (Player.Tool && Player.Tool.id == id) Player.Equip(null);
                    else Player.Equip(ToolLibrary.Create(id, propMaterial));
                    break;
                case ItemCategory.Media: StartMedia(id); break;
                case ItemCategory.Ammo:
                    if (id.StartsWith("bait_")) { FishingRodTool.PreferredBait = id; Toast("BAIT ON THE HOOK NEXT: " + FishLibrary.BaitName(id)); }
                    break;
                case ItemCategory.Throwable: Throw(id); break;
                case ItemCategory.Food: Eat(id); break;
                case ItemCategory.Consumable: UseConsumable(id); break;
                case ItemCategory.Seed:
                    if (id.StartsWith("sapling_")) PlantSapling(id);
                    else { selectedSeed = id; Toast(ItemCatalog.Name(id) + " READY: USE A GARDEN PLOT"); }
                    break;
                case ItemCategory.Kit:
                    foreach (var d in FurnitureLibrary.All) if (d.kit == id) { Build.Select(d.id); if (!Build.Active) Build.SetActive(true); }
                    break;
                case ItemCategory.Clothing: Menus.Open(MenuSystem.Page.Character); break;
                case ItemCategory.Other:
                    if (id.StartsWith("animal_")) MadMax.Animals.AnimalDirector.Instance?.Release(this, id);                // a chick, kid, calf, piglet or pup
                    break;
            }
        }

        void Throw(string id)
        {
            if (Current || !Vitals.Spend(6f)) return;
            if (!Inventory.TakeItem(id)) return;
            var t = Player.transform;
            float power = 10f + Stats.Attribute(Attr.Strength) * 0.6f;
            var p = t.position + Vector3.up * 1.6f + t.forward * 0.5f;
            var v = t.forward * power + Vector3.up * (4f + power * 0.15f) + Player.Velocity;
            var net = MadMax.Net.NetSession.Instance;
            bool authority = !(net && net.IsClient);
            SpawnThrown(id, p, v, authority);
            net?.SendThrow(id, p, v);
            Stats.Practice(Skill.Firearms, 1f);
        }

        /// <summary>A thrown bottle. Only the authority's bottle starts the fire (clients see a visual copy).</summary>
        public void SpawnThrown(string id, Vector3 p, Vector3 v, bool authority)
        {
            bool dynamite = id == "throw_dynamite", pipe = id == "throw_pipebomb", smoke = id == "throw_smoke", rock = id == "throw_rock";
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = dynamite ? "Dynamite" : pipe ? "PipeBomb" : smoke ? "SmokeBomb" : rock ? "Rock" : "Molotov";
            go.transform.localScale = dynamite ? new Vector3(0.07f, 0.24f, 0.07f) : pipe ? new Vector3(0.09f, 0.26f, 0.09f) : smoke ? new Vector3(0.1f, 0.16f, 0.1f) : rock ? Vector3.one * 0.1f : new Vector3(0.09f, 0.22f, 0.09f);
            go.transform.position = p;
            go.GetComponent<MeshRenderer>().sharedMaterial = propMaterial;
            var col = go.GetComponent<Collider>();
            if (Player && Player.TryGetComponent<Collider>(out var pc)) Physics.IgnoreCollision(col, pc);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.8f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = v;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            if (rock) { var rk = go.AddComponent<MadMax.World.Molotov>(); rk.rock = true; rk.authority = authority; go.GetComponent<MeshRenderer>().material.color = new Color(0.45f, 0.42f, 0.4f); return; }
            if (smoke)
            {
                var sb = go.AddComponent<MadMax.World.Explosive>();
                sb.authority = authority; sb.smoke = true; sb.fuse = 1.8f;
                go.GetComponent<MeshRenderer>().material.color = new Color(0.35f, 0.4f, 0.3f);
                var wk = new GameObject("Wick"); wk.transform.SetParent(go.transform, false); wk.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                return;
            }
            if (dynamite || pipe)
            {
                // a lit fuse, then the bang: dynamite breaks rock and digs craters, a pipe bomb shreds what stands near
                var ex = go.AddComponent<MadMax.World.Explosive>();
                ex.authority = authority; ex.source = Player ? Player.gameObject : null;
                ex.fuse = dynamite ? 4f : 3f; ex.radius = dynamite ? 3.2f : 2.2f; ex.power = dynamite ? 8f : 5f; ex.crater = dynamite ? 1.2f : 0.3f;
                var r = go.GetComponent<MeshRenderer>();
                r.material.color = dynamite ? new Color(0.75f, 0.12f, 0.08f) : new Color(0.4f, 0.4f, 0.42f);
                var wick = new GameObject("Wick");
                wick.transform.SetParent(go.transform, false); wick.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                return;
            }
            go.AddComponent<MadMax.World.Molotov>().authority = authority;
            var fire = new GameObject("Wick").AddComponent<Light>();
            fire.transform.SetParent(go.transform, false); fire.type = LightType.Point; fire.range = 3f; fire.intensity = 1.5f; fire.color = new Color(1f, 0.6f, 0.2f);
        }

        /// <summary>A vehicle for an NPC driver (traders, raiders). Registered like any other; not saved while AI-driven.</summary>
        public VehicleDriver SpawnAiVehicle(string design, Vector3 p, Quaternion r)
        {
            var prefab = PrefabFor(design);
            if (!prefab) return null;
            var v = Instantiate(prefab, p, r).GetComponent<VehicleDriver>();
            v.aiDriven = true;
            Register(v, null);
            return v;
        }

        /// <summary>Airfields whose hangar machine has been placed (it is a saved vehicle after that).</summary>
        public readonly HashSet<string> FoundAircraft = new HashSet<string>();
        /// <summary>Sites seen from the air (roadmap 25 scouting).</summary>
        public readonly HashSet<string> Scouted = new HashSet<string>();

        /// <summary>A vehicle found in the world (a hangar's flying machine): free to take, nearly dry tanks.</summary>
        public VehicleDriver SpawnFound(string design, Vector3 p, Quaternion r)
        {
            var prefab = PrefabFor(design);
            if (!prefab) return null;
            var v = Instantiate(prefab, p, r).GetComponent<VehicleDriver>();
            if (v.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = sys.fuelCapacity * 0.15f;
            Register(v, null);
            return v;
        }

        /// <summary>A vehicle crafted on another peer.</summary>
        public void SpawnVehicleRemote(string design, ushort netId, Vector3 p, Quaternion r)
        {
            var prefab = PrefabFor(design);
            if (!prefab) return;
            var v = Instantiate(prefab, p, r).GetComponent<VehicleDriver>();
            v.netId = netId;
            Register(v, fleet);
        }

        /// <summary>Everything carried, kg: the pack and what is in the worn bags.</summary>
        public float CarriedWeight => ItemCatalog.TotalWeight(Inventory) + WornBagsWeight;

        /// <summary>Overloaded above the carry capacity, counted as the back feels it (<see cref="EffectiveLoad"/>).</summary>
        void UpdateEncumbrance() => Player.Encumbered = EffectiveLoad > Stats.CarryCapacity;

        // ---------------------------------------------------------------- media
        void StartMedia(string id)
        {
            var m = MediaLibrary.Get(id);
            if (m == null) return;
            if (Current) { Toast("NOT WHILE DRIVING"); return; }
            if (m.kind == MediaKind.Tape)
            {
                var tv = TvSet.Nearest(Player.transform.position, 3f);
                if (!tv) { Toast("PLAY TAPES ON A TV"); return; }
                watchingTv = tv; tv.Play(id);
            }
            BeginLearning(id, m.name);
        }

        void BeginLearning(string id, string name)
        {
            if (LearningId == id) { StopLearning("STOPPED"); return; }
            if (LearningId != null) StopLearning(null);
            LearningId = id; LearningName = name; LearningProgress = 0f;
            learnStart = Player.transform.position;
            Toast((id.StartsWith("r_") ? "RESEARCHING " : id.StartsWith("vhs_") ? "WATCHING " : "READING ") + name);
        }

        void StopLearning(string why)
        {
            if (watchingTv) watchingTv.Stop();
            watchingTv = null; researchBench = null;
            LearningId = null;
            if (why != null) Toast(why);
        }

        public void StartResearch(ResearchDef r, CraftingStation bench)
        {
            if (Stats.Knows(r.grants)) { Toast("ALREADY KNOWN"); return; }
            if (Stats.Level(r.skill) < r.minLevel) { Toast(CharacterStats.SkillNames[(int)r.skill] + " " + r.minLevel + " NEEDED"); return; }
            foreach (var (t, n) in r.cost) if (Inventory.Get(t) < n) { Toast("NEED " + n + " " + ResourceInfo.Name(t)); return; }
            foreach (var (t, n) in r.cost) Inventory.TrySpend(t, n);
            BeginLearning(r.id, r.name);
            researchBench = bench;
        }

        public bool CanResearch(ResearchDef r)
        {
            if (Stats.Knows(r.grants) || Stats.Level(r.skill) < r.minLevel) return false;
            foreach (var (t, n) in r.cost) if (Inventory.Get(t) < n) return false;
            return true;
        }

        void UpdateLearning()
        {
            if (LearningId == null) return;
            if (Current) { StopLearning("INTERRUPTED"); return; }
            var pos = Player.transform.position;
            float duration, rate;
            ResearchDef research = null; MediaDef media = null;
            if (LearningId.StartsWith("r_"))
            {
                foreach (var r in MediaLibrary.Research) if (r.id == LearningId) research = r;
                if (research == null || !researchBench) { StopLearning(null); return; }
                if (Vector3.Distance(pos, researchBench.transform.position) > 3f) return;          // paused away from the bench
                duration = research.seconds; rate = Stats.ReadingSpeed * MadMax.Building.Bookshelf.ReadingBonus(pos);
            }
            else
            {
                media = MediaLibrary.Get(LearningId);
                if (media == null) { StopLearning(null); return; }
                if (media.kind == MediaKind.Tape)
                {
                    if (!watchingTv || Vector3.Distance(pos, watchingTv.transform.position) > 6f) { StopLearning("TAPE STOPPED"); return; }
                    rate = Player.Sitting ? 1.15f : 1f;
                }
                else
                {
                    if ((pos - learnStart).sqrMagnitude > 0.8f * 0.8f) { StopLearning("STOPPED READING"); return; }
                    // a good chair and a full bookshelf make for faster study
                    rate = Stats.ReadingSpeed * (Player.Sitting && Player.SeatedOn ? Player.SeatedOn.reading : 1f) * MadMax.Building.Bookshelf.ReadingBonus(pos);
                }
                duration = media.seconds;
            }
            LearningProgress += Time.deltaTime * rate / duration;
            if (LearningProgress < 1f) return;

            if (research != null) Stats.Learn(research.grants);
            else
            {
                bool first = !Stats.consumed.Contains(media.id);
                Stats.Practice(media.skill, media.xp * (first ? 1f : 0.15f));
                if (first) Stats.consumed.Add(media.id);
                foreach (var k in media.teaches) Stats.Learn(k);
            }
            StopLearning("FINISHED " + LearningName);
        }

        // ---------------------------------------------------------------- loot
        static readonly string[] LootMedia = { "book_mechanics_1", "book_mechanics_2", "book_gunsmith", "book_builder", "book_scrapper", "book_hotwiring", "book_chemistry", "vhs_driving", "vhs_survival", "vhs_karate", "vhs_demolition" };

        /// <summary>Chance to turn up a book or tape while salvaging (Perception and Salvaging help).</summary>
        public void RollLoot(float baseChance)
        {
            float chance = baseChance * (0.6f + Stats.Attribute(Attr.Perception) * 0.08f) * (1f + Stats.Level(Skill.Salvaging) * 0.1f);
            if (Random.value > chance) return;
            var id = LootMedia[Random.Range(0, LootMedia.Length)];
            if (Random.value < 0.35f) id = Random.value < 0.5f ? ItemIds.Shells : ItemIds.Cutter;
            if (id == ItemIds.Cutter && Inventory.GetItem(id) > 0) id = LootMedia[Random.Range(0, LootMedia.Length)];
            using (Inventory.Source("FOUND")) Inventory.AddItem(id, id == ItemIds.Shells ? 6 : 1);
            Toast("FOUND: " + ItemCatalog.Name(id));
        }
    }
}
