using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.RPG
{
    public enum Wound { Bruise, Scratch, Laceration, DeepWound, Fracture, Burn, Strain }   // append only (Strain: a back given out under a load)
    public enum BodyZone { Head, Torso, ArmL, ArmR, HandL, HandR, LegL, LegR, FootL, FootR }

    /// <summary>One injury on one body zone. Bleeding wounds need a bandage, fractures a splint; dirty or
    /// undisinfected wounds can get infected. Heals over time (faster when treated and fed).</summary>
    [Serializable]
    public class Injury
    {
        public BodyZone zone;
        public Wound type;
        public float severity = 1f;          // 1 fresh .. 0 healed
        public bool bandaged, splinted, disinfected;
        /// <summary>A fragment or bullet lodged in the wound (blasts, gunshots): it won't close past half and slowly
        /// festers until it is cut out at a surgery table (depth stage G).</summary>
        public bool shrapnel;
        public float bandageAge;             // seconds since the bandage went on (dirty after 10 min)
        public float infection;              // 0..1

        public bool Bleeding => !bandaged && (type == Wound.Laceration || type == Wound.DeepWound || (type == Wound.Scratch && severity > 0.6f));
        public float BleedRate => !Bleeding ? 0f : type == Wound.DeepWound ? 0.45f : type == Wound.Laceration ? 0.2f : 0.05f;
        public bool BandageDirty => bandaged && bandageAge > 600f;
        /// <summary>Broken skin: can bleed, be bandaged, get infected (not bruises, fractures or a strained back).</summary>
        public bool Open => type != Wound.Bruise && type != Wound.Fracture && type != Wound.Strain;

        public static readonly string[] ZoneNames = { "HEAD", "TORSO", "LEFT ARM", "RIGHT ARM", "LEFT HAND", "RIGHT HAND", "LEFT LEG", "RIGHT LEG", "LEFT FOOT", "RIGHT FOOT" };
        public static readonly string[] WoundNames = { "BRUISE", "SCRATCH", "LACERATION", "DEEP WOUND", "FRACTURE", "BURN", "STRAINED BACK" };

        /// <summary>How far a wound with <see cref="shrapnel"/> in it can heal (severity never drops below this).</summary>
        public const float ShrapnelFloor = 0.45f;

        /// <summary>Minutes to heal fully when properly treated.</summary>
        public float HealMinutes => type switch { Wound.Bruise => 3f, Wound.Scratch => 5f, Wound.Laceration => 14f, Wound.DeepWound => 28f, Wound.Fracture => 40f, Wound.Strain => 20f, _ => 18f };

        public string Status
        {
            get
            {
                var s = new List<string>();
                if (Bleeding) s.Add("BLEEDING");
                if (bandaged) s.Add(BandageDirty ? "DIRTY BANDAGE" : "BANDAGED");
                if (splinted) s.Add("SPLINTED");
                if (shrapnel) s.Add("SHRAPNEL");
                if (type == Wound.Fracture && !splinted) s.Add("NEEDS SPLINT");
                if (infection > 0.05f) s.Add("INFECTED " + Mathf.RoundToInt(infection * 100) + "%");
                return s.Count == 0 ? Mathf.RoundToInt((1f - severity) * 100) + "% HEALED" : string.Join(" ", s);
            }
        }

        /// <summary>Advance healing, bleeding and infection. Returns health lost this step.</summary>
        public float Tick(float dt, float hygiene, float nutrition)
        {
            float loss = BleedRate * dt;
            if (bandaged) bandageAge += dt;
            bool open = Open;
            if (open && !disinfected && (hygiene < 50f || BandageDirty || !bandaged))
                infection = Mathf.Min(1f, infection + dt / 900f * (hygiene < 25f ? 2f : 1f) * (bandaged && !BandageDirty ? 0.4f : 1f));
            else if (shrapnel) infection = Mathf.Min(1f, infection + dt / 3600f);             // a lodged fragment festers under any dressing
            else infection = Mathf.Max(0f, infection - dt / 600f);
            if (infection > 0.5f) loss += (infection - 0.5f) * 0.3f * dt;
            bool canHeal = type != Wound.Fracture || splinted;
            float rate = canHeal ? (bandaged || type == Wound.Bruise || type == Wound.Scratch || type == Wound.Strain ? 1f : 0.35f) : 0f;
            rate *= Mathf.Lerp(0.3f, 1f, nutrition) * (infection > 0.3f ? 0.2f : 1f);
            severity = Mathf.Max(shrapnel ? ShrapnelFloor : 0f, severity - dt * rate / (HealMinutes * 60f));
            return loss;
        }
    }

    public static class InjuryRules
    {
        static readonly BodyZone[] Upper = { BodyZone.Head, BodyZone.Torso, BodyZone.Torso, BodyZone.ArmL, BodyZone.ArmR, BodyZone.HandL, BodyZone.HandR };
        static readonly BodyZone[] Lower = { BodyZone.LegL, BodyZone.LegR, BodyZone.FootL, BodyZone.FootR };

        /// <summary>Turn a damage event into wounds (crashes, falls, burns). Other causes (hunger, radiation...) leave none.</summary>
        public static void Apply(List<Injury> list, float amount, string cause, System.Random rnd, Func<BodyZone, float> protect = null, Action<BodyZone> deflected = null)
        {
            if (amount < 2f) return;
            // armour on the zone: often stops the wound outright, else makes it lighter
            void Put(Injury inj)
            {
                float p = protect != null ? Mathf.Clamp01(protect(inj.zone)) : 0f;
                if (p > 0f)
                {
                    double r = rnd.NextDouble();
                    if (r < p * 0.6) { deflected?.Invoke(inj.zone); return; }
                    if (r < p)
                    {
                        inj.type = inj.type switch { Wound.DeepWound => Wound.Laceration, Wound.Laceration => Wound.Bruise, Wound.Fracture => Wound.Bruise, Wound.Scratch => Wound.Bruise, _ => inj.type };
                        inj.severity *= 1f - p * 0.5f;
                        if (inj.type == Wound.Bruise) inj.shrapnel = false;
                        deflected?.Invoke(inj.zone);
                    }
                }
                list.Add(inj);
            }
            switch (cause)
            {
                case "CRASH":
                {
                    int n = amount > 25f ? 2 : 1;
                    for (int i = 0; i < n; i++)
                    {
                        var z = Upper[rnd.Next(Upper.Length)];
                        var w = amount > 30f && rnd.NextDouble() < 0.35 ? Wound.Fracture : amount > 18f ? (rnd.NextDouble() < 0.5 ? Wound.DeepWound : Wound.Laceration) : amount > 8f ? Wound.Laceration : (rnd.NextDouble() < 0.5 ? Wound.Scratch : Wound.Bruise);
                        if (w == Wound.Fracture && (z == BodyZone.Head || z == BodyZone.Torso)) w = Wound.DeepWound;
                        Put(new Injury { zone = z, type = w });
                    }
                    break;
                }
                case "FALL":
                {
                    var z = Lower[rnd.Next(Lower.Length)];
                    Put(new Injury { zone = z, type = amount > 22f ? Wound.Fracture : amount > 10f ? Wound.Laceration : Wound.Bruise });
                    break;
                }
                case "MELEE":
                {
                    var z = Upper[rnd.Next(Upper.Length)];
                    Put(new Injury { zone = z, type = amount > 12f ? Wound.Laceration : rnd.NextDouble() < 0.5 ? Wound.Bruise : Wound.Scratch });
                    break;
                }
                case "BITE":
                {
                    // teeth and tusks: legs and arms, torn skin that bleeds (and gets dirty)
                    var zones = new[] { BodyZone.LegL, BodyZone.LegR, BodyZone.ArmL, BodyZone.ArmR, BodyZone.HandL, BodyZone.HandR };
                    Put(new Injury { zone = zones[rnd.Next(zones.Length)], type = amount > 14f ? Wound.DeepWound : amount > 6f ? Wound.Laceration : Wound.Scratch });
                    break;
                }
                case "SHOT":
                {
                    int n = amount > 15f ? 2 : 1;
                    for (int i = 0; i < n; i++)
                    {
                        var all = (BodyZone[])Enum.GetValues(typeof(BodyZone));
                        Put(new Injury { zone = all[rnd.Next(all.Length)], type = amount > 18f ? Wound.DeepWound : Wound.Laceration, shrapnel = amount > 18f && rnd.NextDouble() < 0.4 });   // a lodged bullet
                    }
                    break;
                }
                case "BLAST":
                {
                    // shrapnel and the shock wave: cuts, a burn, and on a big one a broken limb
                    var all = (BodyZone[])Enum.GetValues(typeof(BodyZone));
                    int n = amount > 30f ? 3 : amount > 12f ? 2 : 1;
                    for (int i = 0; i < n; i++)
                    {
                        var z = all[rnd.Next(all.Length)];
                        var w = amount > 35f && rnd.NextDouble() < 0.4 && z != BodyZone.Head && z != BodyZone.Torso ? Wound.Fracture : i == 0 ? Wound.Burn : Wound.Laceration;
                        Put(new Injury { zone = z, type = w, severity = w == Wound.Burn ? 0.7f : 1f, shrapnel = w == Wound.Laceration && rnd.NextDouble() < 0.5 });
                    }
                    break;
                }
                case "SHOCK":
                {
                    // the current goes in at a hand
                    Put(new Injury { zone = rnd.NextDouble() < 0.5 ? BodyZone.HandL : BodyZone.HandR, type = Wound.Burn, severity = 0.5f });
                    break;
                }
                case "BURNED":
                {
                    var all = (BodyZone[])Enum.GetValues(typeof(BodyZone));
                    var z = all[rnd.Next(all.Length)];
                    var existing = list.Find(x => x.zone == z && x.type == Wound.Burn);
                    if (existing != null) existing.severity = Mathf.Min(1f, existing.severity + 0.2f);
                    else Put(new Injury { zone = z, type = Wound.Burn, severity = 0.6f });
                    break;
                }
            }
        }
    }
}
