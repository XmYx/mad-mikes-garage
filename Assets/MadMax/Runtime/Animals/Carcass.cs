using System.Collections.Generic;
using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>A dead animal (roadmap 23): [E] butchers it into meat, hide, bone, feathers and the odd trophy. A blade
    /// in the pack (knife, machete, leaf-spring blade, axe, spear) is needed for the hide and trophies and gets the
    /// full yield; Survival raises it. After a day and a half the meat has turned; after three days only the
    /// vultures remember it.</summary>
    public class Carcass : MonoBehaviour, IInteractable
    {
        public static readonly List<Carcass> All = new List<Carcass>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        static readonly string[] Blades = { "tool_knife", "tool_machete", "tool_leaf_blade", "tool_axe", "tool_spear" };

        [System.NonSerialized] public AnimalDef def;
        public float diedAt, size = 1f;
        public bool butchered;
        float checkT;

        public bool Fresh => DayNight.TotalDays - diedAt < 1.5f;

        public void Init(AnimalDef d, float s) { def = d; size = s; diedAt = DayNight.TotalDays; }

        void OnEnable() { All.Add(this); PartFunctions.Interactables.Add(this); }
        void OnDisable() { All.Remove(this); PartFunctions.Interactables.Remove(this); }

        static string Blade(WastelandGame g)
        {
            foreach (var b in Blades) if (g.Inventory.GetItem(b) > 0) return b;
            return null;
        }

        public string Prompt(WastelandGame g)
        {
            if (butchered || def == null || g.Current) return null;
            return "[E] BUTCHER THE " + def.name + (Blade(g) == null ? " (NO BLADE: MEAT ONLY)" : "") + (Fresh ? "" : " (IT STINKS)");
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (secondary || butchered || def == null) return;
            var blade = Blade(g);
            float yield = size * (0.75f + g.Stats.Level(Skill.Survival) * 0.06f) * (blade != null ? 1f : 0.5f) * GameRules.Current.yield;
            var names = new List<string>();
            foreach (var (id, min, max) in def.drops)
            {
                if (blade == null && (id.StartsWith("res:") || id.StartsWith("trophy_"))) continue;
                int n = Mathf.RoundToInt(Random.Range(min, max + 1) * yield);
                if (id.StartsWith("trophy_")) n = Mathf.Min(1, n);
                if (n <= 0) continue;
                string item = !Fresh && id == "food_meat_raw" ? "food_rotten" : id;
                if (item.StartsWith("res:")) { var t = (ResourceType)int.Parse(item.Substring(4)); g.Inventory.Add(t, n); names.Add(n + " " + ResourceInfo.Name(t)); }
                else { g.Inventory.AddItem(item, n); names.Add((n > 1 ? n + " " : "") + ItemCatalog.Name(item)); }
            }
            butchered = true;
            if (blade != null) g.WearTool(blade, 0.01f);
            g.Stats.Practice(Skill.Survival, 3f);
            BloodStains.Splash(transform.position, 0.8f);
            MadMax.Audio.Sfx.Play("dig", transform.position, 0.6f, 1.2f);
            g.Toast(names.Count > 0 ? "BUTCHERED: " + string.Join(", ", names) : "NOTHING WORTH TAKING");
            Destroy(gameObject, 0.5f);
        }

        void Update()
        {
            if ((checkT -= Time.deltaTime) > 0f) return;
            checkT = 5f;
            if (DayNight.TotalDays - diedAt > 3f) Destroy(gameObject);
        }
    }
}
