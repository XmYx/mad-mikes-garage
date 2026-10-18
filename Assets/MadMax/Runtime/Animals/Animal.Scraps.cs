using MadMax.Game;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Scraps for the animals (2026-10-17): kept pigs, chickens, goats and dogs notice food lying on the ground
    /// near their home (scraps thrown down, rotten food in the yard, a dropped apple), trot over, root or peck at it for a
    /// few seconds and eat it. Enough of it counts as the day's feed (<see cref="ScrapsForDay"/>), so kitchen waste goes
    /// to the pigs instead of rotting into flies. Pigs and dogs take rotten food; the others only what they like or plain
    /// food. Host only (animals are simulated on the authority); the eaten item goes for everyone.</summary>
    public partial class Animal
    {
        public const float ScrapReach = 9f, ScrapEatSeconds = 3f;
        WorldItem scrap;
        float scrapEatUntil = -1f, scrapLookT;
        int scrapsToday, scrapDay = -1;
        /// <summary>Food items eaten off the ground this session (tests).</summary>
        public static int ScrapsEaten;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetScraps() => ScrapsEaten = 0;

        /// <summary>Is this a kept scrap-eater?</summary>
        public bool ScrapEater => Def != null && (owned || town >= 0) && Adult && !Def.flies
            && (Def.id == "pig" || Def.id == "chicken" || Def.id == "goat" || Def.id == "dog");

        /// <summary>Items of ground food this animal needs to call it fed for the day.</summary>
        public int ScrapsForDay => Def.id == "chicken" ? 1 : Def.id == "pig" ? 3 : 2;

        /// <summary>Would this animal eat <paramref name="id"/> off the ground?</summary>
        public bool EatsScrap(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (Def.likes != null) foreach (var l in Def.likes) if (l == id) return true;
            if (id == "food_rotten") return Def.id == "pig" || Def.id == "dog";                      // pigs and dogs eat anything
            if (Def.id == "dog") return id.StartsWith("food_meat") || id == "food_jerky";
            if (Def.id == "chicken") return id.StartsWith("crop_") || id.StartsWith("seed_") || id == "food_bread";
            return id.StartsWith("food_") || id.StartsWith("crop_");
        }

        /// <summary>The food this animal is going for or eating (tests).</summary>
        public WorldItem ScrapTarget => scrap;
        public bool EatingScraps => scrap && scrapEatUntil > 0f;

        /// <summary>From <c>ThinkKept</c> (every ~0.2 s): go for food on the ground near home; true while busy with it.</summary>
        bool ScrapsThink(WastelandGame g)
        {
            if (!ScrapEater || state == State.Sleep) { scrap = null; return false; }
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient) return false;
            var me = transform.position;
            if (scrap)
            {
                if (scrap.transform.parent || !scrap.gameObject.activeInHierarchy) { scrap = null; return false; }   // picked up
                var at = scrap.transform.position;
                float d = Flat(at - me).magnitude;
                if (scrapEatUntil > 0f)
                {
                    state = State.Graze; SetMove(Vector3.zero, 0f);                              // head down at it
                    if (Time.time >= scrapEatUntil) EatScrap();
                    return true;
                }
                if (d < Def.reach + 0.35f) { scrapEatUntil = Time.time + ScrapEatSeconds; state = State.Graze; SetMove(Vector3.zero, 0f); if (Random.value < 0.5f) Say(0); return true; }
                if (Flat(at - home).magnitude > ScrapReach + 4f) { scrap = null; return false; }
                state = State.Wander; goal = at; stateUntil = Time.time + 6f;
                SetMove(at - me, Def.walk * 1.6f);                                               // a brisk trot to it
                return true;
            }
            if ((scrapLookT -= 0.25f) > 0f) return false;
            scrapLookT = 1.5f + Random.value;
            if (scrapDay == DayNight.Day && scrapsToday >= ScrapsForDay * 2) return false;      // full up
            float best = ScrapReach * ScrapReach;
            foreach (var w in WorldItem.All)
            {
                if (!w || w.IsResource || w.OnVehicle || w.transform.parent || !EatsScrap(w.key) || Claimed(w)) continue;
                var p = w.transform.position;
                if (Mathf.Abs(p.y - me.y) > 1.5f || Flat(p - home).sqrMagnitude > (ScrapReach + 3f) * (ScrapReach + 3f)) continue;
                float d2 = Flat(p - me).sqrMagnitude;
                if (d2 < best) { best = d2; scrap = w; }
            }
            if (!scrap) return false;
            scrapEatUntil = -1f;
            return true;
        }

        static bool Claimed(WorldItem w)
        {
            foreach (var a in All) if (a && a.scrap == w) return true;
            return false;
        }

        void EatScrap()
        {
            var w = scrap; scrap = null; scrapEatUntil = -1f;
            if (!w) return;
            if (scrapDay != DayNight.Day) { scrapDay = DayNight.Day; scrapsToday = 0; }
            scrapsToday += Mathf.Max(1, w.count);
            ScrapsEaten++;
            if (scrapsToday >= ScrapsForDay && fedDay != DayNight.Day) { fedDay = DayNight.Day; hungryDays = 0; }
            MadMax.Audio.Sfx.Play("dig", transform.position, 0.15f, Def.id == "chicken" ? 1.8f : 1.2f, 6f);
            var net = MadMax.Net.NetSession.Instance;
            if (net && w.netId != 0) net.SendItemGone(w.netId);
            Object.Destroy(w.gameObject);
            Say(0);
        }
    }
}
