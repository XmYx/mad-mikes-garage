using System.Collections.Generic;
using MadMax.Building;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Puddles that call birds (2026-10-18): a sprinkler that wetted its ground in the last few hours draws two
    /// or three crows by day (not in the rain, not with a scarecrow within its reach). They land on the wet patches,
    /// drink and worm the soil (the crow species' own peck and hop), lift off when someone walks up and come back, and
    /// pull young seedlings on the open beds the sprinkler waters (<see cref="GardenPlot.Peck"/>) — the reason to set a
    /// scarecrow by the garden. They leave when the ground dries, at dusk or when the player is far off.</summary>
    public partial class AnimalDirector
    {
        public const float PuddleBirdReach = 70f, PuddleBirdHours = 3f, PeckEvery = 30f;
        readonly Dictionary<Sprinkler, List<Animal>> puddleBirds = new Dictionary<Sprinkler, List<Animal>>();
        readonly List<Sprinkler> puddleGone = new List<Sprinkler>();
        float puddleT, peckT;
        /// <summary>Seedlings pulled by puddle birds this session (tests).</summary>
        public static int SeedlingsPecked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetPuddles() => SeedlingsPecked = 0;

        /// <summary>Would birds come to <paramref name="s"/>'s wet ground now?</summary>
        public static bool PuddleCalls(Sprinkler s)
        {
            if (!s || s.drip || s.WetAt < 0f || (DayNight.TotalDays - s.WetAt) * 24f > PuddleBirdHours) return false;
            if (DayNight.Darkness > 0.3f || Weather.Raining || Scarecrow.Near(s.transform.position)) return false;
            if (Weather.TemperatureAt(s.transform.position.z) < 0f) return false;
            return true;
        }

        void UpdatePuddleBirds(Vector3 focus)
        {
            if ((puddleT -= 1f) > 0f) return;
            puddleT = 5f;
            puddleGone.Clear();
            foreach (var kv in puddleBirds)
                if (!kv.Key || !PuddleCalls(kv.Key) || Flat(kv.Key.transform.position - focus).magnitude > PuddleBirdReach + 30f) puddleGone.Add(kv.Key);
            foreach (var s in puddleGone)
            {
                foreach (var a in puddleBirds[s]) if (a && a.Alive) Destroy(a.gameObject);
                puddleBirds.Remove(s);
            }
            foreach (var s in Object.FindObjectsByType<Sprinkler>(FindObjectsSortMode.None))
            {
                if (puddleBirds.ContainsKey(s) || Flat(s.transform.position - focus).magnitude > PuddleBirdReach || !PuddleCalls(s)) continue;
                puddleBirds[s] = SpawnPuddleBirds(s);
            }
            if ((peckT -= 5f) > 0f) return;
            peckT = PeckEvery;
            foreach (var kv in puddleBirds) PeckBeds(kv.Key, kv.Value);
        }

        List<Animal> SpawnPuddleBirds(Sprinkler s)
        {
            var list = new List<Animal>();
            var def = AnimalLibrary.Get("crow");
            if (def == null) return list;
            var t = DeformableTerrain.Instance;
            var rnd = new System.Random(s.GetEntityId().GetHashCode() ^ s.Wettings * 31);
            int n = 2 + rnd.Next(2);
            for (int j = 0; j < n; j++)
            {
                float a = Mathf.Lerp(-Sprinkler.Arc, Sprinkler.Arc, (float)rnd.NextDouble());
                float r = Mathf.Lerp(1.2f, s.reach, (float)rnd.NextDouble());
                var p = s.transform.TransformPoint(Quaternion.Euler(0f, a, 0f) * Vector3.forward * r);
                if (t) p.y = t.HeightNoLoad(p.x, p.z);
                var an = Animal.Spawn(def, p, (float)rnd.NextDouble() * 360f, game.propMaterial, "pb:" + s.GetEntityId() + ":" + j);
                list.Add(an);
            }
            MadMax.Audio.Sfx.Play("birds", s.transform.position, 0.45f, 0.9f, 30f);
            return list;
        }

        void PeckBeds(Sprinkler s, List<Animal> birds)
        {
            bool any = false;
            foreach (var a in birds) if (a && a.Alive && Flat(a.transform.position - s.transform.position).magnitude < s.reach + 2f) { any = true; break; }
            if (!any) return;
            foreach (var p in GardenPlot.All)
            {
                if (!p || Flat(p.transform.position - s.transform.position).magnitude > s.reach || !p.Peck(0.04f)) continue;
                SeedlingsPecked++;
                if (game && game.Player && Flat(game.Player.transform.position - p.transform.position).magnitude < 40f)
                    MadMax.Game.Hints.Show("puddle_crows", "CROWS ARE WORMING THE WET SOIL BY THE SPRINKLER AND PULLING YOUR SEEDLINGS: A SCARECROW KEEPS THEM OFF");
            }
        }

        /// <summary>Birds drawn to <paramref name="s"/>'s puddles right now (tests).</summary>
        public int PuddleBirds(Sprinkler s)
        {
            if (!s || !puddleBirds.TryGetValue(s, out var l)) return 0;
            int n = 0; foreach (var a in l) if (a && a.Alive) n++;
            return n;
        }
    }
}
