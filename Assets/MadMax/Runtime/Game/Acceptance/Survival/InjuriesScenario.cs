using System.Collections;
using System.Linq;
using MadMax.Items;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q4 (injury treatment, clothing wear): a broken leg stops running and jumping and slows the walk;
    /// the HEALTH page lists it and a splint helps; a laceration bleeds, treating it without supplies says what is
    /// missing, disinfectant and a bandage stop the bleeding and it starts to heal; a broken arm blocks two-handed tools
    /// with a reason; a hard hit tears the clothes over the wound (half warmth), a sewing kit mends them and a garment
    /// worn through falls apart. Wounds are set directly (disclosed); treatment and movement use the game's paths.</summary>
    class SurvivalInjuries : Scenario
    {
        public override string Id => "survival.injuries";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var s = g.Stats; var P = g.Player;
            if (g.Current) { g.Exit(); yield return null; }
            if (!TestWorld.Pad(4f, 30f, out var pad, out var dir)) { c.Block("no clear lane near the start"); yield break; }
            P.Teleport(pad + Vector3.up * 0.2f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            c.Fixture($"on foot at the start of a clear 30 m lane ({pad.x:0},{pad.z:0})");
            yield return SurvivalKit.GameSeconds(0.4f, 5);
            MedMineScenarios.Healthy(c, "injuries start from a clean slate");
            s.stamina = s.MaxStamina;

            // ---- healthy run as the baseline
            float healthy = 0f;
            yield return Run(P, dir, 1.2f, v => healthy = v);
            c.Metric("run_speed_healthy", healthy, "m/s");
            c.Check(healthy > 3.5f, $"a healthy run ({healthy:0.0} m/s)");

            // ---- a broken leg
            var leg = new Injury { zone = BodyZone.LegL, type = Wound.Fracture, severity = 0.9f };
            s.injuries.Add(leg);
            c.Fixture("a broken left leg (fracture, severity 0.9)");
            c.Check(!g.CanRunInjured && !g.CanJumpInjured, $"no running or jumping on a broken leg (limp {g.LimpL:0.00})");
            float hobble = 0f;
            yield return Run(P, -dir, 1.2f, v => hobble = v);
            c.Metric("run_speed_broken_leg", hobble, "m/s");
            c.Check(hobble < healthy * 0.35f, $"the broken leg holds you to a hobble ({hobble:0.0} m/s against {healthy:0.0})");
            float up = 0f;
            P.jump = true;
            for (int i = 0; i < 20; i++) { yield return null; up = Mathf.Max(up, P.Velocity.y); }
            P.jump = false;
            c.Check(up < 1f, $"no jump on the broken leg (vertical {up:0.0} m/s)");

            yield return SurvivalKit.Press(Controls.Act.Health);
            c.Check(g.Menus.Current == MenuSystem.Page.Health, "the HEALTH key opens the health page");
            var labels = g.Menus.Labels();
            c.Check(labels.Any(l => l != null && l.Contains("LEFT LEG") && l.Contains("FRACTURE")), "the page lists the broken left leg: " + string.Join(" | ", labels));
            c.Screenshot("health_page");
            yield return null;
            float speed0 = g.InjurySpeed;
            int splints = g.Inventory.GetItem("med_splint");
            g.Inventory.AddItem("med_splint", 1);
            c.Fixture("granted a splint");
            g.Treat(leg);                                                                    // the page's ENTER on the entry
            c.Check(leg.splinted && g.Inventory.GetItem("med_splint") == splints, "the splint goes on and is used up");
            c.Check(g.InjurySpeed > speed0, $"a splinted leg walks better ({speed0:0.00} -> {g.InjurySpeed:0.00})");
            g.Menus.Close();
            s.injuries.Remove(leg);

            // ---- a bleeding cut: feedback without supplies, then disinfect and bandage
            var cut = new Injury { zone = BodyZone.ArmR, type = Wound.Laceration, severity = 0.8f };
            s.injuries.Add(cut);
            c.Fixture("a laceration on the right arm");
            int bandages = g.Inventory.GetItem("med_bandage"), disinf = g.Inventory.GetItem("med_disinfectant"), cloth = g.Inventory.Get(ResourceType.Cloth);
            if (bandages > 0) g.Inventory.TakeItem("med_bandage", bandages);
            if (disinf > 0) g.Inventory.TakeItem("med_disinfectant", disinf);
            if (cloth > 0) g.Inventory.TrySpend(ResourceType.Cloth, cloth);
            c.Fixture($"pack emptied of bandages ({bandages}), disinfectant ({disinf}) and cloth ({cloth})");
            float hp0 = s.health;
            yield return SurvivalKit.GameSeconds(1.5f);
            c.Check(cut.Bleeding && s.health < hp0, $"the cut bleeds ({hp0:0.0} -> {s.health:0.0} health)");
            c.Check(g.BleedOutSeconds > 0f, $"the bleed-out clock runs ({g.BleedOutSeconds:0} s)");
            g.Treat(cut);
            c.Check(cut.Bleeding && (g.ToastText ?? "").Contains("NEED"), "treating without supplies says what is missing: " + g.ToastText);
            g.Inventory.AddItem("med_disinfectant", 1); g.Inventory.AddItem("med_bandage", 1);
            c.Fixture("granted a disinfectant and a bandage");
            g.Treat(cut);
            c.Check(cut.disinfected && cut.Bleeding, "first treatment disinfects");
            g.Treat(cut);
            c.Check(cut.bandaged && !cut.Bleeding, "second treatment bandages: the bleeding stops");
            float sev0 = cut.severity, hp1 = s.health;
            yield return SurvivalKit.GameSeconds(2f);
            c.Check(cut.severity < sev0 && s.health >= hp1 - 0.01f, $"the dressed wound heals and no longer costs health (severity {sev0:0.000} -> {cut.severity:0.000})");
            s.injuries.Remove(cut);
            g.Inventory.Add(ResourceType.Cloth, cloth);

            // ---- a broken arm blocks two-handed tools
            var arm = new Injury { zone = BodyZone.ArmR, type = Wound.Fracture, severity = 0.9f };
            s.injuries.Add(arm);
            c.Fixture("a broken right arm");
            var hammer = ToolLibrary.Create(ItemIds.Sledgehammer, g.propMaterial);
            P.Equip(hammer);
            yield return null;
            c.Check(g.ArmBroken, $"the arm counts as broken ({g.ArmHurtR:0.00})");
            if (hammer && hammer.TwoHanded)
            {
                P.Attack(false);
                c.Check(!P.Swinging && (g.ToastText ?? "").Contains("BROKEN ARM"), "a two-handed swing is refused with a reason: " + g.ToastText);
            }
            else c.Note("the sledgehammer is not two-handed");
            s.injuries.Remove(arm);
            P.Equip(null);

            // ---- clothes: torn by a hard hit, mended, worn through
            var outfit = P.Rig.outfit;
            string over = outfit.FirstOrDefault(id => { var d = ClothingLibrary.Get(id); return d != null && d.armor == null && d.warmth > 0f && d.coverage.Keys.Any(bp => bp == BodyPart.Chest); });
            if (!c.Check(over != null, "a garment covers the chest")) yield break;
            float warm0 = g.Insulation().warmth;
            var overDef = ClothingLibrary.Get(over);
            float tear = 0.7f * overDef.durability / Mathf.Max(0.05f, GameRules.Current.DamageTaken * g.QualityWear(ClothingLibrary.ItemId(overDef)));
            g.TearClothes(BodyZone.Torso, tear);
            c.Fixture($"a hard hit to the torso (tears 70 % of the {over}'s life)");
            float cond = g.GarmentCondition(over);
            c.Metric("garment_condition_after_hit", cond, "");
            c.Check(cond < 0.4f, $"the {over} over the wound is torn ({cond * 100f:0} %)");
            c.Check(g.Insulation().warmth < warm0, $"torn clothes keep less warmth ({warm0:0.0} -> {g.Insulation().warmth:0.0})");
            g.Inventory.AddItem("use_sewing_kit", 1); g.Inventory.Add(ResourceType.Cloth, 2);
            c.Fixture("granted a sewing kit and 2 cloth");
            string worst = outfit.OrderBy(id => g.GarmentCondition(id)).First();
            float before = g.GarmentCondition(worst);
            g.UseItem("use_sewing_kit");
            c.Check(g.GarmentCondition(worst) > before + 0.3f && g.Inventory.GetItem("use_sewing_kit") == 0, $"the sewing kit mends the most worn garment ({worst}: {before * 100f:0} -> {g.GarmentCondition(worst) * 100f:0} %)");
            int items = g.Inventory.GetItem(ClothingLibrary.ItemId(overDef));
            g.TearClothes(BodyZone.Torso, 3f);
            c.Check(!g.Wearing(over) || items > 1, $"a garment worn through falls apart ({over} worn: {g.Wearing(over)})");
            c.Check(g.Inventory.GetItem(ClothingLibrary.ItemId(overDef)) == Mathf.Max(0, items - 1), $"and is gone from the pack ({items} -> {g.Inventory.GetItem(ClothingLibrary.ItemId(overDef))})");
            MedMineScenarios.Healthy(c, "cleanup");
        }

        /// <summary>Run (Shift + W) along <paramref name="dir"/> for a while; reports the mean horizontal speed.</summary>
        internal static IEnumerator Run(PlayerCharacter p, Vector3 dir, float seconds, System.Action<float> speed)
        {
            p.viewYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            p.run = true; p.moveInput = new Vector2(0f, 1f);
            yield return SurvivalKit.GameSeconds(0.3f);                                        // up to speed
            var a = p.transform.position; float t0 = Time.time;
            yield return SurvivalKit.GameSeconds(seconds);
            var b = p.transform.position; a.y = b.y = 0f;
            speed(Vector3.Distance(a, b) / Mathf.Max(0.01f, Time.time - t0));
            p.run = false; p.moveInput = Vector2.zero;
            yield return SurvivalKit.GameSeconds(0.3f);
        }
    }
}
