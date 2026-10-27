using System.Collections;
using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Weapon remodels 2026-10-27 (<see cref="BowDraw"/>): the bow string draws back while aiming with an arrow
    /// nocked and slaps forward on the shot, the crossbow is spanned to its latch over the reload and shows the bolt once
    /// loaded, the slingshot band stretches back with a stone in the pouch.</summary>
    public static class ScheduledScenarios1027Weapons
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new BowDrawScenario();
            yield return new CrossbowCock();
            yield return new SlingshotBand();
        }

        internal static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }

        /// <summary>Equip <paramref name="id"/> in third person facing ahead; returns the previous camera mode.</summary>
        internal static ViewMode Ready(WastelandGame g, string id)
        {
            var mode = g.cameraRig ? g.cameraRig.mode : ViewMode.ThirdPerson;
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward(ScheduledScenarios1016.Fwd(g)); }
            g.SetRounds(id, 0);
            g.Player.Equip(ToolLibrary.Create(id, g.propMaterial));
            return mode;
        }

        internal static void Restore(WastelandGame g, string keepTool, ViewMode mode)
        {
            WastelandGame.ForceAim = false;
            if (g.cameraRig) g.cameraRig.mode = mode;
            g.Player.Equip(keepTool != null ? ToolLibrary.Create(keepTool, g.propMaterial) : null);
        }

        internal static void ItemsBack(WastelandGame g, string item, int had)
        {
            int now = g.Inventory.GetItem(item);
            if (now > had) g.Inventory.TakeItem(item, now - had);
            else if (now < had) g.Inventory.AddItem(item, had - now);
        }
    }

    /// <summary>A bow with three arrows: one is nocked when it is drawn; aiming pulls the string (and the arrow) back,
    /// the shot snaps it forward with the string empty, the next arrow is nocked a moment later and drawn again; with
    /// no arrows left nothing sits on the string and aiming draws nothing. A bow held as a visual rests.</summary>
    class BowDrawScenario : Scenario
    {
        public override string Id => "weapons.bow_draw";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ScheduledScenarios1026Tools.OnFoot(g);
            PH.Daylight(c);
            var keepTool = g.Player.Tool ? g.Player.Tool.id : null;
            int had = g.Inventory.GetItem("ammo_arrow"), rounds0 = g.Rounds("tool_bow");
            g.Inventory.AddItem("ammo_arrow", 3);
            var mode = ScheduledScenarios1027Weapons.Ready(g, "tool_bow");
            var bow = g.Player.Tool as RangedTool;
            var bd = bow ? bow.GetComponent<BowDraw>() : null;
            if (!c.Check(bd, "a bow in hand with a live string")) yield break;
            yield return ScheduledScenarios1027Weapons.Until(() => bd.Nocked, 2f);
            c.Check(bd.Nocked && bd.LoadShown, "drawing the bow nocks an arrow");
            c.Check(bd.DrawAmount < 0.05f && Mathf.Abs(bd.NockOffset) < 0.02f, $"the string rests while not aiming ({bd.NockOffset:0.00} m)");
            c.Note(bd.Smooth ? "HD look" : "voxel look");

            WastelandGame.ForceAim = true;
            yield return ScheduledScenarios1027Weapons.Until(() => bd.DrawAmount > 0.99f, 2f);
            c.Check(g.Aiming && bd.DrawAmount > 0.95f && bd.NockOffset > 0.25f, $"aiming draws the string back ({bd.NockOffset:0.00} m)");
            c.Check(bd.LoadShown, "the arrow rides back with the string");
            c.Screenshot("bow_drawn");
            yield return null;

            int shots = g.ShotsFired, rel = bd.Releases;
            bow.Strike(g.Player);
            yield return null;
            c.Check(g.ShotsFired == shots + 1 && bd.Releases == rel + 1, "the arrow is loosed");
            c.Check(bd.DrawAmount < 0.05f && bd.NockOffset < 0.06f, $"the string snaps forward ({bd.NockOffset:0.00} m)");
            c.Check(!bd.Nocked && !bd.LoadShown, "the string is empty after the shot");
            c.Screenshot("bow_loosed");
            yield return ScheduledScenarios1027Weapons.Until(() => bd.Nocked, 1.5f);
            c.Check(bd.Nocked && bd.LoadShown && g.Inventory.GetItem("ammo_arrow") == had + 1, $"the next arrow is nocked a moment later ({g.Inventory.GetItem("ammo_arrow") - had} left in the pack)");
            yield return ScheduledScenarios1027Weapons.Until(() => bd.DrawAmount > 0.95f, 2f);
            c.Check(bd.DrawAmount > 0.95f, "and drawn again while aiming");
            WastelandGame.ForceAim = false;
            yield return ScheduledScenarios1027Weapons.Until(() => bd.DrawAmount < 0.01f, 1f);
            c.Check(bd.DrawAmount < 0.01f && bd.Nocked, "letting go of aim lets the string down, the arrow stays");

            ScheduledScenarios1027Weapons.ItemsBack(g, "ammo_arrow", 0);
            g.SetRounds("tool_bow", 0);
            yield return null; yield return null;
            c.Check(!bd.Nocked && !bd.LoadShown, "out of arrows: nothing on the string");
            WastelandGame.ForceAim = true;
            yield return new WaitForSeconds(0.5f);
            c.Check(bd.DrawAmount < 0.01f, "and nothing to draw");
            WastelandGame.ForceAim = false;

            // an NPC's bow: the tool is a visual only
            var vis = ToolLibrary.Create("tool_bow", g.propMaterial);
            vis.enabled = false;
            vis.transform.position = g.Player.transform.position + ScheduledScenarios1016.Fwd(g) * 2f + Vector3.up;
            yield return null; yield return null;
            var vd = vis.GetComponent<BowDraw>();
            c.Check(vd && !vd.Nocked && !vd.LoadShown && vd.DrawAmount == 0f && Mathf.Abs(vd.NockOffset) < 0.06f, "a bow held as a visual (tool off) rests with an empty string");
            Object.Destroy(vis.gameObject);

            ScheduledScenarios1027Weapons.ItemsBack(g, "ammo_arrow", had);
            g.SetRounds("tool_bow", rounds0);
            ScheduledScenarios1027Weapons.Restore(g, keepTool, mode);
        }
    }

    /// <summary>A crossbow with two bolts and an empty groove: drawing it starts spanning (string forward, no bolt), half
    /// way through the reload the string is half way back, then it latches with a bolt in the groove; the shot throws
    /// the string forward, empties the groove and the next span begins. Its sounds are a twang and a creak / ratchet,
    /// not the dry-fire click.</summary>
    class CrossbowCock : Scenario
    {
        public override string Id => "weapons.crossbow_cock";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ScheduledScenarios1026Tools.OnFoot(g);
            PH.Daylight(c);
            var keepTool = g.Player.Tool ? g.Player.Tool.id : null;
            int had = g.Inventory.GetItem("ammo_bolt"), rounds0 = g.Rounds("tool_crossbow");
            g.Inventory.AddItem("ammo_bolt", 2);
            var mode = ScheduledScenarios1027Weapons.Ready(g, "tool_crossbow");
            var cb = g.Player.Tool as RangedTool;
            var bd = cb ? cb.GetComponent<BowDraw>() : null;
            if (!c.Check(bd && bd.kind == BowDraw.Kind.Crossbow, "a crossbow in hand with a live string")) yield break;
            c.Check(cb.sound != "click" && cb.reloadSound != "click" && !string.IsNullOrEmpty(cb.reloadSound), $"sounds: shot '{cb.sound}', span '{cb.reloadSound}'");
            yield return new WaitForSeconds(0.2f);
            c.Check(cb.Chambering && !bd.Nocked && !bd.LoadShown && bd.DrawAmount < 0.3f, $"spanning starts with the string forward and no bolt ({bd.DrawAmount:0.00})");
            yield return ScheduledScenarios1027Weapons.Until(() => cb.ReloadProgress > 0.55f, 2.5f);
            c.Check(bd.DrawAmount > 0.25f && bd.DrawAmount < 0.85f && !bd.LoadShown, $"half way through: the string half way back ({bd.DrawAmount:0.00})");
            c.Screenshot("crossbow_spanning");
            yield return null;
            yield return ScheduledScenarios1027Weapons.Until(() => bd.Nocked, 3f);
            yield return new WaitForSeconds(0.3f);
            c.Check(bd.Nocked && bd.LoadShown && bd.DrawAmount > 0.95f && bd.NockOffset > 0.3f, $"latched: string at the latch ({bd.NockOffset:0.00} m back), a bolt in the groove");
            c.Screenshot("crossbow_cocked");
            yield return null;

            WastelandGame.ForceAim = true;
            yield return new WaitForSeconds(0.3f);
            int rel = bd.Releases;
            cb.Strike(g.Player);
            yield return null;
            c.Check(bd.Releases == rel + 1 && bd.DrawAmount < 0.15f && !bd.LoadShown, $"the shot throws the string forward and empties the groove ({bd.DrawAmount:0.00})");
            c.Check(cb.Chambering, "the next span begins");
            WastelandGame.ForceAim = false;
            yield return ScheduledScenarios1027Weapons.Until(() => bd.Nocked, 3.5f);
            c.Check(bd.Nocked && bd.DrawAmount > 0.9f, "spanned and loaded again");

            ScheduledScenarios1027Weapons.ItemsBack(g, "ammo_bolt", had);
            g.SetRounds("tool_crossbow", rounds0);
            ScheduledScenarios1027Weapons.Restore(g, keepTool, mode);
        }
    }

    /// <summary>A slingshot with stones: a stone sits in the pouch; aiming stretches the band back toward the holder,
    /// the shot slaps it forward with the pouch empty, the next stone goes in a moment later.</summary>
    class SlingshotBand : Scenario
    {
        public override string Id => "weapons.slingshot_band";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ScheduledScenarios1026Tools.OnFoot(g);
            PH.Daylight(c);
            var keepTool = g.Player.Tool ? g.Player.Tool.id : null;
            int stones0 = g.Inventory.Get(ResourceType.Stone), rounds0 = g.Rounds("tool_slingshot");
            g.Inventory.Add(ResourceType.Stone, 3);
            var mode = ScheduledScenarios1027Weapons.Ready(g, "tool_slingshot");
            var ss = g.Player.Tool as RangedTool;
            var bd = ss ? ss.GetComponent<BowDraw>() : null;
            if (!c.Check(bd && bd.kind == BowDraw.Kind.Slingshot, "a slingshot in hand with a live band")) yield break;
            yield return ScheduledScenarios1027Weapons.Until(() => bd.Nocked, 2f);
            c.Check(bd.Nocked && bd.LoadShown && bd.DrawAmount < 0.05f, "a stone in the pouch, the band slack");
            WastelandGame.ForceAim = true;
            yield return ScheduledScenarios1027Weapons.Until(() => bd.DrawAmount > 0.99f, 2f);
            c.Check(bd.DrawAmount > 0.95f && bd.NockOffset > 0.25f && bd.LoadShown, $"aiming stretches the band back ({bd.NockOffset:0.00} m) with the stone");
            c.Screenshot("slingshot_drawn");
            yield return null;
            int rel = bd.Releases;
            ss.Strike(g.Player);
            yield return null;
            c.Check(bd.Releases == rel + 1 && bd.DrawAmount < 0.05f && !bd.LoadShown, "the shot slaps the band forward, the pouch empty");
            yield return ScheduledScenarios1027Weapons.Until(() => bd.Nocked, 1.5f);
            c.Check(bd.Nocked && bd.LoadShown, "the next stone goes in");
            WastelandGame.ForceAim = false;

            int now = g.Inventory.Get(ResourceType.Stone);
            if (now > stones0) g.Inventory.TrySpend(ResourceType.Stone, now - stones0);
            else if (now < stones0) g.Inventory.Add(ResourceType.Stone, stones0 - now);
            g.SetRounds("tool_slingshot", rounds0);
            ScheduledScenarios1027Weapons.Restore(g, keepTool, mode);
        }
    }
}
