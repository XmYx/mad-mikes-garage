using System.Collections.Generic;
using System.Globalization;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Bags and the back (roadmap "carrying"). A worn bag (<see cref="BagLibrary"/>) gets its own
    /// <see cref="Container"/> (<see cref="Container.worn"/>, listed in <see cref="WornStorage"/>) on the player; taken off,
    /// dropped or placed, its contents go into the bag's item key (a full bag in the pack, a container or on the ground;
    /// [T] on a bag on the ground looks inside). Contents weigh on the back by the bag's <see cref="BagSpec.load"/>
    /// (<see cref="EffectiveLoad"/>): over the comfortable load (<see cref="ComfortLoad"/>: strength, Athletics, a back
    /// brace) the back strains — faster running, swimming, on jumps and climbs, with one-sided loads — and past the
    /// limit it gives out (a STRAINED BACK on the torso: slower, no running or jumping, less stamina) until rest, sleep,
    /// painkillers and time mend it. Tool and gun belts hand their tools straight to the hotbar; luggage fills the hands
    /// ([Q] sets it down). Saved in <c>SaveData.blockBags</c>.</summary>
    public partial class WastelandGame
    {
        // ---- tuning: back strain per second at 100 % over the comfortable load while walking (running 2.5×)
        public const float StrainRate = 0.012f, StrainWarn = 0.5f, StrainAlarm = 0.8f;
        /// <summary>Share of the carry capacity that is comfortable; one-sided kg carried before it counts.</summary>
        public const float ComfortShare = 0.7f, SideFree = 10f;

        readonly Dictionary<string, Container> wornBags = new Dictionary<string, Container>();
        readonly Dictionary<string, string> bagPending = new Dictionary<string, string>();      // garment id → full key to unpack when worn
        readonly HashSet<string> drawnFromBelt = new HashSet<string>();
        readonly List<string> bagScratch = new List<string>();
        string wearKeyHint;
        bool bagsMigrate, bagsLoaded, groundBagLoading;
        float strain, lastVy, handsToastT;
        bool wasTraversing;
        int strainWarned, backTagPct = -1;
        string backTag;
        WorldItem groundItem; Container groundBox; string groundKey;
        static readonly CultureInfo BagsInv = CultureInfo.InvariantCulture;

        /// <summary>0..1 how close the back is to giving out.</summary>
        public float BackStrain => strain;
        /// <summary>Over the comfortable load this frame (0 = none; one-sided kg included).</summary>
        public float BackExcess { get; private set; }
        /// <summary>The worn bag's storage for a garment id (null when not worn).</summary>
        public Container WornBag(string defId) => wornBags.TryGetValue(defId, out var c) && c ? c : null;
        /// <summary>Test / debug: set the back strain directly.</summary>
        public void SetBackStrain(float v) { strain = Mathf.Clamp01(v); strainWarned = strain >= StrainAlarm ? 2 : strain >= StrainWarn ? 1 : 0; }

        // ------------------------------------------------------------------ load
        /// <summary>Kg in the worn bags.</summary>
        public float WornBagsWeight { get { float w = 0f; foreach (var kv in wornBags) if (kv.Value) w += kv.Value.Weight; return w; } }

        /// <summary>What the back feels, kg: the pack, plus each worn bag's contents × its load factor (a good hip belt
        /// carries 40 % of it, a bag in one hand pulls harder) × its make.</summary>
        public float EffectiveLoad
        {
            get
            {
                float w = ItemCatalog.TotalWeight(Inventory);
                foreach (var kv in wornBags)
                {
                    if (!kv.Value) continue;
                    var spec = BagLibrary.Get(kv.Key);
                    w += kv.Value.Weight * (spec != null ? spec.load : 1f) * Mathf.Lerp(1.08f, 0.92f, QualityOf(BagLibrary.Plain(kv.Key)) * 0.5f);
                }
                return w;
            }
        }

        /// <summary>Kg carried without strain: 70 % of the carry capacity, +3 % per Athletics level, +15 % in a back brace.</summary>
        public float ComfortLoad => Stats == null ? 50f : Stats.CarryCapacity * ComfortShare * (1f + Stats.Level(Skill.Athletics) * 0.03f) * (Wearing(BagLibrary.Brace) ? 1.15f : 1f);

        /// <summary>Kg hanging off one side (shoulder bags, hand luggage).</summary>
        public float OneSidedLoad { get { float s = 0f; foreach (var kv in wornBags) { var spec = BagLibrary.Get(kv.Key); if (kv.Value && spec != null) s += spec.side * (kv.Value.Weight + spec.weight); } return s; } }

        /// <summary>A strained back (0..1 how badly; painkillers and a brace dull it).</summary>
        public float BackHurt
        {
            get
            {
                if (Stats == null || Stats.injuries == null) return 0f;
                float m = 0f;
                foreach (var i in Stats.injuries) if (i.type == Wound.Strain) m = Mathf.Max(m, i.severity);
                if (m <= 0f) return 0f;
                if (Stats.painkilled) m *= 0.45f;
                if (Wearing(BagLibrary.Brace)) m *= 0.6f;
                return m;
            }
        }

        /// <summary>Walking speed from the back and the luggage in hand.</summary>
        public float BagSpeed
        {
            get
            {
                float m = 1f - 0.3f * BackHurt;
                foreach (var kv in wornBags) { var spec = BagLibrary.Get(kv.Key); if (kv.Value && spec != null) m *= spec.speed; }
                return m;
            }
        }

        /// <summary>HUD tag before the back gives out ("BACK 62%"), null when fine.</summary>
        public string BackTag
        {
            get
            {
                int pct = strain >= 0.3f || (BackExcess > 0f && strain >= 0.05f) ? Mathf.RoundToInt(strain * 100f) : -1;
                if (pct != backTagPct) { backTagPct = pct; backTag = pct < 0 ? null : "BACK " + pct + "%"; }
                return backTag;
            }
        }

        /// <summary>Health page: "42 / 49 KG, BACK 12%".</summary>
        public string LoadLine => EffectiveLoad.ToString("0", BagsInv) + " / " + ComfortLoad.ToString("0", BagsInv) + " KG, BACK " + Mathf.RoundToInt(strain * 100f) + "%";

        // ------------------------------------------------------------------ the tick
        partial void BagsNewGame()
        {
            ClearWornBags(); bagPending.Clear(); drawnFromBelt.Clear();
            strain = 0f; strainWarned = 0; bagsMigrate = false;
        }

        partial void BagsUpdate()
        {
            if (!Player || !Player.Rig) return;
            float dt = Time.deltaTime;
            SyncWornBags();
            if (bagsLoaded) SettleLoadedBags();
            if (bagsMigrate && wornBags.Count > 0) { bagsMigrate = false; MigrateIntoBags(); }
            TickBelt();
            TickLuggage();
            if (dt > 0f && !(Vitals != null && Vitals.Dead)) { TickBagWear(dt); TickBackStrain(dt); }
            if (groundBox && groundItem && groundItem.key != groundKey) LoadGroundBag();      // changed elsewhere (online)
        }

        /// <summary>Storage for every bag on the body, none for the rest (any path that puts a bag on or takes it off).</summary>
        void SyncWornBags()
        {
            var outfit = Player.Rig.outfit;
            if (wornBags.Count > 0)
            {
                bagScratch.Clear();
                foreach (var kv in wornBags) if (!kv.Value || !outfit.Contains(kv.Key)) bagScratch.Add(kv.Key);
                foreach (var id in bagScratch) TakeOffBag(id);
            }
            for (int i = 0; i < outfit.Count; i++)
            {
                var id = outfit[i];
                if (wornBags.ContainsKey(id)) continue;
                var spec = BagLibrary.Get(id);
                if (spec != null) PutOnBag(id, spec);
            }
        }

        void PutOnBag(string id, BagSpec spec)
        {
            string plain = "cloth_" + id, key = null;
            if (bagPending.TryGetValue(id, out var pk)) { key = pk; bagPending.Remove(id); }
            else if (Inventory.GetItem(plain) <= 0)
            {
                // a full bag from the pack: it comes out as the bag on your back, its contents into its storage
                string pick = wearKeyHint != null && BagLibrary.DefId(wearKeyHint) == id && Inventory.GetItem(wearKeyHint) > 0 ? wearKeyHint : null;
                if (pick == null) foreach (var kv in Inventory.Items) if (kv.Value > 0 && BagLibrary.IsFilled(kv.Key) && BagLibrary.DefId(kv.Key) == id) { pick = kv.Key; break; }
                if (pick != null) { using (Inventory.Source("UNPACKED")) { Inventory.TakeItem(pick); Inventory.AddItem(plain); } key = pick; }
            }
            wearKeyHint = null;
            var go = new GameObject("Bag " + id);
            go.transform.SetParent(Player.transform, false);
            go.transform.localPosition = spec.hands ? new Vector3(0.3f, 0.6f, 0f) : new Vector3(0f, 1.1f, -0.2f);
            var c = go.AddComponent<Container>();
            c.worn = true;
            var cd = ClothingLibrary.Get(id);
            BagLibrary.Setup(c, spec, cd != null ? cd.name : id.ToUpperInvariant());
            if (key != null) BagLibrary.Contents(key, c.inventory);
            wornBags[id] = c;
            WornStorage.Add(c);
        }

        /// <summary>A bag off the body: its contents into its item key in the pack, or — when the bag itself already left
        /// the pack (sold, stored, torn apart) — into the pack. Returns the bag's key now in the pack.</summary>
        string TakeOffBag(string id)
        {
            wornBags.TryGetValue(id, out var c);
            wornBags.Remove(id);
            WornStorage.Remove(c);
            string key = "cloth_" + id;
            if (!c) return key;
            if (!BagLibrary.Empty(c.inventory))
            {
                if (Inventory.GetItem(key) > 0)
                {
                    var full = BagLibrary.Key(id, c.inventory);
                    using (Inventory.Source("PACKED")) { Inventory.TakeItem(key); Inventory.AddItem(full); }
                    key = full;
                }
                else
                {
                    using (Inventory.Source("FROM THE BAG")) MoveAll(c.inventory, Inventory);
                    Toast("YOUR " + c.title + " IS GONE: WHAT WAS IN IT IS IN YOUR PACK");
                }
            }
            c.gameObject.SetActive(false);
            Destroy(c.gameObject);
            return key;
        }

        void ClearWornBags()
        {
            foreach (var kv in wornBags) if (kv.Value) { WornStorage.Remove(kv.Value); kv.Value.gameObject.SetActive(false); Destroy(kv.Value.gameObject); }
            wornBags.Clear();
        }

        static void MoveAll(Inventory from, Inventory to)
        {
            for (int t = 1; t < ResourceInfo.Count; t++) { int n = from.Get((ResourceType)t); if (n > 0 && from.TrySpend((ResourceType)t, n)) to.Add((ResourceType)t, n); }
            foreach (var kv in new List<KeyValuePair<string, int>>(from.Items)) if (kv.Value > 0 && from.TakeItem(kv.Key, kv.Value)) to.AddItem(kv.Key, kv.Value);
        }

        /// <summary>Called as an item leaves the pack (drop, place): when it is the worn bag itself, it comes off with its
        /// contents and the full bag's key leaves instead (one of it).</summary>
        string BagLeaving(string key, ref int n)
        {
            if (key == null || !key.StartsWith("cloth_") || BagLibrary.IsFilled(key) || !Player) return key;
            var id = BagLibrary.DefId(key);
            if (!wornBags.TryGetValue(id, out var c) || !c) return key;
            if (Inventory.GetItem(key) - n >= 1) return key;                                        // a spare goes; the worn one stays on
            if (Player.Rig.outfit.Remove(id)) Player.RebuildBody();
            var k = TakeOffBag(id);
            if (k != key) n = 1;
            return k;
        }

        /// <summary>Put a bag on from the pack (a full one too: its contents unpack into its storage).</summary>
        public bool WearBag(string key)
        {
            var cd = ClothingLibrary.Get(key);
            if (cd == null || !Player || Inventory.GetItem(key) <= 0) return false;
            var rig = Player.Rig;
            foreach (var id in new List<string>(rig.outfit)) { var d = ClothingLibrary.Get(id); if (d != null && d.slot == cd.slot) rig.outfit.Remove(id); }
            SyncWornBags();                                                                          // the one it replaces packs up first
            wearKeyHint = key;
            rig.outfit.Add(cd.id);
            Player.RebuildBody();
            SyncWornBags();
            Toast("WEARING " + cd.name);
            return true;
        }

        /// <summary>A bag of this garment id with something in it is in the pack.</summary>
        public bool HasFullBag(string defId)
        {
            foreach (var kv in Inventory.Items) if (kv.Value > 0 && BagLibrary.IsFilled(kv.Key) && BagLibrary.DefId(kv.Key) == defId) return true;
            return false;
        }

        /// <summary>Take a worn bag off into the pack (its contents stay in it).</summary>
        public bool TakeOffBagToPack(string defId)
        {
            if (!Player || !Player.Rig.outfit.Remove(defId)) return false;
            Player.RebuildBody();
            SyncWornBags();
            return true;
        }

        /// <summary>Move up to <paramref name="n"/> of an item id / "res:N" from the pack into a worn bag (what fits).</summary>
        public int StowInBag(Container bag, string key, int n)
        {
            if (!bag) return 0;
            n = bag.Fits(key, Mathf.Min(n, PackCount(key)));
            if (n <= 0) return 0;
            using (Inventory.Source(null, "INTO THE BAG"))
            {
                if (IsResKey(key, out var t)) { if (!Inventory.TrySpend(t, n)) return 0; bag.inventory.Add(t, n); }
                else { if (!Inventory.TakeItem(key, n)) return 0; bag.inventory.AddItem(key, n); }
            }
            LetGoOf(key);
            return n;
        }

        /// <summary>Move up to <paramref name="n"/> from a bag back into the pack.</summary>
        public int TakeFromBag(Container bag, string key, int n)
        {
            if (!bag) return 0;
            using (Inventory.Source("FROM THE BAG"))
            {
                if (IsResKey(key, out var t)) { n = Mathf.Min(n, bag.inventory.Get(t)); if (n <= 0 || !bag.inventory.TrySpend(t, n)) return 0; Inventory.Add(t, n); }
                else { n = Mathf.Min(n, bag.inventory.GetItem(key)); if (n <= 0 || !bag.inventory.TakeItem(key, n)) return 0; Inventory.AddItem(key, n); }
            }
            if (key.StartsWith("tool_")) UpdateHotbarNow();
            return n;
        }

        /// <summary>Old saves: packs used to add their capacity to the pack; what no longer fits goes into the worn bags.</summary>
        void MigrateIntoBags()
        {
            if (EffectiveLoad <= Stats.CarryCapacity) return;
            var keep = new HashSet<string>();
            foreach (var h in Hotbar) if (h != null) keep.Add(h);
            foreach (var o in Player.Rig.outfit) keep.Add("cloth_" + o);
            int moved = 0;
            foreach (var kv in wornBags)
            {
                var bag = kv.Value;
                if (!bag) continue;
                for (int t = 1; t < ResourceInfo.Count && EffectiveLoad > Stats.CarryCapacity; t++)
                {
                    var key = "res:" + t;
                    int n = bag.Fits(key, Inventory.Get((ResourceType)t));
                    if (n > 0) moved += StowInBag(bag, key, n);
                }
                foreach (var it in new List<KeyValuePair<string, int>>(Inventory.Items))
                {
                    if (EffectiveLoad <= Stats.CarryCapacity) break;
                    if (it.Value <= 0 || keep.Contains(it.Key) || (Player.Tool && Player.Tool.id == it.Key)) continue;
                    int n = bag.Fits(it.Key, it.Value);
                    if (n > 0) moved += StowInBag(bag, it.Key, n);
                }
            }
            if (moved > 0) Toast("PACKS HOLD THEIR OWN LOAD NOW: SOME OF YOUR GEAR WENT INTO YOUR BAG");
        }

        // ------------------------------------------------------------------ belts: tools straight into the hand
        /// <summary>How many of a tool hang on the worn tool / gun belts.</summary>
        public int BeltCount(string id)
        {
            if (id == null || wornBags.Count == 0) return 0;
            int n = 0;
            foreach (var kv in wornBags) { var spec = BagLibrary.Get(kv.Key); if (kv.Value && spec != null && spec.quick) n += kv.Value.inventory.GetItem(id); }
            return n;
        }

        /// <summary>A hotbar pick of a tool that is on a belt: it comes off the belt into the hand (back on it when put away).</summary>
        bool DrawFromBelt(string id)
        {
            foreach (var kv in wornBags)
            {
                var spec = BagLibrary.Get(kv.Key);
                if (!kv.Value || spec == null || !spec.quick || kv.Value.inventory.GetItem(id) <= 0) continue;
                using (Inventory.Source("DRAWN", "DRAWN")) { kv.Value.inventory.TakeItem(id); Inventory.AddItem(id); }
                drawnFromBelt.Add(id);
                return true;
            }
            return false;
        }

        void TickBelt()
        {
            if (drawnFromBelt.Count == 0) return;
            bagScratch.Clear(); bagScratch.AddRange(drawnFromBelt);
            foreach (var id in bagScratch)
            {
                if (Player.Tool && Player.Tool.id == id) continue;
                drawnFromBelt.Remove(id);
                if (Inventory.GetItem(id) <= 0) continue;                                            // dropped, thrown, broken
                foreach (var kv in wornBags)
                {
                    var spec = BagLibrary.Get(kv.Key);
                    if (!kv.Value || spec == null || !spec.quick || kv.Value.Fits(id, 1) <= 0) continue;
                    using (Inventory.Source("ON THE BELT", "ON THE BELT")) { Inventory.TakeItem(id); kv.Value.inventory.AddItem(id); }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ luggage: both hands
        string HandBag() { foreach (var kv in wornBags) { var spec = BagLibrary.Get(kv.Key); if (kv.Value && spec != null && spec.hands) return kv.Key; } return null; }

        void TickLuggage()
        {
            var hand = HandBag();
            if (hand == null) return;
            var name = ClothingLibrary.Get(hand)?.name ?? "BAG";
            if (Player.Tool)
            {
                Player.Equip(null);
                if (Time.time > handsToastT) { handsToastT = Time.time + 3f; Toast("HANDS FULL: [" + Controls.Name(Controls.Act.Drop) + "] SETS THE " + name + " DOWN"); }
            }
            if (Player.Carried) { DropFromPack("cloth_" + hand, 1); Toast("YOU SET THE " + name + " DOWN TO CARRY THE PART"); return; }
            if (!ExternalInput && !Current && !Player.Sitting && !PlacingItem && !(Build && Build.Active) && !Menus.IsOpen && Controls.Down(Controls.Act.Drop))
                DropFromPack("cloth_" + hand, 1);
        }

        // ------------------------------------------------------------------ wear of a bag that is used (handcrafted)
        void TickBagWear(float dt)
        {
            if (wornBags.Count == 0 || Current || Player.Velocity.sqrMagnitude < 0.25f) return;
            foreach (var kv in wornBags)
            {
                var spec = BagLibrary.Get(kv.Key);
                if (!kv.Value || spec == null || spec.wear <= 0f) continue;
                float fill = kv.Value.Weight / Mathf.Max(1f, spec.capacity);
                if (fill <= 0f) continue;
                ClothWear.TryGetValue(kv.Key, out var w);
                float before = w;
                w += dt / 3600f * spec.wear * fill * (Player.run ? 1.5f : 1f) * QualityWear(BagLibrary.Plain(kv.Key));
                var cd = ClothingLibrary.Get(kv.Key);
                if (w >= 1f && cd != null)
                {
                    kv.Value.Spill();                                                                // the straps give way: it all falls out
                    Toast("THE STRAPS OF YOUR " + cd.name + " GAVE WAY: EVERYTHING FELL OUT");
                    FallApart(cd);
                    Player.RebuildBody();
                    return;                                                                          // the sync takes it off next frame
                }
                ClothWear[kv.Key] = w;
                if (before < 0.65f && w >= 0.65f && cd != null) { Toast("YOUR " + cd.name + " IS COMING APART (MEND IT)"); Player.RebuildBody(); }
            }
        }

        // ------------------------------------------------------------------ the back
        Injury BackInjury() { foreach (var i in Stats.injuries) if (i.type == Wound.Strain) return i; return null; }

        void TickBackStrain(float dt)
        {
            var inj = BackInjury();
            bool resting = Current || Player.Sitting || Player.SeatedIn;
            float eff = EffectiveLoad, comfort = Mathf.Max(10f, ComfortLoad);
            float excess = Mathf.Max(0f, eff / comfort - 1f) + Mathf.Max(0f, OneSidedLoad - SideFree) / comfort;
            BackExcess = excess;
            var v = Player.Velocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            float activity = resting ? 0f : Player.Swimming ? 1.5f : speed > 3.6f ? 2.5f : speed > 0.4f ? 1f : 0.35f;
            float ease = Wearing(BagLibrary.Brace) ? 0.8f : 1f;
            float rules = GameRules.Current != null ? GameRules.Current.DamageTaken : 1f;
            // jumps and climbs jolt a loaded back
            bool trav = Player.Traversing;
            bool jumped = !trav && !resting && v.y > 3.2f && lastVy <= 3.2f;                     // a take-off (not a run up a slope)
            bool climbed = trav && !wasTraversing;
            lastVy = v.y; wasTraversing = trav;
            if ((jumped || climbed) && eff > comfort * 0.85f) strain += (climbed ? 0.06f : 0.035f) * (1f + 2f * excess) * ease * rules;
            if (excess > 0f && activity > 0f)
            {
                strain += dt * StrainRate * excess * activity * ease * rules;
                if (inj != null) inj.severity = Mathf.Min(1f, inj.severity + dt * StrainRate * 0.5f * excess * activity);   // carrying on keeps it raw
                if (activity >= 1f) Stats.Practice(Skill.Athletics, dt * 0.02f * Mathf.Min(1f, excess));
            }
            else
            {
                float rest = resting ? 0.006f : activity <= 0.35f ? 0.003f : 0.0015f;
                strain = Mathf.Max(0f, strain - dt * rest * (ease < 1f ? 1.3f : 1f));
                if (inj != null && activity <= 0.35f)                                                // lying, sitting, standing still: the back mends faster
                    inj.severity = Mathf.Max(0f, inj.severity - dt * (resting ? 2f : 1f) * (ease < 1f ? 1.5f : 1f) * (Stats.painkilled ? 1.5f : 1f) / (inj.HealMinutes * 60f));
            }
            if (strain >= 1f) { BackGivesOut(inj); return; }
            if (strain >= StrainAlarm && strainWarned < 2) { strainWarned = 2; Toast("YOUR BACK IS ABOUT TO GIVE OUT: DROP SOME WEIGHT"); MadMax.Audio.Sfx.Play2D("punch", 0.25f, 0.6f); }
            else if (strain >= StrainWarn && strainWarned < 1) { strainWarned = 1; Toast("YOUR BACK ACHES UNDER THE LOAD: LIGHTEN IT OR REST"); }
            else if (strain < StrainWarn * 0.6f) strainWarned = 0;
        }

        void BackGivesOut(Injury inj)
        {
            if (inj == null) Stats.injuries.Add(new Injury { zone = BodyZone.Torso, type = Wound.Strain, severity = 1f });
            else inj.severity = 1f;
            strain = StrainWarn; strainWarned = 1;
            Stats.stamina = Mathf.Min(Stats.stamina, Stats.MaxStamina * 0.2f);
            Toast((inj == null ? "YOUR BACK GAVE OUT" : "YOUR BACK GAVE OUT AGAIN") + ": STRAINED BACK  [O] HEALTH");
            MadMax.Audio.Sfx.Play("punch", Player.transform.position + Vector3.up, 0.6f, 0.5f, 10f);
            Journal.Add("health", "MY BACK GAVE OUT UNDER THE LOAD. REST IT, SLEEP, AND CARRY LESS.");
        }

        /// <summary>Sleep (1 = a night, less for a rest by day) mends the back.</summary>
        void BackSlept(float amount)
        {
            strain = Mathf.Max(0f, strain - amount);
            var inj = BackInjury();
            if (inj == null) return;
            inj.severity = Mathf.Max(0f, inj.severity - 0.7f * amount * (Wearing(BagLibrary.Brace) ? 1.3f : 1f));
            if (inj.severity <= 0.02f) { Stats.injuries.Remove(inj); Toast("YOUR BACK FEELS FINE AGAIN"); }
        }

        /// <summary>Health page ENTER on a strained back: a brace from the pack, else painkillers, else advice.</summary>
        void TreatBack(Injury inj)
        {
            var brace = "cloth_" + BagLibrary.Brace;
            if (!Wearing(BagLibrary.Brace) && Inventory.GetItem(brace) > 0) { WearBag(brace); return; }
            if (!Stats.painkilled && Inventory.TakeItem("med_painkillers"))
            {
                Stats.painkillerUntil = MadMax.World.DayNight.TotalDays * 24f + 3f;
                Stats.painkilled = true;
                Toast("PAINKILLERS: THE BACK EASES (IT HEALS FASTER AT REST)");
                return;
            }
            Toast("REST, SLEEP, CARRY LESS: A BACK BRACE OR PAINKILLERS HELP");
        }

        // ------------------------------------------------------------------ a bag on the ground
        /// <summary>[T] on a bag lying in the world: its contents in the loot window (changes go back into its key).</summary>
        public void OpenGroundBag(WorldItem w)
        {
            if (!w || !BagLibrary.IsBag(w.key)) return;
            var c = w.GetComponent<Container>();
            if (!c)
            {
                c = w.gameObject.AddComponent<Container>();
                c.enabled = false;                                                                   // not "nearby storage": only through this
                var cd = ClothingLibrary.Get(w.key);
                BagLibrary.Setup(c, BagLibrary.Get(w.key), cd != null ? cd.name : "BAG");
                var item = w; var box = c;
                c.inventory.Changed += () => GroundBagChanged(item, box);
            }
            groundItem = w; groundBox = c;
            LoadGroundBag();
            Menus.OpenContainer(c);
        }

        void LoadGroundBag()
        {
            groundBagLoading = true;
            BagLibrary.Contents(groundItem.key, groundBox.inventory);
            groundBagLoading = false;
            groundKey = groundItem.key;
        }

        void GroundBagChanged(WorldItem w, Container c)
        {
            if (groundBagLoading || !w || !c) return;
            var k = BagLibrary.Key(BagLibrary.DefId(w.key), c.inventory);
            if (w == groundItem) groundKey = k;
            if (k == w.key) return;
            w.key = k;
            w.Refresh();
            MadMax.Net.NetSession.Instance?.SendItemSpawn(w, Vector3.zero);
        }

        // ------------------------------------------------------------------ save
        partial void BagsSave(SaveData d) => SaveBags(d);
        partial void BagsLoad(SaveData d) => LoadBags(d);

        /// <summary>"v1", "s|strain", "w|garment|full key" per worn bag with contents, "d|tool" drawn from a belt.</summary>
        public void SaveBags(SaveData d)
        {
            if (d.blockBags == null) d.blockBags = new List<string>();
            d.blockBags.Add("v1");
            d.blockBags.Add("s|" + strain.ToString("0.####", BagsInv));
            foreach (var kv in wornBags) if (kv.Value && !BagLibrary.Empty(kv.Value.inventory)) d.blockBags.Add("w|" + kv.Key + "|" + BagLibrary.Key(kv.Key, kv.Value.inventory));
            foreach (var id in drawnFromBelt) d.blockBags.Add("d|" + id);
        }

        /// <summary>Restore the worn bags' contents (unpacked when the outfit is on, next frame) and the back.</summary>
        public void LoadBags(SaveData d)
        {
            ClearWornBags(); bagPending.Clear(); drawnFromBelt.Clear();
            strain = 0f; strainWarned = 0;
            bool v1 = false;
            if (d != null && d.blockBags != null)
                foreach (var line in d.blockBags)
                {
                    var f = line.Split('|');
                    if (f[0] == "v1") v1 = true;
                    else if (f[0] == "s" && f.Length > 1) float.TryParse(f[1], NumberStyles.Float, BagsInv, out strain);
                    else if (f[0] == "w" && f.Length > 2) bagPending[f[1]] = f[2];
                    else if (f[0] == "d" && f.Length > 1) drawnFromBelt.Add(f[1]);
                }
            bagsMigrate = !v1;
            bagsLoaded = true;
        }

        /// <summary>First frame after a load: saved contents of bags that are not on the body (any more) go into the bag
        /// in the pack, never into a later one.</summary>
        void SettleLoadedBags()
        {
            bagsLoaded = false;
            if (wornBags.Count == 0) bagsMigrate = false;                                           // nothing worn: nothing to move into
            foreach (var kv in bagPending)
            {
                var plain = BagLibrary.Plain(kv.Key);
                if (Inventory.GetItem(plain) > 0) { Inventory.TakeItem(plain); Inventory.AddItem(kv.Value); }
            }
            bagPending.Clear();
        }
    }
}
