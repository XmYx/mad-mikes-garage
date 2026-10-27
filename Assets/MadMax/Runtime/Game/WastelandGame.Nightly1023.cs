using System.Collections.Generic;
using MadMax.Animals;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-23: wash days (<see cref="ClothesLine"/>: wash what you wear with water near,
    /// hang it out, take it in dry), the visit goes both ways (after a neighbour has called in, their house is open in the
    /// evening: supper at their table, a meal and a rumour), and the morning chores on the HOME ledger. Hens on the
    /// roost and the dog at the stable door live in <c>Animals/Animal.Roost</c>; the crows' memory in
    /// <see cref="Scarecrow"/>; the souring, rain-filled slop in <see cref="SlopBucket"/>.</summary>
    public partial class WastelandGame
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset1023() { LastWash = null; LastSupper = null; }

        // ------------------------------------------------------------------ wash day

        /// <summary>What the last wash / take-in did (tests).</summary>
        public static string LastWash;
        /// <summary>Wear a scrub takes off each garment (0..1 of its life); hygiene from fresh clothes.</summary>
        public const float WashMends = 0.05f, FreshClothes = 25f;

        /// <summary>Water for the wash within <see cref="ClothesLine.WaterReach"/> of <paramref name="at"/>: a store of water
        /// (drawn from), else a lake. Null when there is none; <paramref name="clean"/> false for dirty water.</summary>
        string WashWater(Vector3 at, out bool clean)
        {
            clean = true;
            foreach (var s in FluidStore.All)
            {
                if (!s || (s.transform.position - at).sqrMagnitude > ClothesLine.WaterReach * ClothesLine.WaterReach) continue;
                if (s.Frozen || s.Contents < ClothesLine.WashLitres || !FluidStore.WaterOnly(s.Mix)) continue;
                clean = s.Mix[ResourceType.Water] > 0.99f;
                s.Draw(ClothesLine.WashLitres, new FluidMix());
                return s.title;
            }
            var t = DeformableTerrain.Instance;
            if (t)
                for (int r = 2; r <= 12; r += 2)
                    for (int k = 0; k < 12; k++)
                    {
                        float a = k * Mathf.PI / 6f;
                        if (t.WaterDepthNoLoad(at.x + Mathf.Cos(a) * r, at.z + Mathf.Sin(a) * r) > 0.15f) { clean = false; return "THE WATER'S EDGE"; }
                    }
            return null;
        }

        /// <summary>[E] at a bare clothes line: wash what you wear (mends a little) and hang it out.</summary>
        public bool WashClothes(ClothesLine line)
        {
            if (!line || !Player || Player.Rig == null) return false;
            var hang = new List<string>();
            foreach (var id in Player.Rig.outfit)
            {
                var d = ClothingLibrary.Get(id);
                if (d == null || d.slot == ClothingSlot.Pack || d.slot == ClothingSlot.Belt || d.slot == ClothingSlot.Hand || d.slot == ClothingSlot.Shoulder || d.armor != null) continue;
                hang.Add(ClothingLibrary.ItemId(d));
            }
            if (hang.Count == 0) { Toast("NOTHING ON YOU THAT WANTS WASHING"); return false; }
            var water = WashWater(line.transform.position, out bool clean);
            if (water == null) { Toast("NO WATER WITHIN " + (int)ClothesLine.WaterReach + " M: A FILLED POOL, DRUM OR TANK, OR A LAKE"); return false; }
            int mended = 0;
            foreach (var item in hang)
            {
                var d = ClothingLibrary.Get(item);
                if (d == null || !ClothWear.TryGetValue(d.id, out var w) || w <= 0f) continue;
                ClothWear[d.id] = Mathf.Max(0f, w - WashMends * (clean ? 1f : 0.6f));
                mended++;
            }
            line.Hang(hang);
            MadMax.World.Spills.Pour(line.transform.position + line.transform.forward * 1.2f, MadMax.Items.ResourceType.DirtyWater, ClothesLine.WashLitres * 0.7f);   // the tub tipped out beside the line (Nightly1027)
            MadMax.Audio.Sfx.Play("splash", line.transform.position, 0.5f, 0.9f, 12f);
            MadMax.Audio.Sfx.Play("scratch", line.transform.position, 0.35f, 0.7f, 10f);
            Gesture(UseKind.Kneel, null, 1.6f);
            Stats.Practice(MadMax.RPG.Skill.Survival, 0.5f);
            LastWash = "WASHED " + hang.Count + " THINGS IN " + water + (mended > 0 ? ", " + mended + " MENDED A LITTLE" : "");
            Toast(LastWash + ": HUNG OUT TO DRY");
            return true;
        }

        /// <summary>[E] at a line with the wash on it: dry = fresh clothes, damp = they soak you.</summary>
        public void TakeInWash(ClothesLine line)
        {
            if (!line || !line.Hung) return;
            float dusty = line.dust;
            int lost = line.lost.Count;
            float d = line.TakeIn();
            MadMax.Audio.Sfx.Play("scratch", line.transform.position, 0.3f, 1.2f, 8f);
            string after = (lost > 0 ? " (" + lost + " BLEW AWAY IN THE WIND: LOOK DOWNWIND)" : "");
            if (d >= 1f && dusty > 0.35f)                                                           // a dust storm got at it (Nightly1026)
            {
                LastWash = "TOOK THE WASH IN DUSTY: SHOOK IT OUT, NOT MUCH FRESHER" + after;
                Wash(FreshClothes * (1f - dusty) * 0.6f, LastWash);
                Stats.wetness = Mathf.Max(0f, Stats.wetness - 0.5f);
            }
            else if (d >= 1f)
            {
                LastWash = "FRESH CLOTHES OFF THE LINE" + after;
                Wash(FreshClothes * (1f - dusty * 0.5f), LastWash);
                Stats.wetness = Mathf.Max(0f, Stats.wetness - 0.5f);
            }
            else
            {
                LastWash = "TOOK THE WASH IN " + (d < 0.35f ? "SOAKING" : "DAMP") + ": COLD AND CLAMMY" + after;
                Stats.wetness = Mathf.Max(Stats.wetness, 1f - d);
                Toast(LastWash);
            }
        }

        // ------------------------------------------------------------------ supper at the neighbour's

        /// <summary>Neighbour id → (day invited, last supper day).</summary>
        public readonly Dictionary<string, Vector2Int> SupperInvites = new Dictionary<string, Vector2Int>();
        public const float SupperFrom = 17f, SupperTo = 21.5f;
        public const int SupperEvery = 3;
        /// <summary>The last supper (tests).</summary>
        public static string LastSupper;
        int supperHintDay = -1;
        float supperT;

        /// <summary>From the call-in's goodbye: the neighbour's house is open to the player in the evening from now on.</summary>
        public void InviteToSupper(string key, string name)
        {
            if (string.IsNullOrEmpty(key) || SupperInvites.ContainsKey(key)) return;
            SupperInvites[key] = new Vector2Int(DayNight.Day, -99);
            Journal.Add("HOME", (name ?? "THE NEIGHBOUR") + " SAID TO COME FOR SUPPER ANY EVENING");
        }

        public bool SupperHours => DayNight.Hours >= SupperFrom && DayNight.Hours <= SupperTo;

        /// <summary>Why <paramref name="n"/> can't give supper now (null: they can).</summary>
        public string SupperBlocked(MadMax.Npc.Npc n)
        {
            if (!n || !n.Alive || n.Hostile || !SupperInvites.TryGetValue(n.Profile.id, out var v)) return "NOT INVITED";
            if (!SupperHours) return "SUPPER IS FROM 17:00";
            if (DayNight.Day - v.y < SupperEvery) return "YOU ATE THERE LATELY";
            if (Current) return "ON FOOT";
            return null;
        }

        /// <summary>Supper at the neighbour's table: a meal, a seat by them, a rumour, warmer standing.</summary>
        public string HaveSupper(MadMax.Npc.Npc n, string gift = null)
        {
            if (SupperBlocked(n) != null) return null;
            if (gift != null && !Inventory.TakeItem(gift)) gift = null;
            var v = SupperInvites[n.Profile.id];
            SupperInvites[n.Profile.id] = new Vector2Int(v.x, DayNight.Day);
            Stats.hunger = Mathf.Min(100f, Stats.hunger + 45f);
            Stats.thirst = Mathf.Min(100f, Stats.thirst + 25f);
            Stats.fedUntil = Mathf.Max(Stats.fedUntil, DayNight.TotalDays * 24f + 5f);
            Gesture(UseKind.Eat, "food_stew", 2.5f);
            n.Visit(n.transform.position, 40f, x => { }, x => { });                                   // they sit down to it too
            var s = NpcRegistry.Get(n.Profile);
            s.Set(NpcSave.Met);
            int warmth = SupperWarmth(n.Profile.id, gift, out bool talk, out string line);        // what was brought to the table (Nightly1026)
            s.disposition = Mathf.Clamp(s.disposition + warmth, -100, 100);
            var at = n.transform.position;
            string rumour = talk ? GiverRumour(at, true, n.Profile.id) : null;
            string more = gift != null ? CacheRumour(at) : null;                                    // a gift loosens a second tale
            if (string.IsNullOrEmpty(rumour)) { rumour = more ?? (talk ? CacheRumour(at) : null); more = null; }
            if (!string.IsNullOrEmpty(more)) rumour += ". AND " + more;
            string name = n.Profile.Name;
            Remark(n, line ?? "SIT, SIT. IT'S ONLY STEW, BUT IT'S HOT.", 4f);
            LastSupper = "SUPPER AT " + name + "'S" + (gift != null ? " (BROUGHT " + MadMax.Items.ItemIds.Name(gift) + ")" : "") + ": WELL FED" + (string.IsNullOrEmpty(rumour) ? "" : ". OVER THE PLATES: " + rumour);
            Toast(LastSupper);
            Journal.Add("PEOPLE", LastSupper);
            MadMax.Audio.Sfx.Play("chest", at, 0.25f, 1.4f, 8f);
            return LastSupper;
        }

        void SupperOptions(ContextTarget t, List<ContextOption> into)
        {
            if (!(t.target is MadMax.Npc.Npc n) || !SupperInvites.ContainsKey(n.Profile.id)) return;
            into.Add(Opt("SIT DOWN TO SUPPER WITH " + n.Profile.Name, () => HaveSupper(n), true, SupperBlocked(n)));
            var gift = SupperGift();
            if (gift != null) into.Add(Opt("BRING " + MadMax.Items.ItemIds.Name(gift) + " TO SUPPER", () => HaveSupper(n, gift), true, SupperBlocked(n)));
        }

        /// <summary>The invited neighbour waves the player over when they come by in the evening (once a day).</summary>
        void UpdateSupper()
        {
            if (Time.time < supperT || SupperInvites.Count == 0 || !Player || Current || !SupperHours || supperHintDay == DayNight.Day) return;
            supperT = Time.time + 2f;
            var me = Player.transform.position;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || Flat(n.transform.position - me) > 8f || SupperBlocked(n) != null) continue;
                supperHintDay = DayNight.Day;
                Remark(n, Stats.name + "! THERE'S A PLATE FOR YOU IF YOU WANT IT.", 4f);
                Toast(n.Profile.Name + " WAVES YOU OVER: SUPPER (CONTEXT MENU ON THEM)");
                return;
            }
        }

        /// <summary>Map pins: the towns where a supper waits (from <c>NeighbourPins</c>).</summary>
        public void SupperPins(List<Pin> into)
        {
            if (World == null) return;
            foreach (var kv in SupperInvites)
            {
                if (!NeighbourErrands.TryGetValue(kv.Key, out var e) || e.town < 0 || e.town >= World.settlements.Count) continue;
                var t = World.settlements[e.town];
                bool due = DayNight.Day - kv.Value.y >= SupperEvery;
                into.Add(new Pin { label = "SUPPER AT " + (e.name ?? "THE NEIGHBOUR") + "'S" + (due ? " (EVENINGS)" : " (ATE THERE LATELY)"), pos = new Vector3(t.pos.x + 6f, 0f, t.pos.y + 6f), color = new Color32(232, 160, 96, 255) });
            }
        }

        // ------------------------------------------------------------------ the chores line

        /// <summary>The HOME ledger's chores for <paramref name="claim"/> (null: none to list); <paramref name="at"/> = the
        /// first chore's place.</summary>
        public string ChoresLine(ClaimFlag claim, out Vector3 at)
        {
            at = claim ? claim.transform.position : Vector3.zero;
            if (!claim) return null;
            var list = new List<string>();
            var first = at;
            bool any = false;
            void Add(string s, Vector3 p) { list.Add(s); if (!any) { first = p; any = true; } }
            bool animals = false;
            foreach (var a in Animal.All) if (a && a.Alive && a.owned && claim.Inside(a.home)) { animals = true; break; }
            foreach (var st in Stable.All)
            {
                if (!st || !claim.Inside(st.transform.position)) continue;
                if (animals && st.Bedding < 0.35f) Add(st.Bedded ? "BEDDING TRODDEN BROWN" : "NO BEDDING DOWN", st.transform.position);
                if (st.TryGetComponent<Trough>(out var m) && m.manure >= Stable.DirtyManure) Add("DUNG TO SHOVEL (" + m.manure + ")", st.transform.position);
            }
            foreach (var tr in Trough.All)
            {
                if (!tr || !animals || !claim.Inside(tr.transform.position)) continue;
                if (tr.feed < 3f) Add("TROUGH FEED LOW", tr.transform.position);
                if (tr.water < 3f) Add("TROUGH WATER LOW", tr.transform.position);
            }
            foreach (var b in SlopBucket.All)
            {
                if (!b || !claim.Inside(b.transform.position)) continue;
                if (b.water >= 3f) Add("RAIN IN THE SLOP BUCKET", b.transform.position);
                else if (b.scraps > 0 && b.Sour) Add("SLOP SOURING", b.transform.position);
                else if (b.scraps == 0 && animals) Add("SLOP BUCKET EMPTY", b.transform.position);
            }
            foreach (var l in ClothesLine.All)
                if (l && l.Dry && claim.Inside(l.transform.position)) Add("WASH DRY ON THE LINE", l.transform.position);
            foreach (var p in Placeable.All)
                if (p && p.id == "scarecrow" && claim.Inside(p.transform.position) && p.TryGetComponent<Scarecrow>(out var sc) && !sc.Fresh) Add("SCARECROW GONE STALE", p.transform.position);
            at = first;
            if (list.Count == 0) return animals ? "  CHORES: ALL DONE" : null;
            return "  CHORES: " + string.Join(", ", list.Count > 5 ? list.GetRange(0, 5) : list) + (list.Count > 5 ? " +" + (list.Count - 5) : "");
        }

        // ------------------------------------------------------------------ tick + save

        void UpdateNightly1023()
        {
            if (!ContextActions.Has("home.supper")) ContextActions.Register("home.supper", (g, t, into) => { if (t.item == null) g.SupperOptions(t, into); });
            UpdateSupper();
            UpdateNightly1026();
        }

        void Save1023(List<string> lines)
        {
            foreach (var kv in SupperInvites) lines.Add("v|" + kv.Key + "|" + kv.Value.x + "|" + kv.Value.y);
            foreach (var kv in SupperEmpty) lines.Add("y|" + kv.Key + "|" + kv.Value);
            Save1027(lines);
        }

        void Load1023(string[] f)
        {
            if (f[0] == "w" || f[0] == "k") { Load1027(f); return; }
            if (f[0] == "y" && f.Length >= 3) { int.TryParse(f[2], out int e); SupperEmpty[f[1]] = e; return; }
            if (f[0] != "v" || f.Length < 4) return;
            int.TryParse(f[2], out int x); int.TryParse(f[3], out int y);
            SupperInvites[f[1]] = new Vector2Int(x, y);
        }

        void Clear1023()
        {
            SupperInvites.Clear();
            supperHintDay = -1;
            SupperEmpty.Clear();
            Clear1027();
        }
    }
}
