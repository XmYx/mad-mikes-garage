using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Animals;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Wildlife (roadmap 26 Q6): a herd of antelope grazes while the player keeps still at a distance, and bolts
    /// together when one is shot; a pack of three wolves stalks a lone walker by day and comes in; a wild horse is won over
    /// by a calm, crouched approach (trust to half), then fed treats by hand until it is yours ([E]), saddled and ridden:
    /// forward input carries horse and rider, getting off leaves it standing.</summary>
    class AnimalsHerdsTaming : Scenario
    {
        public override string Id => "animals.herds_taming";
        public override float Timeout => 180f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            if (!TestWorld.Pad(12f, 40f, out var pad, out var dir)) { c.Block("no open pad"); yield break; }
            var side = Vector3.Cross(Vector3.up, dir);
            yield return PH.Go(c, pad, PH.Yaw(dir), "an open pad");
            if (g.Stats.health < g.Stats.MaxHealth) { g.Stats.health = g.Stats.MaxHealth; c.Fixture("player at full health"); }

            // ---- a herd grazes, then bolts together
            var ante = AnimalLibrary.Get("antelope");
            var herd = new List<Animal>();
            var spot = pad + dir * 44f;
            for (int i = 0; i < 4; i++)
            {
                var a = Animal.Spawn(ante, PH.Ground(spot + side * (i * 2.5f - 3.75f)), PH.Yaw(side), g.propMaterial, "t:herd:" + i);
                a.herd = 9001; herd.Add(a);
            }
            c.Fixture("a herd of four antelope 44 m away (the player still, crouched)");
            g.Player.crouch = true;
            float t0 = Time.time;
            bool grazed = false, fled = false;
            while (Time.time - t0 < 10f) { if (herd.Any(a => a.state == Animal.State.Graze)) grazed = true; if (herd.Any(a => a.state == Animal.State.Flee)) fled = true; yield return null; }
            c.Note("herd states: " + string.Join(", ", herd.Select(a => a.state)));
            c.Check(grazed && !fled, "the herd grazes and wanders, unbothered");
            herd[0].ApplyHit(herd[0].transform.position + Vector3.up * 0.6f, dir, 0.3f, 0.1f, g.Player.gameObject);
            yield return PH.Until(() => herd.Count(a => a.Alive && a.state == Animal.State.Flee) >= 3, 3f);
            c.Check(herd.Count(a => a.Alive && a.state == Animal.State.Flee) >= 3, "one is hit and the herd bolts together (" + string.Join(", ", herd.Select(a => a.state)) + ")");
            yield return new WaitForSeconds(2f);
            c.Check(herd.All(a => PH.Flat(a.transform.position, g.Player.transform.position) > 45f), "running away from the shooter");
            foreach (var a in herd) if (a) Object.Destroy(a.gameObject);
            g.Player.crouch = false;

            // ---- a pack hunts
            var wolf = AnimalLibrary.Get("wolf");
            var pack = new List<Animal>();
            for (int i = 0; i < 3; i++)
            {
                var a = Animal.Spawn(wolf, PH.Ground(pad + dir * 24f + side * (i * 3f - 3f)), PH.Yaw(-dir), g.propMaterial, "t:pack:" + i);
                a.herd = 9002; pack.Add(a);
            }
            c.Fixture("a pack of three wolves 24 m away, by day");
            float closest = float.MaxValue; bool hunted = false;
            t0 = Time.time;
            while (Time.time - t0 < 15f && closest > 3f)
            {
                foreach (var a in pack) { closest = Mathf.Min(closest, PH.Flat(a.transform.position, g.Player.transform.position)); if (a.state == Animal.State.Stalk || a.state == Animal.State.Attack) hunted = true; }
                yield return null;
            }
            c.Metric("wolf_closest", closest, "m");
            c.Check(hunted && closest < 12f, $"three wolves are bold enough by day: they stalk and close in ({closest:0.0} m)");
            foreach (var a in pack) if (a && a.Alive) a.ApplyHit(a.transform.position + Vector3.up * 0.5f, dir, 10f, 0.2f, g.Player.gameObject);
            yield return null;
            c.Check(pack.All(a => !a.Alive && a.GetComponent<Carcass>()), "shot dead, they leave carcasses to butcher");
            if (g.Stats.health < g.Stats.MaxHealth) { g.Stats.health = g.Stats.MaxHealth; c.Fixture("bites patched up (full health)"); }

            // ---- a wild horse: calm approach, treats, saddle, ride
            var horseDef = AnimalLibrary.Get("horse");
            var hp = PH.Ground(pad + dir * 18f);
            var horse = Animal.Spawn(horseDef, hp, PH.Yaw(-dir), g.propMaterial, "t:horse");
            c.Fixture("a lone wild horse 18 m up the lane");
            g.Player.crouch = true;
            t0 = Time.time;
            while (Time.time - t0 < 45f && horse.trust < 0.5f)
            {
                // keep a calm 11.6 m: inside the 12 m where it gets used to you, outside its crouched flight distance
                var to = horse.transform.position - g.Player.transform.position; to.y = 0f;
                if (Mathf.Abs(to.magnitude - 11.6f) > 0.8f && horse.state != Animal.State.Flee)
                {
                    var p = horse.transform.position - to.normalized * 11.6f;
                    g.Player.Teleport(PH.Ground(p) + Vector3.up * 0.1f, PH.Yaw(to));
                }
                yield return new WaitForSeconds(0.25f);
            }
            c.Fixture("crept along at 11.6 m while it grazed (teleport steps)");
            c.Metric("trust_after_approach", horse.trust, "");
            c.Metric("approach_seconds", Time.time - t0, "s");
            if (!c.Check(horse.trust >= 0.5f, $"a calm, crouched approach wins half its trust ({horse.trust:0.00})")) yield break;
            var near = horse.transform.position - horse.transform.right * 2.2f;
            g.Player.Teleport(PH.Ground(near) + Vector3.up * 0.1f, PH.Yaw(horse.transform.position - near));
            c.Fixture("crept up to its side (2.2 m)");
            yield return new WaitForSeconds(1.5f);
            c.Check(horse.state != Animal.State.Flee && PH.Flat(horse.transform.position, g.Player.transform.position) < 4f, $"at half trust it lets a crouched person come close ({horse.state}, {PH.Flat(horse.transform.position, g.Player.transform.position):0.0} m)");
            int apples = g.Inventory.GetItem("food_apple") + 3;
            PH.Grant(c, g, "food_apple", 3);
            c.Check(horse.Prompt(g).StartsWith("[E] OFFER"), "prompt: " + horse.Prompt(g));
            for (int i = 0; i < 3 && !horse.owned; i++) horse.Use(g, false);
            c.Check(horse.owned && g.Inventory.GetItem("food_apple") == apples - 2 && AnimalDirector.Instance.kept.Contains(horse), "two treats from the hand and it is yours");
            g.Player.crouch = false;
            c.Check(horse.Prompt(g).Contains("NEEDS A SADDLE"), "prompt: " + horse.Prompt(g));
            PH.Grant(c, g, "use_saddle", 1);
            horse.Use(g, false);
            c.Check(horse.saddled, "saddled");
            horse.Use(g, false);
            yield return null;
            if (!c.Check(Animal.Mounted == horse && horse.state == Animal.State.Ridden && g.Player.Sitting, "[E] climbs into the saddle")) yield break;
            c.Screenshot("riding");
            yield return null;
            var r0 = horse.transform.position;
            t0 = Time.time;
            while (Time.time - t0 < 4f) { g.Player.moveInput = new Vector2(0f, 1f); g.Player.run = false; yield return null; }
            g.Player.moveInput = Vector2.zero;
            float rode = PH.Flat(horse.transform.position, r0);
            c.Metric("ridden", rode, "m");
            c.Check(rode > 5f && PH.Flat(g.Player.transform.position, horse.transform.position) < 2.5f, $"forward carries horse and rider {rode:0.0} m");
            yield return new WaitForSeconds(1.5f);
            g.Player.StandUp();
            yield return null;
            yield return null;
            c.Check(!g.Player.Sitting && Animal.Mounted != horse && horse.state != Animal.State.Ridden, "off the horse, it stays (" + horse.state + ")");
        }
    }
}
