using System.Collections;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q4 (the productive home): with the claw hammer in build mode (first-person aim, the game's
    /// placement and key paths) a timber foundation is laid on the ground and paid for, a doorway stands on it and a door
    /// snaps into the doorway, opens, locks and stays shut when locked; a bed, a chest (kit) and a claim flag go down
    /// (the claim counts the pieces); sleeping in the bed at night wakes you at 7; a chest holding a few coins refuses to
    /// be dismantled until emptied; a damaged door is repaired, the foundation upgraded to stone in place, and
    /// dismantling refunds exactly what building charged. A roof held up only by a post collapses when the post is
    /// dismantled. Materials are granted up front (disclosed).</summary>
    class SurvivalBase : Scenario
    {
        public override string Id => "survival.base";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 160f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var inv = g.Inventory; var P = g.Player;
            var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 9f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            var pad = at[0]; var fwd = P.transform.forward; var side = P.transform.right;
            inv.Add(ResourceType.Wood, 80); inv.Add(ResourceType.Cloth, 12); inv.Add(ResourceType.Scrap, 20);
            inv.Add(ResourceType.Stone, 24); inv.Add(ResourceType.Lime, 4); inv.AddItem(ItemIds.ChestKit, 1);
            if (inv.GetItem(ItemIds.ClawHammer) <= 0) inv.AddItem(ItemIds.ClawHammer, 1);
            c.Fixture("granted 80 wood, 12 cloth, 20 scrap, 24 stone, 4 lime, a chest kit and a claw hammer");
            g.UseItem(ItemIds.ClawHammer);
            c.Check(P.Tool && P.Tool.id == ItemIds.ClawHammer, "the claw hammer is in hand");
            var placed = new Placeable[1];
            var w = new Waited();

            // ---- foundation
            var F0 = SurvivalKit.Ground(pad + fwd * 2.6f);
            int wood0 = inv.Get(ResourceType.Wood);
            yield return SurvivalKit.Build(c, "foundation_wood", F0, placed);
            var found = placed[0];
            if (!c.Check(found, "a timber foundation goes down where the player looks")) { Done(g); yield break; }
            c.Check(wood0 - inv.Get(ResourceType.Wood) == g.Build.Cost(10), $"it costs {g.Build.Cost(10)} wood ({wood0 - inv.Get(ResourceType.Wood)} taken)");
            var F = found.transform.position;
            c.Check(Mathf.Abs(F.y - DeformableTerrain.Instance.Height(F.x, F.z)) < 0.6f, "it stands level on the ground");

            // ---- doorway and a door that snaps into it
            yield return SurvivalKit.Build(c, "doorway_wood", F + fwd * 0.8f + Vector3.up * 0.14f, placed);
            var doorway = placed[0];
            c.Check(doorway && Vector3.Angle(doorway.transform.up, Vector3.up) < 5f, "a doorway stands upright on the foundation");
            Placeable door = null;
            if (doorway)
            {
                yield return SurvivalKit.Build(c, "door_wood", doorway.transform.position + Vector3.up * 1.1f, placed, () => (g.Build.Status ?? "").Contains("FIT"));
                door = placed[0];
                c.Check(door && door.GetComponent<Door>() && Vector3.Distance(door.transform.position, doorway.transform.position) < 0.05f, "the door snaps into the doorway");
            }
            g.Build.SetActive(false);
            if (door)
            {
                var d = door.GetComponent<Door>();
                yield return SurvivalKit.WalkTo(g, door.transform.position - fwd * 1.2f, 0.4f, w);
                yield return SurvivalKit.Use(g, door, false, w);
                c.Check(w.ok && d.open, "[E] at the door opens it (the prompt names it)");
                yield return SurvivalKit.Use(g, door, false, w);
                c.Check(!d.open, "[E] again closes it");
                yield return SurvivalKit.Use(g, door, true, w);
                c.Check(d.locked, "[T] locks it (the builder's door)");
                yield return SurvivalKit.Use(g, door, false, w);
                c.Check(!d.open, "a locked door stays shut");
                d.SetLocked(false);
            }

            // ---- furniture on the ground beside it: a bed, a chest from its kit, a claim flag
            P.Teleport(SurvivalKit.Ground(pad) + Vector3.up * 0.1f, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
            yield return SurvivalKit.GameSeconds(0.3f);
            g.Build.SetActive(true);
            int cloth0 = inv.Get(ResourceType.Cloth); wood0 = inv.Get(ResourceType.Wood);
            yield return SurvivalKit.Build(c, "bed", SurvivalKit.Ground(pad + fwd * 1.2f + side * 2.4f), placed);
            var bed = placed[0];
            c.Check(bed && wood0 - inv.Get(ResourceType.Wood) == g.Build.Cost(6) && cloth0 - inv.Get(ResourceType.Cloth) == g.Build.Cost(4), "a bed goes down for its wood and cloth");
            int kits = inv.GetItem(ItemIds.ChestKit);
            yield return SurvivalKit.Build(c, "chest", SurvivalKit.Ground(pad + fwd * 1.2f - side * 2.4f), placed);
            var chest = placed[0];
            c.Check(chest && chest.GetComponent<Container>() && inv.GetItem(ItemIds.ChestKit) == kits - 1, "the chest kit becomes a chest");
            yield return SurvivalKit.Build(c, "claim_flag", SurvivalKit.Ground(pad - side * 2.2f), placed);
            var flag = placed[0] ? placed[0].GetComponent<ClaimFlag>() : null;
            c.Check(flag && ClaimFlag.Near(F) == flag && flag.Pieces() >= 5, $"a claim flag claims the base ({(flag ? flag.Pieces() : 0)} pieces)");
            c.Screenshot("base");
            yield return null;

            // ---- a coin in the chest: no dismantling until it is empty
            if (chest)
            {
                var box = chest.GetComponent<Container>();
                box.inventory.AddItem("coin_chit", 3);
                c.Fixture("3 coin chits in the chest");
                for (int i = 0; i < 4; i++) { SurvivalKit.Face(g, chest.transform.position + Vector3.up * 0.3f); yield return null; }
                yield return SurvivalKit.Press(Controls.Act.BuildDismantle);
                c.Check(chest && box.inventory.GetItem("coin_chit") == 3, "a chest with coins in it is not dismantled (" + g.ToastText + ")");
                box.inventory.TakeItem("coin_chit", 3);
                kits = inv.GetItem(ItemIds.ChestKit);
                for (int i = 0; i < 4; i++) { SurvivalKit.Face(g, chest.transform.position + Vector3.up * 0.3f); yield return null; }
                yield return SurvivalKit.Press(Controls.Act.BuildDismantle);
                yield return null;
                c.Check(!chest && inv.GetItem(ItemIds.ChestKit) == kits + 1, "emptied, it comes apart and the kit comes back");
            }

            // ---- sleep the night in the bed
            g.Build.SetActive(false);
            if (bed)
            {
                DayNight.SetHours(22.5f);
                c.Fixture("clock set to 22:30");
                int day0 = DayNight.Day;
                g.Stats.stamina = g.Stats.MaxStamina * 0.3f;
                yield return SurvivalKit.WalkTo(g, bed.transform.position - fwd * 1.1f, 0.5f, w);
                yield return SurvivalKit.Use(g, bed, false, w);
                c.Check(w.ok && Mathf.Abs(DayNight.Hours - 7f) < 0.2f && DayNight.Day == day0 + 1, $"[E] at the bed sleeps until 07:00 next day ({DayNight.Hours:0.0} h, day {DayNight.Day})");
                c.Check(g.Stats.stamina >= g.Stats.MaxStamina - 0.5f, "and wakes you with full stamina");
                DayNight.SetHours(12f);
            }

            // ---- repair, upgrade, refunds
            P.Teleport(SurvivalKit.Ground(F - fwd * 2.2f) + Vector3.up * 0.1f, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
            yield return SurvivalKit.GameSeconds(0.3f);
            g.Build.SetActive(true);
            g.Build.Select("door_wood");
            if (door)
            {
                door.ApplyHit(door.transform.position + Vector3.up, fwd, 2f, 0.3f, null);
                int hurt = door.hits; wood0 = inv.Get(ResourceType.Wood);
                for (int i = 0; i < 4; i++) { SurvivalKit.Face(g, door.transform.position + Vector3.up * 1.1f); yield return null; }
                yield return SurvivalKit.Press(Controls.Act.BuildRepair);
                c.Check(hurt < door.MaxHits && door.hits == door.MaxHits && inv.Get(ResourceType.Wood) < wood0, $"[R] repairs the battered door ({hurt} -> {door.hits}/{door.MaxHits}) for some wood");
            }
            int stone0 = inv.Get(ResourceType.Stone); wood0 = inv.Get(ResourceType.Wood);
            for (int i = 0; i < 4; i++) { SurvivalKit.Face(g, F + Vector3.up * 0.1f - fwd * 0.6f); yield return null; }
            yield return SurvivalKit.Press(Controls.Act.BuildUpgrade);
            yield return null;
            var stoneF = SurvivalKit.Near("foundation_stone", F, 0.2f);
            c.Check(stoneF && !found, "[U] upgrades the foundation to stone in place");
            c.Check(stone0 - inv.Get(ResourceType.Stone) == g.Build.Cost(16) && inv.Get(ResourceType.Wood) - wood0 == 5, $"paying {g.Build.Cost(16)} stone, half the timber back ({inv.Get(ResourceType.Wood) - wood0} wood)");
            if (bed)
            {
                P.Teleport(SurvivalKit.Ground(bed.transform.position - fwd * 1.8f) + Vector3.up * 0.1f, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
                yield return SurvivalKit.GameSeconds(0.3f);
                wood0 = inv.Get(ResourceType.Wood); cloth0 = inv.Get(ResourceType.Cloth);
                for (int i = 0; i < 4; i++) { SurvivalKit.Face(g, bed.transform.position + Vector3.up * 0.3f); yield return null; }
                yield return SurvivalKit.Press(Controls.Act.BuildDismantle);
                yield return null;
                c.Check(!bed && inv.Get(ResourceType.Wood) - wood0 == g.Build.Cost(6) && inv.Get(ResourceType.Cloth) - cloth0 == g.Build.Cost(4), "[X] dismantles the bed and refunds exactly its building cost");
            }

            // ---- structural support: a roof on a lone post
            var postAt = SurvivalKit.Ground(pad - fwd * 2.5f + side * 3.5f);
            var post = SurvivalKit.Piece(g, "post", postAt, fwd);
            var roof = SurvivalKit.Piece(g, "roof_flat", postAt + Vector3.up * 2.42f, fwd, false);
            c.Fixture("a post with a roof panel on top of it (placed directly)");
            yield return SurvivalKit.GameSeconds(0.2f);
            P.Teleport(SurvivalKit.Ground(postAt - fwd * 1.8f) + Vector3.up * 0.1f, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
            yield return SurvivalKit.GameSeconds(0.3f);
            wood0 = inv.Get(ResourceType.Wood);
            for (int i = 0; i < 4; i++) { SurvivalKit.Face(g, postAt + Vector3.up * 1f); yield return null; }
            yield return SurvivalKit.Press(Controls.Act.BuildDismantle);
            yield return SurvivalKit.Until(() => !roof, 3f, w);
            c.Check(!post && inv.Get(ResourceType.Wood) - wood0 == g.Build.Cost(2), "the post comes down and refunds its wood");
            c.Check(w.ok, "the roof it held up collapses");
            c.Check(stoneF && doorway && door, "pieces still standing on the ground stay up");
            Done(g);
        }

        static void Done(WastelandGame g)
        {
            g.Build.SetActive(false);
            if (g.cameraRig) g.cameraRig.mode = ViewMode.Isometric;
        }
    }
}
