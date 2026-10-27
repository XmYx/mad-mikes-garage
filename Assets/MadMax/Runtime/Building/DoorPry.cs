using System.Collections.Generic;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Crowbar work on a locked door, locker or chest (added on the first pry): the bar is set in the gap and
    /// levered for <see cref="LeverSeconds"/> — the leaf shudders against its frame, metal throws sparks off the bar, wood
    /// splinters — then the lock snaps and the door jolts open past its stop and settles, or it holds and only knocks
    /// back. The player leans on the bar while it lasts (walking off or putting the bar away gives up).</summary>
    public class DoorPry : MonoBehaviour
    {
        public const float LeverSeconds = 1.3f, ShudderEvery = 0.2f, GiveUpReach = 2.4f;

        // ---- counters for tests
        public static int Levers, Snaps, Holds, SparkBursts, SplinterBursts;
        /// <summary>"SNAPPED" / "HELD" / "GAVE UP" for the last lever.</summary>
        public static string LastResult;
        /// <summary>Automation: true / false forces the next lever's outcome (cleared when used).</summary>
        public static bool? ForceOutcome;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Levers = Snaps = Holds = SparkBursts = SplinterBursts = 0; LastResult = null; ForceOutcome = null; chunks.Clear(); }

        static readonly List<DebrisSystem.Chunk> chunks = new List<DebrisSystem.Chunk>(16);

        Door door;
        Placeable piece;
        bool metal, posed;
        float t = -1f, nextShudder;

        /// <summary>A lever under way.</summary>
        public bool Active => t >= 0f;
        /// <summary>0..1 of the lever.</summary>
        public float Progress => t < 0f ? 0f : Mathf.Clamp01(t / LeverSeconds);
        public bool Metal => metal;

        /// <summary>Set the bar in this door's gap (a crowbar swing next to it). False when already levering.</summary>
        public static bool Begin(Door d)
        {
            if (!d) return false;
            if (!d.TryGetComponent<DoorPry>(out var p)) p = d.gameObject.AddComponent<DoorPry>();
            if (p.Active) return false;
            p.door = d;
            p.piece = d.GetComponent<Placeable>();
            string id = p.piece ? p.piece.id : "";
            p.metal = id != null && (id.Contains("metal") || id.Contains("locker") || id.Contains("steel"));
            p.t = 0f; p.nextShudder = 0.1f;
            Levers++;
            var at = p.Gap;
            MadMax.Audio.Sfx.Play("creak", at, 0.8f, p.metal ? 0.75f : 1.05f, 35f);
            MadMax.Audio.Sfx.Play(p.metal ? "scratch" : "hit_wood", at, 0.6f, p.metal ? 1.3f : 0.8f, 30f);
            return true;
        }

        /// <summary>Where the bar bites: the latch edge of a hinged leaf (the far side from the hinge, at hand height),
        /// else the middle of the piece.</summary>
        public Vector3 Gap
        {
            get
            {
                if (door && door.enabled) return transform.TransformPoint(-door.hingeX * 0.92f, 1.05f, 0f);
                var r = GetComponentInChildren<Renderer>();
                return r ? r.bounds.center : transform.position + Vector3.up * 0.5f;
            }
        }

        /// <summary>The bar's push direction out of the leaf (toward the one levering it).</summary>
        Vector3 Out(MadMax.Game.PlayerCharacter pl)
        {
            var o = pl ? pl.transform.position + Vector3.up * 1.1f - Gap : transform.forward;
            o.y = 0f;
            return o.sqrMagnitude > 1e-4f ? o.normalized : Vector3.forward;
        }

        void Update()
        {
            if (t < 0f) return;
            var g = MadMax.Game.WastelandGame.Instance;
            var pl = g ? g.Player : null;
            if (!g || !pl || !door || !(pl.Tool is MadMax.Game.MeleeTool m && m.pries) || g.Current
                || Vector3.Distance(pl.transform.position, Gap) > GiveUpReach || !door.locked)
            {
                End(pl, "GAVE UP");
                return;
            }
            t += Time.deltaTime;
            float u = Progress;
            if (!pl.PoseOverride.HasValue || posed)
            {
                // both hands on the bar, leaning back harder as it goes
                pl.PoseOverride = new MadMax.Game.ToolPose
                {
                    chestX = 22f - 30f * u, chestY = -8f, armRX = -58f + 10f * u, armRZ = 6f, foreR = -38f + 14f * u,
                    armLX = -54f + 10f * u, armLZ = 18f, foreL = -44f + 14f * u, knees = 24f + 8f * Mathf.Sin(u * 20f), handRZ = 30f
                };
                pl.ActionClip = null;
                posed = true;
            }
            if (t >= nextShudder)
            {
                nextShudder = t + ShudderEvery * Random.Range(0.8f, 1.25f);
                if (door.enabled) door.Jolt(Random.Range(10f, 22f) * (0.5f + u));
                Bits(Gap, Out(pl), 0.4f + u * 0.6f);
                if (metal && Random.value < 0.5f) MadMax.Audio.Sfx.Play("scratch", Gap, 0.45f, Random.Range(1.2f, 1.6f), 25f, 0.1f);
            }
            if (t < LeverSeconds) return;

            bool gave = ForceOutcome ?? Random.value < 0.25f + g.Stats.Attribute(MadMax.RPG.Attr.Strength) * 0.06f;
            ForceOutcome = null;
            var at = Gap;
            if (gave)
            {
                MadMax.Audio.Sfx.Play("pry_snap", at, 1f, metal ? 0.9f : 0.75f, 45f);
                MadMax.Audio.Sfx.Play(metal ? "hit_metal" : "hit_wood", at, 0.8f, 0.6f, 40f);
                Bits(at, Out(pl), 2.2f);
                if (door.enabled) door.BurstOpen(); else door.SetLocked(false);
                g.Toast("LOCK BROKEN");
                g.Stats.Practice(MadMax.RPG.Skill.Salvaging, 2f);
                MadMax.Npc.NpcDirector.Instance?.Noise(at, 30f);
                Snaps++;
                End(pl, "SNAPPED");
            }
            else
            {
                MadMax.Audio.Sfx.Play("lock", at, 0.8f, 0.8f, 30f);
                if (door.enabled) door.Jolt(-25f);
                Bits(at, Out(pl), 0.8f);
                g.Toast("THE DOOR HOLDS");
                MadMax.Npc.NpcDirector.Instance?.Noise(at, 18f);
                Holds++;
                End(pl, "HELD");
            }
        }

        void End(MadMax.Game.PlayerCharacter pl, string result)
        {
            t = -1f;
            LastResult = result;
            if (posed && pl) pl.PoseOverride = null;
            posed = false;
        }

        void OnDisable() { if (posed) End(MadMax.Game.WastelandGame.Instance ? MadMax.Game.WastelandGame.Instance.Player : null, "GAVE UP"); }

        void Bits(Vector3 at, Vector3 outDir, float amount) => Burst(at, outDir, metal, amount);

        /// <summary>What a crowbar tears off: sparks off steel, splinters off wood (pooled particles / debris cubes);
        /// <paramref name="amount"/> ~0.5 a scrape .. 2 the lock giving.</summary>
        public static void Burst(Vector3 at, Vector3 outDir, bool metal, float amount)
        {
            if (metal)
            {
                Fx.Sparks(at, outDir + Vector3.up * 0.3f, Mathf.RoundToInt(3 + 9 * amount), Pal.Amber);
                if (amount > 1.5f) Fx.Flash(at, Pal.Amber, 2.5f, 1.5f, 0.06f);
                SparkBursts++;
                return;
            }
            var deb = DebrisSystem.Instance;
            SplinterBursts++;
            if (!deb) return;
            chunks.Clear();
            int n = Mathf.RoundToInt(2 + 6 * amount);
            for (int i = 0; i < n; i++)
                chunks.Add(new DebrisSystem.Chunk { position = at + Random.insideUnitSphere * 0.06f, color = Pal.Wood[2 + (i % 3)] });
            deb.Emit(chunks, 0.025f, outDir * (0.8f + amount), 0.6f);
            if (amount > 1.5f) deb.EmitPuff(at, Pal.Wood[3], 0.12f, outDir * 0.4f + Vector3.up * 0.2f, 0.6f);
        }
    }
}
