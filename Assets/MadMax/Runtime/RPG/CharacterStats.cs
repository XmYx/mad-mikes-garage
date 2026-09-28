using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.RPG
{
    public enum Attr { Strength, Endurance, Agility, Intelligence, Perception }
    public enum Skill { Driving, Mechanics, Salvaging, Construction, Crafting, Demolition, Melee, Firearms, Survival, Farming }

    /// <summary>A base trait picked at character creation. Positive traits cost points, negative ones refund them.</summary>
    public class TraitDef
    {
        public string id, name, description;
        public int cost;                                            // creation points (negative = gives points)
        public int[] attr = new int[5];                             // attribute deltas
        public Dictionary<Skill, float> learn = new Dictionary<Skill, float>();   // skill XP multipliers
        public Dictionary<Skill, int> startLevel = new Dictionary<Skill, int>();
    }

    public static class Traits
    {
        static List<TraitDef> all;
        public static IReadOnlyList<TraitDef> All => all ??= Build();
        public static TraitDef Get(string id) { foreach (var t in All) if (t.id == id) return t; return null; }

        static TraitDef T(string id, string name, int cost, string desc) => new TraitDef { id = id, name = name, cost = cost, description = desc };

        static List<TraitDef> Build()
        {
            var l = new List<TraitDef>();
            var t = T("gearhead", "GEARHEAD", 3, "MECHANICS STARTS AT 2, LEARNS FASTER"); t.startLevel[Skill.Mechanics] = 2; t.learn[Skill.Mechanics] = 1.5f; l.Add(t);
            t = T("scavenger", "SCAVENGER", 2, "SALVAGING +1, MORE SCRAP"); t.startLevel[Skill.Salvaging] = 1; t.learn[Skill.Salvaging] = 1.4f; t.attr[(int)Attr.Perception] = 1; l.Add(t);
            t = T("leadfoot", "LEAD FOOT", 2, "DRIVING STARTS AT 2"); t.startLevel[Skill.Driving] = 2; t.learn[Skill.Driving] = 1.3f; l.Add(t);
            t = T("brawler", "BRAWLER", 2, "MELEE +1, STRENGTH +1"); t.startLevel[Skill.Melee] = 1; t.attr[(int)Attr.Strength] = 1; l.Add(t);
            t = T("bookworm", "BOOKWORM", 2, "INTELLIGENCE +1, READS FASTER"); t.attr[(int)Attr.Intelligence] = 1; l.Add(t);
            t = T("tough", "TOUGH", 3, "ENDURANCE +2"); t.attr[(int)Attr.Endurance] = 2; l.Add(t);
            t = T("builder", "CARPENTER", 2, "CONSTRUCTION STARTS AT 2"); t.startLevel[Skill.Construction] = 2; t.learn[Skill.Construction] = 1.3f; l.Add(t);
            t = T("marksman", "MARKSMAN", 2, "FIREARMS STARTS AT 2"); t.startLevel[Skill.Firearms] = 2; t.learn[Skill.Firearms] = 1.3f; l.Add(t);
            t = T("fastlearner", "FAST LEARNER", 4, "ALL SKILLS LEARN 25% FASTER"); foreach (Skill s in Enum.GetValues(typeof(Skill))) t.learn[s] = 1.25f; l.Add(t);
            t = T("clumsy", "CLUMSY", -2, "AGILITY -1, TOOLS SLOWER"); t.attr[(int)Attr.Agility] = -1; l.Add(t);
            t = T("weakback", "WEAK BACK", -2, "STRENGTH -2, CARRIES LESS"); t.attr[(int)Attr.Strength] = -2; l.Add(t);
            t = T("slowreader", "SLOW READER", -1, "INTELLIGENCE -1"); t.attr[(int)Attr.Intelligence] = -1; l.Add(t);
            t = T("smoker", "SMOKER", -2, "ENDURANCE -2"); t.attr[(int)Attr.Endurance] = -2; l.Add(t);
            t = T("nearsighted", "NEARSIGHTED", -1, "PERCEPTION -2"); t.attr[(int)Attr.Perception] = -2; l.Add(t);
            return l;
        }
    }

    /// <summary>Player RPG state: attributes (1..10), practised skills (level 0..10 from XP), traits, learned knowledge,
    /// vitals. Everything grows by doing: <see cref="Practice"/> feeds skill XP and the attributes behind it.</summary>
    [Serializable]
    public class CharacterStats
    {
        public string name = "WANDERER";
        public int[] attributes = { 5, 5, 5, 5, 5 };
        public float[] attributeXp = new float[5];
        public float[] skillXp = new float[10];
        public List<string> traits = new List<string>();
        public List<string> knowledge = new List<string>();
        public List<string> consumed = new List<string>();         // media already studied (repeat = little XP)
        public float health = -1f, stamina = -1f;
        public float hunger = 80f, thirst = 80f, hygiene = 90f;      // 100 = full / clean
        public float sick;                                         // seconds of food poisoning left
        public float bodyTemp = 37f;                                 // core °C
        public List<Injury> injuries = new List<Injury>();

        /// <summary>Old saves carry shorter skill arrays.</summary>
        public void EnsureArrays()
        {
            if (skillXp == null || skillXp.Length < SkillCount) { var n = new float[SkillCount]; if (skillXp != null) Array.Copy(skillXp, n, skillXp.Length); skillXp = n; }
            if (consumed == null) consumed = new List<string>();
            if (injuries == null) injuries = new List<Injury>();
            if (bodyTemp < 20f) bodyTemp = 37f;
        }

        [NonSerialized] public float learningSpeed = 1f;            // game rule
        public static event Action<string> Notice;                   // "MECHANICS 3", "STRENGTH 6"

        public const int SkillCount = 10, AttrCount = 5, MaxLevel = 10;
        public static readonly string[] SkillNames = { "DRIVING", "MECHANICS", "SALVAGING", "CONSTRUCTION", "CRAFTING", "DEMOLITION", "MELEE", "FIREARMS", "SURVIVAL", "FARMING" };
        public static readonly string[] AttrNames = { "STRENGTH", "ENDURANCE", "AGILITY", "INTELLIGENCE", "PERCEPTION" };
        // which attribute grows alongside each skill
        static readonly Attr[] SkillAttr = { Attr.Agility, Attr.Intelligence, Attr.Perception, Attr.Strength, Attr.Intelligence, Attr.Strength, Attr.Strength, Attr.Perception, Attr.Endurance, Attr.Endurance };

        public static float XpForLevel(int level) => 40f * level * level;
        public int Level(Skill s) => Mathf.Min(MaxLevel, Mathf.FloorToInt(Mathf.Sqrt(skillXp[(int)s] / 40f)));
        public float LevelProgress(Skill s)
        {
            int l = Level(s);
            if (l >= MaxLevel) return 1f;
            return Mathf.InverseLerp(XpForLevel(l), XpForLevel(l + 1), skillXp[(int)s]);
        }

        public int Attribute(Attr a)
        {
            int v = attributes[(int)a];
            foreach (var id in traits) { var t = Traits.Get(id); if (t != null) v += t.attr[(int)a]; }
            return Mathf.Clamp(v, 1, MaxLevel);
        }

        public bool Knows(string k) => string.IsNullOrEmpty(k) || knowledge.Contains(k) || (k.StartsWith("read_") && consumed.Contains(k.Substring(5)));
        public void Learn(string k) { if (!string.IsNullOrEmpty(k) && !knowledge.Contains(k)) { knowledge.Add(k); Notice?.Invoke("LEARNED: " + k.Replace("k_", "").Replace('_', ' ').ToUpperInvariant()); } }

        /// <summary>Apply trait starting levels (once, at creation).</summary>
        public void ApplyTraitStart()
        {
            foreach (var id in traits)
            {
                var t = Traits.Get(id);
                if (t == null) continue;
                foreach (var kv in t.startLevel) skillXp[(int)kv.Key] = Mathf.Max(skillXp[(int)kv.Key], XpForLevel(kv.Value));
            }
            health = MaxHealth; stamina = MaxStamina;
        }

        /// <summary>Gain experience by doing. Intelligence and traits speed learning up.</summary>
        public void Practice(Skill s, float amount)
        {
            float mult = learningSpeed * (0.8f + Attribute(Attr.Intelligence) * 0.04f);
            foreach (var id in traits) { var t = Traits.Get(id); if (t != null && t.learn.TryGetValue(s, out var m)) mult *= m; }
            int before = Level(s);
            skillXp[(int)s] += amount * mult;
            int after = Level(s);
            if (after > before) Notice?.Invoke($"{SkillNames[(int)s]} {after}");
            // the attribute behind the skill grows slowly too
            var a = SkillAttr[(int)s];
            attributeXp[(int)a] += amount * 0.25f;
            float need = 60f * attributes[(int)a] * attributes[(int)a];
            if (attributes[(int)a] < MaxLevel && attributeXp[(int)a] >= need)
            {
                attributeXp[(int)a] -= need;
                attributes[(int)a]++;
                Notice?.Invoke($"{AttrNames[(int)a]} {Attribute(a)}");
            }
        }

        // ---- derived values used by gameplay
        public float MaxHealth => 70f + Attribute(Attr.Endurance) * 6f;
        public float MaxStamina => 60f + Attribute(Attr.Endurance) * 8f;
        public float MoveSpeed => 0.9f + Attribute(Attr.Agility) * 0.02f;
        public float ToolSpeed => 0.85f + (Attribute(Attr.Strength) + Attribute(Attr.Agility)) * 0.012f + (traits.Contains("clumsy") ? -0.1f : 0f);
        public float MeleePower => 0.8f + Attribute(Attr.Strength) * 0.04f + Level(Skill.Melee) * 0.04f;
        public float DemolitionPower => 0.8f + Attribute(Attr.Strength) * 0.03f + Level(Skill.Demolition) * 0.05f;
        public float SalvageYield => 0.8f + Level(Skill.Salvaging) * 0.08f + Attribute(Attr.Perception) * 0.03f + (traits.Contains("scavenger") ? 0.15f : 0f);
        public float BuildCostMult => Mathf.Max(0.6f, 1f - Level(Skill.Construction) * 0.04f);
        public float CraftCostMult => Mathf.Max(0.6f, 1f - Level(Skill.Crafting) * 0.04f);
        public float FuelEfficiency => Mathf.Max(0.7f, 1f - Level(Skill.Survival) * 0.03f);
        public float DrivingGrip => 1f + Level(Skill.Driving) * 0.012f;
        public float Spread => Mathf.Max(0.35f, 1f - Level(Skill.Firearms) * 0.065f);
        public float CarryCapacity => 40f + Attribute(Attr.Strength) * 6f;
        public float ReadingSpeed => 0.6f + Attribute(Attr.Intelligence) * 0.08f + (traits.Contains("bookworm") ? 0.3f : 0f);

        public static int Cost(int baseCost, float mult) => Mathf.Max(1, Mathf.CeilToInt(baseCost * mult));
    }
}
