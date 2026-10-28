using System.Collections.Generic;
using MadMax.Building;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-27: breakfast from the yard (eggs and milk at the stove or the fire: steady hands
    /// till noon, the crew comes to the table), raider lookouts whose binoculars flash on the skyline before a column
    /// reaches you, and the scarecrow trade (town farmers take an old garment for their scarecrow; a gang's scarecrow can
    /// be torn down).</summary>
    public partial class WastelandGame
    {
        // ---- breakfast from the yard
        public const string Breakfast = "food_breakfast";
        /// <summary>Aim spread with steady hands; how far companions come to the table; how long they sit.</summary>
        public const float SteadyAim = 0.75f, BreakfastReach = 30f, BreakfastSeconds = 40f;
        /// <summary>Steady hands until this game hour (total hours); the last breakfast's note and how many sat down (tests).</summary>
        public static float SteadyUntil = -1f;
        public static string LastBreakfast;
        public static int BreakfastCrew;
        public static bool SteadyHands => DayNight.TotalDays * 24f < SteadyUntil;
        readonly List<(MadMax.Npc.Npc n, Vector3 home, float radius)> atBreakfast = new List<(MadMax.Npc.Npc, Vector3, float)>();

        static readonly string[] BreakfastLines =
        {
            "EGGS FROM OUR OWN HENS. TASTES LIKE BEFORE.", "PASS THE MILK.", "NOW THAT'S A START TO A DAY.",
            "SOMEONE FEED THE DOG THE CRUSTS.", "COFFEE WOULD MAKE IT PERFECT. NOBODY'S SEEN COFFEE IN YEARS.",
        };

        /// <summary>From <see cref="Eat"/>: a breakfast before 11:00 steadies the hands till noon; eaten at a table the crew
        /// within <see cref="BreakfastReach"/> m sits down with you.</summary>
        void Ate1027(string id, bool atTable)
        {
            if (id != Breakfast) return;
            float h = DayNight.Hours;
            if (h >= 4.5f && h < 11f)
            {
                SteadyUntil = DayNight.Day * 24f + 12f;
                LastBreakfast = "STEADY HANDS TILL NOON";
                Toast("A YARD BREAKFAST: STEADY HANDS TILL NOON");
            }
            else { LastBreakfast = "A LATE BREAKFAST"; Toast("BREAKFAST AT THIS HOUR - STILL GOOD"); }
            BreakfastCrew = 0;
            if (!atTable || !Player || !DiningTable.Nearest(Player.transform.position, out var table)) return;
            var tp = table.transform.position;
            int k = DayNight.Day;
            foreach (var n in Companions.Live)
            {
                if (!n || !n.Alive || n.proxy || n.Foe || n.Visiting || Flat(n.transform.position - tp) > BreakfastReach) continue;
                atBreakfast.Add((n, n.home, n.homeRadius));
                string line = BreakfastLines[(k + BreakfastCrew) % BreakfastLines.Length];
                n.Visit(tp, BreakfastSeconds, who =>
                {
                    if (table) table.Serve(who.transform.position, Breakfast);
                    Remark(who, line, 4f);
                }, null);
                BreakfastCrew++;
            }
            if (BreakfastCrew > 0) LastBreakfast += ", " + BreakfastCrew + " AT THE TABLE";
        }

        /// <summary>A companion back from the table goes on with what they were doing (the visit sends people home).</summary>
        void UpdateBreakfastCrew()
        {
            for (int i = atBreakfast.Count - 1; i >= 0; i--)
            {
                var (n, home, radius) = atBreakfast[i];
                if (!n) { atBreakfast.RemoveAt(i); continue; }
                if (n.Visiting) continue;
                n.leaving = false; n.home = home; n.homeRadius = radius;
                atBreakfast.RemoveAt(i);
            }
        }

        // ---- lookouts: glint both ways
        /// <summary>Range at which a closing raider column's lookout can flash (m), seconds between flashes per column.</summary>
        public const float LookoutMin = 140f, LookoutMax = 480f, LookoutEvery = 240f;
        public static int LookoutFlashes;
        public static string LastLookout;
        public static Vector3 LastLookoutAt;
        /// <summary>Automation: the sun's direction for lookout flashes (null = <see cref="DayNight.SunDirection"/>).</summary>
        public static Vector3? LookoutSunOverride;
        readonly Dictionary<Convoy, float> lookoutNext = new Dictionary<Convoy, float>(), lookoutDist = new Dictionary<Convoy, float>();
        float lookoutT, flashUntil, flashT, flashStrength;

        void UpdateLookouts()
        {
            if (flashUntil > Time.time && (flashT -= Time.deltaTime) <= 0f)
            {
                flashT = Random.Range(0.25f, 0.45f);
                Fx.Glint(LastLookoutAt, 1.1f + 1.8f * flashStrength, MadMax.Voxel.Pal.LightW);
            }
            if (Time.time < lookoutT) return;
            lookoutT = Time.time + 1f;
            var d = NpcDirector.Instance;
            if (!d || !Player) return;
            var me = FocusPos;
            foreach (var c in d.Convoys)
            {
                if (c == null || !c.raiders || c.phase != Convoy.Phase.Travel) continue;
                var cp = c.Position;
                float dist = Flat(cp - me);
                float was = lookoutDist.TryGetValue(c, out var w) ? w : dist + 1f;
                lookoutDist[c] = dist;
                if (dist < LookoutMin || dist > LookoutMax || dist > was - 0.3f) continue;     // only a column coming your way
                if (lookoutNext.TryGetValue(c, out var next) && Time.time < next) continue;
                var spot = LookoutSpot(cp, me);
                float s = GlintFor(me - spot, LookoutSunOverride ?? DayNight.SunDirection, DayNight.Darkness, Storms.Dust);
                if (s < 0.25f) continue;
                LookoutFlash(c, spot, s);
                break;
            }
        }

        /// <summary>The highest ground on the column's side between it and the player: where a lookout would lie.</summary>
        public static Vector3 LookoutSpot(Vector3 convoy, Vector3 me)
        {
            var t = DeformableTerrain.Instance;
            var dir = me - convoy; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-3f ? dir.normalized : Vector3.forward;
            var side = new Vector3(dir.z, 0f, -dir.x);
            float bh = t ? t.HeightNoLoad(convoy.x, convoy.z) : convoy.y;                    // never below the column itself
            Vector3 best = new Vector3(convoy.x, bh, convoy.z);
            for (int i = 0; i < 9; i++)
            {
                var p = convoy + dir * (30f + (i / 3) * 25f) + side * ((i % 3 - 1) * 35f);
                float h = t ? t.HeightNoLoad(p.x, p.z) : convoy.y;
                if (h > bh) { bh = h; best = new Vector3(p.x, h, p.z); }
            }
            return best;
        }

        /// <summary>The lens catches the sun: a few flashes on the skyline and a word the first time.</summary>
        public void LookoutFlash(Convoy c, Vector3 spot, float strength)
        {
            lookoutNext[c] = Time.time + LookoutEvery;
            LastLookoutAt = spot + Vector3.up * 1.4f;
            flashUntil = Time.time + 1.3f; flashT = 0f; flashStrength = Mathf.Clamp01(strength);
            LookoutFlashes++;
            LastLookout = c.Gang;
            Toast("A FLASH ON THE SKYLINE - SOMEONE'S WATCHING THE ROAD");
            Hints.Show("lookout_glint", "SUN OFF A LENS: A LOOKOUT. A COLUMN ISN'T FAR BEHIND");
            Attention.Ping(LastLookoutAt, Attention.Kind.Hostile, 1f);
        }

        // ---- scarecrow trade
        /// <summary>How close the player stands to a town scarecrow for the trade; scrap a farmer pays for a garment; standing changes.</summary>
        public const float ScarecrowReach = 3f;
        public const int ScarecrowPayMin = 4, ScarecrowPayMax = 9, ScarecrowTownWarmth = 1, GangTornTown = 3, GangTornGang = -6;
        public static string LastScarecrow;

        void ScarecrowOptions(ContextTarget t, List<ContextOption> into)
        {
            if (!Player || Current) return;
            var me = Player.transform.position;
            foreach (var kv in TownScarecrows.Shown)
            {
                if (!kv.Value || Flat(kv.Value.position - me) > ScarecrowReach) continue;
                string key = kv.Key;
                if (TownScarecrows.IsGang(key))
                {
                    into.Add(Opt("TEAR DOWN THE GANG SCARECROW", () => TearDownScarecrow(key)));
                    return;
                }
                if (TownScarecrows.Dressed.ContainsKey(key)) return;
                string spare = SpareGarment();
                if (spare != null) into.Add(Opt("GIVE YOUR " + MadMax.Items.ItemIds.Name(spare) + " FOR THE SCARECROW", () => DressScarecrow(key, spare)));
                return;
            }
        }

        /// <summary>A garment in the pack (not worn, not a bag, not armour), the most worn first.</summary>
        string SpareGarment()
        {
            string best = null; float worst = -1f;
            foreach (var kv in Inventory.Items)
            {
                if (kv.Value <= 0 || !kv.Key.StartsWith("cloth_", System.StringComparison.Ordinal)) continue;
                var d = ClothingLibrary.Get(kv.Key);
                if (d == null || d.slot == ClothingSlot.Pack || d.armor != null) continue;
                float w = ClothWear.TryGetValue(d.id, out var x) ? x : 0f;
                if (w > worst) { worst = w; best = kv.Key; }
            }
            return best;
        }

        /// <summary>The farmer takes the garment for the scarecrow (it wears it from now on) and pays a little.</summary>
        public bool DressScarecrow(string key, string item)
        {
            if (string.IsNullOrEmpty(key) || TownScarecrows.Dressed.ContainsKey(key) || !Inventory.TakeItem(item)) return false;
            TownScarecrows.Dress(key, item);
            int hash = 17; foreach (char ch in key) hash = hash * 31 + ch;
            int pay = ScarecrowPayMin + (hash & 0x7fffffff) % (ScarecrowPayMax - ScarecrowPayMin + 1);
            Inventory.Add(MadMax.Items.ResourceType.Scrap, pay);
            int town = TownScarecrows.TownOf(key);
            if (World != null && town >= 0 && town < World.settlements.Count) Factions.Shift(Factions.OfSettlement(World.settlements[town]), ScarecrowTownWarmth);
            LastScarecrow = "THE FARMER PUT YOUR " + MadMax.Items.ItemIds.Name(item) + " ON THE SCARECROW: " + pay + " SCRAP";
            Toast(LastScarecrow);
            MadMax.Audio.Sfx.Play("cash", Player.transform.position, 0.4f, 1f, 8f);
            return true;
        }

        /// <summary>Down with their colours: the gang won't forget it, the town is glad.</summary>
        public bool TearDownScarecrow(string key)
        {
            if (!TownScarecrows.IsGang(key) || TownScarecrows.Torn.Contains(key)) return false;
            string gang = TownScarecrows.GangOf(key);
            if (TownScarecrows.Shown.TryGetValue(key, out var tr) && tr)
            {
                Fx.Smoke(tr.position + Vector3.up * 1f, Vector3.up * 0.5f, 0.6f, MadMax.Voxel.Pal.WorkshopDust, 2f);
                MadMax.Audio.Sfx.Play("collapse", tr.position, 0.4f, 1.4f, 20f);
            }
            TownScarecrows.TearDown(key);
            if (gang != null) Factions.Shift(Factions.OfGang(gang), GangTornGang);
            int town = TownScarecrows.TownOf(key);
            if (World != null && town >= 0 && town < World.settlements.Count) Factions.Shift(Factions.OfSettlement(World.settlements[town]), GangTornTown);
            Gesture(UseKind.Kneel, null, 1.2f);
            LastScarecrow = "TORE DOWN THE " + (gang ?? "GANG") + " SCARECROW: THEY WON'T LIKE IT, THE TOWN DOES";
            Toast(LastScarecrow);
            Journal.Add("PEOPLE", LastScarecrow);
            return true;
        }

        // ---- hooks
        void UpdateNightly1027()
        {
            if (!ContextActions.Has("town.scarecrow")) ContextActions.Register("town.scarecrow", (g, t, into) => { if (t.item == null) g.ScarecrowOptions(t, into); });
            UpdateBreakfastCrew();
            UpdateLookouts();
            UpdateNightly1028();
        }

        void Save1027(List<string> lines)
        {
            foreach (var kv in TownScarecrows.Dressed) lines.Add("w|" + kv.Key + "|" + kv.Value);
            foreach (var k in TownScarecrows.Torn) lines.Add("k|" + k);
        }

        void Load1027(string[] f)
        {
            if (f[0] == "w" && f.Length >= 3) TownScarecrows.Dress(f[1], f[2]);
            else if (f[0] == "k" && f.Length >= 2) TownScarecrows.TearDown(f[1]);
        }

        void Clear1027()
        {
            TownScarecrows.Dressed.Clear(); TownScarecrows.Torn.Clear();
            SteadyUntil = -1f; LastBreakfast = null; BreakfastCrew = 0; atBreakfast.Clear();
            lookoutNext.Clear(); lookoutDist.Clear(); flashUntil = 0f;
        }
    }
}
