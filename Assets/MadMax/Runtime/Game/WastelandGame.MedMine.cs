using System.Collections.Generic;
using System.Globalization;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Depth stages G and H (MedMine block). Medicine: herbal poultices and willow-bark tea from the campfire,
    /// first-aid kits that treat every wound at once (the clinic bed and surgery table are pieces). Mining: river gold —
    /// every 16 m stretch of water carries some (rivers most, gold-bearing reaches up to half again, lakes and the sea a
    /// third), the gold pan fills a vial of fines that gives a flake of GOLD ORE each time it is full, and a worked stretch
    /// thins out; deep unpropped pits slump now and then (a mine prop within 5 m holds them). Saved in
    /// <c>SaveData.blockMedMine</c>.</summary>
    public partial class WastelandGame
    {
        // ------------------------------------------------------------------ medicine

        /// <summary>MedMine items used from the hotbar or the pack; false when the id is not one of ours.</summary>
        bool MedMineUse(string id)
        {
            switch (id)
            {
                case "med_poultice": ApplyPoultice(); return true;
                case "med_firstaid": UseFirstAidKit(); return true;
            }
            return false;
        }

        /// <summary>After eating or drinking: willow-bark tea dulls the pain for 90 minutes.</summary>
        void MedMineAte(string id)
        {
            if (id != "drink_bark_tea") return;
            Stats.painkillerUntil = Mathf.Max(Stats.painkillerUntil, DayNight.TotalDays * 24f + 1.5f);
            Stats.painkilled = true;
            Toast("WILLOW-BARK TEA: THE ACHES DULL");
        }

        static bool MedOpenWound(Injury i) => i.Open;

        /// <summary>A herbal poultice on the worst open wound (bleeding first): binds it (the dressing soils in five
        /// minutes), draws some infection and heals it a little. False when there was nothing to put it on.</summary>
        public bool ApplyPoultice()
        {
            Injury best = null; float top = -1f;
            foreach (var inj in Stats.injuries)
            {
                if (!MedOpenWound(inj)) continue;
                float score = (inj.Bleeding ? 10f + inj.BleedRate * 10f : 0f) + inj.severity + inj.infection * 2f + (!inj.bandaged || inj.BandageDirty ? 1f : 0f);
                if (score > top) { top = score; best = inj; }
            }
            if (best == null) { Toast("NO OPEN WOUND TO DRESS"); return false; }
            if (!Inventory.TakeItem("med_poultice")) return false;
            bool bled = best.Bleeding;
            best.bandaged = true; best.bandageAge = 300f;
            best.infection = Mathf.Max(0f, best.infection - 0.3f);
            best.severity = Mathf.Max(best.shrapnel ? Injury.ShrapnelFloor : 0f, best.severity - 0.08f);
            Stats.health = Mathf.Min(Stats.MaxHealth, Stats.health + 3f);
            Stats.Practice(Skill.Survival, 2f);
            Toast("HERBAL POULTICE ON THE " + Injury.ZoneNames[(int)best.zone] + (bled ? (best.type == Wound.DeepWound ? ": THE BLEEDING SLOWS" : ": THE BLEEDING STOPS") : ": IT DRAWS THE WOUND"));
            return true;
        }

        /// <summary>A first-aid kit: splints every fracture, disinfects and dresses every open wound in one go. Returns
        /// the wounds treated (the kit is kept when there is nothing to treat).</summary>
        public int UseFirstAidKit()
        {
            int n = 0;
            foreach (var inj in Stats.injuries)
            {
                bool did = false;
                if (inj.type == Wound.Fracture && !inj.splinted) { inj.splinted = true; did = true; }
                if (MedOpenWound(inj))
                {
                    if (!inj.disinfected) { inj.disinfected = true; inj.infection = Mathf.Max(0f, inj.infection - 0.6f); did = true; }
                    if (!inj.bandaged || inj.BandageDirty) { inj.bandaged = true; inj.bandageAge = 0f; did = true; }
                }
                if (did) n++;
            }
            if (n == 0) { Toast("FIRST AID KIT: NOTHING TO TREAT"); return 0; }
            if (!Inventory.TakeItem("med_firstaid")) return 0;
            Stats.Practice(Skill.Survival, 2f + n);
            MadMax.Audio.Sfx.Play2D("scratch", 0.4f, 1.1f);
            Toast("FIRST AID KIT: TREATED " + n + " WOUND" + (n == 1 ? "" : "S"));
            return n;
        }

        // ------------------------------------------------------------------ mining: river gold

        const float GoldCellSize = 16f;
        /// <summary>Pans and sluiced soil taken per 16 m stretch: the colour thins as a spot is worked. Saved.</summary>
        readonly Dictionary<Vector2Int, int> goldWorked = new Dictionary<Vector2Int, int>();
        /// <summary>Gold fines in the pan's vial (0..1): a flake of GOLD ORE each time it fills. Saved.</summary>
        public float GoldFines { get; private set; }
        int pansTaken;

        static Vector2Int GoldCell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / GoldCellSize), Mathf.FloorToInt(p.z / GoldCellSize));

        /// <summary>How much gold the water at <paramref name="p"/> carries: about 1 in a river (0.6..1.5 along its
        /// gold-bearing reaches), a third of that at a lake or sea shore, 0 on dry land; a worked stretch keeps a fifth.</summary>
        public float GoldRichness(Vector3 p)
        {
            if (!terrain || World == null) return 0f;
            float rich;
            if (World.RiverAt(p.x, p.z, out _, out float half, out float dist) && dist < half + 1.5f && terrain.WaterDepthNoLoad(p.x, p.z) > 0.02f) rich = 1f;
            else if (terrain.WaterDepthNoLoad(p.x, p.z) > 0.02f) rich = 0.33f;
            else return 0f;
            float reach = Mathf.PerlinNoise(p.x * 0.006f + (World.seed % 997) * 0.13f, p.z * 0.006f - 41.7f);
            goldWorked.TryGetValue(GoldCell(p), out int worked);
            return rich * (0.6f + 0.9f * reach) * Mathf.Clamp(1f - worked / 400f, 0.2f, 1f);
        }

        /// <summary>Note <paramref name="n"/> pans (or sluiced loads) taken from the stretch at <paramref name="p"/>.</summary>
        public void WorkGold(Vector3 p, int n)
        {
            var c = GoldCell(p);
            goldWorked.TryGetValue(c, out int w);
            goldWorked[c] = w + n;
        }

        /// <summary>One pan of the bed at <paramref name="at"/>: sand every other pan or so, the stretch's gold into the
        /// vial (Survival helps). Deterministic per stretch and pan count. Returns the GOLD ORE flakes won (0 or 1).</summary>
        public int PanGold(Vector3 at)
        {
            float rich = GoldRichness(at);
            var cell = GoldCell(at);
            var r = new System.Random(MadMax.Npc.Market.Seed(cell.x, cell.y, (World != null ? World.seed : 0) + pansTaken++));
            if (r.NextDouble() < 0.5) Inventory.Add(ResourceType.Sand, 1);
            GoldFines += 0.2f * rich * (1f + Stats.Level(Skill.Survival) * 0.06f) * (0.6f + 0.8f * (float)r.NextDouble());
            WorkGold(at, 1);
            if (GoldFines < 1f) return 0;
            GoldFines -= 1f;
            Inventory.Add(ResourceType.GoldOre, 1);
            MadMax.Story.Story.Note("gold_pan");
            return 1;
        }

        // ------------------------------------------------------------------ mining: slumping pits

        /// <summary>Depth (m below the natural ground) past which an unpropped pit can slump.</summary>
        public const float SlumpDepth = 2.2f;
        float slumpCheck = 3f;

        /// <summary>A mine prop stands within <paramref name="r"/> m.</summary>
        public static bool Propped(Vector3 p, float r)
        {
            foreach (var pl in Placeable.All)
                if (pl && pl.id == "mine_prop" && (pl.transform.position - p).sqrMagnitude < r * r) return true;
            return false;
        }

        /// <summary>Standing at <paramref name="p"/> is at risk of a slump: dug <see cref="SlumpDepth"/> m or more below the
        /// natural ground, a wall rising 1.2 m+ within 2.5 m (<paramref name="wall"/> points at the highest), no mine prop
        /// within 5 m.</summary>
        public bool SlumpRisk(Vector3 p, out Vector3 wall)
        {
            wall = Vector3.zero;
            if (!terrain || terrain.DugDepth(p.x, p.z) < SlumpDepth) return false;
            float rim = -1f, floor = terrain.Height(p.x, p.z);
            for (int i = 0; i < 8; i++)
            {
                var d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                var q = p + d * 2.5f;
                float up = terrain.Height(q.x, q.z) - floor;
                if (up > rim) { rim = up; wall = d; }
            }
            return rim >= 1.2f && !Propped(p, 5f);
        }

        /// <summary>In a deep unpropped pit a shoulder of earth sometimes slides in (a little fill at the foot of the wall,
        /// a knock). Gentle: one check every 3 s, 4 % each.</summary>
        void UpdateSlumps(float dt)
        {
            if ((slumpCheck -= dt) > 0f) return;
            slumpCheck = 3f;
            if (!terrain || !Player || Current || Player.Sitting || Vitals == null || Vitals.Dead) return;
            var p = Player.transform.position;
            if (!SlumpRisk(p, out var wall)) return;
            Hints.Show("mine_prop", "DEEP PITS SLUMP: SET A MINE PROP (BUILD, STRUCTURE) WITHIN 5 M TO HOLD THE WALLS");
            if (Random.value > 0.04f) return;
            var at = p + wall * 1.3f;
            at.y = terrain.Height(at.x, at.z);
            terrain.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dump, at, 1.1f, 0.3f, 0);
            MadMax.Net.NetSession.Instance?.SendTerraform((byte)DeformableTerrain.TerraOp.Dump, at, 1.1f, 0.3f, 0);
            Fx.Smoke(at + Vector3.up * 0.6f, Vector3.up * 0.6f - wall, 0.6f, new Color(0.45f, 0.36f, 0.26f, 0.8f), 2.5f);
            MadMax.Audio.Sfx.Play("collapse", at, 0.6f, 1.2f, 30f);
            Vitals.Hurt(6f, "CRASH");
            Toast("THE PIT WALL SLUMPS IN! PROP IT WITH TIMBER (MINE PROP)");
        }

        // ------------------------------------------------------------------ hooks

        partial void MedMineUpdate() => UpdateSlumps(Time.deltaTime);

        partial void MedMineNewGame()
        {
            goldWorked.Clear();
            GoldFines = 0f; pansTaken = 0;
        }

        partial void MedMineSave(SaveData d)
        {
            var l = d.blockMedMine;
            l.Clear();
            l.Add("fines=" + GoldFines.ToString("0.####", CultureInfo.InvariantCulture));
            l.Add("pans=" + pansTaken);
            foreach (var kv in goldWorked) l.Add("gold=" + kv.Key.x + "," + kv.Key.y + "," + kv.Value);
        }

        partial void MedMineLoad(SaveData d)
        {
            goldWorked.Clear();
            GoldFines = 0f; pansTaken = 0;
            if (d.blockMedMine == null) return;
            foreach (var e in d.blockMedMine)
            {
                int eq = e.IndexOf('=');
                if (eq < 0) continue;
                string key = e.Substring(0, eq), val = e.Substring(eq + 1);
                switch (key)
                {
                    case "fines": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var f); GoldFines = Mathf.Clamp01(f); break;
                    case "pans": int.TryParse(val, out pansTaken); break;
                    case "gold":
                        var p = val.Split(',');
                        if (p.Length == 3 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int z) && int.TryParse(p[2], out int n)) goldWorked[new Vector2Int(x, z)] = n;
                        break;
                }
            }
        }
    }
}
