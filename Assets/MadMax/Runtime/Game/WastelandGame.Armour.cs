using System.Collections.Generic;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Body armour (roadmap 12): garments with <see cref="ClothingDef.armor"/> protect the injury zones they
    /// cover against melee, shots, crashes, falls and burns — less damage, and wounds on those zones are often stopped
    /// or made lighter (InjuryRules). Protection fades with the garment's condition; blows it takes wear it. Armour
    /// is heavy (weight counts in the pack) and metal clanks: moving makes noise NPCs hear.</summary>
    public partial class WastelandGame
    {
        static readonly Dictionary<(string, int), float> coverCache = new Dictionary<(string, int), float>();
        float armourNoiseT, deflectToastT;

        public static int KindOf(string cause) => cause switch
        {
            "MELEE" => (int)DamageKind.Melee, "SHOT" => (int)DamageKind.Shot, "BLAST" => (int)DamageKind.Shot,
            "CRASH" => (int)DamageKind.Crash, "FALL" => (int)DamageKind.Fall, "BURNED" => (int)DamageKind.Burn, _ => -1
        };

        /// <summary>Share of an injury zone a garment covers (0..1).</summary>
        public static float Cover(ClothingDef d, BodyZone z)
        {
            if (coverCache.TryGetValue((d.id, (int)z), out var c)) return c;
            var parts = PartsOf(z);
            float sum = 0f;
            foreach (var p in parts) if (d.coverage.TryGetValue(p, out var r)) sum += Mathf.Clamp01(r.y - r.x);
            return coverCache[(d.id, (int)z)] = sum / parts.Length;
        }

        /// <summary>How much of a blow of <paramref name="kind"/> on zone <paramref name="z"/> the outfit stops (0..0.9).</summary>
        public float Protection(BodyZone z, int kind)
        {
            if (kind < 0 || !Player || !Player.Rig) return 0f;
            float pass = 1f;
            foreach (var id in Player.Rig.outfit)
            {
                var d = ClothingLibrary.Get(id);
                if (d?.armor == null) continue;
                pass *= 1f - d.armor[kind] * Cover(d, z) * (0.4f + 0.6f * GarmentCondition(d.id));
            }
            return Mathf.Min(0.9f, 1f - pass);
        }

        static readonly BodyZone[] UpperZones = { BodyZone.Head, BodyZone.Torso, BodyZone.Torso, BodyZone.ArmL, BodyZone.ArmR, BodyZone.HandL, BodyZone.HandR };
        static readonly BodyZone[] LowerZones = { BodyZone.LegL, BodyZone.LegR, BodyZone.FootL, BodyZone.FootR };
        static readonly BodyZone[] AllZones = (BodyZone[])System.Enum.GetValues(typeof(BodyZone));

        /// <summary>Damage multiplier from armour for a cause (averaged over the zones such blows land on).</summary>
        public float ArmourFactor(string cause)
        {
            int k = KindOf(cause);
            if (k < 0) return 1f;
            var zones = cause == "FALL" ? LowerZones : cause == "MELEE" || cause == "CRASH" ? UpperZones : AllZones;
            float p = 0f;
            foreach (var z in zones) p += Protection(z, k);
            return 1f - 0.7f * p / zones.Length;
        }

        /// <summary>The armour on a zone caught a wound: it takes the wear instead.</summary>
        void Deflected(BodyZone z, float amount)
        {
            TearClothes(z, amount / 90f);
            if (Time.time > deflectToastT) { deflectToastT = Time.time + 4f; Toast("YOUR ARMOUR TOOK THE BLOW (" + Injury.ZoneNames[(int)z] + ")"); }
            MadMax.Audio.Sfx.Play("hit_metal", Player.transform.position + Vector3.up, 0.5f, 1.3f);
        }

        /// <summary>Clank of the worn armour (0..1+).</summary>
        public float ArmourNoise { get { float n = 0f; foreach (var d in Worn()) n += d.noise; return n; } }

        void UpdateArmourNoise(float dt)
        {
            float noise = Mathf.Min(1f, ArmourNoise);
            if (noise < 0.05f || Current || !Player || Player.Velocity.sqrMagnitude < 1f) return;
            if ((armourNoiseT -= dt) > 0f) return;
            armourNoiseT = 0.7f;
            float radius = (4f + noise * 12f) * (Player.run ? 1.8f : 1f);
            MadMax.Npc.NpcDirector.Instance?.Noise(Player.transform.position, radius);
            if (Player.run && noise > 0.3f) MadMax.Audio.Sfx.Play("chain", Player.transform.position + Vector3.up, Mathf.Min(0.35f, noise * 0.3f), 1.5f, 15f, 0.4f);
        }

        /// <summary>Armour on an NPC (raiders): share of a hit it stops, from what they wear.</summary>
        public static float NpcProtection(List<string> outfit, int kind)
        {
            float pass = 1f;
            foreach (var id in outfit)
            {
                var d = ClothingLibrary.Get(id);
                if (d?.armor == null) continue;
                float cover = (Cover(d, BodyZone.Torso) * 3f + Cover(d, BodyZone.Head) * 1.5f + Cover(d, BodyZone.ArmL) + Cover(d, BodyZone.ArmR) + Cover(d, BodyZone.LegL) + Cover(d, BodyZone.LegR)) / 8.5f;
                pass *= 1f - d.armor[kind] * cover;
            }
            return 1f - pass;
        }
    }
}
