using System.Collections;
using System.Linq;
using System.Reflection;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q4 (survival): hunger and thirst drain (thirst faster), eating and drinking restore them, food
    /// rots in the pack and hardly at all in a chilled ice box (every portion accounted for), rotten food makes you sick
    /// and pills cure it; cold air drives the felt temperature and the core down, warm clothes, a fire and a roof over
    /// your head each raise it; rain soaks you in the open (less in a waterproof layer), a roof lets you dry, and wet
    /// clothes chill; a radiation source hurts and a hazmat layer cuts the dose. Weather and the clock are pinned
    /// (disclosed); drains run on an accelerated survival clock, never a changed physics time scale.</summary>
    class SurvivalVitals : Scenario
    {
        public override string Id => "survival.vitals";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 180f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var s = g.Stats;
            var got = new Waited(); var pad = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 7f, got, pad);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            MedMineScenarios.Healthy(c, "vitals start from a clean slate");
            DayNight.SetHours(12f);
            WeatherPin.Set(false, 22f, 0f);
            c.Fixture("clock set to noon; weather held dry at 22 C");
            var me = g.Player.transform;

            // ---- hunger and thirst drain (survival clock x20)
            float rate0 = g.Rules.hungerRate;
            g.Rules.hungerRate = 20f;
            c.Fixture("hunger/thirst clock x20 for 3 s");
            s.hunger = 60f; s.thirst = 60f;
            float h0 = s.hunger, t0 = s.thirst, tt = Time.time;
            yield return SurvivalKit.GameSeconds(3f, 10);
            float span = Mathf.Max(0.01f, Time.time - tt);
            g.Rules.hungerRate = rate0;
            float hungerMin = (h0 - s.hunger) / span * 60f / 20f, thirstMin = (t0 - s.thirst) / span * 60f / 20f;
            c.Metric("hunger_per_game_min", hungerMin, "%");
            c.Metric("thirst_per_game_min", thirstMin, "%");
            c.Check(hungerMin > 1f && hungerMin < 4f, $"hunger drains about 2.2 %/min at normal rate ({hungerMin:0.00})");
            c.Check(thirstMin > hungerMin, $"thirst drains faster than hunger ({thirstMin:0.00} > {hungerMin:0.00})");

            // ---- eating and drinking
            var food = FoodLibrary.AllFood.Where(f => f.hunger >= 15f && f.sickChance <= 0f && f.rads <= 0f).OrderBy(f => f.id).FirstOrDefault();
            if (c.Check(food != null, "a safe meal exists in the food library"))
            {
                g.Inventory.AddItem(food.id, 1);
                c.Fixture("granted 1 " + food.id);
                s.hunger = 30f; s.sick = 0f;
                g.UseItem(food.id);
                c.Check(Mathf.Abs(s.hunger - Mathf.Min(100f, 30f + food.hunger)) < 0.6f && g.Inventory.GetItem(food.id) == 0, $"eating {food.name} restores {food.hunger:0} hunger (now {s.hunger:0}) and uses it up");
                c.Check(s.sick <= 0f, "a clean meal does not make you sick");
            }
            g.Inventory.AddItem(ItemIds.Canteen, 1); g.Inventory.Add(ResourceType.Water, 2);
            c.Fixture("granted a canteen and 2 L water");
            s.thirst = 40f;
            int water0 = g.Inventory.Get(ResourceType.Water);
            g.UseItem(ItemIds.Canteen);
            c.Check(s.thirst >= 84f && g.Inventory.Get(ResourceType.Water) == water0 - 1, $"a drink from the canteen: thirst 40 -> {s.thirst:0}, one litre used");

            // ---- spoilage: pack against a chilled ice box, every portion accounted for
            var perishable = FoodLibrary.AllFood.Where(f => f.spoilMinutes > 0f && f.id != "food_rotten").OrderBy(f => f.spoilMinutes).FirstOrDefault();
            var box = SurvivalKit.Piece(g, "ice_box", me.position + me.right * 2f, -me.right);
            yield return null;
            var cont = box ? box.GetComponent<Container>() : null; var cold = box ? box.GetComponent<ColdStore>() : null;
            var spoil = typeof(WastelandGame).GetMethod("Spoil", BindingFlags.NonPublic | BindingFlags.Instance);
            if (perishable == null || !cont || !cold || spoil == null) c.Block("spoilage fixture missing (perishable food, ice box, or the spoil tick)");
            else
            {
                cont.inventory.AddItem(ColdStore.Ice, 3); cold.temp = 3f;
                var stash = g.Inventory.Items.Where(kv => kv.Value > 0 && FoodLibrary.Get(kv.Key) is FoodDef fd && fd.spoilMinutes > 0f).ToList();
                foreach (var kv in stash) g.Inventory.TakeItem(kv.Key, kv.Value);
                if (stash.Count > 0) c.Fixture("other perishables set aside: " + string.Join(", ", stash.Select(kv => kv.Value + " " + kv.Key)));
                int rot0 = g.Inventory.GetItem("food_rotten"), rotBox0 = cont.inventory.GetItem("food_rotten");
                g.Inventory.AddItem(perishable.id, 20); cont.inventory.AddItem(perishable.id, 20);
                c.Fixture($"20 {perishable.id} in the pack and 20 in an ice box chilled with ice; {perishable.spoilMinutes * 0.5f:0} game minutes of spoilage simulated at once");
                c.Check(cont.SpoilFactor < 0.2f, $"the iced box keeps food cold (spoil factor {cont.SpoilFactor:0.00})");
                spoil.Invoke(g, new object[] { perishable.spoilMinutes * 60f * 0.5f });
                int packFresh = g.Inventory.GetItem(perishable.id), packRot = g.Inventory.GetItem("food_rotten") - rot0;
                int boxFresh = cont.inventory.GetItem(perishable.id), boxRot = cont.inventory.GetItem("food_rotten") - rotBox0;
                c.Metric("rotted_in_pack", packRot, "of 20"); c.Metric("rotted_in_icebox", boxRot, "of 20");
                c.Check(packRot >= 8, $"half the shelf life in the pack rots about half ({packRot} of 20)");
                c.Check(boxRot <= 3 && boxRot < packRot, $"the ice box keeps it far better ({boxRot} of 20)");
                c.Check(packFresh + packRot == 20 && boxFresh + boxRot == 20, "every portion is either fresh or rotten (none lost)");
                g.Inventory.TakeItem(perishable.id, packFresh);
                foreach (var kv in stash) g.Inventory.AddItem(kv.Key, kv.Value);
            }

            // ---- sickness: rotten food, then pills
            s.sick = 0f;
            int bites = 0;
            while (s.sick <= 0f && bites < 8) { g.Inventory.AddItem("food_rotten", 1); g.UseItem("food_rotten"); bites++; }
            c.Fixture(bites + " rotten portion(s) eaten until sick");
            if (c.Check(s.sick > 0f, $"rotten food makes you sick (after {bites})"))
            {
                float hp0 = s.health, sick0 = s.sick;
                yield return SurvivalKit.GameSeconds(1.5f);
                c.Check(s.sick < sick0 && s.health < hp0, $"sickness wears on (health {hp0:0.00} -> {s.health:0.00}, {s.sick:0} s left)");
                g.Inventory.AddItem(ItemIds.Pills, 1);
                g.UseItem(ItemIds.Pills);
                c.Check(s.sick <= 0f, "pills cure food poisoning");
            }
            MedMineScenarios.Healthy(c, "after the sickness check");

            // ---- temperature: cold air, clothes, a fire, a roof
            WeatherPin.Set(false, -8f, 0f);
            c.Fixture("air held at -8 C, dry");
            yield return SurvivalKit.GameSeconds(1.2f);
            float body0 = s.bodyTemp, felt0 = g.FeltTemperature;
            yield return SurvivalKit.GameSeconds(4f);
            c.Metric("felt_cold", felt0, "C");
            c.Check(felt0 < 12f, $"-8 C air feels cold through the starter clothes ({felt0:0.0} C felt)");
            c.Check(s.bodyTemp < body0, $"the core cools in the cold ({body0:0.000} -> {s.bodyTemp:0.000} C)");
            var warm = ClothingLibrary.All.Where(d => d.warmth > 0f && d.armor == null && !g.Player.Rig.outfit.Contains(d.id)).OrderByDescending(d => d.warmth).FirstOrDefault();
            float warmth0 = g.Insulation().warmth;
            string swapped = warm != null ? Dress(g, warm) : null;
            if (warm != null) c.Fixture("dressed in " + warm.id + (swapped != null ? " (instead of " + swapped + ")" : ""));
            yield return SurvivalKit.GameSeconds(0.3f);
            float felt1 = g.FeltTemperature;
            c.Check(warm != null && g.Insulation().warmth > warmth0 && felt1 > felt0 + 0.5f, $"warm clothes raise the felt temperature ({felt0:0.0} -> {felt1:0.0} C, warmth {warmth0:0.0} -> {g.Insulation().warmth:0.0})");
            var fire = Fire.Ignite(SurvivalKit.Ground(me.position + me.forward * 2.8f) + Vector3.up * 0.1f, null, 60f, 0.6f);
            c.Fixture("a fire lit 2.8 m away");
            yield return SurvivalKit.GameSeconds(0.5f);
            float felt2 = g.FeltTemperature;
            c.Check(fire && felt2 > felt1 + 1f, $"a fire nearby warms you ({felt1:0.0} -> {felt2:0.0} C)");
            if (fire) Object.Destroy(fire.gameObject);
            yield return SurvivalKit.GameSeconds(0.3f);
            felt1 = g.FeltTemperature;
            var roof = SurvivalKit.Piece(g, "roof_flat", me.position + Vector3.up * 2.9f, me.forward, false);
            c.Fixture("a roof panel 2.9 m overhead");
            var w = new Waited();
            yield return SurvivalKit.Until(() => g.Sheltered, 3f, w);
            yield return SurvivalKit.GameSeconds(0.2f);
            float felt3 = g.FeltTemperature;
            c.Check(w.ok, "the roof counts as shelter");
            c.Check(felt3 > felt1 + 0.5f, $"shelter takes the edge off the cold ({felt1:0.0} -> {felt3:0.0} C)");

            // ---- rain: soaking, waterproofing, drying under the roof, wet clothes chill
            if (roof) Object.Destroy(roof.gameObject);
            if (swapped != null || warm != null) Undress(g, warm, swapped);
            WeatherPin.Set(true, 3f);
            c.Fixture("rain held at 3 C");
            yield return SurvivalKit.Until(() => !g.Sheltered, 3f);
            s.wetness = 0f;
            float soakBare = 0f;
            { float w0 = s.wetness, tA = Time.time; yield return SurvivalKit.GameSeconds(4f); soakBare = (s.wetness - w0) / Mathf.Max(0.01f, Time.time - tA) * 60f; }
            c.Metric("soak_per_min_bare", soakBare, "");
            c.Check(soakBare > 0.1f, $"rain soaks you in the open ({soakBare:0.00}/min)");
            var coat = ClothingLibrary.All.Where(d => d.waterproof >= 0.3f).OrderByDescending(d => d.waterproof).FirstOrDefault();
            if (coat != null)
            {
                float wpBare = g.Waterproof;
                string off = Dress(g, coat);
                c.Fixture("put on " + coat.id);
                float wpCoat = g.Waterproof;
                s.wetness = 0f;
                float w0 = s.wetness, tA = Time.time; yield return SurvivalKit.GameSeconds(4f);
                float soakCoat = (s.wetness - w0) / Mathf.Max(0.01f, Time.time - tA) * 60f;
                c.Metric("soak_per_min_waterproof", soakCoat, "");
                float expect = (1f - wpCoat) / Mathf.Max(0.05f, 1f - wpBare);
                c.Check(wpCoat > wpBare && soakCoat < soakBare && soakCoat <= soakBare * expect * 1.15f + 0.01f, $"a waterproof layer keeps rain off ({soakCoat:0.00} vs {soakBare:0.00}/min, waterproof {wpBare:0.00} -> {wpCoat:0.00})");
                Undress(g, coat, off);
            }
            else c.Note("no waterproof garment in the library");
            s.wetness = 0f; yield return SurvivalKit.GameSeconds(0.2f);
            float feltDry = g.FeltTemperature;
            s.wetness = 0.8f; yield return SurvivalKit.GameSeconds(0.2f);
            float feltWet = g.FeltTemperature;
            c.Check(feltWet < feltDry - 1f, $"wet clothes chill ({feltDry:0.0} dry -> {feltWet:0.0} C soaked)");
            var roof2 = SurvivalKit.Piece(g, "roof_flat", me.position + Vector3.up * 2.9f, me.forward, false);
            yield return SurvivalKit.Until(() => g.Sheltered, 3f);
            s.wetness = 0.8f;
            { float w0 = s.wetness, tA = Time.time; yield return SurvivalKit.GameSeconds(4f); float dry = (w0 - s.wetness) / Mathf.Max(0.01f, Time.time - tA) * 60f; c.Metric("dry_per_min_sheltered", dry, ""); c.Check(dry > 0f, $"under a roof wet clothes dry out even in the rain ({dry:0.000}/min)"); }
            c.Screenshot("sheltered");
            yield return null;
            if (roof2) Object.Destroy(roof2.gameObject);
            s.wetness = 0f;

            // ---- radiation and a hazmat layer
            WeatherPin.Set(false, 20f, 0f);
            var src = new GameObject("TestRadiation").AddComponent<Hazard>();
            src.transform.position = me.position; src.radiation = 1f; src.radius = 6f;
            c.Fixture("a radiation source (1.0 at the centre, 6 m) where the player stands");
            yield return SurvivalKit.GameSeconds(0.5f);
            float rad0 = g.RadiationLevel, hpR = s.health;
            yield return SurvivalKit.GameSeconds(1.5f);
            c.Metric("radiation_bare", rad0, "");
            c.Check(rad0 > 0.1f && s.health < hpR, $"radiation registers and hurts ({rad0:0.00}, health {hpR:0.0} -> {s.health:0.0})");
            var suit = ClothingLibrary.All.Where(d => d.radiation > 0.2f).OrderByDescending(d => d.radiation).FirstOrDefault();
            if (c.Check(suit != null, "a radiation-shielding garment exists"))
            {
                string off = Dress(g, suit);
                c.Fixture("put on " + suit.id);
                yield return SurvivalKit.GameSeconds(0.3f);
                c.Metric("radiation_shielded", g.RadiationLevel, "");
                c.Check(g.RadiationLevel < rad0 * 0.8f, $"{suit.name} cuts the dose ({rad0:0.00} -> {g.RadiationLevel:0.00})");
                Undress(g, suit, off);
            }
            Object.Destroy(src.gameObject);
            WeatherPin.Release();
        }

        /// <summary>Put a garment on (granting the item), taking off what was in its slot; returns that garment's id.</summary>
        internal static string Dress(WastelandGame g, ClothingDef d)
        {
            var outfit = g.Player.Rig.outfit;
            string off = null;
            foreach (var id in outfit.ToList()) { var o = ClothingLibrary.Get(id); if (o != null && o.slot == d.slot) { off = id; outfit.Remove(id); } }
            if (g.Inventory.GetItem(ClothingLibrary.ItemId(d)) <= 0) g.Inventory.AddItem(ClothingLibrary.ItemId(d));
            outfit.Add(d.id);
            g.Player.RebuildBody();
            return off;
        }

        internal static void Undress(WastelandGame g, ClothingDef d, string putBack)
        {
            var outfit = g.Player.Rig.outfit;
            if (d != null) outfit.Remove(d.id);
            if (putBack != null && !outfit.Contains(putBack)) outfit.Add(putBack);
            g.Player.RebuildBody();
        }
    }
}
