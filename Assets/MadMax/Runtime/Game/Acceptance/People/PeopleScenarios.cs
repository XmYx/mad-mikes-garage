using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q6 solo scenarios: people (dialogue, trade, contracts, factions, companions, raiders, combat,
    /// defences), wildlife, radio and the race board. Every one runs in a fresh seed-7 sandbox world and talks to people
    /// through their real interactables and menu rows (<see cref="MenuSystem.Pick"/>).</summary>
    public static class PeopleScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new PeopleDialogue();
            yield return new PeopleTrade();
            yield return new PeopleContracts();
            yield return new PeopleFactions();
            yield return new PeopleCompanion();
            yield return new PeopleRaiders();
            yield return new PeopleCombat();
            yield return new MeleeFeel();
            yield return new IdleClearance();
            yield return new PeopleDefences();
            yield return new AnimalsHerdsTaming();
            yield return new RadioStations();
            yield return new RacingBoard();
        }
    }

    /// <summary>Shared steps of the people scenarios. Every teleport, grant and clock change is disclosed.</summary>
    static class PH
    {
        public static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }

        public static IEnumerator OnFoot(WastelandGame g)
        {
            if (g.Menus.IsOpen) g.Menus.Close();
            if (g.Current) { var v = g.Current; v.throttleInput = v.brakeInput = v.steerInput = 0f; v.handbrake = true; g.Exit(); yield return new WaitForSeconds(0.5f); }
        }

        /// <summary>Shops open and residents up and about (07:00-20:00, not asleep).</summary>
        public static void Daylight(ScenarioContext c)
        {
            if (DayNight.Hours >= 8f && DayNight.Hours <= 16f) return;
            DayNight.SetHours(10f);
            c.Fixture("clock set to 10:00 (daylight: shops open, residents up)");
        }

        public static void Attribute(ScenarioContext c, WastelandGame g, Attr a, int v)
        {
            if (g.Stats.attributes[(int)a] == v) return;
            g.Stats.attributes[(int)a] = v;
            c.Fixture("character created with " + a.ToString().ToUpperInvariant() + " " + v);
        }

        public static void Grant(ScenarioContext c, WastelandGame g, string id, int n)
        {
            if (id.StartsWith("res:")) g.Inventory.Add((MadMax.Items.ResourceType)int.Parse(id.Substring(4)), n);
            else g.Inventory.AddItem(id, n);
            c.Fixture("granted " + n + " " + Trade.Name(id));
        }

        public static int Have(WastelandGame g, string id) => id.StartsWith("res:") ? g.Inventory.Get((MadMax.Items.ResourceType)int.Parse(id.Substring(4))) : g.Inventory.GetItem(id);
        public static int Scrap(WastelandGame g) => g.Inventory.Get(MadMax.Items.ResourceType.Scrap);

        public static float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        public static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        public static Vector3 Ground(Vector3 p)
        {
            var t = DeformableTerrain.Instance;
            p.y = t ? t.Height(p.x, p.z) : p.y;
            return p;
        }

        public static Vector3 Centre(Settlement st) => Ground(new Vector3(st.pos.x, 0f, st.pos.y));

        /// <summary>The settlement nearest the start yard (optionally not a city: villages and towns have a market stall).</summary>
        public static Settlement HomeTown(WastelandGame g, bool noCity)
        {
            g.World.Yard(out var origin, out _, out _);
            Settlement best = null; float bd = float.MaxValue;
            foreach (var st in g.World.settlements)
            {
                if (noCity && st.kind == Biome.City) continue;
                float d = Vector2.Distance(st.pos, new Vector2(origin.x, origin.z));
                if (d < bd) { bd = d; best = st; }
            }
            return best;
        }

        /// <summary>A spot on the road near a settlement's middle (streets are clear of buildings).</summary>
        public static Vector3 Street(WastelandGame g, Settlement st)
        {
            var w = g.World;
            for (int k = 0; k < 600; k++)
            {
                float a = k * 2.39996f, r = 2f + k * 0.12f;
                if (r > st.radius) break;
                var p = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                var s = w.Sample(p.x, p.y);
                if (s.roadDist < 2f && float.IsNaN(s.water)) return Ground(new Vector3(p.x, 0f, p.y));
            }
            return Centre(st);
        }

        public static IEnumerator Go(ScenarioContext c, Vector3 p, float yaw, string what, float settle = 1.2f)
        {
            var g = c.Game;
            p = Ground(p) + Vector3.up * 0.2f;
            g.Player.Teleport(p, yaw);
            c.Fixture("walked to " + what + " (teleport to " + p.x.ToString("0") + "," + p.z.ToString("0") + ")");
            yield return new WaitForSeconds(settle);
        }

        public static IEnumerator ToTown(ScenarioContext c, Settlement st)
        {
            yield return Go(c, Street(c.Game, st), 0f, Market.TownName(st), 2f);
        }

        /// <summary>The nearest living person matching <paramref name="ok"/> within <paramref name="radius"/> of <paramref name="at"/>.</summary>
        public static MadMax.Npc.Npc Nearest(Vector3 at, float radius, System.Func<MadMax.Npc.Npc, bool> ok)
        {
            MadMax.Npc.Npc best = null; float bd = radius;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || n.proxy || !ok(n)) continue;
                float d = Flat(n.transform.position, at);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        public sealed class Found { public MadMax.Npc.Npc npc; public Settlement st; }

        /// <summary>Visit the settlements nearest the start (up to <paramref name="towns"/>) until one has a person matching
        /// <paramref name="ok"/>; waits up to 15 s in each and notes who was there when nobody matched.</summary>
        public static IEnumerator FindIn(ScenarioContext c, System.Func<MadMax.Npc.Npc, bool> ok, bool noCity, int towns, Found into, System.Func<MadMax.Npc.Npc, int> prefer = null)
        {
            var g = c.Game;
            g.World.Yard(out var origin, out _, out _);
            foreach (var st in g.World.settlements.Where(s => !noCity || s.kind != Biome.City).OrderBy(s => Vector2.Distance(s.pos, new Vector2(origin.x, origin.z))).Take(towns))
            {
                yield return ToTown(c, st);
                var centre = Centre(st);
                float reach = st.radius + 40f;
                yield return Until(() => Nearest(centre, reach, ok) != null, 15f);
                var all = MadMax.Npc.Npc.All.Where(n => n && n.Alive && !n.proxy && Flat(n.transform.position, centre) < reach && ok(n)).ToList();
                if (all.Count > 0)
                {
                    into.st = st;
                    into.npc = prefer == null ? Nearest(centre, reach, ok) : all.OrderByDescending(prefer).ThenBy(n => Flat(n.transform.position, centre)).First();
                    yield break;
                }
                c.Note(Market.TownName(st) + " (" + st.kind + ", r " + st.radius.ToString("0") + " m): nobody fits; there: " + string.Join("; ", MadMax.Npc.Npc.All.Where(n => n && n.Alive && Flat(n.transform.position, centre) < reach + 40f)
                    .Select(n => n.Profile.Name + " " + n.Profile.role + (n.Profile.kind != null ? "/" + n.Profile.kind : "") + (n.Profile.Cast ? " cast" : "") + (n.Available ? "" : " unavailable") + (n.Closed ? " closed" : "") + (n.Hostile ? " hostile" : "") + " " + Flat(n.transform.position, centre).ToString("0") + " m")));
            }
        }

        /// <summary>Step up to a person: <paramref name="dist"/> m in front of them, facing them.</summary>
        public static IEnumerator Face(ScenarioContext c, MadMax.Npc.Npc n, float dist)
        {
            var f = n.transform.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            var p = n.transform.position + f.normalized * dist;
            yield return Go(c, p, Yaw(n.transform.position - p), n.Profile.Name, 0.3f);
        }

        /// <summary>[E] on a person (talk, parley, orders), as the interaction key does.</summary>
        public static bool Talk(WastelandGame g, MadMax.Npc.Npc n)
        {
            if (!n) return false;
            if (g.Menus.IsOpen) g.Menus.Close();
            n.Use(g, false);
            return g.Menus.Current == MenuSystem.Page.Talk;
        }

        public static string Line(WastelandGame g) => g.Menus.TalkLine ?? "";
        public static string Rows(WastelandGame g) => string.Join(" | ", g.Menus.Labels());

        /// <summary>A raider on foot (spawned for the test; disclosed by the caller). Unarmed and unarmoured unless asked.</summary>
        public static MadMax.Npc.Npc Raider(WastelandGame g, string id, int seed, Vector3 at, float yaw, bool armed, bool armour, bool aggro = true, string wear = null)
        {
            var p = NpcProfile.Make(id, NpcRole.Raider, seed);
            if (!armed) p.tool = null;
            if (!armour) p.outfit.RemoveAll(o => { var d = ClothingLibrary.Get(o); return d != null && d.armor != null; });
            if (wear != null) p.outfit.Add(wear);
            var n = MadMax.Npc.Npc.Spawn(p, Ground(at) + Vector3.up * 0.1f, yaw, null, g.propMaterial);
            n.aggro = aggro;
            return n;
        }

        /// <summary>A piece on the ground at <paramref name="at"/> facing <paramref name="facing"/>, built by the player.</summary>
        public static Placeable Piece(WastelandGame g, string id, Vector3 at, Vector3 facing)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-4f) facing = Vector3.forward;
            var root = g.Build.Structures;
            at = Ground(at);
            var p = FurnitureLibrary.Spawn(id, root, root.InverseTransformPoint(at), Quaternion.Inverse(root.rotation) * Quaternion.LookRotation(facing.normalized), g.propMaterial);
            if (p) { p.owner = g.Stats.name; _ = p.Id; }
            return p;
        }

        /// <summary>Walk input towards a world direction (camera-relative, as the keys give it).</summary>
        public static void Walk(WastelandGame g, Vector3 worldDir, bool run)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-4f) { g.Player.moveInput = Vector2.zero; g.Player.run = false; return; }
            var local = Quaternion.Euler(0f, -g.Player.viewYaw, 0f) * worldDir.normalized;
            g.Player.moveInput = new Vector2(local.x, local.z);
            g.Player.run = run;
        }

        public static void Stop(WastelandGame g) { g.Player.moveInput = Vector2.zero; g.Player.run = false; g.Player.crouch = false; }

        /// <summary>A disposition change as the conversation scales it by charisma.</summary>
        public static int Scaled(int delta, int cha) => Mathf.RoundToInt(delta > 0 ? delta * (0.7f + cha * 0.06f) : delta * Mathf.Max(0.5f, 1.3f - cha * 0.05f));

        public static int[] Reps() { var r = new int[Factions.Count]; for (int i = 0; i < r.Length; i++) r[i] = Factions.Rep((Faction)i); return r; }
    }
}
