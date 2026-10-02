using System.Collections.Generic;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Fishing rod (roadmap 10). A swing casts the float onto open water ahead (7 m + Survival); fish bite by
    /// lake (biome, toxic), bait, hour and weather. Click while the float dips to set the hook, then hold the button to
    /// reel: reeling and the fish's runs raise the line tension — at the top the line snaps, slack too long and the fish
    /// throws the hook, let it run too far and it is gone. A landed fish gives raw fish by weight, a record per species
    /// and a trophy when big. Clicking while waiting reels in (the bait usually survives).</summary>
    public class FishingRodTool : HandTool
    {
        public enum Phase { Idle, Flying, Waiting, Bite, Hooked }
        public static FishingRodTool Active { get; private set; }
        /// <summary>Bait chosen from the inventory (used first while there is any).</summary>
        public static string PreferredBait;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Active = null; PreferredBait = null; }

        public Transform tip;
        public Phase phase;
        public bool reel;                                     // reel button held (game input or automation)
        public string bait;                                   // on the hook
        public float Tension { get; private set; }
        public float LineOut { get; private set; }
        public string Status { get; private set; }
        public bool Busy => phase != Phase.Idle;

        FishDef fish;
        float fishKg, fishStamina, burst, burstT, slackT, biteUntil, biteRoll, flyT, depth;
        Vector3 bobber, castFrom, castTo, origin, runDir;
        int skill;
        bool iceHole;                       // fishing through a hole cut in a frozen lake
        float flyTime = 0.6f;
        Vector3 lastHole = new Vector3(1e9f, 0f, 0f);
        readonly List<FishDef> pool = new List<FishDef>();
        GameObject bobberGo;
        LineRenderer line;
        static Mesh bobberMesh;

        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance; var t = DeformableTerrain.Instance;
            if (!g || !t || phase != Phase.Idle) return;
            skill = g.Stats.Level(Skill.Survival);
            var fwd = user.transform.forward; fwd.y = 0f; fwd.Normalize();
            var from = user.transform.position;
            bool found = false;
            bool frozen = Frozen(t, from + fwd * 3f);
            for (float d = frozen ? 3.5f : 7f + skill * 0.5f; d >= (frozen ? 1.2f : 2.5f) && !found; d -= 0.5f)
            {
                var p = from + fwd * d;
                if (t.WaterDepth(p.x, p.z) > 0.25f) { castTo = new Vector3(p.x, t.WaterLevel(p.x, p.z), p.z); found = true; }
            }
            if (!found) { g.Toast(frozen ? "THE LAKE IS ICED OVER - STAND AT THE EDGE TO CUT A HOLE" : "CAST INTO OPEN WATER"); return; }
            iceHole = frozen && Frozen(t, castTo);
            bool newHole = iceHole && (Flat(castTo) - Flat(lastHole)).sqrMagnitude > 4f;
            if (iceHole) lastHole = castTo;
            flyTime = newHole ? 2.6f : 0.6f;
            bait = PreferredBait != null && g.Inventory.GetItem(PreferredBait) > 0 ? PreferredBait : null;
            if (bait == null) foreach (var b in FishLibrary.Baits) if (g.Inventory.GetItem(b) > 0) { bait = b; break; }
            if (bait != null) g.Inventory.TakeItem(bait);
            var biome = t.BiomeAt(castTo.x, castTo.z);
            var lake = t.World.LakeAt(castTo.x, castTo.z, out _);
            bool sea = lake == null && t.World.Ocean(castTo.x, castTo.z);
            FishLibrary.Pool(biome, lake != null ? lake.toxic : biome == Biome.Nuclear && !sea, pool, sea);
            depth = t.WaterDepth(castTo.x, castTo.z);
            origin = from; castFrom = TipPos(user); flyT = 0f;
            phase = Phase.Flying; Active = this;
            Status = newHole ? "CUTTING A HOLE IN THE ICE" : iceHole ? "DROPPING THE LINE" : "CASTING";
            MadMax.Audio.Sfx.Play(newHole ? "dig" : "click", castFrom, 0.4f, newHole ? 1.4f : 0.6f, 10f);
            string best = FishLibrary.InSeason(pool, Weather.Season);
            if (best.Length > 0) MadMax.Game.Hints.Show("fish_season" + Weather.Season, Weather.SeasonNames[Weather.Season & 3] + ": " + best + " ARE BITING");
        }

        void Update()
        {
            if (phase == Phase.Idle) return;
            var g = WastelandGame.Instance; var t = DeformableTerrain.Instance;
            var user = g ? g.Player : null;
            if (!g || !t || !user || g.Current || user.Tool != this) { Stop(false, null); return; }
            float dt = Time.deltaTime;
            var d = user.transform.position - origin; d.y = 0f;
            if (d.magnitude > 3f) { Stop(true, "REELED IN"); return; }                        // walked off with the rod
            switch (phase)
            {
                case Phase.Flying:
                    flyT += dt / flyTime;
                    if (flyTime > 1f)
                    {                                                                      // chopping: the bobber waits by the hole
                        bobber = castTo + Vector3.up * 0.02f;
                        if (Mathf.Repeat(flyT * 5f, 1f) < dt / flyTime * 5f) { Splash(castTo, 2); MadMax.Audio.Sfx.Play("dig", castTo, 0.35f, 1.5f, 12f, 0.2f); }
                    }
                    else bobber = Vector3.Lerp(castFrom, castTo, Mathf.Clamp01(flyT)) + Vector3.up * Mathf.Sin(Mathf.Clamp01(flyT) * Mathf.PI) * 2.2f;
                    if (flyT >= 1f)
                    {
                        bobber = castTo; phase = Phase.Waiting; biteRoll = 2f;
                        Splash(bobber, 4); MadMax.Audio.Sfx.Play("splash", bobber, 0.35f, 1.5f, 20f);
                        Status = (iceHole ? "ICE FISHING  " : "WAITING FOR A BITE  ") + FishLibrary.BaitName(bait);
                    }
                    break;
                case Phase.Waiting:
                    bobber.y = castTo.y + Mathf.Sin(Time.time * 2.2f) * 0.015f;
                    if ((biteRoll -= dt) <= 0f)
                    {
                        biteRoll = 1f;
                        var f = FishLibrary.Pick(pool, bait, DayNight.Hours, Weather.Raining, Weather.Temperature, out float total, Weather.Season, iceHole);
                        if (f != null && Random.value < total * 0.0045f * (1f + skill * 0.05f))
                        {
                            fish = f; phase = Phase.Bite; biteUntil = Time.time + 1.1f + skill * 0.05f;
                            MadMax.Audio.Sfx.Play("splash", bobber, 0.5f, 1.8f, 20f);
                            Status = "BITE! CLICK TO STRIKE";
                        }
                    }
                    break;
                case Phase.Bite:
                    bobber.y = castTo.y - 0.07f + Mathf.Sin(Time.time * 30f) * 0.03f;
                    if (Time.time > biteUntil)
                    {
                        fish = null; phase = Phase.Waiting; biteRoll = 3f;
                        if (bait != null && Random.value < 0.5f) { bait = null; Status = "IT STOLE THE BAIT  (CLICK TO REEL IN)"; }
                        else Status = "MISSED IT  " + FishLibrary.BaitName(bait);
                    }
                    break;
                case Phase.Hooked:
                    Fight(dt, g, user, t);
                    break;
            }
            if (phase != Phase.Idle) Draw(TipPos(user));
        }

        /// <summary>Fresh water skins over in a hard frost (the sea never does): casts become a hole by the edge.</summary>
        public static bool Frozen(DeformableTerrain t, Vector3 p) => Frozen(t, p, Weather.Temperature);

        public static bool Frozen(DeformableTerrain t, Vector3 p, float temperature) =>
            temperature <= IceBelow && t.WaterDepth(p.x, p.z) > 0f && !(t.World.LakeAt(p.x, p.z, out _) == null && t.World.Ocean(p.x, p.z));

        public const float IceBelow = -4f;

        /// <summary>Button pressed while the line is out: strike on a bite, reel in while waiting.</summary>
        public void Click()
        {
            var g = WastelandGame.Instance;
            if (phase == Phase.Bite) Hook(g);
            else if (phase == Phase.Waiting) Stop(true, "REELED IN");
        }

        void Hook(WastelandGame g)
        {
            fishKg = FishLibrary.RollKg(fish, depth, skill * 0.08f);
            fishStamina = 1f; slackT = 0f; burstT = 0f; Tension = 0.3f;
            LineOut = Vector3.Distance(Flat(bobber), Flat(g.Player.transform.position));
            phase = Phase.Hooked;
            Status = fish.junk ? "SNAGGED SOMETHING" : fishKg > fish.maxKg * 0.5f ? "SOMETHING BIG!" : "HOOKED!";
            MadMax.Audio.Sfx.Play("splash", bobber, 0.7f, 1f, 25f);
            Splash(bobber, 8);
        }

        void Fight(float dt, WastelandGame g, PlayerCharacter user, DeformableTerrain t)
        {
            var me = Flat(user.transform.position);
            var toFish = Flat(bobber) - me; toFish.y = 0f;
            if (toFish.sqrMagnitude < 0.01f) toFish = user.transform.forward;
            toFish.Normalize();
            if ((burstT -= dt) <= 0f)
            {
                burst = Random.value < 0.45f ? 1f : 0.2f;
                burstT = Random.Range(0.6f, 1.8f);
                runDir = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * toFish;
                if (burst > 0.5f && !fish.junk) Splash(bobber, 3);
            }
            float size = Mathf.Clamp01(fishKg / 12f);
            float pull = fish.strength * (0.35f + 0.65f * burst) * (0.4f + 0.6f * fishStamina) * (0.6f + 0.6f * size) * (1f - skill * 0.03f);
            if (reel)
            {
                Tension += (0.18f + pull * 0.8f) * dt;
                LineOut -= (1.6f + skill * 0.08f) * (1f - pull * 0.7f) * dt;
                fishStamina = Mathf.Max(0f, fishStamina - dt * 0.12f);
            }
            else
            {
                Tension -= (0.6f - pull * 0.5f) * dt;
                LineOut += pull * 1.4f * dt;                                                  // it takes line
                fishStamina = Mathf.Max(0f, fishStamina - dt * 0.05f);
            }
            Tension = Mathf.Clamp(Tension, 0f, 1f);
            slackT = Tension < 0.08f ? slackT + dt : 0f;
            if (Tension >= 1f) { g.WearTool(id, 0.04f); Stop(false, "THE LINE SNAPPED!"); return; }
            if (slackT > 2.5f) { Stop(false, fish.junk ? "IT CAME LOOSE" : "IT THREW THE HOOK"); return; }
            if (LineOut > 16f + skill) { Stop(false, "IT RAN OFF WITH THE LINE"); return; }
            // the float follows the fish at the line's length, swinging with its runs
            var dir = Vector3.Slerp(toFish, runDir, dt * pull * 2f).normalized;
            var p = me + dir * LineOut;
            if (LineOut <= 1.3f || t.WaterDepth(p.x, p.z) < 0.05f) { Land(g); return; }
            bobber = new Vector3(p.x, t.WaterLevel(p.x, p.z) - 0.05f * burst, p.z);
            Status = "REEL (HOLD)  TENSION " + Mathf.RoundToInt(Tension * 100) + "%  LINE " + LineOut.ToString("0.0") + " M";
        }

        void Land(WastelandGame g)
        {
            var at = g.Player.transform.position + Vector3.up;
            if (fish.junk)
            {
                if (fish.id == "boot") { g.Inventory.Add(ResourceType.Scrap, 1); g.Inventory.Add(ResourceType.Rubber, 1); }
                else if (fish.id == "can") g.Inventory.Add(ResourceType.Scrap, 1);
                else
                {
                    foreach (var (item, n) in LootTables.Roll("lockbox", new System.Random(Random.Range(0, int.MaxValue)), 1, g.Stats.Attribute(Attr.Perception)))
                    {
                        if (item.StartsWith("res:")) g.Inventory.Add((ResourceType)int.Parse(item.Substring(4)), n);
                        else g.Inventory.AddItem(item, n);
                    }
                    MadMax.Audio.Sfx.Play("chest", at, 0.7f, 1f);
                }
                g.Toast("FISHED OUT " + (fish.id == "boot" ? "AN " : "A ") + fish.name);
            }
            else
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(fishKg / 0.6f), 1, 12);
                g.Inventory.AddItem(fish.mutant ? "food_fish_glow" : "food_fish_raw", n);
                bool record = g.RecordFish(fish.id, fishKg);
                bool trophy = fishKg >= fish.maxKg * 0.6f;
                if (trophy) g.Inventory.AddItem(fish.mutant ? "trophy_fish_mutant" : "trophy_fish");
                g.Toast((record ? "NEW RECORD: " : "CAUGHT A ") + fishKg.ToString("0.0") + " KG " + fish.name + (trophy ? " (TROPHY)" : ""));
                MadMax.Audio.Sfx.Play(record ? "crowd_cheer" : "pickup", at, record ? 0.3f : 0.6f, 1f);
            }
            Splash(bobber, 10);
            g.Stats.Practice(Skill.Survival, 2f + fishKg * 0.5f);
            g.WearTool(id, 0.01f);
            bait = null;
            Stop(false, null);
        }

        void Stop(bool keepBait, string msg)
        {
            var g = WastelandGame.Instance;
            if (keepBait && bait != null && g && Random.value > 0.2f) g.Inventory.AddItem(bait);
            bait = null; fish = null; phase = Phase.Idle; Tension = 0f; reel = false; Status = null;
            if (Active == this) Active = null;
            if (bobberGo) bobberGo.SetActive(false);
            if (line) line.enabled = false;
            if (msg != null && g) g.Toast(msg);
        }

        void OnDisable() { if (phase != Phase.Idle) Stop(true, null); }
        void OnDestroy() { if (bobberGo) Destroy(bobberGo); }

        Vector3 TipPos(PlayerCharacter user) => tip ? tip.position : user.transform.position + Vector3.up * 1.6f + user.transform.forward;
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static void Splash(Vector3 p, int n)
        {
            for (int i = 0; i < n; i++)
                Fx.Smoke(p + Vector3.up * 0.05f, Random.insideUnitSphere * 0.8f + Vector3.up * 1.2f, 0.08f, new Color(0.75f, 0.85f, 0.95f, 0.7f), 0.5f);
        }

        void Draw(Vector3 tipP)
        {
            if (!bobberGo)
            {
                if (!bobberMesh)
                {
                    var vg = new MadMax.Voxel.VoxelGrid();
                    vg.Box(-1, 0, -1, 1, 0, 1, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Cream[3]));
                    vg.Box(-1, 1, -1, 1, 2, 1, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Crimson, 3));
                    vg.Set(0, 3, 0, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Black[1]));
                    vg.Bevel();
                    bobberMesh = MadMax.Voxel.VoxelMesher.Build(vg, "Bobber");
                }
                bobberGo = new GameObject("Bobber", typeof(MeshFilter), typeof(MeshRenderer));
                bobberGo.GetComponent<MeshFilter>().sharedMesh = bobberMesh;
                bobberGo.GetComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
                bobberGo.transform.localScale = Vector3.one * 0.6f;
                line = bobberGo.AddComponent<LineRenderer>();
                line.sharedMaterial = Fx.TransparentMaterial(null);
                line.widthMultiplier = 0.012f; line.positionCount = 10; line.useWorldSpace = true;
                line.startColor = line.endColor = new Color(0.85f, 0.85f, 0.8f, 0.8f);
            }
            bobberGo.SetActive(true); line.enabled = true;
            bobberGo.transform.position = bobber - Vector3.up * 0.03f;
            // the line sags when slack, straightens under tension
            float sag = (phase == Phase.Hooked ? 1f - Tension : 0.8f) * 0.08f * Vector3.Distance(tipP, bobber);
            for (int i = 0; i < 10; i++)
            {
                float u = i / 9f;
                line.SetPosition(i, Vector3.Lerp(tipP, bobber + Vector3.up * 0.06f, u) + Vector3.down * sag * 4f * u * (1f - u));
            }
        }

        public override ToolPose? IdlePose => phase == Phase.Idle ? (ToolPose?)null : new ToolPose
        {
            chestX = phase == Phase.Hooked ? -10f : 0f, armRX = phase == Phase.Hooked ? -100f : -72f, foreR = -18f,
            armLX = -62f, armLZ = 18f, foreL = -48f + (reel ? Mathf.Sin(Time.time * 18f) * 25f : 0f), knees = phase == Phase.Hooked ? 14f : 4f
        };
    }
}
