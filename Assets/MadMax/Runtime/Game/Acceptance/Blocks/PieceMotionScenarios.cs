using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Placed pieces come alive (<see cref="PieceMotion"/>): a chest's lid lifts while it is open in the loot
    /// window and drops after, a running generator spins its flywheel, a flag waves in the wind; HD world buildings and
    /// stalls (<see cref="Ambience"/>) light their lanterns at dusk and their curtains sway.</summary>
    public static class PieceMotionScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new PieceLid();
            yield return new PieceGenerator();
            yield return new PieceFlag();
            yield return new WorldPropsAlive();
        }

        /// <summary>Wait in real time (the loot window pauses game time).</summary>
        internal static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!ok() && Time.realtimeSinceStartup - t0 < seconds) yield return null;
        }

        /// <summary>The player on a clear pad and piece <paramref name="id"/> spawned 2 m in front (no build costs), with
        /// its motion component once it has looked for its parts.</summary>
        internal static IEnumerator Spawn(ScenarioContext c, string id, Placeable[] into)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(6f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -2f), 0f);
            yield return new WaitForSeconds(0.3f);
            var at = pad; at.y = DeformableTerrain.Instance.Height(at.x, at.z);
            into[0] = FurnitureLibrary.Spawn(id, g.Build.Structures, at, Quaternion.identity, g.propMaterial);
            c.Fixture("player on a clear pad, " + id + " spawned 2 m ahead (no build costs)");
            if (!into[0]) yield break;
            yield return Until(() => { var m = PieceMotion.Of(into[0].gameObject); return m && m.Discovered; }, 3f);
        }

        internal static void Remove(Placeable p) { if (p) Object.Destroy(p.gameObject); }
    }

    /// <summary>A chest opened in the loot window lifts its lid past 30°, and the lid drops shut after the window closes.</summary>
    class PieceLid : Scenario
    {
        public override string Id => "pieces.lid_opens";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var made = new Placeable[1];
            yield return PieceMotionScenarios.Spawn(c, "chest", made);
            var chest = made[0];
            if (!c.Check(chest, "a chest spawns")) yield break;
            var m = PieceMotion.Of(chest.gameObject);
            if (!c.Check(m, "the chest has a motion component")) { PieceMotionScenarios.Remove(chest); yield break; }
            if (float.IsNaN(m.Angle("Lid"))) { c.Block("no Lid part in this look (parts: " + m.Describe() + ")"); PieceMotionScenarios.Remove(chest); yield break; }
            c.Check(Mathf.Abs(m.Angle("Lid")) < 1f, "the lid rests shut before opening");
            g.Menus.OpenContainer(chest.GetComponent<Container>());
            c.Fixture("chest opened in the loot window");
            yield return PieceMotionScenarios.Until(() => Mathf.Abs(m.Angle("Lid")) > 30f, 2f);
            c.Metric("lid open", Mathf.Abs(m.Angle("Lid")), "deg");
            c.Check(Mathf.Abs(m.Angle("Lid")) > 30f, "the lid lifts past 30° while the chest is open");
            c.Screenshot("chest_open");
            yield return null;
            g.Menus.Close();
            yield return PieceMotionScenarios.Until(() => Mathf.Abs(m.Angle("Lid")) < 1f, 2f);
            c.Check(Mathf.Abs(m.Angle("Lid")) < 1f, "the lid drops shut after the window closes");
            PieceMotionScenarios.Remove(chest);
        }
    }

    /// <summary>A fuelled generator switched on spins its flywheel (and stops again when switched off).</summary>
    class PieceGenerator : Scenario
    {
        public override string Id => "pieces.generator_spins";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var made = new Placeable[1];
            yield return PieceMotionScenarios.Spawn(c, "generator", made);
            var piece = made[0];
            if (!c.Check(piece, "a generator spawns")) yield break;
            var m = PieceMotion.Of(piece.gameObject);
            var fly = m ? m.PartTransform("Flywheel") : null;
            if (!fly) { c.Block("no Flywheel part (parts: " + (m ? m.Describe() : "no motion component") + ")"); PieceMotionScenarios.Remove(piece); yield break; }
            var gen = piece.GetComponent<Generator>();
            var r0 = fly.localRotation;
            yield return new WaitForSeconds(0.5f);
            c.Check(Quaternion.Angle(r0, fly.localRotation) < 0.5f, "the flywheel stands still while the generator is off");
            gen.fuel = 20f; gen.on = true;
            c.Fixture("generator fuelled (20 L) and switched on");
            yield return new WaitForSeconds(1.5f);
            float turned = 0f; var last = fly.localRotation;
            for (int i = 0; i < 20; i++) { yield return null; turned += Quaternion.Angle(last, fly.localRotation); last = fly.localRotation; }
            c.Metric("flywheel turn over 20 frames", turned, "deg");
            c.Check(turned > 10f, "the flywheel spins while it runs");
            gen.on = false;
            yield return new WaitForSeconds(4f);
            last = fly.localRotation; yield return new WaitForSeconds(0.3f);
            c.Check(Quaternion.Angle(last, fly.localRotation) < 0.5f, "it coasts down and stops once switched off");
            PieceMotionScenarios.Remove(piece);
        }
    }

    /// <summary>A flag's cloth moves over two seconds in the real wind.</summary>
    class PieceFlag : Scenario
    {
        public override string Id => "pieces.flag_waves";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var made = new Placeable[1];
            yield return PieceMotionScenarios.Spawn(c, "flag", made);
            var piece = made[0];
            if (!c.Check(piece, "a flag spawns")) yield break;
            var m = PieceMotion.Of(piece.gameObject);
            var cloth = m ? m.PartTransform("Flag") : null;
            if (!cloth) { c.Block("no Flag part (parts: " + (m ? m.Describe() : "no motion component") + ")"); PieceMotionScenarios.Remove(piece); yield break; }
            c.Fixture($"wind {Fx.Wind.magnitude:0.0} m/s, gust {WindDust.Gust:0.00}");
            var r0 = cloth.localRotation;
            float most = 0f, moved = 0f; var last = r0;
            float t0 = Time.time;
            while (Time.time - t0 < 2f)
            {
                yield return null;
                most = Mathf.Max(most, Quaternion.Angle(r0, cloth.localRotation));
                moved += Quaternion.Angle(last, cloth.localRotation); last = cloth.localRotation;
            }
            c.Metric("flag swing (largest from start)", most, "deg");
            c.Metric("flag travel over 2 s", moved, "deg");
            c.Check(moved > 1f, "the flag moves over 2 s in the wind");
            PieceMotionScenarios.Remove(piece);
        }
    }

    /// <summary>At a town after dusk (HD pack on): a shop's or stall's lantern burns with its light, and a curtain or
    /// the washing of a house moves in the wind over two seconds.</summary>
    class WorldPropsAlive : Scenario
    {
        public override string Id => "pieces.world_props_alive";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!MadMax.Rendering.HDAssets.Enabled) { c.Block("HD pack off: world props draw their voxels"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var towns = LightsScenarios.Towns(g, Biome.Village, Biome.Town, Biome.City);
            if (towns.Count == 0) { c.Block("no settlement in this world"); yield break; }
            var st = towns[0];
            var t = DeformableTerrain.Instance;
            var at = new Vector3(st.pos.x, 0f, st.pos.y); at.y = t.Height(at.x, at.z);
            g.Player.Teleport(at + Vector3.up * 0.5f, 0f);
            c.Fixture($"player at the centre of settlement {st.index} ({st.kind})");
            float hours0 = DayNight.Hours;
            yield return LightsScenarios.Clock(c, 21f);
            Ambience lit = null, cloth = null; string clothKind = null;
            float t0 = Time.time;
            while (Time.time - t0 < 25f && (!lit || !cloth))
            {
                yield return new WaitForSeconds(0.5f);
                var me = g.Player.transform.position;
                foreach (var a in Ambience.All)
                {
                    if (!a || (a.transform.position - me).sqrMagnitude > Ambience.Reach * Ambience.Reach) continue;
                    if (!lit && a.HasLamp && (a.templateId.StartsWith("Stall_") || a.templateId.StartsWith("Shop")) && a.LampLight) lit = a;
                    if (!cloth) foreach (var k in new[] { "Curtain", "Laundry" }) if (a.PartOf(k)) { cloth = a; clothKind = k; break; }
                }
            }
            c.Metric("ambient props near", Ambience.All.Count, "props");
            if (!lit && !cloth) { c.Block("no HD building with lanterns or curtains within reach (re-export of the buildings missing?)"); DayNight.SetHours(hours0); yield break; }
            if (c.Check(lit, "a shop or stall with a lantern stands within reach"))
            {
                c.Fixture("lantern prop: " + lit.templateId);
                c.Check(lit.LampLit, "its lantern burns after dusk");
                c.Check(lit.LampLight && lit.LampLight.enabled, "with a warm point light (light budget allowing)");
            }
            if (c.Check(cloth, "a house with curtains or washing stands within reach"))
            {
                var tr = cloth.PartOf(clothKind);
                c.Fixture($"{clothKind} of {cloth.templateId}; wind {Fx.Wind.magnitude:0.0} m/s");
                float moved = 0f; var last = tr.localRotation; float t1 = Time.time;
                while (Time.time - t1 < 2f) { yield return null; moved += Quaternion.Angle(last, tr.localRotation); last = tr.localRotation; }
                c.Metric(clothKind.ToLowerInvariant() + " travel over 2 s", moved, "deg");
                c.Check(moved > 0.5f, "the " + clothKind.ToLowerInvariant() + " moves");
            }
            c.Screenshot("town_dusk");
            yield return null;
            DayNight.SetHours(hours0);
        }
    }
}
