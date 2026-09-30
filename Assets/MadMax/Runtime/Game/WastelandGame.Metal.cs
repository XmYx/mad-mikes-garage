using System.Collections.Generic;
using MadMax.Animals;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Depth stage D (metalworking): fitting the machine-shop and forge kits at a tuning bench (one per slot; a
    /// new kit swaps the old one back into the pack) and horseshoes (a shod horse's gallop drains its stamina a third
    /// slower). Shod horses are saved in <c>SaveData.blockMetal</c> as "shod:&lt;animal key&gt;".</summary>
    public partial class WastelandGame
    {
        readonly HashSet<string> shodHorses = new HashSet<string>();

        public bool IsShod(Animal a) => a && shodHorses.Contains(a.key);

        partial void MetalUpdate()
        {
            var a = Animal.Mounted;
            if (a && a.NetSpeed > 6f && shodHorses.Contains(a.key)) a.stamina = Mathf.Min(100f, a.stamina + 3.6f * Time.deltaTime);   // 11/s → 7.4/s
        }

        partial void MetalSave(SaveData d)
        {
            if (d.blockMetal == null) d.blockMetal = new List<string>();
            foreach (var k in shodHorses) d.blockMetal.Add("shod:" + k);
        }

        partial void MetalLoad(SaveData d)
        {
            shodHorses.Clear();
            if (d.blockMetal == null) return;
            foreach (var s in d.blockMetal) if (s != null && s.StartsWith("shod:")) shodHorses.Add(s.Substring(5));
        }

        partial void MetalNewGame() => shodHorses.Clear();

        /// <summary>Use a set of horseshoes on the nearest owned riding animal within 3 m.</summary>
        void ShoeHorse()
        {
            if (Current) { Toast("GET OUT FIRST"); return; }
            Animal best = null; float bd = 3f;
            var me = Player.transform.position;
            foreach (var a in Animal.All)
            {
                if (!a || !a.Alive || !a.owned || a.Def == null || !a.Def.rideable || !a.Adult) continue;
                float d = Vector3.Distance(a.transform.position, me);
                if (d < bd) { bd = d; best = a; }
            }
            if (!best) { Toast("STAND BY YOUR HORSE TO SHOE IT"); return; }
            if (shodHorses.Contains(best.key)) { Toast("ALREADY SHOD"); return; }
            if (!Inventory.TakeItem(MetalItems.Horseshoes)) return;
            shodHorses.Add(best.key);
            MadMax.Audio.Sfx.Play("hit_metal", best.transform.position, 0.5f, 1.4f);
            Stats.Practice(MadMax.RPG.Skill.Crafting, 2f);
            Toast("SHOD: IT GALLOPS LONGER ON STEEL");
        }

        // ------------------------------------------------------------------ drivetrain and chassis kits (tuning bench)
        /// <summary>Tuning page value of a kit slot: what is fitted, and whether a kit for it is in the pack.</summary>
        public string KitValue(VehicleTuning t, VehicleTuning.KitSlot s)
        {
            int cur = t.Fitted(s.slot);
            string v = s.names[Mathf.Clamp(cur, 0, s.names.Length - 1)];
            return NextKit(t, s) > 0 ? v + " (E FIT)" : v;
        }

        /// <summary>The next kit option for a slot the pack holds (after the fitted one), or -1.</summary>
        int NextKit(VehicleTuning t, VehicleTuning.KitSlot s)
        {
            int cur = t.Fitted(s.slot);
            for (int k = 1; k < s.kits.Length; k++)
            {
                int i = (cur + k) % s.kits.Length;
                if (i > 0 && Inventory.GetItem(s.kits[i]) > 0) return i;
            }
            return -1;
        }

        /// <summary>Fit the next kit of a slot from the pack; the one it replaces comes back as an item.</summary>
        public bool FitMetalKit(VehicleTuning t, VehicleTuning.KitSlot s)
        {
            if (!t || s == null) return false;
            if (Stats.Level(MadMax.RPG.Skill.Mechanics) < s.mech) { Toast("NEEDS MECHANICS " + s.mech); return false; }
            int pick = NextKit(t, s);
            if (pick < 0) { Toast("NO " + s.label + " KIT IN THE PACK"); return false; }
            string why = t.CannotFit(s.slot, pick);
            if (why != null) { Toast(why); return false; }
            int cur = t.Fitted(s.slot);
            if (!Inventory.TakeItem(s.kits[pick])) return false;
            if (cur > 0 && s.kits[cur] != null) Inventory.AddItem(s.kits[cur]);
            t.SetFitted(s.slot, pick);
            t.Apply();
            MadMax.Audio.Sfx.Play("ratchet", t.transform.position, 0.8f, 0.9f);
            Stats.Practice(MadMax.RPG.Skill.Mechanics, 6f);
            Toast(s.label + ": " + s.names[pick] + " FITTED" + (cur > 0 ? " (" + s.names[cur] + " KIT BACK IN THE PACK)" : ""));
            return true;
        }
    }
}
